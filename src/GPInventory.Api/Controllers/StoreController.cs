using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using GPInventory.Infrastructure.Data;
using GPInventory.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Cors;
using System.Security.Claims;

namespace GPInventory.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[EnableCors("AllowFrontend")]
public class StoreController : ControllerBase
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<StoreController> _logger;

    public StoreController(ApplicationDbContext context, ILogger<StoreController> logger)
    {
        _context = context;
        _logger = logger;
    }

    private int GetCurrentUserId()
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userIdClaim) || !int.TryParse(userIdClaim, out int userId))
        {
            throw new UnauthorizedAccessException("Usuario no autenticado");
        }
        return userId;
    }

    /// <summary>
    /// Obtener la configuración del score de salud de una tienda
    /// </summary>
    /// <param name="id">ID de la tienda</param>
    /// <returns>Configuración del score</returns>
    [HttpGet("{id}/score-config")]
    [Authorize]
    public async Task<ActionResult<StoreScoreConfigDto>> GetStoreScoreConfig(int id)
    {
        try
        {
            var userId = GetCurrentUserId();
            _logger.LogInformation("🔄 Obteniendo configuración de score para tienda {storeId}, usuario {userId}", id, userId);

            // Verificar que el usuario tenga acceso a esta tienda mediante query SQL
            var accessCheckQuery = @"
                SELECT COUNT(*) as Count
                FROM store s
                INNER JOIN user_has_business uhb ON s.id_business = uhb.id_business
                WHERE s.id = {0}
                  AND s.active = 1
                  AND uhb.id_user = {1}";

            var hasAccess = await _context.Database
                .SqlQueryRaw<CountResult>(accessCheckQuery, id, userId)
                .FirstOrDefaultAsync();

            if (hasAccess == null || hasAccess.Count == 0)
            {
                _logger.LogWarning("⚠️ Usuario {userId} no tiene acceso a la tienda {storeId} o tienda no encontrada", userId, id);
                return NotFound(new { message = "Tienda no encontrada o sin acceso" });
            }

            // Obtener la configuración de la tienda
            var configQuery = @"
                SELECT 
                    s.id as StoreId,
                    s.name as StoreName,
                    s.score_base as ScoreBase,
                    s.score_no_sales_penalty as ScoreNoSalesPenalty,
                    s.score_high_drop_penalty as ScoreHighDropPenalty,
                    s.score_high_drop_threshold as ScoreHighDropThreshold,
                    s.score_medium_drop_penalty as ScoreMediumDropPenalty,
                    s.score_medium_drop_threshold as ScoreMediumDropThreshold,
                    s.score_low_volume_penalty as ScoreLowVolumePenalty,
                    s.score_low_volume_threshold as ScoreLowVolumeThreshold,
                    s.score_critical_stock_penalty as ScoreCriticalStockPenalty,
                    s.score_critical_stock_threshold as ScoreCriticalStockThreshold,
                    s.score_low_stock_penalty as ScoreLowStockPenalty,
                    s.score_low_stock_threshold as ScoreLowStockThreshold,
                    s.score_healthy_threshold as ScoreHealthyThreshold,
                    s.score_warning_threshold as ScoreWarningThreshold
                FROM store s
                WHERE s.id = {0} AND s.active = 1";

            var config = await _context.Database
                .SqlQueryRaw<StoreScoreConfigDto>(configQuery, id)
                .FirstOrDefaultAsync();

            if (config == null)
            {
                _logger.LogWarning("⚠️ No se pudo obtener la configuración de la tienda {storeId}", id);
                return NotFound(new { message = "Configuración no encontrada" });
            }

            _logger.LogInformation("✅ Configuración obtenida para tienda {storeId}", id);
            return Ok(config);
        }
        catch (UnauthorizedAccessException)
        {
            return Unauthorized(new { message = "Usuario no autenticado" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Error obteniendo configuración de score para tienda {storeId}", id);
            return StatusCode(500, new { message = "Error interno del servidor", error = ex.Message });
        }
    }

    /// <summary>
    /// Actualizar la configuración del score de salud de una tienda
    /// </summary>
    /// <param name="id">ID de la tienda</param>
    /// <param name="config">Nueva configuración</param>
    /// <returns>Configuración actualizada</returns>
    [HttpPut("{id}/score-config")]
    [Authorize]
    public async Task<ActionResult<StoreScoreConfigDto>> UpdateStoreScoreConfig(int id, [FromBody] StoreScoreConfigDto config)
    {
        try
        {
            var userId = GetCurrentUserId();
            _logger.LogInformation("🔄 Actualizando configuración de score para tienda {storeId}, usuario {userId}", id, userId);

            // Verificar que el ID coincida
            if (id != config.StoreId)
            {
                return BadRequest(new { message = "El ID de la tienda no coincide" });
            }

            // Verificar que el usuario tenga acceso a esta tienda mediante query SQL
            var accessCheckQuery = @"
                SELECT COUNT(*) as Count
                FROM store s
                INNER JOIN user_has_business uhb ON s.id_business = uhb.id_business
                WHERE s.id = {0}
                  AND s.active = 1
                  AND uhb.id_user = {1}";

            var hasAccess = await _context.Database
                .SqlQueryRaw<CountResult>(accessCheckQuery, id, userId)
                .FirstOrDefaultAsync();

            if (hasAccess == null || hasAccess.Count == 0)
            {
                _logger.LogWarning("⚠️ Usuario {userId} no tiene acceso a la tienda {storeId} o tienda no encontrada", userId, id);
                return NotFound(new { message = "Tienda no encontrada o sin acceso" });
            }

            // Validar que los valores sean razonables
            if (config.ScoreBase < 0 || config.ScoreBase > 200)
            {
                return BadRequest(new { message = "El score base debe estar entre 0 y 200" });
            }

            if (config.ScoreNoSalesPenalty < 0 || config.ScoreNoSalesPenalty > 100)
            {
                return BadRequest(new { message = "Las penalizaciones deben estar entre 0 y 100" });
            }

            // Actualizar la configuración mediante query SQL
            var updateQuery = @"
                UPDATE store 
                SET 
                    score_base = {1},
                    score_no_sales_penalty = {2},
                    score_high_drop_penalty = {3},
                    score_high_drop_threshold = {4},
                    score_medium_drop_penalty = {5},
                    score_medium_drop_threshold = {6},
                    score_low_volume_penalty = {7},
                    score_low_volume_threshold = {8},
                    score_critical_stock_penalty = {9},
                    score_critical_stock_threshold = {10},
                    score_low_stock_penalty = {11},
                    score_low_stock_threshold = {12},
                    score_healthy_threshold = {13},
                    score_warning_threshold = {14}
                WHERE id = {0} AND active = 1";

            await _context.Database.ExecuteSqlRawAsync(
                updateQuery,
                id,
                config.ScoreBase,
                config.ScoreNoSalesPenalty,
                config.ScoreHighDropPenalty,
                config.ScoreHighDropThreshold,
                config.ScoreMediumDropPenalty,
                config.ScoreMediumDropThreshold,
                config.ScoreLowVolumePenalty,
                config.ScoreLowVolumeThreshold,
                config.ScoreCriticalStockPenalty,
                config.ScoreCriticalStockThreshold,
                config.ScoreLowStockPenalty,
                config.ScoreLowStockThreshold,
                config.ScoreHealthyThreshold,
                config.ScoreWarningThreshold
            );

            _logger.LogInformation("✅ Configuración actualizada para tienda {storeId}", id);

            return Ok(config);
        }
        catch (UnauthorizedAccessException)
        {
            return Unauthorized(new { message = "Usuario no autenticado" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Error actualizando configuración de score para tienda {storeId}", id);
            return StatusCode(500, new { message = "Error interno del servidor", error = ex.Message });
        }
    }
}

/// <summary>
/// DTO para contar resultados
/// </summary>
public class CountResult
{
    public int Count { get; set; }
}

/// <summary>
/// DTO para la configuración del score de salud de una tienda
/// </summary>
public class StoreScoreConfigDto
{
    public int StoreId { get; set; }
    public string StoreName { get; set; } = string.Empty;
    public int ScoreBase { get; set; }
    public int ScoreNoSalesPenalty { get; set; }
    public int ScoreHighDropPenalty { get; set; }
    public decimal ScoreHighDropThreshold { get; set; }
    public int ScoreMediumDropPenalty { get; set; }
    public decimal ScoreMediumDropThreshold { get; set; }
    public int ScoreLowVolumePenalty { get; set; }
    public int ScoreLowVolumeThreshold { get; set; }
    public int ScoreCriticalStockPenalty { get; set; }
    public int ScoreCriticalStockThreshold { get; set; }
    public int ScoreLowStockPenalty { get; set; }
    public int ScoreLowStockThreshold { get; set; }
    public int ScoreHealthyThreshold { get; set; }
    public int ScoreWarningThreshold { get; set; }
}
