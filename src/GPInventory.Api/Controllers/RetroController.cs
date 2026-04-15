#pragma warning disable CS8601
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Cors;
using Microsoft.AspNetCore.Mvc;
using MySqlConnector;
using System.Security.Claims;
using GPInventory.Api.Services;

namespace GPInventory.Api.Controllers;

/// <summary>
/// GP Control – Retrospectivas con SSE en tiempo real.
/// Fases: collect → group → vote → closed
/// </summary>
[ApiController]
[Route("api/retro")]
[EnableCors("AllowFrontend")]
[Authorize]
public class RetroController : ControllerBase
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<RetroController> _logger;
    private readonly RetroSseService _sse;

    public RetroController(IConfiguration cfg, ILogger<RetroController> logger, RetroSseService sse)
    {
        _configuration = cfg;
        _logger = logger;
        _sse = sse;
    }

    private MySqlConnection GetConnection()
        => new(_configuration.GetConnectionString("DefaultConnection")!);

    private static bool IsNull(MySqlDataReader r, string col) => r.IsDBNull(r.GetOrdinal(col));

    private int? GetCurrentUserId()
    {
        var claim = User.FindFirst("sub") ?? User.FindFirst("userId")
                 ?? User.FindFirst("id")  ?? User.FindFirst(ClaimTypes.NameIdentifier);
        return int.TryParse(claim?.Value, out int id) ? id : null;
    }
    private string? GetCurrentUserName()
        => User.FindFirst("name")?.Value ?? User.FindFirst(ClaimTypes.Name)?.Value;

    // ── map helpers ───────────────────────────────────────────────────────

    private static object MapSession(MySqlDataReader r) => new {
        id              = r.GetInt32("id"),
        boardId         = r.GetInt32("board_id"),
        iterationId     = IsNull(r, "iteration_id") ? (int?)null : r.GetInt32("iteration_id"),
        name            = r.GetString("name"),
        phase           = r.GetString("phase"),
        iterationName   = IsNull(r, "iteration_name") ? null : r.GetString("iteration_name"),
        createdAt       = r.GetDateTime("created_at"),
        updatedAt       = r.GetDateTime("updated_at"),
    };

    private static object MapCard(MySqlDataReader r) => new {
        id           = r.GetInt32("id"),
        sessionId    = r.GetInt32("session_id"),
        authorUserId = r.GetInt32("author_user_id"),
        authorName   = IsNull(r, "author_name") ? null : r.GetString("author_name"),
        columnName   = r.GetString("column_name"),
        content      = r.GetString("content"),
        groupId      = IsNull(r, "group_id") ? (int?)null : r.GetInt32("group_id"),
        votes        = r.GetInt32("votes"),
        userVoted    = r.GetInt32("user_voted") > 0,
        createdAt    = r.GetDateTime("created_at"),
    };

    private static object MapGroup(MySqlDataReader r) => new {
        id        = r.GetInt32("id"),
        sessionId = r.GetInt32("session_id"),
        name      = r.GetString("name"),
        color     = r.GetString("color"),
    };

    // ====================================================================
    // SESSIONS
    // ====================================================================

    /// GET /api/retro/sessions?boardId=
    [HttpGet("sessions")]
    public async Task<IActionResult> GetSessions([FromQuery] int boardId)
    {
        try
        {
            await using var cn = GetConnection();
            await cn.OpenAsync();
            await using var cmd = cn.CreateCommand();
            cmd.CommandText = @"
                SELECT rs.id, rs.board_id, rs.iteration_id, rs.name, rs.phase, rs.created_at, rs.updated_at,
                       ci.name AS iteration_name
                FROM retro_session rs
                LEFT JOIN control_iteration ci ON ci.id = rs.iteration_id
                WHERE rs.board_id = @bid
                ORDER BY rs.created_at DESC";
            cmd.Parameters.AddWithValue("@bid", boardId);
            await using var r = await cmd.ExecuteReaderAsync();
            var list = new List<object>();
            while (await r.ReadAsync()) list.Add(MapSession(r));
            return Ok(list);
        }
        catch (Exception ex) { _logger.LogError(ex, "GetSessions"); return StatusCode(500, ex.Message); }
    }

    /// GET /api/retro/sessions/{id}
    [HttpGet("sessions/{id:int}")]
    public async Task<IActionResult> GetSession(int id)
    {
        try
        {
            await using var cn = GetConnection();
            await cn.OpenAsync();
            await using var cmd = cn.CreateCommand();
            cmd.CommandText = @"
                SELECT rs.id, rs.board_id, rs.iteration_id, rs.name, rs.phase, rs.created_at, rs.updated_at,
                       ci.name AS iteration_name
                FROM retro_session rs
                LEFT JOIN control_iteration ci ON ci.id = rs.iteration_id
                WHERE rs.id = @id";
            cmd.Parameters.AddWithValue("@id", id);
            await using var r = await cmd.ExecuteReaderAsync();
            if (!await r.ReadAsync()) return NotFound();
            return Ok(MapSession(r));
        }
        catch (Exception ex) { _logger.LogError(ex, "GetSession"); return StatusCode(500, ex.Message); }
    }

    /// POST /api/retro/sessions
    [HttpPost("sessions")]
    public async Task<IActionResult> CreateSession([FromBody] dynamic body)
    {
        try
        {
            using var doc  = System.Text.Json.JsonDocument.Parse(body.ToString());
            var root       = doc.RootElement;
            int boardId    = root.GetProperty("boardId").GetInt32();
            string name    = root.GetProperty("name").GetString()!;
            int? iterId    = root.TryGetProperty("iterationId", out System.Text.Json.JsonElement it)
                             && it.ValueKind != System.Text.Json.JsonValueKind.Null
                             ? it.GetInt32() : (int?)null;
            int userId     = GetCurrentUserId() ?? 0;

            await using var cn = GetConnection();
            await cn.OpenAsync();
            await using var cmd = cn.CreateCommand();
            cmd.CommandText = @"
                INSERT INTO retro_session (board_id, iteration_id, name, created_by_user_id)
                VALUES (@bid, @iid, @name, @uid);
                SELECT LAST_INSERT_ID();";
            cmd.Parameters.AddWithValue("@bid",  boardId);
            cmd.Parameters.AddWithValue("@iid",  iterId.HasValue ? iterId.Value : DBNull.Value);
            cmd.Parameters.AddWithValue("@name", name);
            cmd.Parameters.AddWithValue("@uid",  userId);
            var newId = Convert.ToInt32(await cmd.ExecuteScalarAsync());
            return Ok(new { id = newId });
        }
        catch (Exception ex) { _logger.LogError(ex, "CreateSession"); return StatusCode(500, ex.Message); }
    }

    /// PUT /api/retro/sessions/{id}
    [HttpPut("sessions/{id:int}")]
    public async Task<IActionResult> UpdateSession(int id, [FromBody] dynamic body)
    {
        try
        {
            using var doc = System.Text.Json.JsonDocument.Parse(body.ToString());
            var root = doc.RootElement;
            string? phase = root.TryGetProperty("phase", out System.Text.Json.JsonElement ph) ? ph.GetString() : null;
            string? name  = root.TryGetProperty("name",  out System.Text.Json.JsonElement nm) ? nm.GetString() : null;

            await using var cn = GetConnection();
            await cn.OpenAsync();
            await using var cmd = cn.CreateCommand();
            cmd.CommandText = @"
                UPDATE retro_session SET
                  phase = COALESCE(@phase, phase),
                  name  = COALESCE(@name,  name)
                WHERE id = @id";
            cmd.Parameters.AddWithValue("@phase", (object?)phase ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@name",  (object?)name  ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@id",    id);
            await cmd.ExecuteNonQueryAsync();

            if (phase != null)
                _sse.Notify(id, "phase_changed", new { sessionId = id, phase });

            return Ok();
        }
        catch (Exception ex) { _logger.LogError(ex, "UpdateSession"); return StatusCode(500, ex.Message); }
    }

    /// DELETE /api/retro/sessions/{id}
    [HttpDelete("sessions/{id:int}")]
    public async Task<IActionResult> DeleteSession(int id)
    {
        try
        {
            await using var cn = GetConnection();
            await cn.OpenAsync();
            await using var cmd = cn.CreateCommand();
            cmd.CommandText = "DELETE FROM retro_session WHERE id = @id";
            cmd.Parameters.AddWithValue("@id", id);
            await cmd.ExecuteNonQueryAsync();
            return Ok();
        }
        catch (Exception ex) { _logger.LogError(ex, "DeleteSession"); return StatusCode(500, ex.Message); }
    }

    // ====================================================================
    // CARDS
    // ====================================================================

    private async Task<List<object>> FetchCards(MySqlConnection cn, int sessionId, int? currentUserId)
    {
        await using var cmd = cn.CreateCommand();
        cmd.CommandText = @"
            SELECT c.id, c.session_id, c.author_user_id, c.author_name, c.column_name,
                   c.content, c.group_id, c.created_at,
                   COUNT(v.user_id) AS votes,
                   MAX(CASE WHEN v.user_id = @uid THEN 1 ELSE 0 END) AS user_voted
            FROM retro_card c
            LEFT JOIN retro_card_vote v ON v.card_id = c.id
            WHERE c.session_id = @sid
            GROUP BY c.id
            ORDER BY c.created_at";
        cmd.Parameters.AddWithValue("@sid", sessionId);
        cmd.Parameters.AddWithValue("@uid", currentUserId.HasValue ? currentUserId.Value : DBNull.Value);
        await using var r = await cmd.ExecuteReaderAsync();
        var list = new List<object>();
        while (await r.ReadAsync()) list.Add(MapCard(r));
        return list;
    }

    /// GET /api/retro/sessions/{id}/cards
    [HttpGet("sessions/{id:int}/cards")]
    public async Task<IActionResult> GetCards(int id)
    {
        try
        {
            await using var cn = GetConnection();
            await cn.OpenAsync();
            var userId = GetCurrentUserId();
            return Ok(await FetchCards(cn, id, userId));
        }
        catch (Exception ex) { _logger.LogError(ex, "GetCards"); return StatusCode(500, ex.Message); }
    }

    /// POST /api/retro/sessions/{id}/cards
    [HttpPost("sessions/{id:int}/cards")]
    public async Task<IActionResult> CreateCard(int id, [FromBody] dynamic body)
    {
        try
        {
            using var doc      = System.Text.Json.JsonDocument.Parse(body.ToString());
            var root           = doc.RootElement;
            string col         = root.GetProperty("columnName").GetString()!;
            string content     = root.GetProperty("content").GetString()!;
            int userId         = GetCurrentUserId() ?? 0;
            string? userName   = GetCurrentUserName();

            await using var cn = GetConnection();
            await cn.OpenAsync();
            await using var cmd = cn.CreateCommand();
            cmd.CommandText = @"
                INSERT INTO retro_card (session_id, author_user_id, author_name, column_name, content)
                VALUES (@sid, @uid, @uname, @col, @content);
                SELECT LAST_INSERT_ID();";
            cmd.Parameters.AddWithValue("@sid",     id);
            cmd.Parameters.AddWithValue("@uid",     userId);
            cmd.Parameters.AddWithValue("@uname",   (object?)userName ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@col",     col);
            cmd.Parameters.AddWithValue("@content", content);
            var newId = Convert.ToInt32(await cmd.ExecuteScalarAsync());

            var card = new { id = newId, sessionId = id, authorUserId = userId, authorName = userName,
                             columnName = col, content, groupId = (int?)null, votes = 0, userVoted = false };
            _sse.Notify(id, "card_added", card);
            return Ok(new { id = newId });
        }
        catch (Exception ex) { _logger.LogError(ex, "CreateCard"); return StatusCode(500, ex.Message); }
    }

    /// PUT /api/retro/cards/{id}
    [HttpPut("cards/{cardId:int}")]
    public async Task<IActionResult> UpdateCard(int cardId, [FromBody] dynamic body)
    {
        try
        {
            using var doc = System.Text.Json.JsonDocument.Parse(body.ToString());
            var root = doc.RootElement;
            string? content = root.TryGetProperty("content", out System.Text.Json.JsonElement ct2)   ? ct2.GetString()  : null;
            int? groupId    = root.TryGetProperty("groupId", out System.Text.Json.JsonElement gid)
                              && gid.ValueKind != System.Text.Json.JsonValueKind.Null
                              ? gid.GetInt32() : (int?)null;
            bool clearGroup = root.TryGetProperty("groupId", out System.Text.Json.JsonElement gidRaw)
                              && gidRaw.ValueKind == System.Text.Json.JsonValueKind.Null;

            await using var cn = GetConnection();
            await cn.OpenAsync();

            // get session_id for SSE
            int sessionId;
            await using (var cmd2 = cn.CreateCommand())
            {
                cmd2.CommandText = "SELECT session_id FROM retro_card WHERE id = @id";
                cmd2.Parameters.AddWithValue("@id", cardId);
                sessionId = Convert.ToInt32(await cmd2.ExecuteScalarAsync());
            }

            await using var cmd = cn.CreateCommand();
            cmd.CommandText = @"
                UPDATE retro_card SET
                  content  = COALESCE(@content, content),
                  group_id = CASE WHEN @clearGroup = 1 THEN NULL WHEN @hasGroup = 1 THEN @gid ELSE group_id END
                WHERE id = @cid";
            cmd.Parameters.AddWithValue("@content",    (object?)content ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@clearGroup", clearGroup ? 1 : 0);
            cmd.Parameters.AddWithValue("@hasGroup",   groupId.HasValue ? 1 : 0);
            cmd.Parameters.AddWithValue("@gid",        groupId.HasValue ? groupId.Value : DBNull.Value);
            cmd.Parameters.AddWithValue("@cid",        cardId);
            await cmd.ExecuteNonQueryAsync();

            _sse.Notify(sessionId, "card_updated", new { id = cardId, content, groupId });
            return Ok();
        }
        catch (Exception ex) { _logger.LogError(ex, "UpdateCard"); return StatusCode(500, ex.Message); }
    }

    /// DELETE /api/retro/cards/{id}
    [HttpDelete("cards/{cardId:int}")]
    public async Task<IActionResult> DeleteCard(int cardId)
    {
        try
        {
            await using var cn = GetConnection();
            await cn.OpenAsync();

            int sessionId;
            await using (var cmd2 = cn.CreateCommand())
            {
                cmd2.CommandText = "SELECT session_id FROM retro_card WHERE id = @id";
                cmd2.Parameters.AddWithValue("@id", cardId);
                sessionId = Convert.ToInt32(await cmd2.ExecuteScalarAsync());
            }

            await using var cmd = cn.CreateCommand();
            cmd.CommandText = "DELETE FROM retro_card WHERE id = @id";
            cmd.Parameters.AddWithValue("@id", cardId);
            await cmd.ExecuteNonQueryAsync();

            _sse.Notify(sessionId, "card_deleted", new { id = cardId });
            return Ok();
        }
        catch (Exception ex) { _logger.LogError(ex, "DeleteCard"); return StatusCode(500, ex.Message); }
    }

    // ====================================================================
    // VOTES
    // ====================================================================

    /// POST /api/retro/cards/{cardId}/vote  — toggle vote
    [HttpPost("cards/{cardId:int}/vote")]
    public async Task<IActionResult> ToggleVote(int cardId)
    {
        try
        {
            int userId = GetCurrentUserId() ?? 0;
            await using var cn = GetConnection();
            await cn.OpenAsync();

            int sessionId;
            await using (var cmd2 = cn.CreateCommand())
            {
                cmd2.CommandText = "SELECT session_id FROM retro_card WHERE id = @id";
                cmd2.Parameters.AddWithValue("@id", cardId);
                sessionId = Convert.ToInt32(await cmd2.ExecuteScalarAsync());
            }

            // check existing vote
            bool hasVote;
            await using (var cmd3 = cn.CreateCommand())
            {
                cmd3.CommandText = "SELECT COUNT(*) FROM retro_card_vote WHERE card_id = @cid AND user_id = @uid";
                cmd3.Parameters.AddWithValue("@cid", cardId);
                cmd3.Parameters.AddWithValue("@uid", userId);
                hasVote = Convert.ToInt32(await cmd3.ExecuteScalarAsync()) > 0;
            }

            await using var cmd = cn.CreateCommand();
            if (hasVote)
                cmd.CommandText = "DELETE FROM retro_card_vote WHERE card_id = @cid AND user_id = @uid";
            else
                cmd.CommandText = "INSERT IGNORE INTO retro_card_vote (card_id, user_id) VALUES (@cid, @uid)";
            cmd.Parameters.AddWithValue("@cid", cardId);
            cmd.Parameters.AddWithValue("@uid", userId);
            await cmd.ExecuteNonQueryAsync();

            // get updated vote count
            int voteCount;
            await using (var cmd4 = cn.CreateCommand())
            {
                cmd4.CommandText = "SELECT COUNT(*) FROM retro_card_vote WHERE card_id = @cid";
                cmd4.Parameters.AddWithValue("@cid", cardId);
                voteCount = Convert.ToInt32(await cmd4.ExecuteScalarAsync());
            }

            _sse.Notify(sessionId, "vote_updated", new { cardId, votes = voteCount, userId, userVoted = !hasVote });
            return Ok(new { votes = voteCount, userVoted = !hasVote });
        }
        catch (Exception ex) { _logger.LogError(ex, "ToggleVote"); return StatusCode(500, ex.Message); }
    }

    // ====================================================================
    // GROUPS
    // ====================================================================

    /// GET /api/retro/sessions/{id}/groups
    [HttpGet("sessions/{id:int}/groups")]
    public async Task<IActionResult> GetGroups(int id)
    {
        try
        {
            await using var cn = GetConnection();
            await cn.OpenAsync();
            await using var cmd = cn.CreateCommand();
            cmd.CommandText = "SELECT id, session_id, name, color FROM retro_group WHERE session_id = @sid ORDER BY id";
            cmd.Parameters.AddWithValue("@sid", id);
            await using var r = await cmd.ExecuteReaderAsync();
            var list = new List<object>();
            while (await r.ReadAsync()) list.Add(MapGroup(r));
            return Ok(list);
        }
        catch (Exception ex) { _logger.LogError(ex, "GetGroups"); return StatusCode(500, ex.Message); }
    }

    /// POST /api/retro/sessions/{id}/groups
    [HttpPost("sessions/{id:int}/groups")]
    public async Task<IActionResult> CreateGroup(int id, [FromBody] dynamic body)
    {
        try
        {
            using var doc  = System.Text.Json.JsonDocument.Parse(body.ToString());
            var root       = doc.RootElement;
            string name    = root.GetProperty("name").GetString()!;
            string color   = root.TryGetProperty("color", out System.Text.Json.JsonElement col) ? (col.GetString() ?? "#6366f1") : "#6366f1";

            await using var cn = GetConnection();
            await cn.OpenAsync();
            await using var cmd = cn.CreateCommand();
            cmd.CommandText = @"
                INSERT INTO retro_group (session_id, name, color) VALUES (@sid, @name, @color);
                SELECT LAST_INSERT_ID();";
            cmd.Parameters.AddWithValue("@sid",   id);
            cmd.Parameters.AddWithValue("@name",  name);
            cmd.Parameters.AddWithValue("@color", color);
            var newId = Convert.ToInt32(await cmd.ExecuteScalarAsync());

            _sse.Notify(id, "group_created", new { id = newId, sessionId = id, name, color });
            return Ok(new { id = newId });
        }
        catch (Exception ex) { _logger.LogError(ex, "CreateGroup"); return StatusCode(500, ex.Message); }
    }

    /// DELETE /api/retro/groups/{groupId}
    [HttpDelete("groups/{groupId:int}")]
    public async Task<IActionResult> DeleteGroup(int groupId)
    {
        try
        {
            await using var cn = GetConnection();
            await cn.OpenAsync();

            int sessionId;
            await using (var cmd2 = cn.CreateCommand())
            {
                cmd2.CommandText = "SELECT session_id FROM retro_group WHERE id = @id";
                cmd2.Parameters.AddWithValue("@id", groupId);
                sessionId = Convert.ToInt32(await cmd2.ExecuteScalarAsync());
            }

            await using var cmd = cn.CreateCommand();
            cmd.CommandText = "DELETE FROM retro_group WHERE id = @id";
            cmd.Parameters.AddWithValue("@id", groupId);
            await cmd.ExecuteNonQueryAsync();

            _sse.Notify(sessionId, "group_deleted", new { id = groupId });
            return Ok();
        }
        catch (Exception ex) { _logger.LogError(ex, "DeleteGroup"); return StatusCode(500, ex.Message); }
    }

    // ====================================================================
    // SSE — REAL-TIME
    // ====================================================================

    /// GET /api/retro/sessions/{id}/events?userId=&userName=
    [HttpGet("sessions/{id:int}/events")]
    public async Task StreamRetroEvents(int id, [FromQuery] int? userId, [FromQuery] string? userName, CancellationToken ct)
    {
        Response.Headers["Content-Type"]      = "text/event-stream";
        Response.Headers["Cache-Control"]     = "no-cache";
        Response.Headers["X-Accel-Buffering"] = "no";

        // Add presence and notify everyone
        if (userId.HasValue && !string.IsNullOrEmpty(userName))
        {
            _sse.AddPresence(id, userId.Value, userName);
            _sse.Notify(id, "presence_updated", new { presence = _sse.GetPresence(id) });
        }

        var ch = _sse.Subscribe(id);
        // Send initial connected + current presence snapshot to the new subscriber
        var presenceJson = System.Text.Json.JsonSerializer.Serialize(
            new { presence = _sse.GetPresence(id) },
            new System.Text.Json.JsonSerializerOptions { PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase });
        await Response.Body.WriteAsync(System.Text.Encoding.UTF8.GetBytes($"event: connected\ndata: {{\"sessionId\":{id}}}\n\nevent: presence_updated\ndata: {presenceJson}\n\n"), ct);
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
            _sse.Unsubscribe(id, ch);
            if (userId.HasValue)
            {
                _sse.RemovePresence(id, userId.Value);
                _sse.Notify(id, "presence_updated", new { presence = _sse.GetPresence(id) });
            }
        }
    }
}
