using GPInventory.Application.DTOs.Production;
using GPInventory.Application.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GPInventory.Api.Controllers;

// [Authorize] // Temporarily disabled for testing
[ApiController]
[Route("api/supply-categories")]
public class SupplyCategoriesController : ControllerBase
{
    private readonly ISupplyCategoryService _service;
    private readonly ILogger<SupplyCategoriesController> _logger;

    public SupplyCategoriesController(
        ISupplyCategoryService service,
        ILogger<SupplyCategoriesController> logger)
    {
        _service = service;
        _logger = logger;
    }

    [HttpGet("business/{businessId}")]
    public async Task<ActionResult<IEnumerable<SupplyCategoryDto>>> GetByBusinessId(
        int businessId,
        [FromQuery] bool activeOnly = false)
    {
        try
        {
            var categories = await _service.GetAllByBusinessIdAsync(businessId, activeOnly);
            return Ok(categories);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting supply categories for business {BusinessId}", businessId);
            return StatusCode(500, "Error al obtener las categorías");
        }
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<SupplyCategoryDto>> GetById(int id)
    {
        try
        {
            var category = await _service.GetByIdAsync(id);
            if (category == null)
            {
                return NotFound($"Categoría con ID {id} no encontrada");
            }
            return Ok(category);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting supply category {Id}", id);
            return StatusCode(500, "Error al obtener la categoría");
        }
    }

    [HttpPost]
    public async Task<ActionResult<SupplyCategoryDto>> Create([FromBody] CreateSupplyCategoryDto dto)
    {
        try
        {
            var created = await _service.CreateAsync(dto);
            return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating supply category");
            return StatusCode(500, "Error al crear la categoría");
        }
    }

    [HttpPut("{id}")]
    public async Task<ActionResult<SupplyCategoryDto>> Update(int id, [FromBody] CreateSupplyCategoryDto dto)
    {
        try
        {
            var updated = await _service.UpdateAsync(id, dto);
            return Ok(updated);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(ex.Message);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating supply category {Id}", id);
            return StatusCode(500, "Error al actualizar la categoría");
        }
    }

    [HttpPatch("{id}/toggle-active")]
    public async Task<IActionResult> ToggleActive(int id)
    {
        try
        {
            await _service.ToggleActiveAsync(id);
            return NoContent();
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error toggling active status for category {Id}", id);
            return StatusCode(500, "Error al cambiar el estado de la categoría");
        }
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(int id)
    {
        try
        {
            var result = await _service.DeleteAsync(id);
            if (!result)
            {
                return NotFound($"Categoría con ID {id} no encontrada");
            }
            return NoContent();
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting supply category {Id}", id);
            return StatusCode(500, "Error al eliminar la categoría");
        }
    }
}
