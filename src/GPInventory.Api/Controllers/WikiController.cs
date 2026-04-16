#pragma warning disable CS8601
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Cors;
using Microsoft.AspNetCore.Mvc;
using MySqlConnector;
using System.Security.Claims;

namespace GPInventory.Api.Controllers;

/// <summary>
/// GP Control – Wiki: documentación por empresa con páginas jerárquicas en Markdown.
/// </summary>
[ApiController]
[Route("api/wiki")]
[EnableCors("AllowFrontend")]
[Authorize]
public class WikiController : ControllerBase
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<WikiController> _logger;

    public WikiController(IConfiguration cfg, ILogger<WikiController> logger)
    {
        _configuration = cfg;
        _logger = logger;
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

    // ── helpers ──────────────────────────────────────────────────────────

    private static string Slugify(string title)
    {
        var slug = title.ToLowerInvariant()
            .Replace(" ", "-").Replace("á","a").Replace("é","e").Replace("í","i")
            .Replace("ó","o").Replace("ú","u").Replace("ñ","n");
        var clean = new System.Text.StringBuilder();
        foreach (var c in slug) if (char.IsLetterOrDigit(c) || c == '-') clean.Append(c);
        return clean.ToString();
    }

    private static object MapPage(MySqlDataReader r) => new {
        id             = r.GetInt32("id"),
        businessId     = r.GetInt32("business_id"),
        parentPageId   = IsNull(r, "parent_page_id") ? (int?)null : r.GetInt32("parent_page_id"),
        title          = r.GetString("title"),
        slug           = r.GetString("slug"),
        content        = IsNull(r, "content") ? null : r.GetString("content"),
        orderIndex     = r.GetInt32("order_index"),
        createdAt      = r.GetDateTime("created_at"),
        updatedAt      = r.GetDateTime("updated_at"),
    };

    // ====================================================================
    // PAGES
    // ====================================================================

    /// GET /api/wiki?businessId=
    [HttpGet]
    public async Task<IActionResult> GetPages([FromQuery] int businessId)
    {
        try
        {
            await using var cn = GetConnection();
            await cn.OpenAsync();
            await using var cmd = cn.CreateCommand();
            cmd.CommandText = @"
                SELECT id, business_id, parent_page_id, title, slug, NULL AS content, order_index, created_at, updated_at
                FROM control_wiki_page
                WHERE business_id = @bid
                ORDER BY parent_page_id, order_index, title";
            cmd.Parameters.AddWithValue("@bid", businessId);
            await using var r = await cmd.ExecuteReaderAsync();
            var list = new List<object>();
            while (await r.ReadAsync()) list.Add(MapPage(r));
            return Ok(list);
        }
        catch (Exception ex) { _logger.LogError(ex, "GetPages"); return StatusCode(500, ex.Message); }
    }

    /// GET /api/wiki/{id}
    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetPage(int id)
    {
        try
        {
            await using var cn = GetConnection();
            await cn.OpenAsync();
            await using var cmd = cn.CreateCommand();
            cmd.CommandText = @"
                SELECT id, business_id, parent_page_id, title, slug, content, order_index, created_at, updated_at
                FROM control_wiki_page WHERE id = @id";
            cmd.Parameters.AddWithValue("@id", id);
            await using var r = await cmd.ExecuteReaderAsync();
            if (!await r.ReadAsync()) return NotFound();
            return Ok(MapPage(r));
        }
        catch (Exception ex) { _logger.LogError(ex, "GetPage"); return StatusCode(500, ex.Message); }
    }

    /// POST /api/wiki
    [HttpPost]
    public async Task<IActionResult> CreatePage([FromBody] dynamic body)
    {
        try
        {
            using var doc  = System.Text.Json.JsonDocument.Parse(body.ToString());
            var root       = doc.RootElement;
            int businessId = root.GetProperty("businessId").GetInt32();
            string title   = root.GetProperty("title").GetString()!;
            int? parentId  = root.TryGetProperty("parentPageId", out System.Text.Json.JsonElement pp) && pp.ValueKind != System.Text.Json.JsonValueKind.Null
                             ? pp.GetInt32() : (int?)null;
            string content = root.TryGetProperty("content", out System.Text.Json.JsonElement ct) ? (ct.GetString() ?? "") : "";
            int userId     = GetCurrentUserId() ?? 0;
            string slug    = Slugify(title) + "-" + DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

            await using var cn = GetConnection();
            await cn.OpenAsync();
            await using var cmd = cn.CreateCommand();
            cmd.CommandText = @"
                INSERT INTO control_wiki_page (business_id, parent_page_id, title, slug, content, created_by_user_id)
                VALUES (@bid, @pid, @title, @slug, @content, @uid);
                SELECT LAST_INSERT_ID();";
            cmd.Parameters.AddWithValue("@bid",     businessId);
            cmd.Parameters.AddWithValue("@pid",     parentId.HasValue ? parentId.Value : DBNull.Value);
            cmd.Parameters.AddWithValue("@title",   title);
            cmd.Parameters.AddWithValue("@slug",    slug);
            cmd.Parameters.AddWithValue("@content", content);
            cmd.Parameters.AddWithValue("@uid",     userId);
            var newId = Convert.ToInt32(await cmd.ExecuteScalarAsync());
            return Ok(new { id = newId });
        }
        catch (Exception ex) { _logger.LogError(ex, "CreatePage"); return StatusCode(500, ex.Message); }
    }

    /// PUT /api/wiki/{id}
    [HttpPut("{id:int}")]
    public async Task<IActionResult> UpdatePage(int id, [FromBody] dynamic body)
    {
        try
        {
            using var doc = System.Text.Json.JsonDocument.Parse(body.ToString());
            var root = doc.RootElement;
            string? title   = root.TryGetProperty("title",   out System.Text.Json.JsonElement t)  ? t.GetString()   : null;
            string? content = root.TryGetProperty("content", out System.Text.Json.JsonElement ct) ? ct.GetString()  : null;
            int? parentId   = root.TryGetProperty("parentPageId", out System.Text.Json.JsonElement pp)
                              && pp.ValueKind != System.Text.Json.JsonValueKind.Null
                              ? pp.GetInt32() : (int?)null;

            await using var cn = GetConnection();
            await cn.OpenAsync();
            await using var cmd = cn.CreateCommand();
            cmd.CommandText = @"
                UPDATE control_wiki_page SET
                  title          = COALESCE(@title,   title),
                  content        = COALESCE(@content, content),
                  parent_page_id = CASE WHEN @hasPid = 1 THEN @pid ELSE parent_page_id END
                WHERE id = @id";
            cmd.Parameters.AddWithValue("@title",   (object?)title   ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@content", (object?)content ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@hasPid",  parentId.HasValue ? 1 : 0);
            cmd.Parameters.AddWithValue("@pid",     parentId.HasValue ? parentId.Value : DBNull.Value);
            cmd.Parameters.AddWithValue("@id",      id);
            await cmd.ExecuteNonQueryAsync();
            return Ok();
        }
        catch (Exception ex) { _logger.LogError(ex, "UpdatePage"); return StatusCode(500, ex.Message); }
    }

    /// DELETE /api/wiki/{id}
    [HttpDelete("{id:int}")]
    public async Task<IActionResult> DeletePage(int id)
    {
        try
        {
            await using var cn = GetConnection();
            await cn.OpenAsync();
            await using var cmd = cn.CreateCommand();
            cmd.CommandText = "DELETE FROM control_wiki_page WHERE id = @id";
            cmd.Parameters.AddWithValue("@id", id);
            await cmd.ExecuteNonQueryAsync();
            return Ok();
        }
        catch (Exception ex) { _logger.LogError(ex, "DeletePage"); return StatusCode(500, ex.Message); }
    }

    // ====================================================================
    // TASK ATTACHMENTS
    // ====================================================================

    /// GET /api/wiki/{id}/tasks
    [HttpGet("{id:int}/tasks")]
    public async Task<IActionResult> GetAttachedTasks(int id)
    {
        try
        {
            await using var cn = GetConnection();
            await cn.OpenAsync();
            await using var cmd = cn.CreateCommand();
            cmd.CommandText = @"
                SELECT t.id, t.title, t.type, t.status, t.priority, t.assigned_to_name, t.board_id
                FROM control_wiki_page_task wt
                JOIN control_task t ON t.id = wt.task_id
                WHERE wt.page_id = @pid";
            cmd.Parameters.AddWithValue("@pid", id);
            await using var r = await cmd.ExecuteReaderAsync();
            var list = new List<object>();
            while (await r.ReadAsync())
                list.Add(new {
                    id             = r.GetInt32("id"),
                    title          = r.GetString("title"),
                    type           = r.GetString("type"),
                    status         = r.GetString("status"),
                    priority       = r.GetString("priority"),
                    assignedToName = IsNull(r, "assigned_to_name") ? null : r.GetString("assigned_to_name"),
                    boardId        = r.GetInt32("board_id"),
                });
            return Ok(list);
        }
        catch (Exception ex) { _logger.LogError(ex, "GetAttachedTasks"); return StatusCode(500, ex.Message); }
    }

    /// POST /api/wiki/{id}/tasks/{taskId}
    [HttpPost("{id:int}/tasks/{taskId:int}")]
    public async Task<IActionResult> AttachTask(int id, int taskId)
    {
        try
        {
            await using var cn = GetConnection();
            await cn.OpenAsync();
            await using var cmd = cn.CreateCommand();
            cmd.CommandText = @"
                INSERT IGNORE INTO control_wiki_page_task (page_id, task_id) VALUES (@pid, @tid)";
            cmd.Parameters.AddWithValue("@pid", id);
            cmd.Parameters.AddWithValue("@tid", taskId);
            await cmd.ExecuteNonQueryAsync();
            return Ok();
        }
        catch (Exception ex) { _logger.LogError(ex, "AttachTask"); return StatusCode(500, ex.Message); }
    }

    /// DELETE /api/wiki/{id}/tasks/{taskId}
    [HttpDelete("{id:int}/tasks/{taskId:int}")]
    public async Task<IActionResult> DetachTask(int id, int taskId)
    {
        try
        {
            await using var cn = GetConnection();
            await cn.OpenAsync();
            await using var cmd = cn.CreateCommand();
            cmd.CommandText = "DELETE FROM control_wiki_page_task WHERE page_id = @pid AND task_id = @tid";
            cmd.Parameters.AddWithValue("@pid", id);
            cmd.Parameters.AddWithValue("@tid", taskId);
            await cmd.ExecuteNonQueryAsync();
            return Ok();
        }
        catch (Exception ex) { _logger.LogError(ex, "DetachTask"); return StatusCode(500, ex.Message); }
    }
}
