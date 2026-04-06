#pragma warning disable CS8601
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Cors;
using Microsoft.AspNetCore.Mvc;
using MySqlConnector;
using System.Security.Claims;
using System.Text.Json;

namespace GPInventory.Api.Controllers;

/// <summary>
/// GP Binnacle – Bitácora diaria.
/// Gestión de proyectos, entradas diarias e ítems cronológicos.
/// </summary>
[ApiController]
[Route("api/binnacle")]
[EnableCors("AllowFrontend")]
[Authorize]
public class BinnacleController : ControllerBase
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<BinnacleController> _logger;

    public BinnacleController(IConfiguration configuration, ILogger<BinnacleController> logger)
    {
        _configuration = configuration;
        _logger = logger;
    }

    private MySqlConnection GetConnection()
        => new(_configuration.GetConnectionString("DefaultConnection")!);

    private static bool IsNull(MySqlDataReader r, string col) => r.IsDBNull(r.GetOrdinal(col));

    /// Extrae el userId del JWT. Retorna null si no está disponible.
    private int? GetCurrentUserId()
    {
        var claim = User.FindFirst("sub")
                 ?? User.FindFirst("userId")
                 ?? User.FindFirst("id")
                 ?? User.FindFirst(ClaimTypes.NameIdentifier);
        return int.TryParse(claim?.Value, out int id) ? id : null;
    }

    // ====================================================================
    // PROYECTOS
    // ====================================================================

    /// GET /api/binnacle/projects?businessId=
    [HttpGet("projects")]
    public async Task<IActionResult> GetProjects([FromQuery] int businessId)
    {
        try
        {
            using var conn = GetConnection();
            await conn.OpenAsync();
            using var cmd = new MySqlCommand(@"
                SELECT id, name, description, color, business_id, active,
                       created_by_user_id, created_at, updated_at
                FROM binnacle_project
                WHERE business_id = @B AND active = 1
                ORDER BY name", conn);
            cmd.Parameters.AddWithValue("@B", businessId);

            var list = new List<object>();
            using var r = await cmd.ExecuteReaderAsync();
            while (await r.ReadAsync())
            {
                list.Add(new
                {
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
            }
            return Ok(list);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error obteniendo proyectos de bitácora");
            return StatusCode(500, new { message = "Error interno del servidor" });
        }
    }

    /// POST /api/binnacle/projects
    [HttpPost("projects")]
    public async Task<IActionResult> CreateProject([FromBody] JsonElement body)
    {
        var userId = GetCurrentUserId();
        if (userId == null) return Unauthorized();

        try
        {
            var name        = body.GetProperty("name").GetString()!;
            var description = body.TryGetProperty("description", out var d) ? d.GetString() : null;
            var color       = body.TryGetProperty("color", out var c) ? c.GetString() ?? "#6366F1" : "#6366F1";
            var businessId  = body.GetProperty("businessId").GetInt32();

            using var conn = GetConnection();
            await conn.OpenAsync();
            using var cmd = new MySqlCommand(@"
                INSERT INTO binnacle_project (name, description, color, business_id, active, created_by_user_id)
                VALUES (@Name, @Desc, @Color, @B, 1, @UID);
                SELECT LAST_INSERT_ID();", conn);
            cmd.Parameters.AddWithValue("@Name",  name);
            cmd.Parameters.AddWithValue("@Desc",  (object?)description ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@Color", color);
            cmd.Parameters.AddWithValue("@B",     businessId);
            cmd.Parameters.AddWithValue("@UID",   userId);

            var newId = Convert.ToInt32(await cmd.ExecuteScalarAsync());
            return CreatedAtAction(nameof(GetProjects), new { businessId }, new { id = newId });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creando proyecto de bitácora");
            return StatusCode(500, new { message = "Error interno del servidor" });
        }
    }

    /// PATCH /api/binnacle/projects/{id}
    [HttpPatch("projects/{id:int}")]
    public async Task<IActionResult> UpdateProject(int id, [FromBody] JsonElement body)
    {
        try
        {
            using var conn = GetConnection();
            await conn.OpenAsync();

            var setClauses = new List<string>();
            var cmd = new MySqlCommand("", conn);

            if (body.TryGetProperty("name", out var n))        { setClauses.Add("name=@Name");        cmd.Parameters.AddWithValue("@Name",   n.GetString()); }
            if (body.TryGetProperty("description", out var de)) { setClauses.Add("description=@Desc"); cmd.Parameters.AddWithValue("@Desc",   de.ValueKind == JsonValueKind.Null ? DBNull.Value : de.GetString()); }
            if (body.TryGetProperty("color", out var co))      { setClauses.Add("color=@Color");      cmd.Parameters.AddWithValue("@Color",  co.GetString()); }
            if (body.TryGetProperty("active", out var ac))     { setClauses.Add("active=@Active");    cmd.Parameters.AddWithValue("@Active", ac.GetBoolean() ? 1 : 0); }

            if (setClauses.Count == 0) return BadRequest(new { message = "Nada que actualizar" });

            cmd.CommandText = $"UPDATE binnacle_project SET {string.Join(",", setClauses)} WHERE id=@Id";
            cmd.Parameters.AddWithValue("@Id", id);
            await cmd.ExecuteNonQueryAsync();

            return Ok(new { id });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error actualizando proyecto {Id}", id);
            return StatusCode(500, new { message = "Error interno del servidor" });
        }
    }

    /// DELETE /api/binnacle/projects/{id}  (soft-delete)
    [HttpDelete("projects/{id:int}")]
    public async Task<IActionResult> DeleteProject(int id)
    {
        try
        {
            using var conn = GetConnection();
            await conn.OpenAsync();
            using var cmd = new MySqlCommand("UPDATE binnacle_project SET active=0 WHERE id=@Id", conn);
            cmd.Parameters.AddWithValue("@Id", id);
            await cmd.ExecuteNonQueryAsync();
            return NoContent();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error eliminando proyecto {Id}", id);
            return StatusCode(500, new { message = "Error interno del servidor" });
        }
    }

    // ====================================================================
    // ENTRADAS
    // ====================================================================

    // Helper: leer ítems de una entrada
    private static async Task<List<object>> ReadItems(MySqlConnection conn, int entryId)
    {
        using var cmd = new MySqlCommand(@"
            SELECT id, entry_id, type, content, url, markdown_content, time_at, order_index, created_at, updated_at
            FROM binnacle_item
            WHERE entry_id = @E
            ORDER BY order_index, created_at", conn);
        cmd.Parameters.AddWithValue("@E", entryId);

        var items = new List<object>();
        using var r = await cmd.ExecuteReaderAsync();
        while (await r.ReadAsync())
        {
            items.Add(new
            {
                id              = r.GetInt32("id"),
                entryId         = r.GetInt32("entry_id"),
                type            = r.GetString("type"),
                content         = r.GetString("content"),
                url             = IsNull(r, "url") ? null : r.GetString("url"),
                markdownContent = IsNull(r, "markdown_content") ? null : r.GetString("markdown_content"),
                timeAt          = r.IsDBNull(r.GetOrdinal("time_at")) ? null : r.GetTimeSpan("time_at").ToString(@"hh\:mm\:ss"),
                orderIndex      = r.GetInt32("order_index"),
                createdAt       = r.GetDateTime("created_at"),
                updatedAt       = r.GetDateTime("updated_at"),
            });
        }
        return items;
    }

    /// GET /api/binnacle/entries/today?businessId=&date=&projectId=
    [HttpGet("entries/today")]
    public async Task<IActionResult> GetTodayEntry([FromQuery] int businessId, [FromQuery] string? date, [FromQuery] int? projectId)
    {
        var userId = GetCurrentUserId();
        if (userId == null) return Unauthorized();

        var targetDate = date ?? DateTime.UtcNow.ToString("yyyy-MM-dd");

        try
        {
            using var conn = GetConnection();
            await conn.OpenAsync();

            var sql = @"SELECT id, title, date, business_id, user_id, project_id, summary, meeting_title, created_at, updated_at
                        FROM binnacle_entry
                        WHERE business_id=@B AND user_id=@UID AND date=@Date";

            if (projectId.HasValue)
                sql += " AND project_id=@PID";
            else
                sql += " AND project_id IS NULL";

            using var cmd = new MySqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@B",    businessId);
            cmd.Parameters.AddWithValue("@UID",  userId);
            cmd.Parameters.AddWithValue("@Date", targetDate);
            if (projectId.HasValue) cmd.Parameters.AddWithValue("@PID", projectId.Value);

            using var r = await cmd.ExecuteReaderAsync();
            if (!await r.ReadAsync()) return Ok(null);

            var entryId = r.GetInt32("id");
            var entry = new
            {
                id           = entryId,
                title        = r.GetString("title"),
                date         = r.GetDateTime("date").ToString("yyyy-MM-dd"),
                businessId   = r.GetInt32("business_id"),
                userId       = r.GetInt32("user_id"),
                projectId    = IsNull(r, "project_id") ? (int?)null : r.GetInt32("project_id"),
                summary      = IsNull(r, "summary") ? null : r.GetString("summary"),
                meetingTitle = IsNull(r, "meeting_title") ? null : r.GetString("meeting_title"),
                createdAt    = r.GetDateTime("created_at"),
                updatedAt    = r.GetDateTime("updated_at"),
            };
            await r.CloseAsync();

            var items = await ReadItems(conn, entryId);
            return Ok(new { entry.id, entry.title, entry.date, entry.businessId, entry.userId, entry.projectId, entry.summary, entry.meetingTitle, entry.createdAt, entry.updatedAt, items });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error obteniendo entrada de hoy");
            return StatusCode(500, new { message = "Error interno del servidor" });
        }
    }

    /// GET /api/binnacle/entries?businessId=&projectId=&from=&to=&userId=
    [HttpGet("entries")]
    public async Task<IActionResult> GetEntries(
        [FromQuery] int businessId,
        [FromQuery] int? projectId,
        [FromQuery] string? from,
        [FromQuery] string? to,
        [FromQuery] int? userId)
    {
        var currentUserId = GetCurrentUserId();
        if (currentUserId == null) return Unauthorized();

        try
        {
            using var conn = GetConnection();
            await conn.OpenAsync();

            var where = new List<string> { "e.business_id=@B" };
            var cmd = new MySqlCommand("", conn);
            cmd.Parameters.AddWithValue("@B", businessId);

            // Por defecto sólo ve sus propias entradas excepto si se filtra explícitamente
            var targetUserId = userId ?? currentUserId.Value;
            where.Add("e.user_id=@UID");
            cmd.Parameters.AddWithValue("@UID", targetUserId);

            if (projectId.HasValue) { where.Add("e.project_id=@PID"); cmd.Parameters.AddWithValue("@PID", projectId.Value); }
            if (!string.IsNullOrEmpty(from)) { where.Add("e.date>=@From"); cmd.Parameters.AddWithValue("@From", from); }
            if (!string.IsNullOrEmpty(to))   { where.Add("e.date<=@To");   cmd.Parameters.AddWithValue("@To",   to); }

            cmd.CommandText = $@"
                SELECT e.id, e.title, e.date, e.business_id, e.user_id, e.project_id, e.summary, e.meeting_title,
                       e.created_at, e.updated_at
                FROM binnacle_entry e
                WHERE {string.Join(" AND ", where)}
                ORDER BY e.date DESC";

            var entries = new List<dynamic>();
            using var r = await cmd.ExecuteReaderAsync();
            while (await r.ReadAsync())
            {
                entries.Add(new
                {
                    id           = r.GetInt32("id"),
                    title        = r.GetString("title"),
                    date         = r.GetDateTime("date").ToString("yyyy-MM-dd"),
                    businessId   = r.GetInt32("business_id"),
                    userId       = r.GetInt32("user_id"),
                    projectId    = IsNull(r, "project_id") ? (int?)null : r.GetInt32("project_id"),
                    summary      = IsNull(r, "summary") ? null : r.GetString("summary"),
                    meetingTitle = IsNull(r, "meeting_title") ? null : r.GetString("meeting_title"),
                    createdAt    = r.GetDateTime("created_at"),
                    updatedAt    = r.GetDateTime("updated_at"),
                    items        = new List<object>(), // se rellenan abajo
                });
            }
            await r.CloseAsync();

            // Cargar ítems para cada entrada
            foreach (var entry in entries)
            {
                var items = await ReadItems(conn, entry.id);
                entry.items.AddRange(items);
            }

            return Ok(entries);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error obteniendo entradas de bitácora");
            return StatusCode(500, new { message = "Error interno del servidor" });
        }
    }

    /// GET /api/binnacle/entries/{id}
    [HttpGet("entries/{id:int}")]
    public async Task<IActionResult> GetEntryById(int id)
    {
        try
        {
            using var conn = GetConnection();
            await conn.OpenAsync();
            using var cmd = new MySqlCommand(@"
                SELECT id, title, date, business_id, user_id, project_id, summary, meeting_title, created_at, updated_at
                FROM binnacle_entry WHERE id=@Id", conn);
            cmd.Parameters.AddWithValue("@Id", id);

            using var r = await cmd.ExecuteReaderAsync();
            if (!await r.ReadAsync()) return NotFound();

            var entryId = r.GetInt32("id");
            var entry = new
            {
                id           = entryId,
                title        = r.GetString("title"),
                date         = r.GetDateTime("date").ToString("yyyy-MM-dd"),
                businessId   = r.GetInt32("business_id"),
                userId       = r.GetInt32("user_id"),
                projectId    = IsNull(r, "project_id") ? (int?)null : r.GetInt32("project_id"),
                summary      = IsNull(r, "summary") ? null : r.GetString("summary"),
                meetingTitle = IsNull(r, "meeting_title") ? null : r.GetString("meeting_title"),
                createdAt    = r.GetDateTime("created_at"),
                updatedAt    = r.GetDateTime("updated_at"),
            };
            await r.CloseAsync();

            var items = await ReadItems(conn, entryId);
            return Ok(new { entry.id, entry.title, entry.date, entry.businessId, entry.userId, entry.projectId, entry.summary, entry.meetingTitle, entry.createdAt, entry.updatedAt, items });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error obteniendo entrada {Id}", id);
            return StatusCode(500, new { message = "Error interno del servidor" });
        }
    }

    /// POST /api/binnacle/entries
    [HttpPost("entries")]
    public async Task<IActionResult> CreateEntry([FromBody] JsonElement body)
    {
        var userId = GetCurrentUserId();
        if (userId == null) return Unauthorized();

        try
        {
            var title        = body.GetProperty("title").GetString()!;
            var date         = body.GetProperty("date").GetString()!;
            var businessId   = body.GetProperty("businessId").GetInt32();
            int? projectId   = body.TryGetProperty("projectId", out var pid) && pid.ValueKind != JsonValueKind.Null
                               ? pid.GetInt32() : null;
            var summary      = body.TryGetProperty("summary",      out var s)  ? s.GetString()  : null;
            var meetingTitle = body.TryGetProperty("meetingTitle", out var mt) ? mt.GetString() : null;

            using var conn = GetConnection();
            await conn.OpenAsync();
            using var cmd = new MySqlCommand(@"
                INSERT INTO binnacle_entry (title, date, business_id, user_id, project_id, summary, meeting_title)
                VALUES (@Title, @Date, @B, @UID, @PID, @Summary, @MeetingTitle);
                SELECT LAST_INSERT_ID();", conn);
            cmd.Parameters.AddWithValue("@Title",        title);
            cmd.Parameters.AddWithValue("@Date",         date);
            cmd.Parameters.AddWithValue("@B",            businessId);
            cmd.Parameters.AddWithValue("@UID",          userId);
            cmd.Parameters.AddWithValue("@PID",          (object?)projectId    ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@Summary",      (object?)summary      ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@MeetingTitle", (object?)meetingTitle ?? DBNull.Value);

            var newId = Convert.ToInt32(await cmd.ExecuteScalarAsync());

            return CreatedAtAction(nameof(GetEntryById), new { id = newId }, new
            {
                id = newId, title, date, businessId,
                userId, projectId, summary, meetingTitle,
                items = new List<object>()
            });
        }
        catch (MySqlException ex) when (ex.Number == 1062)
        {
            // Duplicate entry (UNIQUE constraint)
            return Conflict(new { message = "Ya existe una entrada para este día y proyecto" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creando entrada de bitácora");
            return StatusCode(500, new { message = "Error interno del servidor" });
        }
    }

    /// PATCH /api/binnacle/entries/{id}
    [HttpPatch("entries/{id:int}")]
    public async Task<IActionResult> UpdateEntry(int id, [FromBody] JsonElement body)
    {
        try
        {
            using var conn = GetConnection();
            await conn.OpenAsync();

            var setClauses = new List<string>();
            var cmd = new MySqlCommand("", conn);

            if (body.TryGetProperty("title",        out var t))  { setClauses.Add("title=@Title");              cmd.Parameters.AddWithValue("@Title",        t.GetString()); }
            if (body.TryGetProperty("summary",      out var s))  { setClauses.Add("summary=@Summary");          cmd.Parameters.AddWithValue("@Summary",      s.ValueKind == JsonValueKind.Null ? DBNull.Value : s.GetString()); }
            if (body.TryGetProperty("projectId",   out var p))  { setClauses.Add("project_id=@PID");           cmd.Parameters.AddWithValue("@PID",          p.ValueKind == JsonValueKind.Null ? DBNull.Value : p.GetInt32()); }
            if (body.TryGetProperty("meetingTitle", out var mt)) { setClauses.Add("meeting_title=@MeetingTitle"); cmd.Parameters.AddWithValue("@MeetingTitle", mt.ValueKind == JsonValueKind.Null ? DBNull.Value : mt.GetString()); }

            if (setClauses.Count == 0) return BadRequest(new { message = "Nada que actualizar" });

            cmd.CommandText = $"UPDATE binnacle_entry SET {string.Join(",", setClauses)} WHERE id=@Id";
            cmd.Parameters.AddWithValue("@Id", id);
            await cmd.ExecuteNonQueryAsync();

            return Ok(new { id });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error actualizando entrada {Id}", id);
            return StatusCode(500, new { message = "Error interno del servidor" });
        }
    }

    // ====================================================================
    // ÍTEMS
    // ====================================================================

    /// POST /api/binnacle/entries/{entryId}/items
    [HttpPost("entries/{entryId:int}/items")]
    public async Task<IActionResult> AddItem(int entryId, [FromBody] JsonElement body)
    {
        try
        {
            var type            = body.GetProperty("type").GetString()!;
            var content         = body.GetProperty("content").GetString()!;
            var timeAt          = body.TryGetProperty("timeAt",          out var ta) && ta.ValueKind != JsonValueKind.Null ? ta.GetString() : null;
            var url             = body.TryGetProperty("url",             out var ur) && ur.ValueKind != JsonValueKind.Null ? ur.GetString() : null;
            var markdownContent = body.TryGetProperty("markdownContent", out var mc) && mc.ValueKind != JsonValueKind.Null ? mc.GetString() : null;
            var orderIndex      = body.TryGetProperty("orderIndex", out var oi) ? oi.GetInt32() : 0;

            using var conn = GetConnection();
            await conn.OpenAsync();
            using var cmd = new MySqlCommand(@"
                INSERT INTO binnacle_item (entry_id, type, content, url, markdown_content, time_at, order_index)
                VALUES (@EID, @Type, @Content, @Url, @Md, @TimeAt, @Order);
                SELECT LAST_INSERT_ID();", conn);
            cmd.Parameters.AddWithValue("@EID",    entryId);
            cmd.Parameters.AddWithValue("@Type",   type);
            cmd.Parameters.AddWithValue("@Content",content);
            cmd.Parameters.AddWithValue("@Url",    (object?)url             ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@Md",     (object?)markdownContent ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@TimeAt", (object?)timeAt          ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@Order",  orderIndex);

            var newId = Convert.ToInt32(await cmd.ExecuteScalarAsync());

            return CreatedAtAction(nameof(GetEntryById), new { id = entryId }, new
            {
                id = newId, entryId, type, content, url, markdownContent,
                timeAt, orderIndex,
                createdAt = DateTime.UtcNow,
                updatedAt = DateTime.UtcNow,
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error agregando ítem a entrada {EntryId}", entryId);
            return StatusCode(500, new { message = "Error interno del servidor" });
        }
    }

    /// PATCH /api/binnacle/items/{itemId}
    [HttpPatch("items/{itemId:int}")]
    public async Task<IActionResult> UpdateItem(int itemId, [FromBody] JsonElement body)
    {
        try
        {
            using var conn = GetConnection();
            await conn.OpenAsync();

            var setClauses = new List<string>();
            var cmd = new MySqlCommand("", conn);

            if (body.TryGetProperty("type",            out var t))  { setClauses.Add("type=@Type");              cmd.Parameters.AddWithValue("@Type",   t.GetString()); }
            if (body.TryGetProperty("content",         out var c))  { setClauses.Add("content=@Content");         cmd.Parameters.AddWithValue("@Content",c.GetString()); }
            if (body.TryGetProperty("url",             out var ur)) { setClauses.Add("url=@Url");                 cmd.Parameters.AddWithValue("@Url",    ur.ValueKind == JsonValueKind.Null ? DBNull.Value : ur.GetString()); }
            if (body.TryGetProperty("markdownContent", out var md)) { setClauses.Add("markdown_content=@Md");     cmd.Parameters.AddWithValue("@Md",     md.ValueKind == JsonValueKind.Null ? DBNull.Value : md.GetString()); }
            if (body.TryGetProperty("timeAt",          out var ta)) { setClauses.Add("time_at=@TimeAt");          cmd.Parameters.AddWithValue("@TimeAt", ta.ValueKind == JsonValueKind.Null ? DBNull.Value : ta.GetString()); }
            if (body.TryGetProperty("orderIndex",      out var oi)) { setClauses.Add("order_index=@Order");       cmd.Parameters.AddWithValue("@Order",  oi.GetInt32()); }

            if (setClauses.Count == 0) return BadRequest(new { message = "Nada que actualizar" });

            cmd.CommandText = $"UPDATE binnacle_item SET {string.Join(",", setClauses)} WHERE id=@Id";
            cmd.Parameters.AddWithValue("@Id", itemId);
            await cmd.ExecuteNonQueryAsync();

            // Devolver el ítem actualizado
            using var sel = new MySqlCommand(@"
                SELECT id, entry_id, type, content, url, markdown_content, time_at, order_index, created_at, updated_at
                FROM binnacle_item WHERE id=@Id", conn);
            sel.Parameters.AddWithValue("@Id", itemId);
            using var r = await sel.ExecuteReaderAsync();
            if (!await r.ReadAsync()) return NotFound();

            return Ok(new
            {
                id              = r.GetInt32("id"),
                entryId         = r.GetInt32("entry_id"),
                type            = r.GetString("type"),
                content         = r.GetString("content"),
                url             = IsNull(r, "url") ? null : r.GetString("url"),
                markdownContent = IsNull(r, "markdown_content") ? null : r.GetString("markdown_content"),
                timeAt          = r.IsDBNull(r.GetOrdinal("time_at")) ? null : r.GetTimeSpan("time_at").ToString(@"hh\:mm\:ss"),
                orderIndex      = r.GetInt32("order_index"),
                createdAt       = r.GetDateTime("created_at"),
                updatedAt       = r.GetDateTime("updated_at"),
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error actualizando ítem {ItemId}", itemId);
            return StatusCode(500, new { message = "Error interno del servidor" });
        }
    }

    /// DELETE /api/binnacle/items/{itemId}
    [HttpDelete("items/{itemId:int}")]
    public async Task<IActionResult> DeleteItem(int itemId)
    {
        try
        {
            using var conn = GetConnection();
            await conn.OpenAsync();
            using var cmd = new MySqlCommand("DELETE FROM binnacle_item WHERE id=@Id", conn);
            cmd.Parameters.AddWithValue("@Id", itemId);
            await cmd.ExecuteNonQueryAsync();
            return NoContent();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error eliminando ítem {ItemId}", itemId);
            return StatusCode(500, new { message = "Error interno del servidor" });
        }
    }

    /// PUT /api/binnacle/entries/{entryId}/items/reorder
    [HttpPut("entries/{entryId:int}/items/reorder")]
    public async Task<IActionResult> ReorderItems(int entryId, [FromBody] JsonElement body)
    {
        try
        {
            var orderedIds = body.GetProperty("orderedIds")
                               .EnumerateArray()
                               .Select(x => x.GetInt32())
                               .ToList();

            using var conn = GetConnection();
            await conn.OpenAsync();
            using var tx = await conn.BeginTransactionAsync();

            for (int i = 0; i < orderedIds.Count; i++)
            {
                using var cmd = new MySqlCommand(
                    "UPDATE binnacle_item SET order_index=@Ord WHERE id=@Id AND entry_id=@EID", conn, tx);
                cmd.Parameters.AddWithValue("@Ord", i);
                cmd.Parameters.AddWithValue("@Id",  orderedIds[i]);
                cmd.Parameters.AddWithValue("@EID", entryId);
                await cmd.ExecuteNonQueryAsync();
            }

            await tx.CommitAsync();
            return Ok(new { reordered = orderedIds.Count });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error reordenando ítems de entrada {EntryId}", entryId);
            return StatusCode(500, new { message = "Error interno del servidor" });
        }
    }
}
