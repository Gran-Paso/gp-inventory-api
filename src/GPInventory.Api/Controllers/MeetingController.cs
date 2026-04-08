#pragma warning disable CS8601
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Cors;
using Microsoft.AspNetCore.Mvc;
using MySqlConnector;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using Microsoft.IdentityModel.Tokens;
using GPInventory.Api.Services;

namespace GPInventory.Api.Controllers;

/// <summary>
/// GP Meeting – Módulo de Minutas de Reuniones.
/// Gestión de plantillas de reuniones, instancias, participantes, temas y tareas.
/// </summary>
[ApiController]
[Route("api/meetings")]
[EnableCors("AllowFrontend")]
[Authorize]
public class MeetingController : ControllerBase
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<MeetingController> _logger;
    private readonly MeetingSseService _sse;

    public MeetingController(IConfiguration configuration, ILogger<MeetingController> logger, MeetingSseService sse)
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

    // ====================================================================
    // SSE — real-time stream
    // EventSource cannot set Authorization headers, so token is passed
    // as a query-string param and validated manually.
    // ====================================================================

    /// GET /api/meetings/events?businessId=X&token=JWT
    [HttpGet("events")]
    [AllowAnonymous]
    public async Task GetMeetingEvents([FromQuery] int businessId, [FromQuery] string token, CancellationToken ct)
    {
        // Validate JWT manually
        var jwtSettings = _configuration.GetSection("JwtSettings");
        var secretKey = jwtSettings["SecretKey"];
        if (string.IsNullOrEmpty(secretKey) || string.IsNullOrEmpty(token))
        {
            Response.StatusCode = 401;
            return;
        }

        try
        {
            var tokenHandler = new JwtSecurityTokenHandler();
            tokenHandler.ValidateToken(token, new TokenValidationParameters
            {
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secretKey)),
                ValidateIssuer = true,
                ValidIssuer = jwtSettings["Issuer"],
                ValidateAudience = true,
                ValidAudience = jwtSettings["Audience"],
                ValidateLifetime = true,
                ClockSkew = TimeSpan.Zero
            }, out _);
        }
        catch
        {
            Response.StatusCode = 401;
            return;
        }

        Response.ContentType = "text/event-stream";
        Response.Headers["Cache-Control"] = "no-cache";
        Response.Headers["X-Accel-Buffering"] = "no"; // disable nginx buffering

        // Send an initial comment to open the connection
        await Response.WriteAsync(": connected\n\n", ct);
        await Response.Body.FlushAsync(ct);

        var channel = _sse.Subscribe(businessId);
        try
        {
            await foreach (var msg in channel.Reader.ReadAllAsync(ct))
            {
                await Response.WriteAsync(msg, ct);
                await Response.Body.FlushAsync(ct);
            }
        }
        finally
        {
            _sse.Unsubscribe(businessId, channel);
        }
    }

    /// GET /api/meetings/instances/{id}/events?token=JWT
    /// Per-meeting SSE stream — fires "detail.changed" whenever participants,
    /// topics, or tasks inside this meeting are mutated.
    [HttpGet("instances/{id:int}/events")]
    [AllowAnonymous]
    public async Task GetMeetingDetailEvents(int id, [FromQuery] string token, CancellationToken ct)
    {
        var jwtSettings = _configuration.GetSection("JwtSettings");
        var secretKey   = jwtSettings["SecretKey"];
        if (string.IsNullOrEmpty(secretKey) || string.IsNullOrEmpty(token))
        {
            Response.StatusCode = 401;
            return;
        }

        try
        {
            var tokenHandler = new JwtSecurityTokenHandler();
            tokenHandler.ValidateToken(token, new TokenValidationParameters
            {
                ValidateIssuerSigningKey = true,
                IssuerSigningKey         = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secretKey)),
                ValidateIssuer           = true,
                ValidIssuer              = jwtSettings["Issuer"],
                ValidateAudience         = true,
                ValidAudience            = jwtSettings["Audience"],
                ValidateLifetime         = true,
                ClockSkew                = TimeSpan.Zero
            }, out _);
        }
        catch
        {
            Response.StatusCode = 401;
            return;
        }

        Response.ContentType                 = "text/event-stream";
        Response.Headers["Cache-Control"]    = "no-cache";
        Response.Headers["X-Accel-Buffering"] = "no";

        await Response.WriteAsync(": connected\n\n", ct);
        await Response.Body.FlushAsync(ct);

        var channel = _sse.SubscribeMeeting(id);
        try
        {
            await foreach (var msg in channel.Reader.ReadAllAsync(ct))
            {
                await Response.WriteAsync(msg, ct);
                await Response.Body.FlushAsync(ct);
            }
        }
        finally
        {
            _sse.UnsubscribeMeeting(id, channel);
        }
    }

    // ====================================================================
    // HELPERS — read related data
    // ====================================================================

    private static async Task<List<object>> ReadTemplateParticipants(MySqlConnection conn, int templateId)
    {
        using var cmd = new MySqlCommand(@"
            SELECT id, template_id, name, user_id, employee_id, order_index
            FROM meeting_template_participant
            WHERE template_id = @T
            ORDER BY order_index, id", conn);
        cmd.Parameters.AddWithValue("@T", templateId);

        var list = new List<object>();
        using var r = await cmd.ExecuteReaderAsync();
        while (await r.ReadAsync())
            list.Add(new {
                id         = r.GetInt32("id"),
                templateId = r.GetInt32("template_id"),
                name       = r.GetString("name"),
                userId     = IsNull(r, "user_id")     ? (int?)null : r.GetInt32("user_id"),
                employeeId = IsNull(r, "employee_id") ? (int?)null : r.GetInt32("employee_id"),
                orderIndex = r.GetInt32("order_index"),
            });
        return list;
    }

    private static async Task<List<object>> ReadParticipants(MySqlConnection conn, int meetingId)
    {
        using var cmd = new MySqlCommand(@"
            SELECT id, meeting_id, name, status, user_id, employee_id, order_index
            FROM meeting_participant
            WHERE meeting_id = @M
            ORDER BY order_index, id", conn);
        cmd.Parameters.AddWithValue("@M", meetingId);

        var list = new List<object>();
        using var r = await cmd.ExecuteReaderAsync();
        while (await r.ReadAsync())
            list.Add(new {
                id         = r.GetInt32("id"),
                meetingId  = r.GetInt32("meeting_id"),
                name       = r.GetString("name"),
                status     = r.GetString("status"),
                userId     = IsNull(r, "user_id")     ? (int?)null : r.GetInt32("user_id"),
                employeeId = IsNull(r, "employee_id") ? (int?)null : r.GetInt32("employee_id"),
                orderIndex = r.GetInt32("order_index"),
            });
        return list;
    }

    private static async Task<List<object>> ReadTasks(MySqlConnection conn, int topicId)
    {
        using var cmd = new MySqlCommand(@"
            SELECT id, topic_id, meeting_id, type, description, responsible, status, due_date, origin_task_id, created_at, updated_at
            FROM meeting_task
            WHERE topic_id = @T
            ORDER BY created_at", conn);
        cmd.Parameters.AddWithValue("@T", topicId);

        var list = new List<object>();
        using var r = await cmd.ExecuteReaderAsync();
        while (await r.ReadAsync())
        {
            list.Add(new
            {
                id           = r.GetInt32("id"),
                topicId      = r.GetInt32("topic_id"),
                meetingId    = r.GetInt32("meeting_id"),
                type         = r.GetString("type"),
                description  = r.GetString("description"),
                responsible  = IsNull(r, "responsible") ? null : r.GetString("responsible"),
                status       = r.GetString("status"),
                dueDate      = IsNull(r, "due_date") ? null : r.GetDateTime("due_date").ToString("yyyy-MM-dd"),
                originTaskId = IsNull(r, "origin_task_id") ? (int?)null : r.GetInt32("origin_task_id"),
                createdAt    = r.GetDateTime("created_at"),
                updatedAt    = r.GetDateTime("updated_at"),
            });
        }
        return list;
    }

    private static async Task<List<object>> ReadTopicsWithTasks(MySqlConnection conn, int meetingId)
    {
        using var topicCmd = new MySqlCommand(@"
            SELECT id, meeting_id, title, raised_by, order_index, created_at, updated_at
            FROM meeting_topic
            WHERE meeting_id = @M
            ORDER BY order_index, id", conn);
        topicCmd.Parameters.AddWithValue("@M", meetingId);

        var topics = new List<(int id, object data)>();
        using var tr = await topicCmd.ExecuteReaderAsync();
        while (await tr.ReadAsync())
        {
            var id = tr.GetInt32("id");
            topics.Add((id, new
            {
                id          = id,
                meetingId   = tr.GetInt32("meeting_id"),
                title       = tr.GetString("title"),
                raisedBy    = IsNull(tr, "raised_by") ? null : tr.GetString("raised_by"),
                orderIndex  = tr.GetInt32("order_index"),
                createdAt   = tr.GetDateTime("created_at"),
                updatedAt   = tr.GetDateTime("updated_at"),
                tasks       = new List<object>(),
            }));
        }
        await tr.CloseAsync();

        var result = new List<object>();
        foreach (var (topicId, topicData) in topics)
        {
            var tasks = await ReadTasks(conn, topicId);
            // Attach tasks dynamically
            var td = (dynamic)topicData;
            ((List<object>)td.tasks).AddRange(tasks);
            result.Add(topicData);
        }
        return result;
    }

    // ====================================================================
    // BUSINESS PEOPLE — usuarios y empleados del negocio
    // ====================================================================

    /// GET /api/meetings/business-people?businessId=
    /// Retorna la lista de personas asociadas al negocio:
    /// usuarios (user_has_business) + empleados activos sin usuario duplicado (hr_employee).
    [HttpGet("business-people")]
    public async Task<IActionResult> GetBusinessPeople([FromQuery] int businessId)
    {
        try
        {
            using var conn = GetConnection();
            await conn.OpenAsync();
            using var cmd = new MySqlCommand(@"
                SELECT u.id AS user_id, NULL AS employee_id,
                       TRIM(CONCAT(COALESCE(u.name,''), ' ', COALESCE(u.lastname,''))) COLLATE utf8mb4_unicode_ci AS full_name,
                       'user' AS person_type
                FROM user_has_business uhb
                JOIN user u ON u.id = uhb.id_user
                WHERE uhb.id_business = @B AND u.active = 1

                UNION ALL

                SELECT he.user_id, he.id AS employee_id,
                       TRIM(CONCAT(he.first_name, ' ', he.last_name)) COLLATE utf8mb4_unicode_ci AS full_name,
                       'employee' AS person_type
                FROM hr_employee he
                WHERE he.business_id = @B AND he.active = 1 AND he.status = 'active'
                  AND (he.user_id IS NULL
                       OR he.user_id NOT IN (
                           SELECT id_user FROM user_has_business WHERE id_business = @B AND id_user IS NOT NULL
                       ))
                ORDER BY full_name", conn);
            cmd.Parameters.AddWithValue("@B", businessId);

            var list = new List<object>();
            using var r = await cmd.ExecuteReaderAsync();
            while (await r.ReadAsync())
                list.Add(new {
                    userId     = IsNull(r, "user_id")     ? (int?)null : r.GetInt32("user_id"),
                    employeeId = IsNull(r, "employee_id") ? (int?)null : r.GetInt32("employee_id"),
                    name       = r.GetString("full_name"),
                    type       = r.GetString("person_type"),
                });
            return Ok(list);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error obteniendo personas del negocio {Id}", businessId);
            return StatusCode(500, new { message = "Error interno del servidor" });
        }
    }

    // ====================================================================
    // TEMPLATES
    // ====================================================================

    /// GET /api/meetings/templates?businessId=
    [HttpGet("templates")]
    public async Task<IActionResult> GetTemplates([FromQuery] int businessId)
    {
        try
        {
            using var conn = GetConnection();
            await conn.OpenAsync();
            using var cmd = new MySqlCommand(@"
                SELECT id, business_id, name, facilitator, location, schedule, objective, active,
                       created_by_user_id, created_at, updated_at
                FROM meeting_template
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
                    businessId        = r.GetInt32("business_id"),
                    name              = r.GetString("name"),
                    facilitator       = IsNull(r, "facilitator") ? null : r.GetString("facilitator"),
                    location          = IsNull(r, "location") ? null : r.GetString("location"),
                    schedule          = IsNull(r, "schedule") ? null : r.GetString("schedule"),
                    objective         = IsNull(r, "objective") ? null : r.GetString("objective"),
                    active            = r.GetBoolean("active"),
                    createdByUserId   = r.GetInt32("created_by_user_id"),
                    createdAt         = r.GetDateTime("created_at"),
                    updatedAt         = r.GetDateTime("updated_at"),
                    participants      = new List<object>(),
                });
            }
            await r.CloseAsync();

            foreach (dynamic t in list)
            {
                var parts = await ReadTemplateParticipants(conn, t.id);
                ((List<object>)t.participants).AddRange(parts);
            }

            return Ok(list);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error obteniendo plantillas de reunión");
            return StatusCode(500, new { message = "Error interno del servidor" });
        }
    }

    /// POST /api/meetings/templates
    [HttpPost("templates")]
    public async Task<IActionResult> CreateTemplate([FromBody] JsonElement body)
    {
        var userId = GetCurrentUserId();
        if (userId == null) return Unauthorized();
        try
        {
            var name        = body.GetProperty("name").GetString()!;
            var businessId  = body.GetProperty("businessId").GetInt32();
            var facilitator = body.TryGetProperty("facilitator", out var f) ? f.GetString() : null;
            var location    = body.TryGetProperty("location",    out var l) ? l.GetString() : null;
            var schedule    = body.TryGetProperty("schedule",    out var s) ? s.GetString() : null;
            var objective   = body.TryGetProperty("objective",   out var o) ? o.GetString() : null;

            using var conn = GetConnection();
            await conn.OpenAsync();
            using var cmd = new MySqlCommand(@"
                INSERT INTO meeting_template (business_id, name, facilitator, location, schedule, objective, created_by_user_id)
                VALUES (@B, @Name, @Fac, @Loc, @Sch, @Obj, @UID);
                SELECT LAST_INSERT_ID();", conn);
            cmd.Parameters.AddWithValue("@B",    businessId);
            cmd.Parameters.AddWithValue("@Name", name);
            cmd.Parameters.AddWithValue("@Fac",  (object?)facilitator ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@Loc",  (object?)location    ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@Sch",  (object?)schedule    ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@Obj",  (object?)objective   ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@UID",  userId);

            var newId = Convert.ToInt32(await cmd.ExecuteScalarAsync());
            return CreatedAtAction(nameof(GetTemplates), new { businessId }, new { id = newId, businessId, name, facilitator, location, schedule, objective, active = true, participants = new List<object>() });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creando plantilla de reunión");
            return StatusCode(500, new { message = "Error interno del servidor" });
        }
    }

    /// PATCH /api/meetings/templates/{id}
    [HttpPatch("templates/{id:int}")]
    public async Task<IActionResult> UpdateTemplate(int id, [FromBody] JsonElement body)
    {
        try
        {
            using var conn = GetConnection();
            await conn.OpenAsync();
            var setClauses = new List<string>();
            var cmd = new MySqlCommand("", conn);

            if (body.TryGetProperty("name",        out var n))  { setClauses.Add("name=@Name");        cmd.Parameters.AddWithValue("@Name", n.GetString()); }
            if (body.TryGetProperty("facilitator", out var f))  { setClauses.Add("facilitator=@Fac");  cmd.Parameters.AddWithValue("@Fac",  f.ValueKind == JsonValueKind.Null ? DBNull.Value : f.GetString()); }
            if (body.TryGetProperty("location",    out var l))  { setClauses.Add("location=@Loc");     cmd.Parameters.AddWithValue("@Loc",  l.ValueKind == JsonValueKind.Null ? DBNull.Value : l.GetString()); }
            if (body.TryGetProperty("schedule",    out var s))  { setClauses.Add("schedule=@Sch");     cmd.Parameters.AddWithValue("@Sch",  s.ValueKind == JsonValueKind.Null ? DBNull.Value : s.GetString()); }
            if (body.TryGetProperty("objective",   out var o))  { setClauses.Add("objective=@Obj");    cmd.Parameters.AddWithValue("@Obj",  o.ValueKind == JsonValueKind.Null ? DBNull.Value : o.GetString()); }
            if (body.TryGetProperty("active",      out var ac)) { setClauses.Add("active=@Active");    cmd.Parameters.AddWithValue("@Active", ac.GetBoolean() ? 1 : 0); }

            if (setClauses.Count == 0) return BadRequest(new { message = "Nada que actualizar" });
            cmd.CommandText = $"UPDATE meeting_template SET {string.Join(",", setClauses)} WHERE id=@Id";
            cmd.Parameters.AddWithValue("@Id", id);
            await cmd.ExecuteNonQueryAsync();
            return Ok(new { id });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error actualizando plantilla {Id}", id);
            return StatusCode(500, new { message = "Error interno del servidor" });
        }
    }

    /// DELETE /api/meetings/templates/{id}  (soft-delete)
    [HttpDelete("templates/{id:int}")]
    public async Task<IActionResult> DeleteTemplate(int id)
    {
        try
        {
            using var conn = GetConnection();
            await conn.OpenAsync();
            using var cmd = new MySqlCommand("UPDATE meeting_template SET active=0 WHERE id=@Id", conn);
            cmd.Parameters.AddWithValue("@Id", id);
            await cmd.ExecuteNonQueryAsync();
            return NoContent();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error eliminando plantilla {Id}", id);
            return StatusCode(500, new { message = "Error interno del servidor" });
        }
    }

    // ── Template participants ─────────────────────────────────────────────────

    /// POST /api/meetings/templates/{id}/participants
    [HttpPost("templates/{id:int}/participants")]
    public async Task<IActionResult> AddTemplateParticipant(int id, [FromBody] JsonElement body)
    {
        try
        {
            var name       = body.GetProperty("name").GetString()!;
            var orderIndex = body.TryGetProperty("orderIndex",  out var oi) ? oi.GetInt32() : 0;
            int? userId    = body.TryGetProperty("userId",     out var ui) && ui.ValueKind != JsonValueKind.Null ? ui.GetInt32() : (int?)null;
            int? employeeId= body.TryGetProperty("employeeId", out var ei) && ei.ValueKind != JsonValueKind.Null ? ei.GetInt32() : (int?)null;
            using var conn = GetConnection();
            await conn.OpenAsync();
            using var cmd = new MySqlCommand(@"
                INSERT INTO meeting_template_participant (template_id, name, user_id, employee_id, order_index)
                VALUES (@T, @Name, @UID, @EID, @Ord);
                SELECT LAST_INSERT_ID();", conn);
            cmd.Parameters.AddWithValue("@T",    id);
            cmd.Parameters.AddWithValue("@Name", name);
            cmd.Parameters.AddWithValue("@UID",  (object?)userId     ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@EID",  (object?)employeeId ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@Ord",  orderIndex);
            var newId = Convert.ToInt32(await cmd.ExecuteScalarAsync());
            return Ok(new { id = newId, templateId = id, name, userId, employeeId, orderIndex });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error agregando participante a plantilla {Id}", id);
            return StatusCode(500, new { message = "Error interno del servidor" });
        }
    }

    /// DELETE /api/meetings/template-participants/{id}
    [HttpDelete("template-participants/{id:int}")]
    public async Task<IActionResult> DeleteTemplateParticipant(int id)
    {
        try
        {
            using var conn = GetConnection();
            await conn.OpenAsync();
            using var cmd = new MySqlCommand("DELETE FROM meeting_template_participant WHERE id=@Id", conn);
            cmd.Parameters.AddWithValue("@Id", id);
            await cmd.ExecuteNonQueryAsync();
            return NoContent();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error eliminando participante de plantilla {Id}", id);
            return StatusCode(500, new { message = "Error interno del servidor" });
        }
    }

    // ====================================================================
    // MEETING INSTANCES
    // ====================================================================

    /// GET /api/meetings/instances?businessId=&templateId=&from=&to=
    [HttpGet("instances")]
    public async Task<IActionResult> GetInstances(
        [FromQuery] int businessId,
        [FromQuery] int? templateId,
        [FromQuery] string? from,
        [FromQuery] string? to)
    {
        try
        {
            using var conn = GetConnection();
            await conn.OpenAsync();

            var where = new List<string> { "business_id=@B" };
            var cmd = new MySqlCommand("", conn);
            cmd.Parameters.AddWithValue("@B", businessId);
            if (templateId.HasValue) { where.Add("template_id=@TID");  cmd.Parameters.AddWithValue("@TID", templateId.Value); }
            if (!string.IsNullOrEmpty(from)) { where.Add("date>=@From"); cmd.Parameters.AddWithValue("@From", from); }
            if (!string.IsNullOrEmpty(to))   { where.Add("date<=@To");   cmd.Parameters.AddWithValue("@To",   to); }

            cmd.CommandText = $@"
                SELECT mi.id, mi.business_id, mi.template_id, mi.name, mi.facilitator, mi.date,
                       mi.location, mi.schedule, mi.objective, mi.status,
                       mi.created_by_user_id, mi.created_at, mi.updated_at,
                       (SELECT COUNT(*) FROM meeting_topic     WHERE meeting_id = mi.id) AS topic_count,
                       (SELECT COUNT(*) FROM meeting_participant WHERE meeting_id = mi.id) AS participant_count
                FROM meeting_instance mi
                WHERE {string.Join(" AND ", where.Select(w => "mi." + w))}
                ORDER BY mi.date DESC, mi.id DESC";

            var list = new List<object>();
            using var r = await cmd.ExecuteReaderAsync();
            while (await r.ReadAsync())
            {
                list.Add(new
                {
                    id               = r.GetInt32("id"),
                    businessId       = r.GetInt32("business_id"),
                    templateId       = IsNull(r, "template_id") ? (int?)null : r.GetInt32("template_id"),
                    name             = r.GetString("name"),
                    facilitator      = IsNull(r, "facilitator") ? null : r.GetString("facilitator"),
                    date             = r.GetDateTime("date").ToString("yyyy-MM-dd"),
                    location         = IsNull(r, "location") ? null : r.GetString("location"),
                    schedule         = IsNull(r, "schedule") ? null : r.GetString("schedule"),
                    objective        = IsNull(r, "objective") ? null : r.GetString("objective"),
                    status           = r.GetString("status"),
                    createdByUserId  = r.GetInt32("created_by_user_id"),
                    createdAt        = r.GetDateTime("created_at"),
                    updatedAt        = r.GetDateTime("updated_at"),
                    topicCount       = r.GetInt32("topic_count"),
                    participantCount = r.GetInt32("participant_count"),
                });
            }
            await r.CloseAsync();

            return Ok(list);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error obteniendo instancias de reunión");
            return StatusCode(500, new { message = "Error interno del servidor" });
        }
    }

    /// GET /api/meetings/instances/{id}  (full detail)
    [HttpGet("instances/{id:int}")]
    public async Task<IActionResult> GetInstance(int id)
    {
        try
        {
            using var conn = GetConnection();
            await conn.OpenAsync();
            using var cmd = new MySqlCommand(@"
                SELECT id, business_id, template_id, name, facilitator, date, location, schedule, objective, status,
                       created_by_user_id, created_at, updated_at
                FROM meeting_instance WHERE id=@Id", conn);
            cmd.Parameters.AddWithValue("@Id", id);

            using var r = await cmd.ExecuteReaderAsync();
            if (!await r.ReadAsync()) return NotFound();

            var meeting = new
            {
                id              = r.GetInt32("id"),
                businessId      = r.GetInt32("business_id"),
                templateId      = IsNull(r, "template_id") ? (int?)null : r.GetInt32("template_id"),
                name            = r.GetString("name"),
                facilitator     = IsNull(r, "facilitator") ? null : r.GetString("facilitator"),
                date            = r.GetDateTime("date").ToString("yyyy-MM-dd"),
                location        = IsNull(r, "location") ? null : r.GetString("location"),
                schedule        = IsNull(r, "schedule") ? null : r.GetString("schedule"),
                objective       = IsNull(r, "objective") ? null : r.GetString("objective"),
                status          = r.GetString("status"),
                createdByUserId = r.GetInt32("created_by_user_id"),
                createdAt       = r.GetDateTime("created_at"),
                updatedAt       = r.GetDateTime("updated_at"),
            };
            await r.CloseAsync();

            var participants = await ReadParticipants(conn, id);
            var topics       = await ReadTopicsWithTasks(conn, id);

            return Ok(new { meeting.id, meeting.businessId, meeting.templateId, meeting.name, meeting.facilitator, meeting.date, meeting.location, meeting.schedule, meeting.objective, meeting.status, meeting.createdByUserId, meeting.createdAt, meeting.updatedAt, participants, topics });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error obteniendo reunión {Id}", id);
            return StatusCode(500, new { message = "Error interno del servidor" });
        }
    }

    /// POST /api/meetings/instances
    [HttpPost("instances")]
    public async Task<IActionResult> CreateInstance([FromBody] JsonElement body)
    {
        var userId = GetCurrentUserId();
        if (userId == null) return Unauthorized();
        try
        {
            var name       = body.GetProperty("name").GetString()!;
            var businessId = body.GetProperty("businessId").GetInt32();
            var date       = body.GetProperty("date").GetString()!;
            int? templId   = body.TryGetProperty("templateId", out var tid) && tid.ValueKind != JsonValueKind.Null ? tid.GetInt32() : null;
            var facilitator = body.TryGetProperty("facilitator", out var f) ? f.GetString() : null;
            var location    = body.TryGetProperty("location",    out var l) ? l.GetString() : null;
            var schedule    = body.TryGetProperty("schedule",    out var s) ? s.GetString() : null;
            var objective   = body.TryGetProperty("objective",   out var o) ? o.GetString() : null;

            using var conn = GetConnection();
            await conn.OpenAsync();
            using var cmd = new MySqlCommand(@"
                INSERT INTO meeting_instance (business_id, template_id, name, facilitator, date, location, schedule, objective, created_by_user_id)
                VALUES (@B, @TID, @Name, @Fac, @Date, @Loc, @Sch, @Obj, @UID);
                SELECT LAST_INSERT_ID();", conn);
            cmd.Parameters.AddWithValue("@B",    businessId);
            cmd.Parameters.AddWithValue("@TID",  (object?)templId    ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@Name", name);
            cmd.Parameters.AddWithValue("@Fac",  (object?)facilitator ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@Date", date);
            cmd.Parameters.AddWithValue("@Loc",  (object?)location    ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@Sch",  (object?)schedule    ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@Obj",  (object?)objective   ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@UID",  userId);

            var newId = Convert.ToInt32(await cmd.ExecuteScalarAsync());

            // If template has default participants, copy them into the meeting
            if (templId.HasValue)
            {
                using var pCmd = new MySqlCommand(@"
                    INSERT INTO meeting_participant (meeting_id, name, order_index)
                    SELECT @MID, name, order_index FROM meeting_template_participant WHERE template_id=@TID", conn);
                pCmd.Parameters.AddWithValue("@MID", newId);
                pCmd.Parameters.AddWithValue("@TID", templId.Value);
                await pCmd.ExecuteNonQueryAsync();
            }

            var participants = await ReadParticipants(conn, newId);
            _sse.Notify(businessId, "meeting.created", new { id = newId });
            return CreatedAtAction(nameof(GetInstance), new { id = newId }, new { id = newId, businessId, templateId = templId, name, facilitator, date, location, schedule, objective, status = "draft", participants, topics = new List<object>() });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creando instancia de reunión");
            return StatusCode(500, new { message = "Error interno del servidor" });
        }
    }

    /// PATCH /api/meetings/instances/{id}
    [HttpPatch("instances/{id:int}")]
    public async Task<IActionResult> UpdateInstance(int id, [FromBody] JsonElement body)
    {
        try
        {
            using var conn = GetConnection();
            await conn.OpenAsync();
            var setClauses = new List<string>();
            var cmd = new MySqlCommand("", conn);

            if (body.TryGetProperty("name",        out var n))  { setClauses.Add("name=@Name");           cmd.Parameters.AddWithValue("@Name", n.GetString()); }
            if (body.TryGetProperty("facilitator", out var f))  { setClauses.Add("facilitator=@Fac");     cmd.Parameters.AddWithValue("@Fac",  f.ValueKind == JsonValueKind.Null ? DBNull.Value : f.GetString()); }
            if (body.TryGetProperty("date",        out var d))  { setClauses.Add("date=@Date");           cmd.Parameters.AddWithValue("@Date", d.GetString()); }
            if (body.TryGetProperty("location",    out var l))  { setClauses.Add("location=@Loc");        cmd.Parameters.AddWithValue("@Loc",  l.ValueKind == JsonValueKind.Null ? DBNull.Value : l.GetString()); }
            if (body.TryGetProperty("schedule",    out var s))  { setClauses.Add("schedule=@Sch");        cmd.Parameters.AddWithValue("@Sch",  s.ValueKind == JsonValueKind.Null ? DBNull.Value : s.GetString()); }
            if (body.TryGetProperty("objective",   out var o))  { setClauses.Add("objective=@Obj");       cmd.Parameters.AddWithValue("@Obj",  o.ValueKind == JsonValueKind.Null ? DBNull.Value : o.GetString()); }
            if (body.TryGetProperty("status",      out var st)) { setClauses.Add("status=@Status");       cmd.Parameters.AddWithValue("@Status", st.GetString()); }

            if (setClauses.Count == 0) return BadRequest(new { message = "Nada que actualizar" });

            // Resolve businessId for SSE notification
            using var bidCmd = new MySqlCommand("SELECT business_id FROM meeting_instance WHERE id=@Id", conn);
            bidCmd.Parameters.AddWithValue("@Id", id);
            var bidRaw = await bidCmd.ExecuteScalarAsync();
            var notifyBusinessId = bidRaw != null ? Convert.ToInt32(bidRaw) : (int?)null;

            cmd.CommandText = $"UPDATE meeting_instance SET {string.Join(",", setClauses)} WHERE id=@Id";
            cmd.Parameters.AddWithValue("@Id", id);
            await cmd.ExecuteNonQueryAsync();

            if (notifyBusinessId.HasValue)
                _sse.Notify(notifyBusinessId.Value, "meeting.updated", new { id });

            return Ok(new { id });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error actualizando reunión {Id}", id);
            return StatusCode(500, new { message = "Error interno del servidor" });
        }
    }

    /// DELETE /api/meetings/instances/{id}
    [HttpDelete("instances/{id:int}")]
    public async Task<IActionResult> DeleteInstance(int id)
    {
        try
        {
            using var conn = GetConnection();
            await conn.OpenAsync();

            // Resolve businessId before deletion so we can notify afterwards
            using var bidCmd = new MySqlCommand("SELECT business_id FROM meeting_instance WHERE id=@Id", conn);
            bidCmd.Parameters.AddWithValue("@Id", id);
            var bidRaw = await bidCmd.ExecuteScalarAsync();
            var notifyBusinessId = bidRaw != null ? Convert.ToInt32(bidRaw) : (int?)null;

            using var cmd = new MySqlCommand("DELETE FROM meeting_instance WHERE id=@Id", conn);
            cmd.Parameters.AddWithValue("@Id", id);
            await cmd.ExecuteNonQueryAsync();

            if (notifyBusinessId.HasValue)
                _sse.Notify(notifyBusinessId.Value, "meeting.deleted", new { id });

            return NoContent();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error eliminando reunión {Id}", id);
            return StatusCode(500, new { message = "Error interno del servidor" });
        }
    }

    /// GET /api/meetings/instances/{id}/previous-pending
    /// Tareas pendientes de la reunión anterior con la misma plantilla (carry-forward)
    [HttpGet("instances/{id:int}/previous-pending")]
    public async Task<IActionResult> GetPreviousPending(int id)
    {
        try
        {
            using var conn = GetConnection();
            await conn.OpenAsync();

            // Get template_id and date for this meeting
            using var infoCmd = new MySqlCommand("SELECT template_id, date FROM meeting_instance WHERE id=@Id", conn);
            infoCmd.Parameters.AddWithValue("@Id", id);
            using var ir = await infoCmd.ExecuteReaderAsync();
            if (!await ir.ReadAsync()) return NotFound();
            var templateId = IsNull(ir, "template_id") ? (int?)null : ir.GetInt32("template_id");
            var meetingDate = ir.GetDateTime("date");
            await ir.CloseAsync();

            if (!templateId.HasValue) return Ok(new List<object>());

            // Find the most recent previous meeting with the same template
            using var prevCmd = new MySqlCommand(@"
                SELECT id, name, date FROM meeting_instance
                WHERE template_id=@TID AND id != @MID AND date <= @Date
                ORDER BY date DESC, id DESC
                LIMIT 1", conn);
            prevCmd.Parameters.AddWithValue("@TID",  templateId.Value);
            prevCmd.Parameters.AddWithValue("@MID",  id);
            prevCmd.Parameters.AddWithValue("@Date", meetingDate.ToString("yyyy-MM-dd"));
            using var pr = await prevCmd.ExecuteReaderAsync();
            if (!await pr.ReadAsync()) return Ok(new List<object>());
            var prevId      = pr.GetInt32("id");
            var prevName    = pr.GetString("name");
            var prevDate    = pr.GetDateTime("date").ToString("yyyy-MM-dd");
            await pr.CloseAsync();

            // Get pending/in_progress tasks from that meeting
            using var taskCmd = new MySqlCommand(@"
                SELECT mt.id, mt.description, mt.responsible, mt.status, mt.due_date,
                       top.title as topic_title
                FROM meeting_task mt
                JOIN meeting_topic top ON top.id = mt.topic_id
                WHERE mt.meeting_id = @PrevId AND mt.type = 'task'
                  AND mt.status IN ('pending','in_progress')
                ORDER BY top.order_index, mt.created_at", conn);
            taskCmd.Parameters.AddWithValue("@PrevId", prevId);

            var tasks = new List<object>();
            using var tr = await taskCmd.ExecuteReaderAsync();
            while (await tr.ReadAsync())
            {
                tasks.Add(new
                {
                    id           = tr.GetInt32("id"),
                    topicTitle   = tr.GetString("topic_title"),
                    description  = tr.GetString("description"),
                    responsible  = IsNull(tr, "responsible") ? null : tr.GetString("responsible"),
                    status       = tr.GetString("status"),
                    dueDate      = IsNull(tr, "due_date") ? null : tr.GetDateTime("due_date").ToString("yyyy-MM-dd"),
                    meetingDate  = prevDate,
                    meetingName  = prevName,
                });
            }
            return Ok(tasks);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error obteniendo tareas pendientes previas para reunión {Id}", id);
            return StatusCode(500, new { message = "Error interno del servidor" });
        }
    }

    // ====================================================================
    // MEETING PARTICIPANTS
    // ====================================================================

    /// POST /api/meetings/instances/{id}/participants
    [HttpPost("instances/{id:int}/participants")]
    public async Task<IActionResult> AddParticipant(int id, [FromBody] JsonElement body)
    {
        try
        {
            var name        = body.GetProperty("name").GetString()!;
            var status      = body.TryGetProperty("status",     out var st) ? st.GetString() ?? "unknown" : "unknown";
            var orderIndex  = body.TryGetProperty("orderIndex", out var oi) ? oi.GetInt32() : 0;
            int? userId     = body.TryGetProperty("userId",     out var ui) && ui.ValueKind != JsonValueKind.Null ? ui.GetInt32() : (int?)null;
            int? employeeId = body.TryGetProperty("employeeId", out var ei) && ei.ValueKind != JsonValueKind.Null ? ei.GetInt32() : (int?)null;

            using var conn = GetConnection();
            await conn.OpenAsync();
            using var cmd = new MySqlCommand(@"
                INSERT INTO meeting_participant (meeting_id, name, status, user_id, employee_id, order_index)
                VALUES (@M, @Name, @Status, @UID, @EID, @Ord);
                SELECT LAST_INSERT_ID();", conn);
            cmd.Parameters.AddWithValue("@M",      id);
            cmd.Parameters.AddWithValue("@Name",   name);
            cmd.Parameters.AddWithValue("@Status", status);
            cmd.Parameters.AddWithValue("@UID",    (object?)userId     ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@EID",    (object?)employeeId ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@Ord",    orderIndex);
            var newId = Convert.ToInt32(await cmd.ExecuteScalarAsync());
            _sse.NotifyMeeting(id, "detail.changed");
            return Ok(new { id = newId, meetingId = id, name, status, userId, employeeId, orderIndex });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error agregando participante a reunión {Id}", id);
            return StatusCode(500, new { message = "Error interno del servidor" });
        }
    }

    /// PATCH /api/meetings/participants/{id}
    [HttpPatch("participants/{id:int}")]
    public async Task<IActionResult> UpdateParticipant(int id, [FromBody] JsonElement body)
    {
        try
        {
            using var conn = GetConnection();
            await conn.OpenAsync();

            // Resolve meetingId for SSE notification
            using var midCmd = new MySqlCommand("SELECT meeting_id FROM meeting_participant WHERE id=@Id", conn);
            midCmd.Parameters.AddWithValue("@Id", id);
            var midRaw = await midCmd.ExecuteScalarAsync();
            var notifyMeetingId = midRaw != null ? Convert.ToInt32(midRaw) : (int?)null;

            var setClauses = new List<string>();
            var cmd = new MySqlCommand("", conn);

            if (body.TryGetProperty("name",       out var n))  { setClauses.Add("name=@Name");       cmd.Parameters.AddWithValue("@Name",   n.GetString()); }
            if (body.TryGetProperty("status",     out var st)) { setClauses.Add("status=@Status");   cmd.Parameters.AddWithValue("@Status", st.GetString()); }
            if (body.TryGetProperty("userId",     out var ui)) { setClauses.Add("user_id=@UID");     cmd.Parameters.AddWithValue("@UID",    ui.ValueKind == JsonValueKind.Null ? (object)DBNull.Value : ui.GetInt32()); }
            if (body.TryGetProperty("employeeId", out var ei)) { setClauses.Add("employee_id=@EID"); cmd.Parameters.AddWithValue("@EID",    ei.ValueKind == JsonValueKind.Null ? (object)DBNull.Value : ei.GetInt32()); }

            if (setClauses.Count == 0) return BadRequest(new { message = "Nada que actualizar" });
            cmd.CommandText = $"UPDATE meeting_participant SET {string.Join(",", setClauses)} WHERE id=@Id";
            cmd.Parameters.AddWithValue("@Id", id);
            await cmd.ExecuteNonQueryAsync();

            if (notifyMeetingId.HasValue)
                _sse.NotifyMeeting(notifyMeetingId.Value, "detail.changed");

            return Ok(new { id });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error actualizando participante {Id}", id);
            return StatusCode(500, new { message = "Error interno del servidor" });
        }
    }

    /// DELETE /api/meetings/participants/{id}
    [HttpDelete("participants/{id:int}")]
    public async Task<IActionResult> DeleteParticipant(int id)
    {
        try
        {
            using var conn = GetConnection();
            await conn.OpenAsync();

            // Resolve meetingId before deletion
            using var midCmd = new MySqlCommand("SELECT meeting_id FROM meeting_participant WHERE id=@Id", conn);
            midCmd.Parameters.AddWithValue("@Id", id);
            var midRaw = await midCmd.ExecuteScalarAsync();
            var notifyMeetingId = midRaw != null ? Convert.ToInt32(midRaw) : (int?)null;

            using var cmd = new MySqlCommand("DELETE FROM meeting_participant WHERE id=@Id", conn);
            cmd.Parameters.AddWithValue("@Id", id);
            await cmd.ExecuteNonQueryAsync();

            if (notifyMeetingId.HasValue)
                _sse.NotifyMeeting(notifyMeetingId.Value, "detail.changed");

            return NoContent();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error eliminando participante {Id}", id);
            return StatusCode(500, new { message = "Error interno del servidor" });
        }
    }

    // ====================================================================
    // TOPICS
    // ====================================================================

    /// POST /api/meetings/instances/{id}/topics
    [HttpPost("instances/{id:int}/topics")]
    public async Task<IActionResult> AddTopic(int id, [FromBody] JsonElement body)
    {
        try
        {
            var title      = body.GetProperty("title").GetString()!;
            var raisedBy   = body.TryGetProperty("raisedBy",   out var rb) ? rb.GetString() : null;
            var orderIndex = body.TryGetProperty("orderIndex", out var oi) ? oi.GetInt32() : 0;

            using var conn = GetConnection();
            await conn.OpenAsync();
            using var cmd = new MySqlCommand(@"
                INSERT INTO meeting_topic (meeting_id, title, raised_by, order_index)
                VALUES (@M, @Title, @RB, @Ord);
                SELECT LAST_INSERT_ID();", conn);
            cmd.Parameters.AddWithValue("@M",     id);
            cmd.Parameters.AddWithValue("@Title", title);
            cmd.Parameters.AddWithValue("@RB",    (object?)raisedBy ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@Ord",   orderIndex);
            var newId = Convert.ToInt32(await cmd.ExecuteScalarAsync());
            _sse.NotifyMeeting(id, "detail.changed");
            return Ok(new { id = newId, meetingId = id, title, raisedBy, orderIndex, tasks = new List<object>(), createdAt = DateTime.UtcNow, updatedAt = DateTime.UtcNow });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error agregando tema a reunión {Id}", id);
            return StatusCode(500, new { message = "Error interno del servidor" });
        }
    }

    /// PATCH /api/meetings/topics/{id}
    [HttpPatch("topics/{id:int}")]
    public async Task<IActionResult> UpdateTopic(int id, [FromBody] JsonElement body)
    {
        try
        {
            using var conn = GetConnection();
            await conn.OpenAsync();

            // Resolve meetingId for SSE notification
            using var midCmd = new MySqlCommand("SELECT meeting_id FROM meeting_topic WHERE id=@Id", conn);
            midCmd.Parameters.AddWithValue("@Id", id);
            var midRaw = await midCmd.ExecuteScalarAsync();
            var notifyMeetingId = midRaw != null ? Convert.ToInt32(midRaw) : (int?)null;

            var setClauses = new List<string>();
            var cmd = new MySqlCommand("", conn);

            if (body.TryGetProperty("title",      out var t))  { setClauses.Add("title=@Title");         cmd.Parameters.AddWithValue("@Title", t.GetString()); }
            if (body.TryGetProperty("raisedBy",   out var rb)) { setClauses.Add("raised_by=@RB");        cmd.Parameters.AddWithValue("@RB",    rb.ValueKind == JsonValueKind.Null ? DBNull.Value : rb.GetString()); }
            if (body.TryGetProperty("orderIndex", out var oi)) { setClauses.Add("order_index=@Ord");     cmd.Parameters.AddWithValue("@Ord",   oi.GetInt32()); }

            if (setClauses.Count == 0) return BadRequest(new { message = "Nada que actualizar" });
            cmd.CommandText = $"UPDATE meeting_topic SET {string.Join(",", setClauses)} WHERE id=@Id";
            cmd.Parameters.AddWithValue("@Id", id);
            await cmd.ExecuteNonQueryAsync();

            if (notifyMeetingId.HasValue)
                _sse.NotifyMeeting(notifyMeetingId.Value, "detail.changed");

            return Ok(new { id });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error actualizando tema {Id}", id);
            return StatusCode(500, new { message = "Error interno del servidor" });
        }
    }

    /// DELETE /api/meetings/topics/{id}
    [HttpDelete("topics/{id:int}")]
    public async Task<IActionResult> DeleteTopic(int id)
    {
        try
        {
            using var conn = GetConnection();
            await conn.OpenAsync();

            // Resolve meetingId before deletion
            using var midCmd = new MySqlCommand("SELECT meeting_id FROM meeting_topic WHERE id=@Id", conn);
            midCmd.Parameters.AddWithValue("@Id", id);
            var midRaw = await midCmd.ExecuteScalarAsync();
            var notifyMeetingId = midRaw != null ? Convert.ToInt32(midRaw) : (int?)null;

            using var cmd = new MySqlCommand("DELETE FROM meeting_topic WHERE id=@Id", conn);
            cmd.Parameters.AddWithValue("@Id", id);
            await cmd.ExecuteNonQueryAsync();

            if (notifyMeetingId.HasValue)
                _sse.NotifyMeeting(notifyMeetingId.Value, "detail.changed");

            return NoContent();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error eliminando tema {Id}", id);
            return StatusCode(500, new { message = "Error interno del servidor" });
        }
    }

    // ====================================================================
    // TASKS / COMMENTS
    // ====================================================================

    /// POST /api/meetings/topics/{topicId}/tasks
    [HttpPost("topics/{topicId:int}/tasks")]
    public async Task<IActionResult> AddTask(int topicId, [FromBody] JsonElement body)
    {
        try
        {
            var type        = body.TryGetProperty("type",        out var tp) ? tp.GetString() ?? "task" : "task";
            var description = body.GetProperty("description").GetString()!;
            // Comments have no responsible/status/dueDate
            var responsible = (type == "comment") ? null : (body.TryGetProperty("responsible", out var resp) ? resp.GetString() : null);
            var status      = (type == "comment") ? "pending" : (body.TryGetProperty("status",  out var st)  ? st.GetString()  ?? "pending" : "pending");
            var dueDate     = (type == "comment") ? null : (body.TryGetProperty("dueDate", out var dd) && dd.ValueKind != JsonValueKind.Null ? dd.GetString() : null);
            int? originId   = body.TryGetProperty("originTaskId", out var oid) && oid.ValueKind != JsonValueKind.Null ? oid.GetInt32() : null;

            // Get meeting_id from topic
            using var conn = GetConnection();
            await conn.OpenAsync();
            using var topicCmd = new MySqlCommand("SELECT meeting_id FROM meeting_topic WHERE id=@T", conn);
            topicCmd.Parameters.AddWithValue("@T", topicId);
            var meetingId = Convert.ToInt32(await topicCmd.ExecuteScalarAsync());

            using var cmd = new MySqlCommand(@"
                INSERT INTO meeting_task (topic_id, meeting_id, type, description, responsible, status, due_date, origin_task_id)
                VALUES (@T, @M, @Type, @Desc, @Resp, @Status, @DD, @Orig);
                SELECT LAST_INSERT_ID();", conn);
            cmd.Parameters.AddWithValue("@T",      topicId);
            cmd.Parameters.AddWithValue("@M",      meetingId);
            cmd.Parameters.AddWithValue("@Type",   type);
            cmd.Parameters.AddWithValue("@Desc",   description);
            cmd.Parameters.AddWithValue("@Resp",   (object?)responsible ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@Status", status);
            cmd.Parameters.AddWithValue("@DD",     (object?)dueDate    ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@Orig",   (object?)originId   ?? DBNull.Value);

            var newId = Convert.ToInt32(await cmd.ExecuteScalarAsync());
            _sse.NotifyMeeting(meetingId, "detail.changed");
            return Ok(new { id = newId, topicId, meetingId, type, description, responsible, status, dueDate, originTaskId = originId, createdAt = DateTime.UtcNow, updatedAt = DateTime.UtcNow });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error agregando tarea a tema {TopicId}", topicId);
            return StatusCode(500, new { message = "Error interno del servidor" });
        }
    }

    /// PATCH /api/meetings/tasks/{id}
    [HttpPatch("tasks/{id:int}")]
    public async Task<IActionResult> UpdateTask(int id, [FromBody] JsonElement body)
    {
        try
        {
            using var conn = GetConnection();
            await conn.OpenAsync();

            // Verify task type — comments cannot have status changed to done
            if (body.TryGetProperty("status", out var stProp))
            {
                using var typeCmd = new MySqlCommand("SELECT type FROM meeting_task WHERE id=@Id", conn);
                typeCmd.Parameters.AddWithValue("@Id", id);
                var taskType = (string?)await typeCmd.ExecuteScalarAsync();
                if (taskType == "comment" && stProp.GetString() == "done")
                    return BadRequest(new { message = "Los comentarios no pueden marcarse como terminados" });
            }

            var setClauses = new List<string>();
            var cmd = new MySqlCommand("", conn);

            if (body.TryGetProperty("description", out var d))  { setClauses.Add("description=@Desc");    cmd.Parameters.AddWithValue("@Desc",   d.GetString()); }
            if (body.TryGetProperty("responsible",  out var r))  { setClauses.Add("responsible=@Resp");   cmd.Parameters.AddWithValue("@Resp",   r.ValueKind == JsonValueKind.Null ? DBNull.Value : r.GetString()); }
            if (body.TryGetProperty("status",       out var st)) { setClauses.Add("status=@Status");      cmd.Parameters.AddWithValue("@Status", st.GetString()); }
            if (body.TryGetProperty("dueDate",      out var dd)) { setClauses.Add("due_date=@DD");        cmd.Parameters.AddWithValue("@DD",     dd.ValueKind == JsonValueKind.Null ? DBNull.Value : dd.GetString()); }

            if (setClauses.Count == 0) return BadRequest(new { message = "Nada que actualizar" });

            // Resolve meetingId for SSE notification
            using var midCmd = new MySqlCommand("SELECT meeting_id FROM meeting_task WHERE id=@Id", conn);
            midCmd.Parameters.AddWithValue("@Id", id);
            var midRaw = await midCmd.ExecuteScalarAsync();
            var notifyMeetingId = midRaw != null ? Convert.ToInt32(midRaw) : (int?)null;

            cmd.CommandText = $"UPDATE meeting_task SET {string.Join(",", setClauses)} WHERE id=@Id";
            cmd.Parameters.AddWithValue("@Id", id);
            await cmd.ExecuteNonQueryAsync();

            if (notifyMeetingId.HasValue)
                _sse.NotifyMeeting(notifyMeetingId.Value, "detail.changed");

            return Ok(new { id });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error actualizando tarea {Id}", id);
            return StatusCode(500, new { message = "Error interno del servidor" });
        }
    }

    /// DELETE /api/meetings/tasks/{id}
    [HttpDelete("tasks/{id:int}")]
    public async Task<IActionResult> DeleteTask(int id)
    {
        try
        {
            using var conn = GetConnection();
            await conn.OpenAsync();

            // Resolve meetingId before deletion
            using var midCmd = new MySqlCommand("SELECT meeting_id FROM meeting_task WHERE id=@Id", conn);
            midCmd.Parameters.AddWithValue("@Id", id);
            var midRaw = await midCmd.ExecuteScalarAsync();
            var notifyMeetingId = midRaw != null ? Convert.ToInt32(midRaw) : (int?)null;

            using var cmd = new MySqlCommand("DELETE FROM meeting_task WHERE id=@Id", conn);
            cmd.Parameters.AddWithValue("@Id", id);
            await cmd.ExecuteNonQueryAsync();

            if (notifyMeetingId.HasValue)
                _sse.NotifyMeeting(notifyMeetingId.Value, "detail.changed");

            return NoContent();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error eliminando tarea {Id}", id);
            return StatusCode(500, new { message = "Error interno del servidor" });
        }
    }
}
