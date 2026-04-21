#pragma warning disable CS8601
using GPInventory.Api.Hubs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Cors;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using MySqlConnector;
using System.Security.Claims;
using System.Text.Json;

namespace GPInventory.Api.Controllers;

/// <summary>
/// GP-CRM: Gestión de prospectos, pipeline de ventas, actividades y métricas.
/// </summary>
[ApiController]
[Route("api/crm")]
[EnableCors("AllowFrontend")]
[Authorize]
public class CrmController : ControllerBase
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<CrmController> _logger;
    private readonly IHubContext<CrmHub> _hub;

    public CrmController(IConfiguration configuration, ILogger<CrmController> logger, IHubContext<CrmHub> hub)
    {
        _configuration = configuration;
        _logger = logger;
        _hub = hub;
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

    /// Notifica a todos los clientes del negocio que algo cambió.
    private Task Broadcast(int businessId, string eventName, object? payload = null)
        => _hub.Clients
               .Group(CrmHub.GroupName(businessId))
               .SendAsync(eventName, payload ?? new { });

    /// Obtiene business_id de cualquier tabla con esa columna.
    private async Task<int?> FetchBusinessId(string table, int entityId)
    {
        try
        {
            using var conn = GetConnection();
            await conn.OpenAsync();
            using var cmd = new MySqlCommand($"SELECT business_id FROM {table} WHERE id=@ID", conn);
            cmd.Parameters.AddWithValue("@ID", entityId);
            var r = await cmd.ExecuteScalarAsync();
            return r is null || r == DBNull.Value ? null : Convert.ToInt32(r);
        }
        catch { return null; }
    }

    /// Broadcast fire-and-forget (no lanza excepción si falla).
    private async Task BroadcastSafe(int? businessId, string evt)
    {
        if (businessId.HasValue)
            try { await Broadcast(businessId.Value, evt); } catch { /* no critical */ }
    }

    // ================================================================
    // PIPELINES
    // ================================================================

    /// GET /api/crm/pipelines?businessId=
    /// Retorna los pipelines del negocio. Si no existe ninguno, crea un pipeline
    /// "Principal" por defecto y migra los stages huérfanos a él.
    [HttpGet("pipelines")]
    public async Task<IActionResult> GetPipelines([FromQuery] int businessId)
    {
        try
        {
            using var conn = GetConnection();
            await conn.OpenAsync();

            // Verificar si existen pipelines
            using var countCmd = new MySqlCommand(
                "SELECT COUNT(*) FROM crm_pipeline WHERE business_id=@B", conn);
            countCmd.Parameters.AddWithValue("@B", businessId);
            var count = Convert.ToInt32(await countCmd.ExecuteScalarAsync());

            if (count == 0)
            {
                // Crear pipeline "Principal" por defecto
                using var insCmd = new MySqlCommand(@"
                    INSERT INTO crm_pipeline (business_id, name) VALUES (@B, 'Principal');
                    SELECT LAST_INSERT_ID();", conn);
                insCmd.Parameters.AddWithValue("@B", businessId);
                var defaultId = Convert.ToInt32(await insCmd.ExecuteScalarAsync());

                // Migrar stages huérfanos (pipeline_id NULL) a este pipeline
                using var migCmd = new MySqlCommand(@"
                    UPDATE crm_stage SET pipeline_id=@P WHERE business_id=@B AND pipeline_id IS NULL", conn);
                migCmd.Parameters.AddWithValue("@P", defaultId);
                migCmd.Parameters.AddWithValue("@B", businessId);
                await migCmd.ExecuteNonQueryAsync();

                // Migrar deals huérfanos
                using var migDCmd = new MySqlCommand(@"
                    UPDATE crm_deal SET pipeline_id=@P WHERE business_id=@B AND pipeline_id IS NULL", conn);
                migDCmd.Parameters.AddWithValue("@P", defaultId);
                migDCmd.Parameters.AddWithValue("@B", businessId);
                await migDCmd.ExecuteNonQueryAsync();
            }

            using var cmd = new MySqlCommand(@"
                SELECT p.id, p.business_id, p.name, p.created_at,
                       COUNT(s.id) AS stage_count
                FROM crm_pipeline p
                LEFT JOIN crm_stage s ON s.pipeline_id = p.id
                WHERE p.business_id = @B
                GROUP BY p.id
                ORDER BY p.created_at ASC", conn);
            cmd.Parameters.AddWithValue("@B", businessId);

            var list = new List<object>();
            using var r = await cmd.ExecuteReaderAsync();
            while (await r.ReadAsync())
                list.Add(new {
                    id          = r.GetInt32("id"),
                    businessId  = r.GetInt32("business_id"),
                    name        = r.GetString("name"),
                    stageCount  = r.GetInt32("stage_count"),
                    createdAt   = r.GetDateTime("created_at"),
                });

            return Ok(new { success = true, data = list });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error GetPipelines: {M}", ex.Message);
            return StatusCode(500, new { success = false, message = ex.Message });
        }
    }

    /// POST /api/crm/pipelines
    [HttpPost("pipelines")]
    public async Task<IActionResult> CreatePipeline([FromBody] JsonElement body)
    {
        try
        {
            var businessId = body.GetProperty("businessId").GetInt32();
            var name       = body.GetProperty("name").GetString()!;

            using var conn = GetConnection();
            await conn.OpenAsync();
            using var cmd = new MySqlCommand(@"
                INSERT INTO crm_pipeline (business_id, name) VALUES (@B, @N);
                SELECT LAST_INSERT_ID();", conn);
            cmd.Parameters.AddWithValue("@B", businessId);
            cmd.Parameters.AddWithValue("@N", name);
            var id = Convert.ToInt32(await cmd.ExecuteScalarAsync());

            await BroadcastSafe(businessId, "PipelinesChanged");
            return Ok(new { success = true, data = new { id, businessId, name, stageCount = 0 } });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error CreatePipeline: {M}", ex.Message);
            return StatusCode(500, new { success = false, message = ex.Message });
        }
    }

    /// PUT /api/crm/pipelines/{id}
    [HttpPut("pipelines/{id}")]
    public async Task<IActionResult> UpdatePipeline(int id, [FromBody] JsonElement body)
    {
        try
        {
            var name = body.GetProperty("name").GetString()!;
            using var conn = GetConnection();
            await conn.OpenAsync();
            using var cmd = new MySqlCommand("UPDATE crm_pipeline SET name=@N WHERE id=@ID", conn);
            cmd.Parameters.AddWithValue("@N",  name);
            cmd.Parameters.AddWithValue("@ID", id);
            await cmd.ExecuteNonQueryAsync();
            await BroadcastSafe(await FetchBusinessId("crm_pipeline", id), "PipelinesChanged");
            return Ok(new { success = true });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error UpdatePipeline: {M}", ex.Message);
            return StatusCode(500, new { success = false, message = ex.Message });
        }
    }

    /// DELETE /api/crm/pipelines/{id}
    [HttpDelete("pipelines/{id}")]
    public async Task<IActionResult> DeletePipeline(int id)
    {
        try
        {
            using var conn = GetConnection();
            await conn.OpenAsync();

            // Verificar que no sea el último pipeline del negocio
            using var bizCmd = new MySqlCommand("SELECT business_id FROM crm_pipeline WHERE id=@ID", conn);
            bizCmd.Parameters.AddWithValue("@ID", id);
            var bizIdRaw = await bizCmd.ExecuteScalarAsync();
            if (bizIdRaw is null || bizIdRaw == DBNull.Value)
                return NotFound(new { success = false, message = "Pipeline no encontrado." });
            var bizId = Convert.ToInt32(bizIdRaw);

            using var cntCmd = new MySqlCommand("SELECT COUNT(*) FROM crm_pipeline WHERE business_id=@B", conn);
            cntCmd.Parameters.AddWithValue("@B", bizId);
            if (Convert.ToInt32(await cntCmd.ExecuteScalarAsync()) <= 1)
                return BadRequest(new { success = false, message = "No puedes eliminar el único pipeline del negocio." });

            using var cmd = new MySqlCommand("DELETE FROM crm_pipeline WHERE id=@ID", conn);
            cmd.Parameters.AddWithValue("@ID", id);
            await cmd.ExecuteNonQueryAsync();
            await BroadcastSafe(bizId, "PipelinesChanged");
            return Ok(new { success = true });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error DeletePipeline: {M}", ex.Message);
            return StatusCode(500, new { success = false, message = ex.Message });
        }
    }

    // ================================================================
    // STAGES
    // ================================================================

    /// GET /api/crm/stages?businessId=&pipelineId=
    [HttpGet("stages")]
    public async Task<IActionResult> GetStages([FromQuery] int businessId, [FromQuery] int? pipelineId)
    {
        try
        {
            using var conn = GetConnection();
            await conn.OpenAsync();
            using var cmd = new MySqlCommand(@"
                SELECT id, business_id, pipeline_id, name, order_index, color, is_won, is_lost, is_sale, deal_count, created_at
                FROM crm_stage
                WHERE business_id = @B
                  AND (@P IS NULL OR pipeline_id = @P)
                ORDER BY order_index ASC", conn);
            cmd.Parameters.AddWithValue("@B", businessId);
            cmd.Parameters.AddWithValue("@P", pipelineId.HasValue ? pipelineId.Value : (object)DBNull.Value);

            var list = new List<object>();
            using var r = await cmd.ExecuteReaderAsync();
            while (await r.ReadAsync())
                list.Add(new {
                    id          = r.GetInt32("id"),
                    businessId  = r.GetInt32("business_id"),
                    pipelineId  = IsNull(r, "pipeline_id") ? (int?)null : r.GetInt32("pipeline_id"),
                    name        = r.GetString("name"),
                    orderIndex  = r.GetInt32("order_index"),
                    color       = r.GetString("color"),
                    isWon       = r.GetBoolean("is_won"),
                    isLost      = r.GetBoolean("is_lost"),
                    isSale      = r.GetBoolean("is_sale"),
                    dealCount   = r.GetInt32("deal_count"),
                    createdAt   = r.GetDateTime("created_at"),
                });

            return Ok(new { success = true, data = list });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error GetStages: {M}", ex.Message);
            return StatusCode(500, new { success = false, message = ex.Message });
        }
    }

    /// POST /api/crm/stages
    [HttpPost("stages")]
    public async Task<IActionResult> CreateStage([FromBody] JsonElement body)
    {
        try
        {
            var businessId  = body.GetProperty("businessId").GetInt32();
            var name        = body.GetProperty("name").GetString()!;
            var color       = body.TryGetProperty("color", out var c) ? c.GetString() ?? "#10b981" : "#10b981";
            var isWon       = body.TryGetProperty("isWon",  out var iw) && iw.GetBoolean();
            var isLost      = body.TryGetProperty("isLost", out var il) && il.GetBoolean();
            var isSale      = body.TryGetProperty("isSale", out var isa) && isa.GetBoolean();
            var pipelineId  = body.TryGetProperty("pipelineId", out var pip) ? pip.GetInt32() : (int?)null;

            using var conn = GetConnection();
            await conn.OpenAsync();

            // Calcular siguiente order_index dentro del pipeline
            using var cmdMax = new MySqlCommand(
                "SELECT COALESCE(MAX(order_index),0)+1 FROM crm_stage WHERE business_id=@B AND (@P IS NULL OR pipeline_id=@P)", conn);
            cmdMax.Parameters.AddWithValue("@B", businessId);
            cmdMax.Parameters.AddWithValue("@P", pipelineId.HasValue ? pipelineId.Value : (object)DBNull.Value);
            var nextIdx = Convert.ToInt32(await cmdMax.ExecuteScalarAsync());

            using var cmd = new MySqlCommand(@"
                INSERT INTO crm_stage (business_id, pipeline_id, name, order_index, color, is_won, is_lost, is_sale)
                VALUES (@B, @P, @N, @O, @C, @W, @L, @S);
                SELECT LAST_INSERT_ID();", conn);
            cmd.Parameters.AddWithValue("@B", businessId);
            cmd.Parameters.AddWithValue("@P", pipelineId.HasValue ? pipelineId.Value : (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@N", name);
            cmd.Parameters.AddWithValue("@O", nextIdx);
            cmd.Parameters.AddWithValue("@C", color);
            cmd.Parameters.AddWithValue("@W", isWon);
            cmd.Parameters.AddWithValue("@L", isLost);
            cmd.Parameters.AddWithValue("@S", isSale);
            var id = Convert.ToInt32(await cmd.ExecuteScalarAsync());

            await BroadcastSafe(businessId, "StagesChanged");
            return Ok(new { success = true, data = new { id, businessId, pipelineId, name, orderIndex = nextIdx, color, isWon, isLost, isSale } });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error CreateStage: {M}", ex.Message);
            return StatusCode(500, new { success = false, message = ex.Message });
        }
    }

    /// PUT /api/crm/stages/{id}
    [HttpPut("stages/{id}")]
    public async Task<IActionResult> UpdateStage(int id, [FromBody] JsonElement body)
    {
        try
        {
            using var conn = GetConnection();
            await conn.OpenAsync();
            using var cmd = new MySqlCommand(@"
                UPDATE crm_stage SET
                    name        = COALESCE(@N, name),
                    color       = COALESCE(@C, color),
                    is_won      = @W,
                    is_lost     = @L,
                    is_sale     = @S,
                    order_index = COALESCE(@O, order_index)
                WHERE id = @ID", conn);
            cmd.Parameters.AddWithValue("@N",  body.TryGetProperty("name",       out var n)  ? n.GetString()  : null);
            cmd.Parameters.AddWithValue("@C",  body.TryGetProperty("color",      out var c)  ? c.GetString()  : null);
            cmd.Parameters.AddWithValue("@W",  body.TryGetProperty("isWon",      out var iw) ? iw.GetBoolean() : false);
            cmd.Parameters.AddWithValue("@L",  body.TryGetProperty("isLost",     out var il) ? il.GetBoolean() : false);
            cmd.Parameters.AddWithValue("@S",  body.TryGetProperty("isSale",     out var isa) ? isa.GetBoolean() : false);
            cmd.Parameters.AddWithValue("@O",  body.TryGetProperty("orderIndex", out var o)  ? o.GetInt32()   : (object?)null);
            cmd.Parameters.AddWithValue("@ID", id);
            await cmd.ExecuteNonQueryAsync();
            await BroadcastSafe(await FetchBusinessId("crm_stage", id), "StagesChanged");
            return Ok(new { success = true });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error UpdateStage: {M}", ex.Message);
            return StatusCode(500, new { success = false, message = ex.Message });
        }
    }

    /// POST /api/crm/stages/reorder  — body: [{ id, orderIndex }]
    [HttpPost("stages/reorder")]
    public async Task<IActionResult> ReorderStages([FromBody] List<JsonElement> items)
    {
        try
        {
            using var conn = GetConnection();
            await conn.OpenAsync();
            using var tx = await conn.BeginTransactionAsync();
            foreach (var item in items)
            {
                using var cmd = new MySqlCommand(
                    "UPDATE crm_stage SET order_index=@O WHERE id=@ID", conn, tx);
                cmd.Parameters.AddWithValue("@O",  item.GetProperty("orderIndex").GetInt32());
                cmd.Parameters.AddWithValue("@ID", item.GetProperty("id").GetInt32());
                await cmd.ExecuteNonQueryAsync();
            }
            await tx.CommitAsync();
            if (items.Count > 0)
            {
                var firstId = items[0].GetProperty("id").GetInt32();
                await BroadcastSafe(await FetchBusinessId("crm_stage", firstId), "StagesChanged");
            }
            return Ok(new { success = true });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error ReorderStages: {M}", ex.Message);
            return StatusCode(500, new { success = false, message = ex.Message });
        }
    }

    /// DELETE /api/crm/stages/{id}
    [HttpDelete("stages/{id}")]
    public async Task<IActionResult> DeleteStage(int id)
    {
        try
        {
            using var conn = GetConnection();
            await conn.OpenAsync();
            using var cmd = new MySqlCommand("DELETE FROM crm_stage WHERE id=@ID", conn);
            cmd.Parameters.AddWithValue("@ID", id);
            var stageBiz2 = await FetchBusinessId("crm_stage", id);
            await cmd.ExecuteNonQueryAsync();
            await BroadcastSafe(stageBiz2, "StagesChanged");
            return Ok(new { success = true });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error DeleteStage: {M}", ex.Message);
            return StatusCode(500, new { success = false, message = ex.Message });
        }
    }

    // ================================================================
    // PROSPECT TAGS
    // ================================================================

    /// GET /api/crm/prospect-tags?businessId=
    [HttpGet("prospect-tags")]
    public async Task<IActionResult> GetProspectTags([FromQuery] int businessId)
    {
        try
        {
            using var conn = GetConnection();
            await conn.OpenAsync();
            using var cmd = new MySqlCommand(
                "SELECT id, business_id, name, color FROM crm_prospect_tag WHERE business_id=@B ORDER BY name", conn);
            cmd.Parameters.AddWithValue("@B", businessId);
            var list = new List<object>();
            using var r = await cmd.ExecuteReaderAsync();
            while (await r.ReadAsync())
                list.Add(new { id = r.GetInt32("id"), businessId = r.GetInt32("business_id"), name = r.GetString("name"), color = r.GetString("color") });
            return Ok(new { success = true, data = list });
        }
        catch (Exception ex) { return StatusCode(500, new { success = false, message = ex.Message }); }
    }

    /// POST /api/crm/prospect-tags
    [HttpPost("prospect-tags")]
    public async Task<IActionResult> CreateProspectTag([FromBody] JsonElement body)
    {
        try
        {
            var businessId = body.GetProperty("businessId").GetInt32();
            var name       = body.GetProperty("name").GetString()!;
            var color      = body.TryGetProperty("color", out var c) ? c.GetString() ?? "#10b981" : "#10b981";
            using var conn = GetConnection();
            await conn.OpenAsync();
            using var cmd = new MySqlCommand(
                "INSERT INTO crm_prospect_tag (business_id,name,color) VALUES (@B,@N,@C); SELECT LAST_INSERT_ID();", conn);
            cmd.Parameters.AddWithValue("@B", businessId);
            cmd.Parameters.AddWithValue("@N", name);
            cmd.Parameters.AddWithValue("@C", color);
            var id = Convert.ToInt32(await cmd.ExecuteScalarAsync());
            await BroadcastSafe(businessId, "TagsChanged");
            return Ok(new { success = true, data = new { id, businessId, name, color } });
        }
        catch (Exception ex) { return StatusCode(500, new { success = false, message = ex.Message }); }
    }

    /// DELETE /api/crm/prospect-tags/{id}
    [HttpDelete("prospect-tags/{id}")]
    public async Task<IActionResult> DeleteProspectTag(int id)
    {
        try
        {
            using var conn = GetConnection();
            await conn.OpenAsync();
            using var cmd = new MySqlCommand("DELETE FROM crm_prospect_tag WHERE id=@ID", conn);
            cmd.Parameters.AddWithValue("@ID", id);
            var tagBiz = await FetchBusinessId("crm_prospect_tag", id);
            await cmd.ExecuteNonQueryAsync();
            await BroadcastSafe(tagBiz, "TagsChanged");
            return Ok(new { success = true });
        }
        catch (Exception ex) { return StatusCode(500, new { success = false, message = ex.Message }); }
    }

    // ================================================================
    // PROSPECTS (CRM)
    // ================================================================

    /// GET /api/crm/prospects?businessId=&search=&tagId=
    [HttpGet("prospects")]
    public async Task<IActionResult> GetCrmProspects([FromQuery] int businessId,
        [FromQuery] string? search, [FromQuery] int? tagId)
    {
        try
        {
            using var conn = GetConnection();
            await conn.OpenAsync();
            using var cmd = new MySqlCommand(@"
                SELECT p.id, p.business_id, p.name, p.email, p.phone, p.company, p.position,
                       p.source, p.notes, p.created_at,
                       GROUP_CONCAT(DISTINCT JSON_OBJECT('id',t.id,'name',t.name,'color',t.color)) AS tags_json
                FROM prospect p
                LEFT JOIN crm_prospect_has_tag pht ON pht.prospect_id = p.id
                LEFT JOIN crm_prospect_tag t        ON t.id = pht.tag_id
                WHERE p.business_id = @B
                  AND (@S IS NULL OR p.name LIKE @SL OR p.email LIKE @SL OR p.company LIKE @SL)
                  AND (@T IS NULL OR pht.tag_id = @T)
                GROUP BY p.id
                ORDER BY p.created_at DESC", conn);
            cmd.Parameters.AddWithValue("@B",  businessId);
            cmd.Parameters.AddWithValue("@S",  search != null ? search : (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@SL", search != null ? $"%{search}%" : (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@T",  tagId.HasValue ? tagId.Value : (object)DBNull.Value);

            var list = new List<object>();
            using var r = await cmd.ExecuteReaderAsync();
            while (await r.ReadAsync())
            {
                var tagsRaw = IsNull(r, "tags_json") ? "[]" : r.GetString("tags_json");
                var tags    = ParseTagsJson(tagsRaw);
                list.Add(new {
                    id         = r.GetInt32("id"),
                    businessId = r.GetInt32("business_id"),
                    name       = r.GetString("name"),
                    email      = IsNull(r, "email")    ? null : r.GetString("email"),
                    phone      = IsNull(r, "phone")    ? null : r.GetString("phone"),
                    company    = IsNull(r, "company")  ? null : r.GetString("company"),
                    position   = IsNull(r, "position") ? null : r.GetString("position"),
                    source     = IsNull(r, "source")   ? null : r.GetString("source"),
                    notes      = IsNull(r, "notes")    ? null : r.GetString("notes"),
                    createdAt  = r.GetDateTime("created_at"),
                    tags,
                });
            }
            return Ok(new { success = true, data = list });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error GetCrmProspects: {M}", ex.Message);
            return StatusCode(500, new { success = false, message = ex.Message });
        }
    }

    /// POST /api/crm/prospects
    [HttpPost("prospects")]
    public async Task<IActionResult> CreateCrmProspect([FromBody] JsonElement body)
    {
        try
        {
            using var conn = GetConnection();
            await conn.OpenAsync();
            using var tx = await conn.BeginTransactionAsync();

            using var cmd = new MySqlCommand(@"
                INSERT INTO prospect (business_id, name, email, phone, company, position, source, notes)
                VALUES (@B,@N,@E,@P,@CO,@PO,@S,@NO);
                SELECT LAST_INSERT_ID();", conn, tx);
            cmd.Parameters.AddWithValue("@B",  body.GetProperty("businessId").GetInt32());
            cmd.Parameters.AddWithValue("@N",  body.GetProperty("name").GetString()!);
            cmd.Parameters.AddWithValue("@E",  body.TryGetProperty("email",    out var e)  ? e.GetString()  : (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@P",  body.TryGetProperty("phone",    out var ph) ? ph.GetString() : (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@CO", body.TryGetProperty("company",  out var co) ? co.GetString() : (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@PO", body.TryGetProperty("position", out var po) ? po.GetString() : (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@S",  body.TryGetProperty("source",   out var s)  ? s.GetString()  : (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@NO", body.TryGetProperty("notes",    out var no) ? no.GetString() : (object)DBNull.Value);
            var id = Convert.ToInt32(await cmd.ExecuteScalarAsync());

            // Tags
            if (body.TryGetProperty("tagIds", out var tagIds))
                foreach (var tag in tagIds.EnumerateArray())
                {
                    using var tagCmd = new MySqlCommand(
                        "INSERT IGNORE INTO crm_prospect_has_tag (prospect_id, tag_id) VALUES (@P,@T)", conn, tx);
                    tagCmd.Parameters.AddWithValue("@P", id);
                    tagCmd.Parameters.AddWithValue("@T", tag.GetInt32());
                    await tagCmd.ExecuteNonQueryAsync();
                }

            await tx.CommitAsync();

            // Devolver el prospecto completo para que el frontend actualice el estado
            using var selCmd = new MySqlCommand(@"
                SELECT p.id, p.business_id, p.name, p.email, p.phone, p.company, p.position,
                       p.source, p.notes, p.created_at,
                       GROUP_CONCAT(DISTINCT JSON_OBJECT('id',t.id,'name',t.name,'color',t.color)) AS tags_json
                FROM prospect p
                LEFT JOIN crm_prospect_has_tag pht ON pht.prospect_id = p.id
                LEFT JOIN crm_prospect_tag t        ON t.id = pht.tag_id
                WHERE p.id = @ID
                GROUP BY p.id", conn);
            selCmd.Parameters.AddWithValue("@ID", id);
            using var sr = await selCmd.ExecuteReaderAsync();
            await sr.ReadAsync();
            var tagsRaw = IsNull(sr, "tags_json") ? "[]" : sr.GetString("tags_json");
            var created = new {
                id         = sr.GetInt32("id"),
                businessId = sr.GetInt32("business_id"),
                name       = sr.GetString("name"),
                email      = IsNull(sr, "email")    ? null : sr.GetString("email"),
                phone      = IsNull(sr, "phone")    ? null : sr.GetString("phone"),
                company    = IsNull(sr, "company")  ? null : sr.GetString("company"),
                position   = IsNull(sr, "position") ? null : sr.GetString("position"),
                source     = IsNull(sr, "source")   ? null : sr.GetString("source"),
                notes      = IsNull(sr, "notes")    ? null : sr.GetString("notes"),
                createdAt  = sr.GetDateTime("created_at"),
                tags       = ParseTagsJson(tagsRaw),
            };

            await BroadcastSafe(created.businessId, "ProspectsChanged");
            return Ok(new { success = true, data = created });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error CreateCrmProspect: {M}", ex.Message);
            return StatusCode(500, new { success = false, message = ex.Message });
        }
    }

    /// PUT /api/crm/prospects/{id}
    [HttpPut("prospects/{id}")]
    public async Task<IActionResult> UpdateCrmProspect(int id, [FromBody] JsonElement body)
    {
        try
        {
            using var conn = GetConnection();
            await conn.OpenAsync();
            using var tx = await conn.BeginTransactionAsync();

            using var cmd = new MySqlCommand(@"
                UPDATE prospect SET
                    name     = COALESCE(@N,  name),
                    email    = COALESCE(@E,  email),
                    phone    = COALESCE(@P,  phone),
                    company  = COALESCE(@CO, company),
                    position = COALESCE(@PO, position),
                    source   = COALESCE(@S,  source),
                    notes    = COALESCE(@NO, notes)
                WHERE id = @ID", conn, tx);
            cmd.Parameters.AddWithValue("@N",  body.TryGetProperty("name",     out var n)  ? n.GetString()  : (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@E",  body.TryGetProperty("email",    out var e)  ? e.GetString()  : (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@P",  body.TryGetProperty("phone",    out var ph) ? ph.GetString() : (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@CO", body.TryGetProperty("company",  out var co) ? co.GetString() : (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@PO", body.TryGetProperty("position", out var po) ? po.GetString() : (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@S",  body.TryGetProperty("source",   out var s)  ? s.GetString()  : (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@NO", body.TryGetProperty("notes",    out var no) ? no.GetString() : (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@ID", id);
            await cmd.ExecuteNonQueryAsync();

            // Reemplazar tags
            if (body.TryGetProperty("tagIds", out var tagIds))
            {
                using var delCmd = new MySqlCommand("DELETE FROM crm_prospect_has_tag WHERE prospect_id=@P", conn, tx);
                delCmd.Parameters.AddWithValue("@P", id);
                await delCmd.ExecuteNonQueryAsync();
                foreach (var tag in tagIds.EnumerateArray())
                {
                    using var tagCmd = new MySqlCommand(
                        "INSERT IGNORE INTO crm_prospect_has_tag (prospect_id, tag_id) VALUES (@P,@T)", conn, tx);
                    tagCmd.Parameters.AddWithValue("@P", id);
                    tagCmd.Parameters.AddWithValue("@T", tag.GetInt32());
                    await tagCmd.ExecuteNonQueryAsync();
                }
            }

            await tx.CommitAsync();

            // Devolver el prospecto actualizado completo
            using var selCmd = new MySqlCommand(@"
                SELECT p.id, p.business_id, p.name, p.email, p.phone, p.company, p.position,
                       p.source, p.notes, p.created_at,
                       GROUP_CONCAT(DISTINCT JSON_OBJECT('id',t.id,'name',t.name,'color',t.color)) AS tags_json
                FROM prospect p
                LEFT JOIN crm_prospect_has_tag pht ON pht.prospect_id = p.id
                LEFT JOIN crm_prospect_tag t        ON t.id = pht.tag_id
                WHERE p.id = @ID
                GROUP BY p.id", conn);
            selCmd.Parameters.AddWithValue("@ID", id);
            using var sr = await selCmd.ExecuteReaderAsync();
            await sr.ReadAsync();
            var tagsRaw = IsNull(sr, "tags_json") ? "[]" : sr.GetString("tags_json");
            var updated = new {
                id         = sr.GetInt32("id"),
                businessId = sr.GetInt32("business_id"),
                name       = sr.GetString("name"),
                email      = IsNull(sr, "email")    ? null : sr.GetString("email"),
                phone      = IsNull(sr, "phone")    ? null : sr.GetString("phone"),
                company    = IsNull(sr, "company")  ? null : sr.GetString("company"),
                position   = IsNull(sr, "position") ? null : sr.GetString("position"),
                source     = IsNull(sr, "source")   ? null : sr.GetString("source"),
                notes      = IsNull(sr, "notes")    ? null : sr.GetString("notes"),
                createdAt  = sr.GetDateTime("created_at"),
                tags       = ParseTagsJson(tagsRaw),
            };

            await BroadcastSafe(updated.businessId, "ProspectsChanged");
            return Ok(new { success = true, data = updated });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error UpdateCrmProspect: {M}", ex.Message);
            return StatusCode(500, new { success = false, message = ex.Message });
        }
    }

    /// DELETE /api/crm/prospects/{id}
    [HttpDelete("prospects/{id}")]
    public async Task<IActionResult> DeleteCrmProspect(int id)
    {
        try
        {
            using var conn = GetConnection();
            await conn.OpenAsync();
            using var cmd = new MySqlCommand("DELETE FROM prospect WHERE id=@ID", conn);
            cmd.Parameters.AddWithValue("@ID", id);
            var prosBiz = await FetchBusinessId("prospect", id);
            await cmd.ExecuteNonQueryAsync();
            await BroadcastSafe(prosBiz, "ProspectsChanged");
            return Ok(new { success = true });
        }
        catch (Exception ex) { return StatusCode(500, new { success = false, message = ex.Message }); }
    }

    // ================================================================
    // DEALS
    // ================================================================

    /// GET /api/crm/deals?businessId=&stageId=&status=
    [HttpGet("deals")]
    public async Task<IActionResult> GetDeals([FromQuery] int businessId,
        [FromQuery] int? stageId, [FromQuery] string? status)
    {
        try
        {
            using var conn = GetConnection();
            await conn.OpenAsync();
            using var cmd = new MySqlCommand(@"
                SELECT d.id, d.business_id, d.prospect_id, d.stage_id, d.title,
                       d.amount, d.currency, d.owner_user_id, d.expected_close_date,
                       d.notes, d.status, d.won_at, d.lost_at, d.created_at, d.updated_at,
                       d.client_id,
                       p.name AS prospect_name, p.company AS prospect_company,
                       s.name AS stage_name, s.color AS stage_color
                FROM crm_deal d
                LEFT JOIN prospect  p ON p.id = d.prospect_id
                INNER JOIN crm_stage s ON s.id = d.stage_id
                WHERE d.business_id = @B
                  AND (@ST IS NULL OR d.stage_id = @ST)
                  AND (@SS IS NULL OR d.status   = @SS)
                ORDER BY d.created_at DESC", conn);
            cmd.Parameters.AddWithValue("@B",  businessId);
            cmd.Parameters.AddWithValue("@ST", stageId.HasValue ? stageId.Value : (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@SS", status ?? (object)DBNull.Value);

            var list = new List<object>();
            using var r = await cmd.ExecuteReaderAsync();
            while (await r.ReadAsync())
                list.Add(MapDeal(r));

            return Ok(new { success = true, data = list });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error GetDeals: {M}", ex.Message);
            return StatusCode(500, new { success = false, message = ex.Message });
        }
    }

    /// GET /api/crm/closed-deals?businessId=&pipelineId=&from=&to=
    [HttpGet("closed-deals")]
    public async Task<IActionResult> GetClosedDeals(
        [FromQuery] int businessId,
        [FromQuery] int? pipelineId,
        [FromQuery] string? from,
        [FromQuery] string? to)
    {
        try
        {
            using var conn = GetConnection();
            await conn.OpenAsync();

            DateTime? fromDate = from != null ? DateTime.Parse(from) : null;
            DateTime? toDate   = to   != null ? DateTime.Parse(to).AddDays(1)   : null;

            var sql = @"
                SELECT d.id, d.business_id, d.prospect_id, d.stage_id, d.title,
                       d.amount, d.currency, d.owner_user_id, d.expected_close_date,
                       d.notes, d.status, d.won_at, d.lost_at, d.created_at, d.updated_at,
                       d.client_id,
                       p.name    AS prospect_name,   p.company AS prospect_company,
                       s.name    AS stage_name,      s.color   AS stage_color,
                       s.is_won  AS stage_is_won,    s.is_lost AS stage_is_lost,
                       s.pipeline_id
                FROM crm_deal d
                LEFT JOIN prospect   p ON p.id = d.prospect_id
                INNER JOIN crm_stage s ON s.id = d.stage_id
                WHERE d.business_id = @B
                  AND d.status IN ('won','lost')
                  AND (@PID IS NULL OR s.pipeline_id = @PID)
                  AND (@FROM IS NULL OR COALESCE(d.won_at, d.lost_at, d.updated_at) >= @FROM)
                  AND (@TO   IS NULL OR COALESCE(d.won_at, d.lost_at, d.updated_at) <  @TO)
                ORDER BY COALESCE(d.won_at, d.lost_at, d.updated_at) DESC";

            using var cmd = new MySqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@B",    businessId);
            cmd.Parameters.AddWithValue("@PID",  pipelineId.HasValue ? pipelineId.Value : (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@FROM", fromDate.HasValue ? fromDate.Value : (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@TO",   toDate.HasValue   ? toDate.Value   : (object)DBNull.Value);

            var deals = new List<object>();
            int wonCount = 0, lostCount = 0;
            decimal totalRevenue = 0;
            double totalDaysToClose = 0;
            int dealsWithDays = 0;

            using var r = await cmd.ExecuteReaderAsync();
            while (await r.ReadAsync())
            {
                var status    = r.GetString("status");
                var amount    = IsNull2(r, "amount") ? (decimal?)null : r.GetDecimal("amount");
                var createdAt = r.GetDateTime("created_at");
                DateTime? closedAt = status == "won"
                    ? (IsNull2(r, "won_at")  ? (DateTime?)null : r.GetDateTime("won_at"))
                    : (IsNull2(r, "lost_at") ? (DateTime?)null : r.GetDateTime("lost_at"));

                if (status == "won") { wonCount++; if (amount.HasValue) totalRevenue += amount.Value; }
                else                   lostCount++;

                if (closedAt.HasValue) { totalDaysToClose += (closedAt.Value - createdAt).TotalDays; dealsWithDays++; }

                deals.Add(new {
                    id                = r.GetInt32("id"),
                    businessId        = r.GetInt32("business_id"),
                    prospectId        = r.GetInt32("prospect_id"),
                    stageId           = r.GetInt32("stage_id"),
                    title             = r.GetString("title"),
                    amount,
                    currency          = r.GetString("currency"),
                    status,
                    wonAt             = IsNull2(r, "won_at")              ? (DateTime?)null : r.GetDateTime("won_at"),
                    lostAt            = IsNull2(r, "lost_at")             ? (DateTime?)null : r.GetDateTime("lost_at"),
                    clientId          = IsNull2(r, "client_id")           ? (int?)null      : r.GetInt32("client_id"),
                    createdAt,
                    updatedAt         = r.GetDateTime("updated_at"),
                    prospectName      = IsNull2(r, "prospect_name")    ? null : r.GetString("prospect_name"),
                    prospectCompany   = IsNull2(r, "prospect_company") ? null : r.GetString("prospect_company"),
                    stageName         = r.GetString("stage_name"),
                    stageColor        = r.GetString("stage_color"),
                    stageIsWon        = r.GetBoolean("stage_is_won"),
                    stageIsLost       = r.GetBoolean("stage_is_lost"),
                    pipelineId        = r.GetInt32("pipeline_id"),
                    daysToClose       = closedAt.HasValue ? (int?)(int)(closedAt.Value - createdAt).TotalDays : null,
                });
            }

            int total      = wonCount + lostCount;
            double winRate = total > 0 ? Math.Round((double)wonCount / total * 100, 1) : 0;
            double avgDays = dealsWithDays > 0 ? Math.Round(totalDaysToClose / dealsWithDays, 1) : 0;

            return Ok(new {
                success = true,
                data = new {
                    kpis = new { wonCount, lostCount, total, winRate, totalRevenue, avgDaysToClose = avgDays },
                    deals,
                }
            });
        }
        catch (Exception ex) { return StatusCode(500, new { success = false, message = ex.Message }); }
    }

    /// GET /api/crm/deals/{id}
    [HttpGet("deals/{id}")]
    public async Task<IActionResult> GetDeal(int id)
    {
        try
        {
            using var conn = GetConnection();
            await conn.OpenAsync();

            // Deal base
            using var cmd = new MySqlCommand(@"
                SELECT d.id, d.business_id, d.prospect_id, d.stage_id, d.title,
                       d.amount, d.currency, d.owner_user_id, d.expected_close_date,
                       d.notes, d.status, d.won_at, d.lost_at, d.created_at, d.updated_at,
                       d.client_id,
                       p.name AS prospect_name, p.company AS prospect_company,
                       s.name AS stage_name, s.color AS stage_color
                FROM crm_deal d
                LEFT JOIN prospect  p ON p.id = d.prospect_id
                INNER JOIN crm_stage s ON s.id = d.stage_id
                WHERE d.id = @ID", conn);
            cmd.Parameters.AddWithValue("@ID", id);
            using var r = await cmd.ExecuteReaderAsync();
            if (!await r.ReadAsync()) return NotFound(new { success = false, message = "Deal no encontrado" });
            var deal = MapDeal(r);
            await r.CloseAsync();

            // Stage history
            using var hCmd = new MySqlCommand(@"
                SELECT id, stage_id, stage_name, entered_at, exited_at
                FROM crm_deal_stage_history
                WHERE deal_id = @ID ORDER BY entered_at ASC", conn);
            hCmd.Parameters.AddWithValue("@ID", id);
            var history = new List<object>();
            using var hr = await hCmd.ExecuteReaderAsync();
            while (await hr.ReadAsync())
                history.Add(new {
                    id        = hr.GetInt32("id"),
                    stageId   = hr.GetInt32("stage_id"),
                    stageName = hr.GetString("stage_name"),
                    enteredAt = hr.GetDateTime("entered_at"),
                    exitedAt  = IsNull(hr, "exited_at") ? (DateTime?)null : hr.GetDateTime("exited_at"),
                    daysInStage = IsNull(hr, "exited_at")
                        ? (DateTime.UtcNow - hr.GetDateTime("entered_at")).TotalDays
                        : (hr.GetDateTime("exited_at") - hr.GetDateTime("entered_at")).TotalDays,
                });

            return Ok(new { success = true, data = new { deal, stageHistory = history } });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error GetDeal: {M}", ex.Message);
            return StatusCode(500, new { success = false, message = ex.Message });
        }
    }

    /// POST /api/crm/deals
    [HttpPost("deals")]
    public async Task<IActionResult> CreateDeal([FromBody] JsonElement body)
    {
        try
        {
            using var conn = GetConnection();
            await conn.OpenAsync();
            using var tx = await conn.BeginTransactionAsync();

            var stageId    = body.GetProperty("stageId").GetInt32();
            var prospectId = body.TryGetProperty("prospectId", out var pid) ? pid.GetInt32() : (int?)null;

            // Obtener nombre del stage para historia
            using var sCmd = new MySqlCommand("SELECT name FROM crm_stage WHERE id=@S", conn, tx);
            sCmd.Parameters.AddWithValue("@S", stageId);
            var stageName = (string)(await sCmd.ExecuteScalarAsync() ?? "");

            using var cmd = new MySqlCommand(@"
                INSERT INTO crm_deal (business_id, prospect_id, stage_id, title, amount, currency,
                                      owner_user_id, expected_close_date, notes)
                VALUES (@B,@PR,@S,@T,@A,@CU,@O,@EC,@NO);
                SELECT LAST_INSERT_ID();", conn, tx);
            cmd.Parameters.AddWithValue("@B",  body.GetProperty("businessId").GetInt32());
            cmd.Parameters.AddWithValue("@PR", prospectId.HasValue ? prospectId.Value : (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@S",  stageId);
            cmd.Parameters.AddWithValue("@T",  body.GetProperty("title").GetString()!);
            cmd.Parameters.AddWithValue("@A",  body.TryGetProperty("amount",            out var a)  ? a.GetDecimal()  : (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@CU", body.TryGetProperty("currency",          out var cu) ? cu.GetString()! : "CLP");
            cmd.Parameters.AddWithValue("@O",  body.TryGetProperty("ownerUserId",       out var o)  ? o.GetInt32()    : (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@EC", body.TryGetProperty("expectedCloseDate", out var ec) ? ec.GetString()! : (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@NO", body.TryGetProperty("notes",             out var no) ? no.GetString()  : (object)DBNull.Value);
            var id = Convert.ToInt32(await cmd.ExecuteScalarAsync());

            // Registrar entrada en historial
            using var hCmd = new MySqlCommand(@"
                INSERT INTO crm_deal_stage_history (deal_id, stage_id, stage_name)
                VALUES (@D, @S, @SN)", conn, tx);
            hCmd.Parameters.AddWithValue("@D",  id);
            hCmd.Parameters.AddWithValue("@S",  stageId);
            hCmd.Parameters.AddWithValue("@SN", stageName);
            await hCmd.ExecuteNonQueryAsync();

            // Incrementar deal_count del stage
            using var dcCmd = new MySqlCommand(
                "UPDATE crm_stage SET deal_count=deal_count+1 WHERE id=@S", conn, tx);
            dcCmd.Parameters.AddWithValue("@S", stageId);
            await dcCmd.ExecuteNonQueryAsync();

            await tx.CommitAsync();
            await BroadcastSafe(body.TryGetProperty("businessId", out var biz) ? biz.GetInt32() : (int?)null, "DealsChanged");
            return Ok(new { success = true, data = new { id } });
        }
        catch (MySqlException my)
        {
            _logger.LogError(my, "Error CreateDeal MySQL [{C}]: {M}", my.ErrorCode, my.Message);
            var msg = my.ErrorCode switch {
                MySqlErrorCode.ColumnCannotBeNull   => "El campo requerido no puede estar vacío.",
                MySqlErrorCode.NoReferencedRow      => "El prospecto, etapa o propietario indicado no existe.",
                MySqlErrorCode.NoReferencedRow2     => "El prospecto, etapa o propietario indicado no existe.",
                MySqlErrorCode.DuplicateKeyEntry    => "Ya existe un registro con esos datos.",
                _                                   => "Error de base de datos al crear el deal.",
            };
            return BadRequest(new { success = false, message = msg });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error CreateDeal: {M}", ex.Message);
            return StatusCode(500, new { success = false, message = "Error inesperado al crear el deal." });
        }
    }

    /// PUT /api/crm/deals/{id}
    [HttpPut("deals/{id}")]
    public async Task<IActionResult> UpdateDeal(int id, [FromBody] JsonElement body)
    {
        try
        {
            using var conn = GetConnection();
            await conn.OpenAsync();
            using var cmd = new MySqlCommand(@"
                UPDATE crm_deal SET
                    title               = COALESCE(@T,  title),
                    amount              = COALESCE(@A,  amount),
                    currency            = COALESCE(@CU, currency),
                    owner_user_id       = COALESCE(@O,  owner_user_id),
                    expected_close_date = COALESCE(@EC, expected_close_date),
                    notes               = COALESCE(@NO, notes)
                WHERE id = @ID", conn);
            cmd.Parameters.AddWithValue("@T",  body.TryGetProperty("title",             out var t)  ? t.GetString()   : (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@A",  body.TryGetProperty("amount",            out var a)  ? a.GetDecimal()  : (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@CU", body.TryGetProperty("currency",          out var cu) ? cu.GetString()! : (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@O",  body.TryGetProperty("ownerUserId",       out var o)  ? o.GetInt32()    : (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@EC", body.TryGetProperty("expectedCloseDate", out var ec) ? ec.GetString()! : (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@NO", body.TryGetProperty("notes",             out var no) ? no.GetString()  : (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@ID", id);
            await cmd.ExecuteNonQueryAsync();
            await BroadcastSafe(await FetchBusinessId("crm_deal", id), "DealsChanged");
            return Ok(new { success = true });
        }
        catch (Exception ex) { return StatusCode(500, new { success = false, message = ex.Message }); }
    }

    /// POST /api/crm/deals/{id}/move  — body: { stageId }
    [HttpPost("deals/{id}/move")]
    public async Task<IActionResult> MoveDeal(int id, [FromBody] JsonElement body)
    {
        try
        {
            var newStageId = body.GetProperty("stageId").GetInt32();
            using var conn = GetConnection();
            await conn.OpenAsync();
            using var tx = await conn.BeginTransactionAsync();

            // Stage actual
            using var curCmd = new MySqlCommand("SELECT stage_id FROM crm_deal WHERE id=@ID", conn, tx);
            curCmd.Parameters.AddWithValue("@ID", id);
            var oldStageId = Convert.ToInt32(await curCmd.ExecuteScalarAsync());

            if (oldStageId == newStageId) { await tx.CommitAsync(); return Ok(new { success = true }); }

            // Nombre del nuevo stage
            using var sCmd = new MySqlCommand("SELECT name FROM crm_stage WHERE id=@S", conn, tx);
            sCmd.Parameters.AddWithValue("@S", newStageId);
            var newStageName = (string)(await sCmd.ExecuteScalarAsync() ?? "");

            // Cerrar entrada actual en historial
            using var exitCmd = new MySqlCommand(@"
                UPDATE crm_deal_stage_history
                SET exited_at = NOW()
                WHERE deal_id=@D AND stage_id=@OS AND exited_at IS NULL", conn, tx);
            exitCmd.Parameters.AddWithValue("@D",  id);
            exitCmd.Parameters.AddWithValue("@OS", oldStageId);
            await exitCmd.ExecuteNonQueryAsync();

            // Crear nueva entrada en historial
            using var hCmd = new MySqlCommand(@"
                INSERT INTO crm_deal_stage_history (deal_id, stage_id, stage_name)
                VALUES (@D,@S,@SN)", conn, tx);
            hCmd.Parameters.AddWithValue("@D",  id);
            hCmd.Parameters.AddWithValue("@S",  newStageId);
            hCmd.Parameters.AddWithValue("@SN", newStageName);
            await hCmd.ExecuteNonQueryAsync();

            // Mover deal y actualizar deal_count
            using var mvCmd = new MySqlCommand("UPDATE crm_deal SET stage_id=@S WHERE id=@ID", conn, tx);
            mvCmd.Parameters.AddWithValue("@S",  newStageId);
            mvCmd.Parameters.AddWithValue("@ID", id);
            await mvCmd.ExecuteNonQueryAsync();

            using var decCmd = new MySqlCommand("UPDATE crm_stage SET deal_count=GREATEST(deal_count-1,0) WHERE id=@OS", conn, tx);
            decCmd.Parameters.AddWithValue("@OS", oldStageId);
            await decCmd.ExecuteNonQueryAsync();

            using var incCmd = new MySqlCommand("UPDATE crm_stage SET deal_count=deal_count+1 WHERE id=@S", conn, tx);
            incCmd.Parameters.AddWithValue("@S", newStageId);
            await incCmd.ExecuteNonQueryAsync();

            await tx.CommitAsync();
            await BroadcastSafe(await FetchBusinessId("crm_deal", id), "DealsChanged");
            return Ok(new { success = true });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error MoveDeal: {M}", ex.Message);
            return StatusCode(500, new { success = false, message = ex.Message });
        }
    }

    /// POST /api/crm/deals/{id}/won
    [HttpPost("deals/{id}/won")]
    public async Task<IActionResult> MarkWon(int id)
    {
        try
        {
            using var conn = GetConnection();
            await conn.OpenAsync();
            using var cmd = new MySqlCommand(@"
                UPDATE crm_deal SET status='won', won_at=NOW() WHERE id=@ID;
                UPDATE crm_deal_stage_history SET exited_at=NOW()
                    WHERE deal_id=@ID AND exited_at IS NULL;", conn);
            cmd.Parameters.AddWithValue("@ID", id);
            await cmd.ExecuteNonQueryAsync();
            await BroadcastSafe(await FetchBusinessId("crm_deal", id), "DealsChanged");
            return Ok(new { success = true });
        }
        catch (Exception ex) { return StatusCode(500, new { success = false, message = ex.Message }); }
    }

    /// POST /api/crm/deals/{id}/lost
    [HttpPost("deals/{id}/lost")]
    public async Task<IActionResult> MarkLost(int id)
    {
        try
        {
            using var conn = GetConnection();
            await conn.OpenAsync();
            using var cmd = new MySqlCommand(@"
                UPDATE crm_deal SET status='lost', lost_at=NOW() WHERE id=@ID;
                UPDATE crm_deal_stage_history SET exited_at=NOW()
                    WHERE deal_id=@ID AND exited_at IS NULL;", conn);
            cmd.Parameters.AddWithValue("@ID", id);
            await cmd.ExecuteNonQueryAsync();
            await BroadcastSafe(await FetchBusinessId("crm_deal", id), "DealsChanged");
            return Ok(new { success = true });
        }
        catch (Exception ex) { return StatusCode(500, new { success = false, message = ex.Message }); }
    }

    /// DELETE /api/crm/deals/{id}
    [HttpDelete("deals/{id}")]
    public async Task<IActionResult> DeleteDeal(int id)
    {
        try
        {
            using var conn = GetConnection();
            await conn.OpenAsync();
            // Decrementar deal_count antes de borrar
            using var dcCmd = new MySqlCommand(@"
                UPDATE crm_stage s
                INNER JOIN crm_deal d ON d.stage_id=s.id
                SET s.deal_count=GREATEST(s.deal_count-1,0)
                WHERE d.id=@ID", conn);
            dcCmd.Parameters.AddWithValue("@ID", id);
            await dcCmd.ExecuteNonQueryAsync();

            using var cmd = new MySqlCommand("DELETE FROM crm_deal WHERE id=@ID", conn);
            cmd.Parameters.AddWithValue("@ID", id);
            var dealBiz = await FetchBusinessId("crm_deal", id);
            await cmd.ExecuteNonQueryAsync();
            await BroadcastSafe(dealBiz, "DealsChanged");
            return Ok(new { success = true });
        }
        catch (Exception ex) { return StatusCode(500, new { success = false, message = ex.Message }); }
    }

    // ================================================================
    // CLIENT PROMOTION
    // ================================================================

    /// GET /api/crm/relationship-types
    [HttpGet("relationship-types")]
    public async Task<IActionResult> GetRelationshipTypes()
    {
        try
        {
            using var conn = GetConnection();
            await conn.OpenAsync();
            using var cmd = new MySqlCommand(
                "SELECT id, code, label FROM service_client_relationship_type ORDER BY sort_order, label",
                conn);
            var list = new List<object>();
            using var r = await cmd.ExecuteReaderAsync();
            while (await r.ReadAsync())
                list.Add(new {
                    id    = r.GetInt32("id"),
                    code  = r.GetString("code"),
                    label = r.GetString("label"),
                });
            return Ok(new { success = true, data = list });
        }
        catch (Exception ex) { return StatusCode(500, new { success = false, message = ex.Message }); }
    }

    /// GET /api/crm/clients?businessId=&search=
    [HttpGet("clients")]
    public async Task<IActionResult> GetClients([FromQuery] int businessId, [FromQuery] string? search)
    {
        try
        {
            using var conn = GetConnection();
            await conn.OpenAsync();
            var sql = @"
                SELECT sc.id, sc.business_id, sc.name, sc.rut, sc.email, sc.phone,
                       sc.address, sc.city, sc.contact_person, sc.client_type,
                       sc.segment, sc.active, sc.created_at,
                       sc.parent_client_id,
                       rt.label AS relationship_name
                FROM service_client sc
                LEFT JOIN service_client_relationship_type rt
                       ON rt.id = sc.relationship_type_id
                WHERE sc.business_id = @B AND sc.active = 1
                  AND (@S IS NULL OR sc.name LIKE @SL OR sc.rut LIKE @SL OR sc.email LIKE @SL)
                ORDER BY COALESCE(sc.parent_client_id, sc.id), sc.parent_client_id IS NULL DESC, sc.name";
            using var cmd = new MySqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@B",  businessId);
            cmd.Parameters.AddWithValue("@S",  string.IsNullOrEmpty(search) ? (object)DBNull.Value : search);
            cmd.Parameters.AddWithValue("@SL", string.IsNullOrEmpty(search) ? (object)DBNull.Value : $"%{search}%");
            var list = new List<object>();
            using var r = await cmd.ExecuteReaderAsync();
            while (await r.ReadAsync())
                list.Add(new {
                    id               = r.GetInt32("id"),
                    businessId       = r.GetInt32("business_id"),
                    name             = r.GetString("name"),
                    rut              = IsNull2(r, "rut")               ? null : r.GetString("rut"),
                    email            = IsNull2(r, "email")             ? null : r.GetString("email"),
                    phone            = IsNull2(r, "phone")             ? null : r.GetString("phone"),
                    address          = IsNull2(r, "address")           ? null : r.GetString("address"),
                    city             = IsNull2(r, "city")              ? null : r.GetString("city"),
                    contactPerson    = IsNull2(r, "contact_person")    ? null : r.GetString("contact_person"),
                    clientType       = r.GetInt32("client_type") == 1 ? "company" : "individual",
                    segment          = IsNull2(r, "segment")           ? null : r.GetString("segment"),
                    active           = r.GetBoolean("active"),
                    createdAt        = r.GetDateTime("created_at"),
                    parentClientId   = IsNull2(r, "parent_client_id")  ? (int?)null : r.GetInt32("parent_client_id"),
                    relationshipName = IsNull2(r, "relationship_name") ? null : r.GetString("relationship_name"),
                });
            return Ok(new { success = true, data = list });
        }
        catch (Exception ex) { return StatusCode(500, new { success = false, message = ex.Message }); }
    }

    /// POST /api/crm/deals/{id}/promote-client
    [HttpPost("deals/{id}/promote-client")]
    public async Task<IActionResult> PromoteToClient(int id, [FromBody] JsonElement body)
    {
        try
        {
            using var conn = GetConnection();
            await conn.OpenAsync();

            // Fetch deal to get businessId and prospectId
            using var dealCmd = new MySqlCommand(
                "SELECT business_id, prospect_id, client_id FROM crm_deal WHERE id = @ID", conn);
            dealCmd.Parameters.AddWithValue("@ID", id);
            using var dr = await dealCmd.ExecuteReaderAsync();
            if (!await dr.ReadAsync()) return NotFound(new { success = false, message = "Deal not found" });
            var bizId      = dr.GetInt32("business_id");
            var prospectId = dr.GetInt32("prospect_id");
            var existingClientId = IsNull2(dr, "client_id") ? (int?)null : dr.GetInt32("client_id");
            await dr.CloseAsync();

            // Extract fields from body
            var name          = body.TryGetProperty("name",          out var fn)  ? fn.GetString()  : null;
            var clientTypeStr = body.TryGetProperty("clientType",    out var ft)  ? ft.GetString()  : "individual";
            var clientTypeInt = clientTypeStr == "company" ? 1 : 0;
            var rut           = body.TryGetProperty("rut",           out var fru) ? fru.GetString() : null;
            var email         = body.TryGetProperty("email",         out var fe)  ? fe.GetString()  : null;
            var phone         = body.TryGetProperty("phone",         out var fph) ? fph.GetString() : null;
            var address       = body.TryGetProperty("address",       out var fa)  ? fa.GetString()  : null;
            var city          = body.TryGetProperty("city",          out var fci) ? fci.GetString() : null;
            var contactPerson = body.TryGetProperty("contactPerson", out var fco) ? fco.GetString() : null;
            var segment       = body.TryGetProperty("segment",       out var fs)  ? fs.GetString()  : null;
            var notes         = body.TryGetProperty("notes",         out var fno) ? fno.GetString() : null;

            int clientId;
            if (existingClientId.HasValue)
            {
                // Update existing client
                clientId = existingClientId.Value;
                using var upCmd = new MySqlCommand(@"
                    UPDATE service_client SET
                        name = @N, client_type = @CT, rut = @RU, email = @E, phone = @PH,
                        address = @A, city = @CI, contact_person = @CO, segment = @SE, notes = @NO
                    WHERE id = @CID", conn);
                upCmd.Parameters.AddWithValue("@N",   name);
                upCmd.Parameters.AddWithValue("@CT",  clientTypeInt);
                upCmd.Parameters.AddWithValue("@RU",  (object?)rut           ?? DBNull.Value);
                upCmd.Parameters.AddWithValue("@E",   (object?)email         ?? DBNull.Value);
                upCmd.Parameters.AddWithValue("@PH",  (object?)phone         ?? DBNull.Value);
                upCmd.Parameters.AddWithValue("@A",   (object?)address       ?? DBNull.Value);
                upCmd.Parameters.AddWithValue("@CI",  (object?)city          ?? DBNull.Value);
                upCmd.Parameters.AddWithValue("@CO",  (object?)contactPerson ?? DBNull.Value);
                upCmd.Parameters.AddWithValue("@SE",  (object?)segment       ?? DBNull.Value);
                upCmd.Parameters.AddWithValue("@NO",  (object?)notes         ?? DBNull.Value);
                upCmd.Parameters.AddWithValue("@CID", clientId);
                await upCmd.ExecuteNonQueryAsync();
            }
            else
            {
                // Insert new service_client
                using var insCmd = new MySqlCommand(@"
                    INSERT INTO service_client
                        (business_id, name, client_type, rut, email, phone, address, city, contact_person, segment, notes, active)
                    VALUES
                        (@B, @N, @CT, @RU, @E, @PH, @A, @CI, @CO, @SE, @NO, 1);
                    SELECT LAST_INSERT_ID();", conn);
                insCmd.Parameters.AddWithValue("@B",  bizId);
                insCmd.Parameters.AddWithValue("@N",  name);
                insCmd.Parameters.AddWithValue("@CT", clientTypeInt);
                insCmd.Parameters.AddWithValue("@RU", (object?)rut           ?? DBNull.Value);
                insCmd.Parameters.AddWithValue("@E",  (object?)email         ?? DBNull.Value);
                insCmd.Parameters.AddWithValue("@PH", (object?)phone         ?? DBNull.Value);
                insCmd.Parameters.AddWithValue("@A",  (object?)address       ?? DBNull.Value);
                insCmd.Parameters.AddWithValue("@CI", (object?)city          ?? DBNull.Value);
                insCmd.Parameters.AddWithValue("@CO", (object?)contactPerson ?? DBNull.Value);
                insCmd.Parameters.AddWithValue("@SE", (object?)segment       ?? DBNull.Value);
                insCmd.Parameters.AddWithValue("@NO", (object?)notes         ?? DBNull.Value);
                clientId = Convert.ToInt32(await insCmd.ExecuteScalarAsync());
            }

            // Link client to deal and prospect
            using var linkCmd = new MySqlCommand(@"
                UPDATE crm_deal SET client_id = @CID WHERE id = @DID;
                UPDATE prospect  SET promoted_client_id = @CID WHERE id = @PID;", conn);
            linkCmd.Parameters.AddWithValue("@CID", clientId);
            linkCmd.Parameters.AddWithValue("@DID", id);
            linkCmd.Parameters.AddWithValue("@PID", prospectId);
            await linkCmd.ExecuteNonQueryAsync();

            // Insert beneficiaries (sub-clients)
            var beneficiariesCreated = 0;
            if (body.TryGetProperty("beneficiaries", out var bens) && bens.ValueKind == JsonValueKind.Array)
            {
                foreach (var ben in bens.EnumerateArray())
                {
                    var benName       = ben.TryGetProperty("name",               out var bn)  ? bn.GetString()  : null;
                    if (string.IsNullOrWhiteSpace(benName)) continue;
                    var benRut        = ben.TryGetProperty("rut",                out var br)  ? br.GetString()  : null;
                    var benEmail      = ben.TryGetProperty("email",              out var be)  ? be.GetString()  : null;
                    var benPhone      = ben.TryGetProperty("phone",              out var bp)  ? bp.GetString()  : null;
                    var benBirthDate  = ben.TryGetProperty("birthDate",          out var bd)  ? bd.GetString()  : null;
                    var benRelTypeId  = ben.TryGetProperty("relationshipTypeId", out var brt) && brt.ValueKind == JsonValueKind.Number
                                          ? brt.GetInt32() : (int?)null;

                    using var benCmd = new MySqlCommand(@"
                        INSERT INTO service_client
                            (business_id, parent_client_id, relationship_type_id, name, rut, email, phone, birth_date,
                             client_type, active)
                        VALUES
                            (@B, @PID, @RTID, @N, @RU, @E, @PH, @BD, 0, 1);", conn);
                    benCmd.Parameters.AddWithValue("@B",    bizId);
                    benCmd.Parameters.AddWithValue("@PID",  clientId);
                    benCmd.Parameters.AddWithValue("@RTID", (object?)benRelTypeId  ?? DBNull.Value);
                    benCmd.Parameters.AddWithValue("@N",    benName);
                    benCmd.Parameters.AddWithValue("@RU",   (object?)benRut        ?? DBNull.Value);
                    benCmd.Parameters.AddWithValue("@E",    (object?)benEmail      ?? DBNull.Value);
                    benCmd.Parameters.AddWithValue("@PH",   (object?)benPhone      ?? DBNull.Value);
                    benCmd.Parameters.AddWithValue("@BD",   (object?)benBirthDate  ?? DBNull.Value);
                    await benCmd.ExecuteNonQueryAsync();
                    beneficiariesCreated++;
                }
            }

            await BroadcastSafe(bizId, "DealsChanged");

            return Ok(new { success = true, data = new { clientId, name, clientType = clientTypeStr, email, phone, beneficiariesCreated } });
        }
        catch (Exception ex) { return StatusCode(500, new { success = false, message = ex.Message }); }
    }

    // ================================================================
    // ACTIVITIES
    // ================================================================

    /// GET /api/crm/activities?dealId=
    [HttpGet("activities")]
    public async Task<IActionResult> GetActivities([FromQuery] int dealId)
    {
        try
        {
            using var conn = GetConnection();
            await conn.OpenAsync();
            using var cmd = new MySqlCommand(@"
                SELECT a.*, u.name AS user_name
                FROM crm_activity a
                LEFT JOIN user u ON u.id = a.user_id
                WHERE a.deal_id = @D
                ORDER BY a.created_at DESC", conn);
            cmd.Parameters.AddWithValue("@D", dealId);
            var list = new List<object>();
            using var r = await cmd.ExecuteReaderAsync();
            while (await r.ReadAsync()) list.Add(MapActivity(r));
            return Ok(new { success = true, data = list });
        }
        catch (Exception ex) { return StatusCode(500, new { success = false, message = ex.Message }); }
    }

    /// GET /api/crm/activities/pending?businessId=
    [HttpGet("activities/pending")]
    public async Task<IActionResult> GetPendingActivities([FromQuery] int businessId)
    {
        try
        {
            using var conn = GetConnection();
            await conn.OpenAsync();
            using var cmd = new MySqlCommand(@"
                SELECT a.*, u.name AS user_name
                FROM crm_activity a
                LEFT JOIN user u ON u.id = a.user_id
                WHERE a.business_id = @B AND a.done = 0
                ORDER BY a.due_date ASC, a.created_at DESC", conn);
            cmd.Parameters.AddWithValue("@B", businessId);
            var list = new List<object>();
            using var r = await cmd.ExecuteReaderAsync();
            while (await r.ReadAsync()) list.Add(MapActivity(r));
            return Ok(new { success = true, data = list });
        }
        catch (Exception ex) { return StatusCode(500, new { success = false, message = ex.Message }); }
    }

    /// POST /api/crm/activities
    [HttpPost("activities")]
    public async Task<IActionResult> CreateActivity([FromBody] JsonElement body)
    {
        try
        {
            var userId = GetCurrentUserId();
            using var conn = GetConnection();
            await conn.OpenAsync();
            using var cmd = new MySqlCommand(@"
                INSERT INTO crm_activity
                    (business_id, deal_id, prospect_id, user_id, type, subject, body,
                     due_date, email_from, email_to, email_cc, email_status,
                     call_direction, call_outcome, duration_min)
                VALUES (@B,@D,@PR,@U,@TY,@SU,@BO,
                        @DD,@EF,@ET,@EC,@ES,
                        @CD,@CO,@DU);
                SELECT LAST_INSERT_ID();", conn);
            cmd.Parameters.AddWithValue("@B",  body.TryGetProperty("businessId", out var biz) ? biz.GetInt32() : (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@D",  body.TryGetProperty("dealId",      out var d)  ? d.GetInt32()   : (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@PR", body.TryGetProperty("prospectId",  out var pr) ? pr.GetInt32()  : (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@U",  userId.HasValue ? userId.Value : (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@TY", body.GetProperty("type").GetString()!);
            cmd.Parameters.AddWithValue("@SU", body.TryGetProperty("subject",       out var su) ? su.GetString() : (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@BO", body.TryGetProperty("body",          out var bo) ? bo.GetString() : (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@DD", body.TryGetProperty("dueDate",       out var dd) ? dd.GetString() : (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@EF", body.TryGetProperty("emailFrom",     out var ef) ? ef.GetString() : (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@ET", body.TryGetProperty("emailTo",       out var et) ? et.GetString() : (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@EC", body.TryGetProperty("emailCc",       out var ec) ? ec.GetString() : (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@ES", body.TryGetProperty("emailStatus",   out var es) ? es.GetString() : (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@CD", body.TryGetProperty("callDirection", out var cd) ? cd.GetString() : (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@CO", body.TryGetProperty("callOutcome",   out var co) ? co.GetString() : (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@DU", body.TryGetProperty("durationMin",   out var du) ? du.GetInt32()  : (object)DBNull.Value);
            var id = Convert.ToInt32(await cmd.ExecuteScalarAsync());
            await BroadcastSafe(body.TryGetProperty("businessId", out var actBiz) ? actBiz.GetInt32() : (int?)null, "ActivitiesChanged");
            return Ok(new { success = true, data = new { id } });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error CreateActivity: {M}", ex.Message);
            return StatusCode(500, new { success = false, message = ex.Message });
        }
    }

    /// PUT /api/crm/activities/{id}
    [HttpPut("activities/{id}")]
    public async Task<IActionResult> UpdateActivity(int id, [FromBody] JsonElement body)
    {
        try
        {
            using var conn = GetConnection();
            await conn.OpenAsync();
            using var cmd = new MySqlCommand(@"
                UPDATE crm_activity SET
                    subject        = COALESCE(@SU, subject),
                    body           = COALESCE(@BO, body),
                    due_date       = COALESCE(@DD, due_date),
                    call_outcome   = COALESCE(@CO, call_outcome),
                    duration_min   = COALESCE(@DU, duration_min)
                WHERE id = @ID", conn);
            cmd.Parameters.AddWithValue("@SU", body.TryGetProperty("subject",     out var su) ? su.GetString() : (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@BO", body.TryGetProperty("body",        out var bo) ? bo.GetString() : (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@DD", body.TryGetProperty("dueDate",     out var dd) ? dd.GetString() : (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@CO", body.TryGetProperty("callOutcome", out var co) ? co.GetString() : (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@DU", body.TryGetProperty("durationMin", out var du) ? du.GetInt32()  : (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@ID", id);
            await cmd.ExecuteNonQueryAsync();
            await BroadcastSafe(await FetchBusinessId("crm_activity", id), "ActivitiesChanged");
            return Ok(new { success = true });
        }
        catch (Exception ex) { return StatusCode(500, new { success = false, message = ex.Message }); }
    }

    /// POST /api/crm/activities/{id}/done
    [HttpPost("activities/{id}/done")]
    public async Task<IActionResult> MarkActivityDone(int id)
    {
        try
        {
            using var conn = GetConnection();
            await conn.OpenAsync();
            using var cmd = new MySqlCommand(
                "UPDATE crm_activity SET done=1, done_at=NOW() WHERE id=@ID", conn);
            cmd.Parameters.AddWithValue("@ID", id);
            await cmd.ExecuteNonQueryAsync();
            await BroadcastSafe(await FetchBusinessId("crm_activity", id), "ActivitiesChanged");
            return Ok(new { success = true });
        }
        catch (Exception ex) { return StatusCode(500, new { success = false, message = ex.Message }); }
    }

    /// DELETE /api/crm/activities/{id}
    [HttpDelete("activities/{id}")]
    public async Task<IActionResult> DeleteActivity(int id)
    {
        try
        {
            using var conn = GetConnection();
            await conn.OpenAsync();
            using var cmd = new MySqlCommand("DELETE FROM crm_activity WHERE id=@ID", conn);
            cmd.Parameters.AddWithValue("@ID", id);
            var actBiz2 = await FetchBusinessId("crm_activity", id);
            await cmd.ExecuteNonQueryAsync();
            await BroadcastSafe(actBiz2, "ActivitiesChanged");
            return Ok(new { success = true });
        }
        catch (Exception ex) { return StatusCode(500, new { success = false, message = ex.Message }); }
    }

    // ================================================================
    // METRICS
    // ================================================================

    /// GET /api/crm/metrics?businessId=&from=&to=
    [HttpGet("metrics")]
    public async Task<IActionResult> GetMetrics([FromQuery] int businessId,
        [FromQuery] string? from, [FromQuery] string? to)
    {
        try
        {
            using var conn = GetConnection();
            await conn.OpenAsync();

            // Conteos generales
            using var kpiCmd = new MySqlCommand(@"
                SELECT
                    (SELECT COUNT(*) FROM prospect      WHERE business_id=@B) AS totalProspects,
                    COALESCE(SUM(d.status='open'),0)                           AS openDeals,
                    COALESCE(SUM(d.status='won'),0)                            AS wonDeals,
                    COALESCE(SUM(d.status='lost'),0)                           AS lostDeals,
                    COALESCE(SUM(CASE WHEN d.status='won'  THEN d.amount END),0) AS totalRevenue,
                    COALESCE(SUM(CASE WHEN d.status='open' THEN d.amount END),0) AS expectedRevenue
                FROM crm_deal d
                WHERE d.business_id=@B
                  AND (@F IS NULL OR d.created_at >= @F)
                  AND (@T IS NULL OR d.created_at <= @T)", conn);
            kpiCmd.Parameters.AddWithValue("@B", businessId);
            kpiCmd.Parameters.AddWithValue("@F", from ?? (object)DBNull.Value);
            kpiCmd.Parameters.AddWithValue("@T", to   ?? (object)DBNull.Value);
            using var kr = await kpiCmd.ExecuteReaderAsync();
            await kr.ReadAsync();
            var totalProspects  = Convert.ToInt32(kr.GetValue(kr.GetOrdinal("totalProspects")));
            var openDeals       = Convert.ToInt32(kr.GetValue(kr.GetOrdinal("openDeals")));
            var wonDeals        = Convert.ToInt32(kr.GetValue(kr.GetOrdinal("wonDeals")));
            var lostDeals       = Convert.ToInt32(kr.GetValue(kr.GetOrdinal("lostDeals")));
            var totalRevenue    = IsNull(kr, "totalRevenue")    ? 0m : kr.GetDecimal("totalRevenue");
            var expectedRevenue = IsNull(kr, "expectedRevenue") ? 0m : kr.GetDecimal("expectedRevenue");
            var conversionRate  = (wonDeals + lostDeals) > 0 ? Math.Round((double)wonDeals / (wonDeals + lostDeals) * 100, 1) : 0d;
            await kr.CloseAsync();

            // Avg deal duration (won deals)
            using var avgCmd = new MySqlCommand(@"
                SELECT COALESCE(AVG(TIMESTAMPDIFF(DAY, created_at, won_at)),0) AS avgDays
                FROM crm_deal WHERE business_id=@B AND status='won'
                  AND (@F IS NULL OR created_at >= @F) AND (@T IS NULL OR created_at <= @T)", conn);
            avgCmd.Parameters.AddWithValue("@B", businessId);
            avgCmd.Parameters.AddWithValue("@F", from ?? (object)DBNull.Value);
            avgCmd.Parameters.AddWithValue("@T", to   ?? (object)DBNull.Value);
            var avgDealDuration = Convert.ToDouble(await avgCmd.ExecuteScalarAsync());

            // Por etapa (embudo)
            using var stageCmd = new MySqlCommand(@"
                SELECT s.id AS stageId, s.name AS stageName,
                       COUNT(d.id) AS cnt,
                       COALESCE(SUM(d.amount),0) AS totalAmount
                FROM crm_stage s
                LEFT JOIN crm_deal d ON d.stage_id=s.id AND d.business_id=@B
                    AND (@F IS NULL OR d.created_at>=@F) AND (@T IS NULL OR d.created_at<=@T)
                WHERE s.business_id=@B
                GROUP BY s.id, s.name, s.order_index
                ORDER BY s.order_index ASC", conn);
            stageCmd.Parameters.AddWithValue("@B", businessId);
            stageCmd.Parameters.AddWithValue("@F", from ?? (object)DBNull.Value);
            stageCmd.Parameters.AddWithValue("@T", to   ?? (object)DBNull.Value);
            var byStage = new List<object>();
            using var sr = await stageCmd.ExecuteReaderAsync();
            while (await sr.ReadAsync())
                byStage.Add(new { stageId = sr.GetInt32("stageId"), stageName = sr.GetString("stageName"), count = sr.GetInt32("cnt"), totalAmount = sr.GetDecimal("totalAmount") });
            await sr.CloseAsync();

            // Por tipo de actividad
            using var actCmd = new MySqlCommand(@"
                SELECT type, COUNT(*) AS cnt
                FROM crm_activity
                WHERE business_id=@B
                  AND (@F IS NULL OR created_at>=@F) AND (@T IS NULL OR created_at<=@T)
                GROUP BY type", conn);
            actCmd.Parameters.AddWithValue("@B", businessId);
            actCmd.Parameters.AddWithValue("@F", from ?? (object)DBNull.Value);
            actCmd.Parameters.AddWithValue("@T", to   ?? (object)DBNull.Value);
            var byActivityType = new List<object>();
            using var ar = await actCmd.ExecuteReaderAsync();
            while (await ar.ReadAsync())
                byActivityType.Add(new { type = ar.GetString("type"), count = ar.GetInt32("cnt") });
            await ar.CloseAsync();

            // Mensual (últimos 12 meses)
            using var mCmd = new MySqlCommand(@"
                SELECT DATE_FORMAT(created_at,'%Y-%m') AS month,
                       SUM(status='won')  AS won,
                       SUM(status='lost') AS lost,
                       COALESCE(SUM(CASE WHEN status='won' THEN amount END),0) AS revenue
                FROM crm_deal
                WHERE business_id=@B
                  AND created_at >= DATE_SUB(NOW(), INTERVAL 12 MONTH)
                GROUP BY month ORDER BY month ASC", conn);
            mCmd.Parameters.AddWithValue("@B", businessId);
            var monthlyDeals = new List<object>();
            using var mr = await mCmd.ExecuteReaderAsync();
            while (await mr.ReadAsync())
                monthlyDeals.Add(new { month = mr.GetString("month"), won = mr.GetInt32("won"), lost = mr.GetInt32("lost"), revenue = mr.GetDecimal("revenue") });

            return Ok(new { success = true, data = new {
                totalProspects, openDeals, wonDeals, lostDeals,
                totalRevenue, expectedRevenue, conversionRate, avgDealDuration,
                byStage, byActivityType, monthlyDeals,
            }});
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error GetMetrics: {M}", ex.Message);
            return StatusCode(500, new { success = false, message = ex.Message });
        }
    }

    // ================================================================
    // HELPERS
    // ================================================================

    private static object MapDeal(MySqlDataReader r) => new {
        id                = r.GetInt32("id"),
        businessId        = r.GetInt32("business_id"),
        prospectId        = r.GetInt32("prospect_id"),
        stageId           = r.GetInt32("stage_id"),
        title             = r.GetString("title"),
        amount            = IsNull2(r, "amount")              ? (decimal?)null : r.GetDecimal("amount"),
        currency          = r.GetString("currency"),
        ownerUserId       = IsNull2(r, "owner_user_id")       ? (int?)null     : r.GetInt32("owner_user_id"),
        expectedCloseDate = IsNull2(r, "expected_close_date") ? (DateTime?)null: r.GetDateTime("expected_close_date"),
        notes             = IsNull2(r, "notes")               ? null            : r.GetString("notes"),
        status            = r.GetString("status"),
        wonAt             = IsNull2(r, "won_at")              ? (DateTime?)null : r.GetDateTime("won_at"),
        lostAt            = IsNull2(r, "lost_at")             ? (DateTime?)null : r.GetDateTime("lost_at"),
        clientId          = IsNull2(r, "client_id")           ? (int?)null      : r.GetInt32("client_id"),
        createdAt         = r.GetDateTime("created_at"),
        updatedAt         = r.GetDateTime("updated_at"),
        prospectName      = IsNull2(r, "prospect_name")    ? null : r.GetString("prospect_name"),
        prospectCompany   = IsNull2(r, "prospect_company")  ? null : r.GetString("prospect_company"),
        stageName         = r.GetString("stage_name"),
        stageColor        = r.GetString("stage_color"),
    };

    private static object MapActivity(MySqlDataReader r) => new {
        id             = r.GetInt32("id"),
        businessId     = r.GetInt32("business_id"),
        dealId         = IsNull2(r, "deal_id")      ? (int?)null     : r.GetInt32("deal_id"),
        prospectId     = IsNull2(r, "prospect_id")  ? (int?)null     : r.GetInt32("prospect_id"),
        userId         = IsNull2(r, "user_id")       ? (int?)null     : r.GetInt32("user_id"),
        userName       = IsNull2(r, "user_name")     ? null           : r.GetString("user_name"),
        type           = r.GetString("type"),
        subject        = IsNull2(r, "subject")       ? null           : r.GetString("subject"),
        body           = IsNull2(r, "body")          ? null           : r.GetString("body"),
        done           = r.GetBoolean("done"),
        dueDate        = IsNull2(r, "due_date")      ? (DateTime?)null: r.GetDateTime("due_date"),
        doneAt         = IsNull2(r, "done_at")       ? (DateTime?)null: r.GetDateTime("done_at"),
        emailFrom      = IsNull2(r, "email_from")    ? null           : r.GetString("email_from"),
        emailTo        = IsNull2(r, "email_to")      ? null           : r.GetString("email_to"),
        emailCc        = IsNull2(r, "email_cc")      ? null           : r.GetString("email_cc"),
        emailStatus    = IsNull2(r, "email_status")  ? null           : r.GetString("email_status"),
        callDirection  = IsNull2(r, "call_direction")? null           : r.GetString("call_direction"),
        callOutcome    = IsNull2(r, "call_outcome")  ? null           : r.GetString("call_outcome"),
        durationMin    = IsNull2(r, "duration_min")  ? (int?)null     : r.GetInt32("duration_min"),
        createdAt      = r.GetDateTime("created_at"),
    };

    private static bool IsNull2(MySqlDataReader r, string col) => r.IsDBNull(r.GetOrdinal(col));

    private static List<object> ParseTagsJson(string raw)
    {
        var result = new List<object>();
        if (string.IsNullOrWhiteSpace(raw) || raw == "[]") return result;
        try
        {
            // GROUP_CONCAT retorna "obj1,obj2" sin los corchetes del array
            var json = raw.StartsWith("[") ? raw : $"[{raw}]";
            var arr  = JsonSerializer.Deserialize<List<JsonElement>>(json);
            if (arr == null) return result;
            foreach (var el in arr)
                result.Add(new {
                    id    = el.GetProperty("id").GetInt32(),
                    name  = el.GetProperty("name").GetString(),
                    color = el.GetProperty("color").GetString(),
                });
        }
        catch { /* devolver lista vacía si falla el parseo */ }
        return result;
    }
}
