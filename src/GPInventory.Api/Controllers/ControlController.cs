#pragma warning disable CS8601
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Cors;
using Microsoft.AspNetCore.Mvc;
using MySqlConnector;
using System.Security.Claims;
using System.Text.Json;
using GPInventory.Api.Services;

namespace GPInventory.Api.Controllers;

/// <summary>
/// GP Control — Tableros, Iteraciones, Tareas recursivas, To-Do Lists y SSE en tiempo real.
/// </summary>
[ApiController]
[Route("api/control")]
[EnableCors("AllowFrontend")]
[Authorize]
public class ControlController : ControllerBase
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<ControlController> _logger;
    private readonly ControlSseService _sse;

    public ControlController(IConfiguration configuration, ILogger<ControlController> logger, ControlSseService sse)
    {
        _configuration = configuration;
        _logger = logger;
        _sse = sse;
    }

    private MySqlConnection GetConnection()
        => new(_configuration.GetConnectionString("DefaultConnection")!);

    private static bool IsNull(MySqlDataReader r, string col) => r.IsDBNull(r.GetOrdinal(col));

    private int? GetCurrentUserId()
    {
        var claim = User.FindFirst("sub")
                 ?? User.FindFirst("userId")
                 ?? User.FindFirst("id")
                 ?? User.FindFirst(ClaimTypes.NameIdentifier);
        return int.TryParse(claim?.Value, out int id) ? id : null;
    }

    private string? GetCurrentUserName()
    {
        return User.FindFirst("name")?.Value
            ?? User.FindFirst(ClaimTypes.Name)?.Value;
    }

    // ====================================================================
    // BOARDS
    // ====================================================================

    /// GET /api/control/boards?businessId=
    [HttpGet("boards")]
    public async Task<IActionResult> GetBoards([FromQuery] int businessId)
    {
        try
        {
            using var conn = GetConnection();
            await conn.OpenAsync();
            using var cmd = new MySqlCommand(@"
                SELECT id, name, description, color, business_id, active,
                       created_by_user_id, created_at, updated_at
                FROM control_board
                WHERE business_id = @B AND active = 1
                ORDER BY name", conn);
            cmd.Parameters.AddWithValue("@B", businessId);
            var list = new List<object>();
            using var r = await cmd.ExecuteReaderAsync();
            while (await r.ReadAsync())
                list.Add(new {
                    id                = r.GetInt32("id"),
                    name              = r.GetString("name"),
                    description       = IsNull(r, "description") ? null : r.GetString("description"),
                    color             = r.GetString("color"),
                    businessId        = r.GetInt32("business_id"),
                    active            = r.GetBoolean("active"),
                    createdByUserId   = r.GetInt32("created_by_user_id"),
                    createdAt         = r.GetDateTime("created_at"),
                    updatedAt         = r.GetDateTime("updated_at"),
                });
            return Ok(list);
        }
        catch (Exception ex) { _logger.LogError(ex, "GetBoards"); return StatusCode(500, ex.Message); }
    }

    /// POST /api/control/boards
    [HttpPost("boards")]
    public async Task<IActionResult> CreateBoard([FromBody] JsonElement body)
    {
        var userId = GetCurrentUserId();
        if (!body.TryGetProperty("businessId", out var bizEl)) return BadRequest("businessId required");
        var name        = body.TryGetProperty("name", out var nEl) ? nEl.GetString() : "Nuevo tablero";
        var description = body.TryGetProperty("description", out var dEl) ? dEl.GetString() : null;
        var color       = body.TryGetProperty("color", out var cEl) ? cEl.GetString() : "#6366f1";
        var bizId       = bizEl.GetInt32();
        try
        {
            using var conn = GetConnection();
            await conn.OpenAsync();
            using var cmd = new MySqlCommand(@"
                INSERT INTO control_board (business_id, name, description, color, created_by_user_id)
                VALUES (@B, @N, @D, @C, @U);
                SELECT LAST_INSERT_ID();", conn);
            cmd.Parameters.AddWithValue("@B", bizId);
            cmd.Parameters.AddWithValue("@N", name);
            cmd.Parameters.AddWithValue("@D", (object?)description ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@C", color);
            cmd.Parameters.AddWithValue("@U", (object?)userId ?? DBNull.Value);
            var newId = Convert.ToInt32(await cmd.ExecuteScalarAsync());
            _sse.NotifyBoard(bizId, "board_created", new { id = newId, name, color });
            return Ok(new { id = newId });
        }
        catch (Exception ex) { _logger.LogError(ex, "CreateBoard"); return StatusCode(500, ex.Message); }
    }

    /// PUT /api/control/boards/{id}
    [HttpPut("boards/{id:int}")]
    public async Task<IActionResult> UpdateBoard(int id, [FromBody] JsonElement body)
    {
        var setClauses = new List<string>();
        var cmd = new MySqlCommand();
        if (body.TryGetProperty("name",        out var nEl)) { setClauses.Add("name=@N");        cmd.Parameters.AddWithValue("@N", nEl.GetString()); }
        if (body.TryGetProperty("description", out var dEl)) { setClauses.Add("description=@D"); cmd.Parameters.AddWithValue("@D", (object?)dEl.GetString() ?? DBNull.Value); }
        if (body.TryGetProperty("color",       out var cEl)) { setClauses.Add("color=@C");       cmd.Parameters.AddWithValue("@C", cEl.GetString()); }
        if (body.TryGetProperty("active",      out var aEl)) { setClauses.Add("active=@A");      cmd.Parameters.AddWithValue("@A", aEl.GetBoolean()); }
        if (setClauses.Count == 0) return BadRequest("Nothing to update");
        setClauses.Add("updated_at=NOW()");
        try
        {
            using var conn = GetConnection();
            await conn.OpenAsync();
            cmd.Connection = conn;
            cmd.CommandText = $"UPDATE control_board SET {string.Join(",", setClauses)} WHERE id=@ID";
            cmd.Parameters.AddWithValue("@ID", id);
            await cmd.ExecuteNonQueryAsync();
            _sse.NotifyBoard(id, "board_updated", new { id });
            return Ok(new { id });
        }
        catch (Exception ex) { _logger.LogError(ex, "UpdateBoard"); return StatusCode(500, ex.Message); }
    }

    /// DELETE /api/control/boards/{id}
    [HttpDelete("boards/{id:int}")]
    public async Task<IActionResult> DeleteBoard(int id)
    {
        try
        {
            using var conn = GetConnection();
            await conn.OpenAsync();
            using var cmd = new MySqlCommand("UPDATE control_board SET active=0, updated_at=NOW() WHERE id=@ID", conn);
            cmd.Parameters.AddWithValue("@ID", id);
            await cmd.ExecuteNonQueryAsync();
            _sse.NotifyBoard(id, "board_deleted", new { id });
            return Ok(new { id });
        }
        catch (Exception ex) { _logger.LogError(ex, "DeleteBoard"); return StatusCode(500, ex.Message); }
    }

    // ====================================================================
    // BOARD MEMBERS
    // ====================================================================

    /// GET /api/control/boards/{boardId}/members
    [HttpGet("boards/{boardId:int}/members")]
    public async Task<IActionResult> GetMembers(int boardId)
    {
        try
        {
            using var conn = GetConnection();
            await conn.OpenAsync();
            using var cmd = new MySqlCommand(@"
                SELECT m.id, m.board_id, m.user_id, m.role, m.joined_at,
                       u.name AS user_name, u.email AS user_email
                FROM control_board_member m
                LEFT JOIN user u ON u.id = m.user_id
                WHERE m.board_id = @B ORDER BY m.joined_at", conn);
            cmd.Parameters.AddWithValue("@B", boardId);
            var list = new List<object>();
            using var r = await cmd.ExecuteReaderAsync();
            while (await r.ReadAsync())
                list.Add(new {
                    id        = r.GetInt32("id"),
                    boardId   = r.GetInt32("board_id"),
                    userId    = r.GetInt32("user_id"),
                    role      = r.GetString("role"),
                    joinedAt  = r.GetDateTime("joined_at"),
                    userName  = IsNull(r, "user_name")  ? null : r.GetString("user_name"),
                    userEmail = IsNull(r, "user_email") ? null : r.GetString("user_email"),
                });
            return Ok(list);
        }
        catch (Exception ex) { _logger.LogError(ex, "GetMembers"); return StatusCode(500, ex.Message); }
    }

    /// POST /api/control/boards/{boardId}/members
    [HttpPost("boards/{boardId:int}/members")]
    public async Task<IActionResult> AddMember(int boardId, [FromBody] JsonElement body)
    {
        if (!body.TryGetProperty("userId", out var uEl)) return BadRequest("userId required");
        var role   = body.TryGetProperty("role", out var rEl) ? rEl.GetString() : "member";
        var userId = uEl.GetInt32();
        try
        {
            using var conn = GetConnection();
            await conn.OpenAsync();
            using var cmd = new MySqlCommand(@"
                INSERT IGNORE INTO control_board_member (board_id, user_id, role) VALUES (@B, @U, @R);
                SELECT LAST_INSERT_ID();", conn);
            cmd.Parameters.AddWithValue("@B", boardId);
            cmd.Parameters.AddWithValue("@U", userId);
            cmd.Parameters.AddWithValue("@R", role);
            var newId = Convert.ToInt32(await cmd.ExecuteScalarAsync());
            _sse.NotifyBoard(boardId, "member_added", new { boardId, userId, role });
            return Ok(new { id = newId });
        }
        catch (Exception ex) { _logger.LogError(ex, "AddMember"); return StatusCode(500, ex.Message); }
    }

    /// DELETE /api/control/boards/{boardId}/members/{userId}
    [HttpDelete("boards/{boardId:int}/members/{userId:int}")]
    public async Task<IActionResult> RemoveMember(int boardId, int userId)
    {
        try
        {
            using var conn = GetConnection();
            await conn.OpenAsync();
            using var cmd = new MySqlCommand("DELETE FROM control_board_member WHERE board_id=@B AND user_id=@U", conn);
            cmd.Parameters.AddWithValue("@B", boardId);
            cmd.Parameters.AddWithValue("@U", userId);
            await cmd.ExecuteNonQueryAsync();
            _sse.NotifyBoard(boardId, "member_removed", new { boardId, userId });
            return Ok(new { boardId, userId });
        }
        catch (Exception ex) { _logger.LogError(ex, "RemoveMember"); return StatusCode(500, ex.Message); }
    }

    // ====================================================================
    // ITERATIONS / SPRINTS
    // ====================================================================

    /// GET /api/control/boards/{boardId}/iterations
    [HttpGet("boards/{boardId:int}/iterations")]
    public async Task<IActionResult> GetIterations(int boardId)
    {
        try
        {
            using var conn = GetConnection();
            await conn.OpenAsync();
            using var cmd = new MySqlCommand(@"
                SELECT id, board_id, name, goal, start_date, end_date,
                       status, order_index, created_at, updated_at
                FROM control_iteration
                WHERE board_id = @B ORDER BY order_index, id", conn);
            cmd.Parameters.AddWithValue("@B", boardId);
            var list = new List<object>();
            using var r = await cmd.ExecuteReaderAsync();
            while (await r.ReadAsync())
                list.Add(new {
                    id          = r.GetInt32("id"),
                    boardId     = r.GetInt32("board_id"),
                    name        = r.GetString("name"),
                    goal        = IsNull(r, "goal") ? null : r.GetString("goal"),
                    startDate   = IsNull(r, "start_date") ? (DateTime?)null : r.GetDateTime("start_date"),
                    endDate     = IsNull(r, "end_date") ? (DateTime?)null : r.GetDateTime("end_date"),
                    status      = r.GetString("status"),
                    orderIndex  = r.GetInt32("order_index"),
                    createdAt   = r.GetDateTime("created_at"),
                    updatedAt   = r.GetDateTime("updated_at"),
                });
            return Ok(list);
        }
        catch (Exception ex) { _logger.LogError(ex, "GetIterations"); return StatusCode(500, ex.Message); }
    }

    /// POST /api/control/boards/{boardId}/iterations
    [HttpPost("boards/{boardId:int}/iterations")]
    public async Task<IActionResult> CreateIteration(int boardId, [FromBody] JsonElement body)
    {
        var name      = body.TryGetProperty("name",      out var nEl) ? nEl.GetString() : "Nueva iteración";
        var goal      = body.TryGetProperty("goal",      out var gEl) ? gEl.GetString() : null;
        var startDate = body.TryGetProperty("startDate", out var sdEl) ? sdEl.GetString() : null;
        var endDate   = body.TryGetProperty("endDate",   out var edEl) ? edEl.GetString() : null;
        var status    = body.TryGetProperty("status",    out var stEl) ? stEl.GetString() : "planning";
        try
        {
            using var conn = GetConnection();
            await conn.OpenAsync();
            using var cmd = new MySqlCommand(@"
                INSERT INTO control_iteration (board_id, name, goal, start_date, end_date, status)
                VALUES (@B, @N, @G, @SD, @ED, @ST);
                SELECT LAST_INSERT_ID();", conn);
            cmd.Parameters.AddWithValue("@B",  boardId);
            cmd.Parameters.AddWithValue("@N",  name);
            cmd.Parameters.AddWithValue("@G",  (object?)goal ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@SD", string.IsNullOrEmpty(startDate) ? (object)DBNull.Value : startDate);
            cmd.Parameters.AddWithValue("@ED", string.IsNullOrEmpty(endDate)   ? (object)DBNull.Value : endDate);
            cmd.Parameters.AddWithValue("@ST", status);
            var newId = Convert.ToInt32(await cmd.ExecuteScalarAsync());
            _sse.NotifyBoard(boardId, "iteration_created", new { id = newId, boardId, name, status });
            return Ok(new { id = newId });
        }
        catch (Exception ex) { _logger.LogError(ex, "CreateIteration"); return StatusCode(500, ex.Message); }
    }

    /// PUT /api/control/iterations/{id}
    [HttpPut("iterations/{id:int}")]
    public async Task<IActionResult> UpdateIteration(int id, [FromBody] JsonElement body)
    {
        var setClauses = new List<string>();
        var cmd = new MySqlCommand();
        if (body.TryGetProperty("name",      out var nEl))  { setClauses.Add("name=@N");       cmd.Parameters.AddWithValue("@N",  nEl.GetString()); }
        if (body.TryGetProperty("goal",      out var gEl))  { setClauses.Add("goal=@G");       cmd.Parameters.AddWithValue("@G",  (object?)gEl.GetString() ?? DBNull.Value); }
        if (body.TryGetProperty("startDate", out var sdEl)) { setClauses.Add("start_date=@SD");cmd.Parameters.AddWithValue("@SD", string.IsNullOrEmpty(sdEl.GetString()) ? (object)DBNull.Value : sdEl.GetString()); }
        if (body.TryGetProperty("endDate",   out var edEl)) { setClauses.Add("end_date=@ED");  cmd.Parameters.AddWithValue("@ED", string.IsNullOrEmpty(edEl.GetString()) ? (object)DBNull.Value : edEl.GetString()); }
        if (body.TryGetProperty("status",    out var stEl)) { setClauses.Add("status=@ST");    cmd.Parameters.AddWithValue("@ST", stEl.GetString()); }
        if (body.TryGetProperty("orderIndex",out var oEl))  { setClauses.Add("order_index=@OI");cmd.Parameters.AddWithValue("@OI", oEl.GetInt32()); }
        if (setClauses.Count == 0) return BadRequest("Nothing to update");
        setClauses.Add("updated_at=NOW()");
        try
        {
            using var conn = GetConnection();
            await conn.OpenAsync();
            cmd.Connection = conn;
            cmd.CommandText = $"UPDATE control_iteration SET {string.Join(",", setClauses)} WHERE id=@ID";
            cmd.Parameters.AddWithValue("@ID", id);
            // get boardId for SSE
            var boardIdCmd = new MySqlCommand("SELECT board_id FROM control_iteration WHERE id=@ID", conn);
            boardIdCmd.Parameters.AddWithValue("@ID", id);
            var boardId = Convert.ToInt32(await boardIdCmd.ExecuteScalarAsync());
            await cmd.ExecuteNonQueryAsync();
            _sse.NotifyBoard(boardId, "iteration_updated", new { id, boardId });
            return Ok(new { id });
        }
        catch (Exception ex) { _logger.LogError(ex, "UpdateIteration"); return StatusCode(500, ex.Message); }
    }

    /// DELETE /api/control/iterations/{id}
    [HttpDelete("iterations/{id:int}")]
    public async Task<IActionResult> DeleteIteration(int id)
    {
        try
        {
            using var conn = GetConnection();
            await conn.OpenAsync();
            var boardIdCmd = new MySqlCommand("SELECT board_id FROM control_iteration WHERE id=@ID", conn);
            boardIdCmd.Parameters.AddWithValue("@ID", id);
            var boardId = Convert.ToInt32(await boardIdCmd.ExecuteScalarAsync());
            using var cmd = new MySqlCommand("DELETE FROM control_iteration WHERE id=@ID", conn);
            cmd.Parameters.AddWithValue("@ID", id);
            await cmd.ExecuteNonQueryAsync();
            _sse.NotifyBoard(boardId, "iteration_deleted", new { id, boardId });
            return Ok(new { id });
        }
        catch (Exception ex) { _logger.LogError(ex, "DeleteIteration"); return StatusCode(500, ex.Message); }
    }

    // ====================================================================
    // TASKS
    // ====================================================================

    /// GET /api/control/boards/{boardId}/tasks?iterationId=&status=&parentTaskId=
    [HttpGet("boards/{boardId:int}/tasks")]
    public async Task<IActionResult> GetTasks(
        int boardId,
        [FromQuery] int? iterationId = null,
        [FromQuery] string? status = null,
        [FromQuery] int? parentTaskId = null,
        [FromQuery] bool rootOnly = false)
    {
        try
        {
            using var conn = GetConnection();
            await conn.OpenAsync();

            var where = new List<string> { "t.board_id = @B" };
            var cmdParams = new List<(string name, object value)> { ("@B", boardId) };

            if (iterationId.HasValue) { where.Add("t.iteration_id = @IT"); cmdParams.Add(("@IT", iterationId.Value)); }
            if (!string.IsNullOrEmpty(status)) { where.Add("t.status = @ST"); cmdParams.Add(("@ST", status)); }
            if (parentTaskId.HasValue) { where.Add("t.parent_task_id = @PT"); cmdParams.Add(("@PT", parentTaskId.Value)); }
            else if (rootOnly) { where.Add("t.parent_task_id IS NULL"); }

            var sql = $@"
                SELECT t.id, t.board_id, t.iteration_id, t.parent_task_id, t.type, t.title,
                       t.description, t.status, t.priority, t.assigned_to_user_id, t.assigned_to_name,
                       t.due_date, t.start_date, t.estimated_hours, t.actual_hours,
                       t.order_index, t.tags, t.meeting_task_id,
                       t.created_by_user_id, t.created_at, t.updated_at,
                       (SELECT COUNT(*) FROM control_task sub WHERE sub.parent_task_id = t.id) AS child_count,
                       (SELECT COUNT(*) FROM control_task_comment c WHERE c.task_id = t.id) AS comment_count
                FROM control_task t
                WHERE {string.Join(" AND ", where)}
                ORDER BY t.order_index, t.id";

            using var cmd = new MySqlCommand(sql, conn);
            foreach (var (n, v) in cmdParams)
                cmd.Parameters.AddWithValue(n, v);

            var list = new List<object>();
            using var r = await cmd.ExecuteReaderAsync();
            while (await r.ReadAsync())
                list.Add(MapTask(r));
            return Ok(list);
        }
        catch (Exception ex) { _logger.LogError(ex, "GetTasks"); return StatusCode(500, ex.Message); }
    }

    /// GET /api/control/tasks/{id}
    [HttpGet("tasks/{id:int}")]
    public async Task<IActionResult> GetTask(int id)
    {
        try
        {
            using var conn = GetConnection();
            await conn.OpenAsync();
            using var cmd = new MySqlCommand(@"
                SELECT t.id, t.board_id, t.iteration_id, t.parent_task_id, t.type, t.title,
                       t.description, t.status, t.priority, t.assigned_to_user_id, t.assigned_to_name,
                       t.due_date, t.start_date, t.estimated_hours, t.actual_hours,
                       t.order_index, t.tags, t.meeting_task_id,
                       t.created_by_user_id, t.created_at, t.updated_at,
                       (SELECT COUNT(*) FROM control_task sub WHERE sub.parent_task_id = t.id) AS child_count,
                       (SELECT COUNT(*) FROM control_task_comment c WHERE c.task_id = t.id) AS comment_count
                FROM control_task t
                WHERE t.id = @ID", conn);
            cmd.Parameters.AddWithValue("@ID", id);
            using var r = await cmd.ExecuteReaderAsync();
            if (!await r.ReadAsync()) return NotFound();
            return Ok(MapTask(r));
        }
        catch (Exception ex) { _logger.LogError(ex, "GetTask"); return StatusCode(500, ex.Message); }
    }

    /// POST /api/control/tasks
    [HttpPost("tasks")]
    public async Task<IActionResult> CreateTask([FromBody] JsonElement body)
    {
        var userId = GetCurrentUserId();
        if (!body.TryGetProperty("boardId", out var bEl)) return BadRequest("boardId required");
        if (!body.TryGetProperty("title",   out var tEl)) return BadRequest("title required");

        var boardId      = bEl.GetInt32();
        var title        = tEl.GetString()!;
        var iterationId  = body.TryGetProperty("iterationId",    out var itEl)   && itEl.ValueKind != JsonValueKind.Null ? itEl.GetInt32() : (int?)null;
        var parentTaskId = body.TryGetProperty("parentTaskId",   out var ptEl)   && ptEl.ValueKind != JsonValueKind.Null ? ptEl.GetInt32() : (int?)null;
        var type         = body.TryGetProperty("type",            out var tyEl)  ? tyEl.GetString() : "task";
        var description  = body.TryGetProperty("description",    out var dEl)    ? dEl.GetString() : null;
        var status       = body.TryGetProperty("status",         out var stEl)   ? stEl.GetString() : "todo";
        var priority     = body.TryGetProperty("priority",       out var prEl)   ? prEl.GetString() : "medium";
        var assignedId   = body.TryGetProperty("assignedToUserId",out var auEl)  && auEl.ValueKind != JsonValueKind.Null ? auEl.GetInt32() : (int?)null;
        var assignedName = body.TryGetProperty("assignedToName", out var anEl)   ? anEl.GetString() : null;
        var dueDate      = body.TryGetProperty("dueDate",        out var ddEl)   && ddEl.ValueKind != JsonValueKind.Null ? ddEl.GetString() : null;
        var startDate    = body.TryGetProperty("startDate",      out var sdEl)   && sdEl.ValueKind != JsonValueKind.Null ? sdEl.GetString() : null;
        var estHours     = body.TryGetProperty("estimatedHours", out var ehEl)   && ehEl.ValueKind != JsonValueKind.Null ? ehEl.GetDecimal() : (decimal?)null;
        var tagsJson     = body.TryGetProperty("tags",           out var taEl)   && taEl.ValueKind == JsonValueKind.Array ? taEl.GetRawText() : null;
        var meetingTaskId= body.TryGetProperty("meetingTaskId",  out var mtEl)   && mtEl.ValueKind != JsonValueKind.Null ? mtEl.GetInt32() : (int?)null;

        try
        {
            using var conn = GetConnection();
            await conn.OpenAsync();
            using var cmd = new MySqlCommand(@"
                INSERT INTO control_task
                    (board_id, iteration_id, parent_task_id, type, title, description, status, priority,
                     assigned_to_user_id, assigned_to_name, due_date, start_date, estimated_hours,
                     tags, meeting_task_id, created_by_user_id)
                VALUES (@BD, @IT, @PT, @TY, @TI, @DE, @ST, @PR, @AU, @AN, @DD, @SD, @EH, @TA, @MT, @CU);
                SELECT LAST_INSERT_ID();", conn);
            cmd.Parameters.AddWithValue("@BD", boardId);
            cmd.Parameters.AddWithValue("@IT", (object?)iterationId  ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@PT", (object?)parentTaskId ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@TY", type);
            cmd.Parameters.AddWithValue("@TI", title);
            cmd.Parameters.AddWithValue("@DE", (object?)description  ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@ST", status);
            cmd.Parameters.AddWithValue("@PR", priority);
            cmd.Parameters.AddWithValue("@AU", (object?)assignedId   ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@AN", (object?)assignedName ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@DD", string.IsNullOrEmpty(dueDate)   ? (object)DBNull.Value : dueDate);
            cmd.Parameters.AddWithValue("@SD", string.IsNullOrEmpty(startDate) ? (object)DBNull.Value : startDate);
            cmd.Parameters.AddWithValue("@EH", (object?)estHours ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@TA", (object?)tagsJson  ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@MT", (object?)meetingTaskId ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@CU", (object?)userId ?? DBNull.Value);
            var newId = Convert.ToInt32(await cmd.ExecuteScalarAsync());
            _sse.NotifyBoard(boardId, "task_created", new { id = newId, boardId, title, status, type, parentTaskId, iterationId });
            return Ok(new { id = newId });
        }
        catch (Exception ex) { _logger.LogError(ex, "CreateTask"); return StatusCode(500, ex.Message); }
    }

    /// PUT /api/control/tasks/{id}
    [HttpPut("tasks/{id:int}")]
    public async Task<IActionResult> UpdateTask(int id, [FromBody] JsonElement body)
    {
        var setClauses = new List<string>();
        var cmd = new MySqlCommand();
        if (body.TryGetProperty("title",         out var tEl))  { setClauses.Add("title=@TI");              cmd.Parameters.AddWithValue("@TI", tEl.GetString()); }
        if (body.TryGetProperty("description",   out var dEl))  { setClauses.Add("description=@DE");        cmd.Parameters.AddWithValue("@DE", (object?)dEl.GetString() ?? DBNull.Value); }
        if (body.TryGetProperty("type",          out var tyEl)) { setClauses.Add("type=@TY");               cmd.Parameters.AddWithValue("@TY", tyEl.GetString()); }
        if (body.TryGetProperty("status",        out var stEl)) { setClauses.Add("status=@ST");             cmd.Parameters.AddWithValue("@ST", stEl.GetString()); }
        if (body.TryGetProperty("priority",      out var prEl)) { setClauses.Add("priority=@PR");           cmd.Parameters.AddWithValue("@PR", prEl.GetString()); }
        if (body.TryGetProperty("assignedToUserId", out var auEl)) { setClauses.Add("assigned_to_user_id=@AU"); cmd.Parameters.AddWithValue("@AU", auEl.ValueKind != JsonValueKind.Null ? auEl.GetInt32() : (object)DBNull.Value); }
        if (body.TryGetProperty("assignedToName",   out var anEl)) { setClauses.Add("assigned_to_name=@AN"); cmd.Parameters.AddWithValue("@AN", (object?)anEl.GetString() ?? DBNull.Value); }
        if (body.TryGetProperty("dueDate",   out var ddEl)) { setClauses.Add("due_date=@DD");   cmd.Parameters.AddWithValue("@DD", string.IsNullOrEmpty(ddEl.GetString()) ? (object)DBNull.Value : ddEl.GetString()); }
        if (body.TryGetProperty("startDate", out var sdEl)) { setClauses.Add("start_date=@SD"); cmd.Parameters.AddWithValue("@SD", string.IsNullOrEmpty(sdEl.GetString()) ? (object)DBNull.Value : sdEl.GetString()); }
        if (body.TryGetProperty("estimatedHours", out var ehEl)) { setClauses.Add("estimated_hours=@EH"); cmd.Parameters.AddWithValue("@EH", ehEl.ValueKind != JsonValueKind.Null ? ehEl.GetDecimal() : (object)DBNull.Value); }
        if (body.TryGetProperty("actualHours",    out var ahEl)) { setClauses.Add("actual_hours=@AH");    cmd.Parameters.AddWithValue("@AH", ahEl.ValueKind != JsonValueKind.Null ? ahEl.GetDecimal() : (object)DBNull.Value); }
        if (body.TryGetProperty("iterationId", out var itEl)) { setClauses.Add("iteration_id=@IT"); cmd.Parameters.AddWithValue("@IT", itEl.ValueKind != JsonValueKind.Null ? itEl.GetInt32() : (object)DBNull.Value); }
        if (body.TryGetProperty("parentTaskId",out var ptEl)) { setClauses.Add("parent_task_id=@PT"); cmd.Parameters.AddWithValue("@PT", ptEl.ValueKind != JsonValueKind.Null ? ptEl.GetInt32() : (object)DBNull.Value); }
        if (body.TryGetProperty("orderIndex",  out var oEl))  { setClauses.Add("order_index=@OI");   cmd.Parameters.AddWithValue("@OI", oEl.GetInt32()); }
        if (body.TryGetProperty("tags",        out var taEl) && taEl.ValueKind == JsonValueKind.Array) { setClauses.Add("tags=@TA"); cmd.Parameters.AddWithValue("@TA", taEl.GetRawText()); }
        if (body.TryGetProperty("meetingTaskId", out var mtEl)) { setClauses.Add("meeting_task_id=@MT"); cmd.Parameters.AddWithValue("@MT", mtEl.ValueKind != JsonValueKind.Null ? mtEl.GetInt32() : (object)DBNull.Value); }
        if (setClauses.Count == 0) return BadRequest("Nothing to update");
        setClauses.Add("updated_at=NOW()");
        try
        {
            using var conn = GetConnection();
            await conn.OpenAsync();
            cmd.Connection = conn;
            cmd.CommandText = $"UPDATE control_task SET {string.Join(",", setClauses)} WHERE id=@ID";
            cmd.Parameters.AddWithValue("@ID", id);
            // fetch boardId for SSE
            var boardIdCmd = new MySqlCommand("SELECT board_id FROM control_task WHERE id=@ID", conn);
            boardIdCmd.Parameters.AddWithValue("@ID", id);
            var boardId = Convert.ToInt32(await boardIdCmd.ExecuteScalarAsync());
            await cmd.ExecuteNonQueryAsync();
            _sse.NotifyBoard(boardId, "task_updated", new { id, boardId });
            _sse.NotifyTask(id, "task_updated", new { id });
            return Ok(new { id });
        }
        catch (Exception ex) { _logger.LogError(ex, "UpdateTask"); return StatusCode(500, ex.Message); }
    }

    /// DELETE /api/control/tasks/{id}
    [HttpDelete("tasks/{id:int}")]
    public async Task<IActionResult> DeleteTask(int id)
    {
        try
        {
            using var conn = GetConnection();
            await conn.OpenAsync();
            var boardIdCmd = new MySqlCommand("SELECT board_id FROM control_task WHERE id=@ID", conn);
            boardIdCmd.Parameters.AddWithValue("@ID", id);
            var boardId = Convert.ToInt32(await boardIdCmd.ExecuteScalarAsync());
            using var cmd = new MySqlCommand("DELETE FROM control_task WHERE id=@ID", conn);
            cmd.Parameters.AddWithValue("@ID", id);
            await cmd.ExecuteNonQueryAsync();
            _sse.NotifyBoard(boardId, "task_deleted", new { id, boardId });
            return Ok(new { id });
        }
        catch (Exception ex) { _logger.LogError(ex, "DeleteTask"); return StatusCode(500, ex.Message); }
    }

    // ====================================================================
    // TASK RELATIONS
    // ====================================================================

    /// GET /api/control/tasks/{id}/relations
    [HttpGet("tasks/{id:int}/relations")]
    public async Task<IActionResult> GetRelations(int id)
    {
        try
        {
            using var conn = GetConnection();
            await conn.OpenAsync();
            using var cmd = new MySqlCommand(@"
                SELECT r.id, r.source_task_id, r.target_task_id, r.relation_type, r.created_at,
                       t.title AS target_title, t.status AS target_status, t.type AS target_type
                FROM control_task_relation r
                JOIN control_task t ON t.id = r.target_task_id
                WHERE r.source_task_id = @ID
                UNION ALL
                SELECT r.id, r.source_task_id, r.target_task_id, r.relation_type, r.created_at,
                       t.title AS target_title, t.status AS target_status, t.type AS target_type
                FROM control_task_relation r
                JOIN control_task t ON t.id = r.source_task_id
                WHERE r.target_task_id = @ID2
                ORDER BY created_at", conn);
            cmd.Parameters.AddWithValue("@ID",  id);
            cmd.Parameters.AddWithValue("@ID2", id);
            var list = new List<object>();
            using var r = await cmd.ExecuteReaderAsync();
            while (await r.ReadAsync())
                list.Add(new {
                    id            = r.GetInt32("id"),
                    sourceTaskId  = r.GetInt32("source_task_id"),
                    targetTaskId  = r.GetInt32("target_task_id"),
                    relationType  = r.GetString("relation_type"),
                    targetTitle   = r.GetString("target_title"),
                    targetStatus  = r.GetString("target_status"),
                    targetType    = r.GetString("target_type"),
                    createdAt     = r.GetDateTime("created_at"),
                });
            return Ok(list);
        }
        catch (Exception ex) { _logger.LogError(ex, "GetRelations"); return StatusCode(500, ex.Message); }
    }

    /// POST /api/control/tasks/{id}/relations
    [HttpPost("tasks/{id:int}/relations")]
    public async Task<IActionResult> AddRelation(int id, [FromBody] JsonElement body)
    {
        var userId = GetCurrentUserId();
        if (!body.TryGetProperty("targetTaskId",  out var ttEl)) return BadRequest("targetTaskId required");
        if (!body.TryGetProperty("relationType",  out var rtEl)) return BadRequest("relationType required");
        var targetTaskId  = ttEl.GetInt32();
        var relationType  = rtEl.GetString()!;
        try
        {
            using var conn = GetConnection();
            await conn.OpenAsync();
            using var cmd = new MySqlCommand(@"
                INSERT IGNORE INTO control_task_relation (source_task_id, target_task_id, relation_type, created_by_user_id)
                VALUES (@S, @T, @R, @U); SELECT LAST_INSERT_ID();", conn);
            cmd.Parameters.AddWithValue("@S", id);
            cmd.Parameters.AddWithValue("@T", targetTaskId);
            cmd.Parameters.AddWithValue("@R", relationType);
            cmd.Parameters.AddWithValue("@U", (object?)userId ?? DBNull.Value);
            var newId = Convert.ToInt32(await cmd.ExecuteScalarAsync());
            _sse.NotifyTask(id, "relation_added", new { id = newId, taskId = id, targetTaskId, relationType });
            _sse.NotifyTask(targetTaskId, "relation_added", new { id = newId, taskId = id, targetTaskId, relationType });
            return Ok(new { id = newId });
        }
        catch (Exception ex) { _logger.LogError(ex, "AddRelation"); return StatusCode(500, ex.Message); }
    }

    /// DELETE /api/control/relations/{id}
    [HttpDelete("relations/{id:int}")]
    public async Task<IActionResult> DeleteRelation(int id)
    {
        try
        {
            using var conn = GetConnection();
            await conn.OpenAsync();
            var readCmd = new MySqlCommand("SELECT source_task_id, target_task_id FROM control_task_relation WHERE id=@ID", conn);
            readCmd.Parameters.AddWithValue("@ID", id);
            int sourceId = 0, targetId = 0;
            using (var r = await readCmd.ExecuteReaderAsync())
                if (await r.ReadAsync()) { sourceId = r.GetInt32(0); targetId = r.GetInt32(1); }
            using var cmd = new MySqlCommand("DELETE FROM control_task_relation WHERE id=@ID", conn);
            cmd.Parameters.AddWithValue("@ID", id);
            await cmd.ExecuteNonQueryAsync();
            if (sourceId > 0) _sse.NotifyTask(sourceId, "relation_removed", new { id });
            if (targetId > 0) _sse.NotifyTask(targetId, "relation_removed", new { id });
            return Ok(new { id });
        }
        catch (Exception ex) { _logger.LogError(ex, "DeleteRelation"); return StatusCode(500, ex.Message); }
    }

    // ====================================================================
    // TASK COMMENTS
    // ====================================================================

    /// GET /api/control/tasks/{id}/comments
    [HttpGet("tasks/{id:int}/comments")]
    public async Task<IActionResult> GetComments(int id)
    {
        try
        {
            using var conn = GetConnection();
            await conn.OpenAsync();
            using var cmd = new MySqlCommand(@"
                SELECT id, task_id, user_id, user_name, content, created_at, updated_at
                FROM control_task_comment WHERE task_id = @ID ORDER BY created_at", conn);
            cmd.Parameters.AddWithValue("@ID", id);
            var list = new List<object>();
            using var r = await cmd.ExecuteReaderAsync();
            while (await r.ReadAsync())
                list.Add(new {
                    id        = r.GetInt32("id"),
                    taskId    = r.GetInt32("task_id"),
                    userId    = r.GetInt32("user_id"),
                    userName  = IsNull(r, "user_name") ? null : r.GetString("user_name"),
                    content   = r.GetString("content"),
                    createdAt = r.GetDateTime("created_at"),
                    updatedAt = r.GetDateTime("updated_at"),
                });
            return Ok(list);
        }
        catch (Exception ex) { _logger.LogError(ex, "GetComments"); return StatusCode(500, ex.Message); }
    }

    /// POST /api/control/tasks/{id}/comments
    [HttpPost("tasks/{id:int}/comments")]
    public async Task<IActionResult> AddComment(int id, [FromBody] JsonElement body)
    {
        var userId   = GetCurrentUserId();
        var userName = GetCurrentUserName();
        if (!body.TryGetProperty("content", out var cEl)) return BadRequest("content required");
        var content = cEl.GetString()!;
        try
        {
            using var conn = GetConnection();
            await conn.OpenAsync();
            using var cmd = new MySqlCommand(@"
                INSERT INTO control_task_comment (task_id, user_id, user_name, content)
                VALUES (@TI, @UI, @UN, @CO); SELECT LAST_INSERT_ID();", conn);
            cmd.Parameters.AddWithValue("@TI", id);
            cmd.Parameters.AddWithValue("@UI", (object?)userId ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@UN", (object?)userName ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@CO", content);
            var newId = Convert.ToInt32(await cmd.ExecuteScalarAsync());
            _sse.NotifyTask(id, "comment_added", new { id = newId, taskId = id, userId, userName, content });
            return Ok(new { id = newId });
        }
        catch (Exception ex) { _logger.LogError(ex, "AddComment"); return StatusCode(500, ex.Message); }
    }

    /// DELETE /api/control/comments/{id}
    [HttpDelete("comments/{id:int}")]
    public async Task<IActionResult> DeleteComment(int id)
    {
        try
        {
            using var conn = GetConnection();
            await conn.OpenAsync();
            var readCmd = new MySqlCommand("SELECT task_id FROM control_task_comment WHERE id=@ID", conn);
            readCmd.Parameters.AddWithValue("@ID", id);
            var taskId = Convert.ToInt32(await readCmd.ExecuteScalarAsync());
            using var cmd = new MySqlCommand("DELETE FROM control_task_comment WHERE id=@ID", conn);
            cmd.Parameters.AddWithValue("@ID", id);
            await cmd.ExecuteNonQueryAsync();
            _sse.NotifyTask(taskId, "comment_removed", new { id });
            return Ok(new { id });
        }
        catch (Exception ex) { _logger.LogError(ex, "DeleteComment"); return StatusCode(500, ex.Message); }
    }

    // ====================================================================
    // TO-DO LISTS
    // ====================================================================

    /// GET /api/control/boards/{boardId}/todos?userId=
    [HttpGet("boards/{boardId:int}/todos")]
    public async Task<IActionResult> GetTodoLists(int boardId, [FromQuery] int? userId = null)
    {
        try
        {
            using var conn = GetConnection();
            await conn.OpenAsync();
            var where = "l.board_id = @B AND (l.is_shared = 1";
            if (userId.HasValue) where += " OR l.owner_user_id = @OU";
            where += ")";
            var sql = $@"
                SELECT l.id, l.board_id, l.name, l.owner_user_id, l.is_shared, l.order_index, l.created_at, l.updated_at
                FROM control_todo_list l
                WHERE {where} ORDER BY l.order_index, l.id";
            using var cmd = new MySqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@B", boardId);
            if (userId.HasValue) cmd.Parameters.AddWithValue("@OU", userId.Value);
            var lists = new List<object>();
            using var r = await cmd.ExecuteReaderAsync();
            while (await r.ReadAsync())
                lists.Add(new {
                    id           = r.GetInt32("id"),
                    boardId      = r.GetInt32("board_id"),
                    name         = r.GetString("name"),
                    ownerUserId  = r.GetInt32("owner_user_id"),
                    isShared     = r.GetBoolean("is_shared"),
                    orderIndex   = r.GetInt32("order_index"),
                    createdAt    = r.GetDateTime("created_at"),
                    updatedAt    = r.GetDateTime("updated_at"),
                });
            return Ok(lists);
        }
        catch (Exception ex) { _logger.LogError(ex, "GetTodoLists"); return StatusCode(500, ex.Message); }
    }

    /// POST /api/control/boards/{boardId}/todos
    [HttpPost("boards/{boardId:int}/todos")]
    public async Task<IActionResult> CreateTodoList(int boardId, [FromBody] JsonElement body)
    {
        var userId   = GetCurrentUserId();
        var name     = body.TryGetProperty("name",     out var nEl) ? nEl.GetString() : "Lista de tareas";
        var isShared = body.TryGetProperty("isShared", out var sEl) ? sEl.GetBoolean() : false;
        try
        {
            using var conn = GetConnection();
            await conn.OpenAsync();
            using var cmd = new MySqlCommand(@"
                INSERT INTO control_todo_list (board_id, name, owner_user_id, is_shared)
                VALUES (@B, @N, @U, @S); SELECT LAST_INSERT_ID();", conn);
            cmd.Parameters.AddWithValue("@B", boardId);
            cmd.Parameters.AddWithValue("@N", name);
            cmd.Parameters.AddWithValue("@U", (object?)userId ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@S", isShared);
            var newId = Convert.ToInt32(await cmd.ExecuteScalarAsync());
            return Ok(new { id = newId });
        }
        catch (Exception ex) { _logger.LogError(ex, "CreateTodoList"); return StatusCode(500, ex.Message); }
    }

    /// GET /api/control/todos/{listId}/items
    [HttpGet("todos/{listId:int}/items")]
    public async Task<IActionResult> GetTodoItems(int listId)
    {
        try
        {
            using var conn = GetConnection();
            await conn.OpenAsync();
            using var cmd = new MySqlCommand(@"
                SELECT id, list_id, title, done, due_date, control_task_id, order_index, created_at, updated_at
                FROM control_todo_item WHERE list_id = @L ORDER BY order_index, id", conn);
            cmd.Parameters.AddWithValue("@L", listId);
            var list = new List<object>();
            using var r = await cmd.ExecuteReaderAsync();
            while (await r.ReadAsync())
                list.Add(new {
                    id            = r.GetInt32("id"),
                    listId        = r.GetInt32("list_id"),
                    title         = r.GetString("title"),
                    done          = r.GetBoolean("done"),
                    dueDate       = IsNull(r, "due_date") ? (DateTime?)null : r.GetDateTime("due_date"),
                    controlTaskId = IsNull(r, "control_task_id") ? (int?)null : r.GetInt32("control_task_id"),
                    orderIndex    = r.GetInt32("order_index"),
                    createdAt     = r.GetDateTime("created_at"),
                    updatedAt     = r.GetDateTime("updated_at"),
                });
            return Ok(list);
        }
        catch (Exception ex) { _logger.LogError(ex, "GetTodoItems"); return StatusCode(500, ex.Message); }
    }

    /// POST /api/control/todos/{listId}/items
    [HttpPost("todos/{listId:int}/items")]
    public async Task<IActionResult> CreateTodoItem(int listId, [FromBody] JsonElement body)
    {
        if (!body.TryGetProperty("title", out var tEl)) return BadRequest("title required");
        var title         = tEl.GetString()!;
        var dueDate       = body.TryGetProperty("dueDate",       out var ddEl) && ddEl.ValueKind != JsonValueKind.Null ? ddEl.GetString() : null;
        var controlTaskId = body.TryGetProperty("controlTaskId", out var ctEl) && ctEl.ValueKind != JsonValueKind.Null ? ctEl.GetInt32() : (int?)null;
        try
        {
            using var conn = GetConnection();
            await conn.OpenAsync();
            using var cmd = new MySqlCommand(@"
                INSERT INTO control_todo_item (list_id, title, due_date, control_task_id)
                VALUES (@L, @T, @D, @CT); SELECT LAST_INSERT_ID();", conn);
            cmd.Parameters.AddWithValue("@L",  listId);
            cmd.Parameters.AddWithValue("@T",  title);
            cmd.Parameters.AddWithValue("@D",  string.IsNullOrEmpty(dueDate) ? (object)DBNull.Value : dueDate);
            cmd.Parameters.AddWithValue("@CT", (object?)controlTaskId ?? DBNull.Value);
            var newId = Convert.ToInt32(await cmd.ExecuteScalarAsync());
            return Ok(new { id = newId });
        }
        catch (Exception ex) { _logger.LogError(ex, "CreateTodoItem"); return StatusCode(500, ex.Message); }
    }

    /// PUT /api/control/todo-items/{id}
    [HttpPut("todo-items/{id:int}")]
    public async Task<IActionResult> UpdateTodoItem(int id, [FromBody] JsonElement body)
    {
        var setClauses = new List<string>();
        var cmd = new MySqlCommand();
        if (body.TryGetProperty("title",  out var tEl)) { setClauses.Add("title=@T");   cmd.Parameters.AddWithValue("@T", tEl.GetString()); }
        if (body.TryGetProperty("done",   out var dEl)) { setClauses.Add("done=@D");    cmd.Parameters.AddWithValue("@D", dEl.GetBoolean()); }
        if (body.TryGetProperty("dueDate",out var ddEl)){ setClauses.Add("due_date=@DD");cmd.Parameters.AddWithValue("@DD", string.IsNullOrEmpty(ddEl.GetString()) ? (object)DBNull.Value : ddEl.GetString()); }
        if (body.TryGetProperty("orderIndex", out var oEl)) { setClauses.Add("order_index=@OI"); cmd.Parameters.AddWithValue("@OI", oEl.GetInt32()); }
        if (setClauses.Count == 0) return BadRequest("Nothing to update");
        setClauses.Add("updated_at=NOW()");
        try
        {
            using var conn = GetConnection();
            await conn.OpenAsync();
            cmd.Connection = conn;
            cmd.CommandText = $"UPDATE control_todo_item SET {string.Join(",", setClauses)} WHERE id=@ID";
            cmd.Parameters.AddWithValue("@ID", id);
            await cmd.ExecuteNonQueryAsync();
            return Ok(new { id });
        }
        catch (Exception ex) { _logger.LogError(ex, "UpdateTodoItem"); return StatusCode(500, ex.Message); }
    }

    /// DELETE /api/control/todo-items/{id}
    [HttpDelete("todo-items/{id:int}")]
    public async Task<IActionResult> DeleteTodoItem(int id)
    {
        try
        {
            using var conn = GetConnection();
            await conn.OpenAsync();
            using var cmd = new MySqlCommand("DELETE FROM control_todo_item WHERE id=@ID", conn);
            cmd.Parameters.AddWithValue("@ID", id);
            await cmd.ExecuteNonQueryAsync();
            return Ok(new { id });
        }
        catch (Exception ex) { _logger.LogError(ex, "DeleteTodoItem"); return StatusCode(500, ex.Message); }
    }

    /// DELETE /api/control/todos/{listId}
    [HttpDelete("todos/{listId:int}")]
    public async Task<IActionResult> DeleteTodoList(int listId)
    {
        try
        {
            using var conn = GetConnection();
            await conn.OpenAsync();
            using var delItems = new MySqlCommand("DELETE FROM control_todo_item WHERE list_id=@L", conn);
            delItems.Parameters.AddWithValue("@L", listId);
            await delItems.ExecuteNonQueryAsync();
            using var cmd = new MySqlCommand("DELETE FROM control_todo_list WHERE id=@ID", conn);
            cmd.Parameters.AddWithValue("@ID", listId);
            await cmd.ExecuteNonQueryAsync();
            return Ok(new { id = listId });
        }
        catch (Exception ex) { _logger.LogError(ex, "DeleteTodoList"); return StatusCode(500, ex.Message); }
    }

    // ====================================================================
    // SSE — REAL-TIME EVENTS
    // ====================================================================

    /// GET /api/control/boards/{boardId}/events?userId=&userName=
    [HttpGet("boards/{boardId:int}/events")]
    public async Task StreamBoardEvents(int boardId, [FromQuery] int? userId, [FromQuery] string? userName, CancellationToken ct)
    {
        Response.Headers["Content-Type"]  = "text/event-stream";
        Response.Headers["Cache-Control"] = "no-cache";
        Response.Headers["X-Accel-Buffering"] = "no";

        if (userId.HasValue && !string.IsNullOrEmpty(userName))
        {
            _sse.AddPresence(boardId, userId.Value, userName);
            _sse.NotifyBoard(boardId, "presence_updated", new { presence = _sse.GetPresence(boardId) });
        }

        var ch = _sse.SubscribeBoard(boardId);
        await Response.Body.WriteAsync(System.Text.Encoding.UTF8.GetBytes($"event: connected\ndata: {{\"boardId\":{boardId}}}\n\n"), ct);
        await Response.Body.FlushAsync(ct);

        try
        {
            await foreach (var msg in ch.Reader.ReadAllAsync(ct))
            {
                await Response.Body.WriteAsync(System.Text.Encoding.UTF8.GetBytes(msg), ct);
                await Response.Body.FlushAsync(ct);
            }
        }
        catch (OperationCanceledException) { }
        finally
        {
            _sse.UnsubscribeBoard(boardId, ch);
            if (userId.HasValue)
            {
                _sse.RemovePresence(boardId, userId.Value);
                _sse.NotifyBoard(boardId, "presence_updated", new { presence = _sse.GetPresence(boardId) });
            }
        }
    }

    /// GET /api/control/tasks/{taskId}/events
    [HttpGet("tasks/{taskId:int}/events")]
    public async Task StreamTaskEvents(int taskId, CancellationToken ct)
    {
        Response.Headers["Content-Type"]  = "text/event-stream";
        Response.Headers["Cache-Control"] = "no-cache";
        Response.Headers["X-Accel-Buffering"] = "no";

        var ch = _sse.SubscribeTask(taskId);
        await Response.Body.WriteAsync(System.Text.Encoding.UTF8.GetBytes($"event: connected\ndata: {{\"taskId\":{taskId}}}\n\n"), ct);
        await Response.Body.FlushAsync(ct);

        try
        {
            await foreach (var msg in ch.Reader.ReadAllAsync(ct))
            {
                await Response.Body.WriteAsync(System.Text.Encoding.UTF8.GetBytes(msg), ct);
                await Response.Body.FlushAsync(ct);
            }
        }
        catch (OperationCanceledException) { }
        finally { _sse.UnsubscribeTask(taskId, ch); }
    }

    // ====================================================================
    // HELPERS
    // ====================================================================

    private static object MapTask(MySqlDataReader r)
    {
        string? tagsRaw = null;
        try { tagsRaw = r.IsDBNull(r.GetOrdinal("tags")) ? null : r.GetString("tags"); } catch { }
        List<string>? tags = null;
        if (!string.IsNullOrEmpty(tagsRaw))
            try { tags = JsonSerializer.Deserialize<List<string>>(tagsRaw); } catch { }

        return new {
            id                = r.GetInt32("id"),
            boardId           = r.GetInt32("board_id"),
            iterationId       = r.IsDBNull(r.GetOrdinal("iteration_id"))   ? (int?)null : r.GetInt32("iteration_id"),
            parentTaskId      = r.IsDBNull(r.GetOrdinal("parent_task_id")) ? (int?)null : r.GetInt32("parent_task_id"),
            type              = r.GetString("type"),
            title             = r.GetString("title"),
            description       = r.IsDBNull(r.GetOrdinal("description"))    ? null : r.GetString("description"),
            status            = r.GetString("status"),
            priority          = r.GetString("priority"),
            assignedToUserId  = r.IsDBNull(r.GetOrdinal("assigned_to_user_id")) ? (int?)null : r.GetInt32("assigned_to_user_id"),
            assignedToName    = r.IsDBNull(r.GetOrdinal("assigned_to_name"))    ? null : r.GetString("assigned_to_name"),
            dueDate           = r.IsDBNull(r.GetOrdinal("due_date"))   ? (DateTime?)null : r.GetDateTime("due_date"),
            startDate         = r.IsDBNull(r.GetOrdinal("start_date")) ? (DateTime?)null : r.GetDateTime("start_date"),
            estimatedHours    = r.IsDBNull(r.GetOrdinal("estimated_hours")) ? (decimal?)null : r.GetDecimal("estimated_hours"),
            actualHours       = r.IsDBNull(r.GetOrdinal("actual_hours"))    ? (decimal?)null : r.GetDecimal("actual_hours"),
            orderIndex        = r.GetInt32("order_index"),
            tags              = tags,
            meetingTaskId     = r.IsDBNull(r.GetOrdinal("meeting_task_id"))    ? (int?)null : r.GetInt32("meeting_task_id"),
            createdByUserId   = r.GetInt32("created_by_user_id"),
            createdAt         = r.GetDateTime("created_at"),
            updatedAt         = r.GetDateTime("updated_at"),
            childCount        = r.GetInt32("child_count"),
            commentCount      = r.GetInt32("comment_count"),
        };
    }
}
