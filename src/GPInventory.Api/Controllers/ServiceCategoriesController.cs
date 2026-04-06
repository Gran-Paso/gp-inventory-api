using GPInventory.Application.DTOs.Services;
using GPInventory.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Cors;
using Microsoft.AspNetCore.Mvc;

namespace GPInventory.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[EnableCors("AllowFrontend")]
public class ServiceCategoriesController : ControllerBase
{
    private readonly IServiceCategoryService _service;
    private readonly ILogger<ServiceCategoriesController> _logger;

    public ServiceCategoriesController(IServiceCategoryService service, ILogger<ServiceCategoriesController> logger)
    {
        _service = service;
        _logger = logger;
    }

    /// <summary>
    /// Obtiene todas las categorías de servicio de un negocio
    /// </summary>
    [HttpGet("business/{businessId}")]
    [Authorize]
    [ProducesResponseType(200)]
    [ProducesResponseType(401)]
    [ProducesResponseType(500)]
    public async Task<ActionResult<IEnumerable<ServiceCategoryDto>>> GetByBusiness(int businessId)
    {
        try
        {
            _logger.LogInformation($"Obteniendo categorías de servicio para business {businessId}");
            var categories = await _service.GetAllAsync(businessId);
            return Ok(categories);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error al obtener categorías para business {businessId}");
            return StatusCode(500, new { message = "Error interno del servidor" });
        }
    }

    /// <summary>
    /// Obtiene una categoría por su ID
    /// </summary>
    [HttpGet("{id}")]
    [Authorize]
    [ProducesResponseType(200)]
    [ProducesResponseType(404)]
    [ProducesResponseType(401)]
    [ProducesResponseType(500)]
    public async Task<ActionResult<ServiceCategoryDto>> GetById(int id)
    {
        try
        {
            _logger.LogInformation($"Obteniendo categoría {id}");
            var category = await _service.GetByIdAsync(id);
            return Ok(category);
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, $"Categoría {id} no encontrada");
            return NotFound(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error al obtener categoría {id}");
            return StatusCode(500, new { message = "Error interno del servidor" });
        }
    }

    /// <summary>
    /// Crea una nueva categoría de servicio
    /// </summary>
    [HttpPost]
    [Authorize]
    [ProducesResponseType(201)]
    [ProducesResponseType(400)]
    [ProducesResponseType(401)]
    [ProducesResponseType(500)]
    public async Task<ActionResult<ServiceCategoryDto>> Create([FromBody] CreateServiceCategoryDto dto)
    {
        try
        {
            _logger.LogInformation("Creando nueva categoría de servicio");
            var category = await _service.CreateAsync(dto);
            return CreatedAtAction(nameof(GetById), new { id = category.Id }, category);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al crear categoría");
            return StatusCode(500, new { message = "Error interno del servidor", error = ex.Message });
        }
    }

    /// <summary>
    /// Actualiza una categoría existente
    /// </summary>
    [HttpPut("{id}")]
    [Authorize]
    [ProducesResponseType(200)]
    [ProducesResponseType(400)]
    [ProducesResponseType(404)]
    [ProducesResponseType(401)]
    [ProducesResponseType(500)]
    public async Task<ActionResult<ServiceCategoryDto>> Update(int id, [FromBody] UpdateServiceCategoryDto dto)
    {
        try
        {
            _logger.LogInformation($"Actualizando categoría {id}");
            var category = await _service.UpdateAsync(id, dto);
            return Ok(category);
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, $"Categoría {id} no encontrada");
            return NotFound(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error al actualizar categoría {id}");
            return StatusCode(500, new { message = "Error interno del servidor" });
        }
    }

    /// <summary>
    /// Elimina una categoría
    /// </summary>
    [HttpDelete("{id}")]
    [Authorize]
    [ProducesResponseType(204)]
    [ProducesResponseType(404)]
    [ProducesResponseType(401)]
    [ProducesResponseType(500)]
    public async Task<ActionResult> Delete(int id)
    {
        try
        {
            _logger.LogInformation($"Eliminando categoría {id}");
            await _service.DeleteAsync(id);
            return NoContent();
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, $"Categoría {id} no encontrada");
            return NotFound(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error al eliminar categoría {id}");
            return StatusCode(500, new { message = "Error interno del servidor" });
        }
    }
}
