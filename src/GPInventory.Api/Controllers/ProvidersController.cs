using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using GPInventory.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Cors;

namespace GPInventory.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[EnableCors("AllowFrontend")]
public class ProvidersController : ControllerBase
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<ProvidersController> _logger;

    public ProvidersController(ApplicationDbContext context, ILogger<ProvidersController> logger)
    {
        _context = context;
        _logger = logger;
    }

    /// <summary>
    /// Obtiene todos los proveedores con filtros opcionales
    /// </summary>
    /// <param name="businessId">ID del negocio (opcional)</param>
    /// <param name="search">Búsqueda por nombre (opcional)</param>
    /// <returns>Lista de proveedores</returns>
    [HttpGet]
    [Authorize]
    [ProducesResponseType(200)]
    [ProducesResponseType(401)]
    [ProducesResponseType(500)]
    public async Task<ActionResult<IEnumerable<object>>> GetProviders(
        [FromQuery] int? businessId = null,
        [FromQuery] string? search = null)
    {
        try
        {
            _logger.LogInformation("Obteniendo proveedores con filtros");

            var query = _context.Providers
                .Include(p => p.Business)
                .AsQueryable();

            if (businessId.HasValue)
            {
                query = query.Where(p => p.BusinessId == businessId.Value);
            }

            if (!string.IsNullOrEmpty(search))
            {
                query = query.Where(p => p.Name.Contains(search));
            }

            var providers = await query
                .Select(p => new
                {
                    id = p.Id,
                    name = p.Name,
                    business = new { id = p.Business!.Id, companyName = p.Business.CompanyName }
                })
                .ToListAsync();

            _logger.LogInformation($"Se encontraron {providers.Count} proveedores");
            return Ok(providers);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al obtener proveedores");
            return StatusCode(500, new { message = "Error interno del servidor" });
        }
    }

    /// <summary>
    /// Obtiene un proveedor específico por ID
    /// </summary>
    /// <param name="id">ID del proveedor</param>
    /// <returns>Proveedor encontrado</returns>
    [HttpGet("{id}")]
    [Authorize]
    [ProducesResponseType(200)]
    [ProducesResponseType(401)]
    [ProducesResponseType(404)]
    [ProducesResponseType(500)]
    public async Task<ActionResult<object>> GetProvider(int id)
    {
        try
        {
            _logger.LogInformation("Obteniendo proveedor con ID: {id}", id);

            var provider = await _context.Providers
                .Include(p => p.Business)
                .Where(p => p.Id == id)
                .Select(p => new
                {
                    id = p.Id,
                    name = p.Name,
                    business = new { id = p.Business!.Id, companyName = p.Business.CompanyName }
                })
                .FirstOrDefaultAsync();

            if (provider == null)
            {
                return NotFound(new { message = "Proveedor no encontrado" });
            }

            return Ok(provider);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al obtener proveedor con ID: {id}", id);
            return StatusCode(500, new { message = "Error interno del servidor" });
        }
    }

    /// <summary>
    /// Crea un nuevo proveedor
    /// </summary>
    /// <param name="request">Datos del nuevo proveedor</param>
    /// <returns>Proveedor creado</returns>
    [HttpPost]
    [Authorize]
    [ProducesResponseType(201)]
    [ProducesResponseType(400)]
    [ProducesResponseType(401)]
    [ProducesResponseType(409)]
    [ProducesResponseType(500)]
    public async Task<ActionResult<object>> CreateProvider([FromBody] CreateProviderRequest request)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(request.Name))
            {
                return BadRequest(new { message = "El nombre del proveedor es requerido" });
            }

            if (request.BusinessId <= 0)
            {
                return BadRequest(new { message = "El negocio es requerido" });
            }

            _logger.LogInformation("Creando nuevo proveedor: {providerName}", request.Name);

            // Verificar que el negocio existe
            var businessExists = await _context.Businesses.AnyAsync(b => b.Id == request.BusinessId);
            if (!businessExists)
            {
                return BadRequest(new { message = "El negocio especificado no existe" });
            }

            // Verificar si ya existe un proveedor con el mismo nombre en el mismo negocio
            var existingProvider = await _context.Providers
                .FirstOrDefaultAsync(p => p.Name.ToLower() == request.Name.ToLower().Trim() 
                                     && p.BusinessId == request.BusinessId);

            if (existingProvider != null)
            {
                return Conflict(new { message = "Ya existe un proveedor con ese nombre en este negocio" });
            }

            var newProvider = new GPInventory.Domain.Entities.Provider
            {
                Name = request.Name.Trim(),
                BusinessId = request.BusinessId
            };

            _context.Providers.Add(newProvider);
            await _context.SaveChangesAsync();

            // Obtener el proveedor creado con sus relaciones
            var createdProvider = await _context.Providers
                .Include(p => p.Business)
                .Where(p => p.Id == newProvider.Id)
                .Select(p => new
                {
                    id = p.Id,
                    name = p.Name,
                    business = new { id = p.Business.Id, companyName = p.Business.CompanyName }
                })
                .FirstOrDefaultAsync();

            _logger.LogInformation("Proveedor creado exitosamente: {providerName} con ID: {providerId}", request.Name, newProvider.Id);
            return CreatedAtAction(nameof(GetProvider), new { id = newProvider.Id }, createdProvider);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al crear proveedor");
            return StatusCode(500, new { message = "Error interno del servidor" });
        }
    }

    /// <summary>
    /// Actualiza un proveedor existente
    /// </summary>
    /// <param name="id">ID del proveedor a actualizar</param>
    /// <param name="request">Datos actualizados del proveedor</param>
    /// <returns>Proveedor actualizado</returns>
    [HttpPut("{id}")]
    [Authorize]
    [ProducesResponseType(200)]
    [ProducesResponseType(400)]
    [ProducesResponseType(401)]
    [ProducesResponseType(404)]
    [ProducesResponseType(409)]
    [ProducesResponseType(500)]
    public async Task<ActionResult<object>> UpdateProvider(int id, [FromBody] UpdateProviderRequest request)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(request.Name))
            {
                return BadRequest(new { message = "El nombre del proveedor es requerido" });
            }

            _logger.LogInformation("Actualizando proveedor con ID: {id}", id);

            var existingProvider = await _context.Providers.FindAsync(id);
            if (existingProvider == null)
            {
                return NotFound(new { message = "Proveedor no encontrado" });
            }

            // Verificar si ya existe otro proveedor con el mismo nombre en el mismo negocio
            var duplicateProvider = await _context.Providers
                .FirstOrDefaultAsync(p => p.Name.ToLower() == request.Name.ToLower().Trim() 
                                     && p.BusinessId == existingProvider.BusinessId
                                     && p.Id != id);

            if (duplicateProvider != null)
            {
                return Conflict(new { message = "Ya existe otro proveedor con ese nombre en este negocio" });
            }

            // Actualizar campos
            existingProvider.Name = request.Name.Trim();

            await _context.SaveChangesAsync();

            // Obtener el proveedor actualizado con sus relaciones
            var updatedProvider = await _context.Providers
                .Include(p => p.Business)
                .Where(p => p.Id == id)
                .Select(p => new
                {
                    id = p.Id,
                    name = p.Name,
                    business = new { id = p.Business.Id, companyName = p.Business.CompanyName }
                })
                .FirstOrDefaultAsync();

            _logger.LogInformation("Proveedor actualizado exitosamente: {providerName} con ID: {id}", request.Name, id);
            return Ok(updatedProvider);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al actualizar proveedor con ID: {id}", id);
            return StatusCode(500, new { message = "Error interno del servidor" });
        }
    }

    /// <summary>
    /// Elimina un proveedor
    /// </summary>
    /// <param name="id">ID del proveedor a eliminar</param>
    /// <returns>Confirmación de eliminación</returns>
    [HttpDelete("{id}")]
    [Authorize]
    [ProducesResponseType(200)]
    [ProducesResponseType(401)]
    [ProducesResponseType(404)]
    [ProducesResponseType(409)]
    [ProducesResponseType(500)]
    public async Task<ActionResult> DeleteProvider(int id)
    {
        try
        {
            _logger.LogInformation("Eliminando proveedor con ID: {id}", id);

            var existingProvider = await _context.Providers.FindAsync(id);
            if (existingProvider == null)
            {
                return NotFound(new { message = "Proveedor no encontrado" });
            }

            // Verificar si tiene movimientos de stock asociados
            var hasStockMovements = await _context.Stocks.AnyAsync(s => s.ProviderId == id);
            if (hasStockMovements)
            {
                return Conflict(new { message = "No se puede eliminar el proveedor porque tiene movimientos de stock asociados" });
            }

            _context.Providers.Remove(existingProvider);
            await _context.SaveChangesAsync();

            _logger.LogInformation("Proveedor eliminado exitosamente: {providerName} con ID: {id}", existingProvider.Name, id);
            return Ok(new { message = "Proveedor eliminado exitosamente", providerId = id });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al eliminar proveedor con ID: {id}", id);
            return StatusCode(500, new { message = "Error interno del servidor" });
        }
    }

    /// <summary>
    /// Busca proveedores por nombre (para autocompletado)
    /// </summary>
    /// <param name="businessId">ID del negocio</param>
    /// <param name="query">Término de búsqueda</param>
    /// <returns>Lista de nombres de proveedores que coinciden</returns>
    [HttpGet("search")]
    [Authorize]
    [ProducesResponseType(200)]
    [ProducesResponseType(400)]
    [ProducesResponseType(401)]
    [ProducesResponseType(500)]
    public async Task<ActionResult<IEnumerable<string>>> SearchProviders(
        [FromQuery] int businessId,
        [FromQuery] string query)
    {
        try
        {
            if (businessId <= 0)
            {
                return BadRequest(new { message = "ID de negocio requerido" });
            }

            if (string.IsNullOrWhiteSpace(query) || query.Length < 2)
            {
                return Ok(new List<string>());
            }

            _logger.LogInformation("Buscando proveedores para negocio: {businessId} con query: {query}", businessId, query);

            var providerNames = await _context.Providers
                .Where(p => p.BusinessId == businessId && p.Name.Contains(query))
                .Select(p => p.Name)
                .Distinct()
                .OrderBy(name => name)
                .Take(10)
                .ToListAsync();

            return Ok(providerNames);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al buscar proveedores");
            return StatusCode(500, new { message = "Error interno del servidor" });
        }
    }
}

/// <summary>
/// Modelo para crear un nuevo proveedor
/// </summary>
public class CreateProviderRequest
{
    /// <summary>
    /// Nombre del proveedor
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// ID del negocio
    /// </summary>
    public int BusinessId { get; set; }
}

/// <summary>
/// Modelo para actualizar un proveedor existente
/// </summary>
public class UpdateProviderRequest
{
    /// <summary>
    /// Nombre del proveedor
    /// </summary>
    public string Name { get; set; } = string.Empty;
}
