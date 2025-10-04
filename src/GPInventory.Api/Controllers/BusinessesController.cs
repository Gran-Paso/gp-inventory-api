using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using GPInventory.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Cors;
using System.Security.Claims;

namespace GPInventory.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[EnableCors("AllowFrontend")]
public class BusinessesController : ControllerBase
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<BusinessesController> _logger;

    public BusinessesController(ApplicationDbContext context, ILogger<BusinessesController> logger)
    {
        _context = context;
        _logger = logger;
    }

    /// <summary>
    /// Obtener todos los negocios disponibles
    /// </summary>
    /// <returns>Lista de negocios</returns>
    [HttpGet]
    [Authorize]
    public async Task<ActionResult<List<BusinessResult>>> GetBusinesses()
    {
        try
        {
            var userId = GetCurrentUserId();
            _logger.LogInformation("🔄 Obteniendo negocios para usuario {userId} usando SQL optimizado", userId);

            var businessesQuery = @"
                SELECT DISTINCT
                    b.id as Id,
                    b.company_name as Name
                FROM business b 
                INNER JOIN user_has_business ub ON b.id = ub.id_business
                INNER JOIN user u ON u.id = ub.id_user
                WHERE ub.id_user = {0}
                  AND u.active = 1
                ORDER BY b.company_name";

            var businesses = await _context.Database
                .SqlQueryRaw<BusinessResult>(businessesQuery, userId)
                .ToListAsync();

            _logger.LogInformation("✅ Encontrados {count} negocios", businesses.Count);

            return Ok(businesses);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Error obteniendo negocios");
            return StatusCode(500, new { message = "Error interno del servidor", error = ex.Message });
        }
    }

    /// <summary>
    /// Obtener un negocio específico por ID
    /// </summary>
    /// <param name="id">ID del negocio</param>
    /// <returns>Información del negocio</returns>
    [HttpGet("{id}")]
    [Authorize]
    public async Task<ActionResult<BusinessResult>> GetBusinessById(int id)
    {
        try
        {
            var userId = GetCurrentUserId();
            _logger.LogInformation("🔄 Obteniendo negocio {businessId} para usuario {userId} usando SQL optimizado", id, userId);

            var businessQuery = @"
                SELECT 
                    b.id as Id,
                    b.company_name as Name
                FROM business b 
                INNER JOIN user_business ub ON b.id = ub.id_business
                WHERE b.id = {0} 
                  AND ub.id_user = {1}
                  AND ub.active = 1";

            var business = await _context.Database
                .SqlQueryRaw<BusinessResult>(businessQuery, id, userId)
                .FirstOrDefaultAsync();

            if (business == null)
            {
                return NotFound(new { message = "Negocio no encontrado" });
            }

            _logger.LogInformation("✅ Negocio {businessId} encontrado: {name}", id, business.Name);

            return Ok(business);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Error obteniendo negocio {businessId}", id);
            return StatusCode(500, new { message = "Error interno del servidor", error = ex.Message });
        }
    }

    /// <summary>
    /// Obtener todas las tiendas de un negocio específico
    /// </summary>
    /// <param name="businessId">ID del negocio</param>
    /// <returns>Lista de tiendas del negocio</returns>
    [HttpGet("{businessId}/stores")]
    [Authorize]
    public async Task<ActionResult<List<StoreResult>>> GetStoresByBusiness(int businessId)
    {
        try
        {
            var userId = GetCurrentUserId();
            _logger.LogInformation("🔄 Obteniendo tiendas para negocio {businessId} y usuario {userId} usando SQL optimizado", businessId, userId);

            // Verificar que el usuario tiene acceso al negocio
            var businessAccessQuery = @"
                SELECT COUNT(1) as Value
                FROM business b 
                INNER JOIN user_has_business ub ON b.id = ub.id_business
                INNER JOIN user u ON u.id = ub.id_user
                WHERE b.id = {0} 
                  AND ub.id_user = {1}
                  AND u.active = 1";

            var hasAccess = await _context.Database
                .SqlQueryRaw<int>(businessAccessQuery, businessId, userId)
                .FirstAsync() > 0;

            if (!hasAccess)
            {
                return NotFound(new { message = "Negocio no encontrado o acceso denegado" });
            }

            // Obtener tiendas usando SQL puro
            var storesQuery = @"
                SELECT 
                    s.id as Id,
                    COALESCE(s.name, '') as Name,
                    s.location as Location,
                    COALESCE(s.id_business, {0}) as BusinessId
                FROM store s
                WHERE s.id_business = {0} AND COALESCE(s.active, 0) = 1
                ORDER BY s.name";

            var stores = await _context.Database
                .SqlQueryRaw<StoreResult>(storesQuery, businessId)
                .ToListAsync();

            _logger.LogInformation("✅ Encontradas {count} tiendas para negocio {businessId}", stores.Count, businessId);

            return Ok(stores);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Error obteniendo tiendas para negocio {businessId}", businessId);
            return StatusCode(500, new { message = "Error interno del servidor", error = ex.Message });
        }
    }

    #region Helper Methods

    private int GetCurrentUserId()
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);
        if (userIdClaim == null || !int.TryParse(userIdClaim.Value, out int userId))
        {
            throw new UnauthorizedAccessException("User ID not found in token");
        }
        return userId;
    }

    #endregion
}

/// <summary>
/// Clase para mapear resultados de negocios
/// </summary>
public class BusinessResult
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
}

/// <summary>
/// Clase para mapear resultados de tiendas
/// </summary>
public class StoreResult
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Location { get; set; }
    public int BusinessId { get; set; }
}
