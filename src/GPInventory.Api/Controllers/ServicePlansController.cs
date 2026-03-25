using GPInventory.Application.DTOs.Services;
using GPInventory.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Cors;
using Microsoft.AspNetCore.Mvc;

namespace GPInventory.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[EnableCors("AllowFrontend")]
public class ServicePlansController : ControllerBase
{
    private readonly IServicePlanService _service;
    private readonly ILogger<ServicePlansController> _logger;

    public ServicePlansController(IServicePlanService service, ILogger<ServicePlansController> logger)
    {
        _service = service;
        _logger = logger;
    }

    /// <summary>
    /// Obtiene un plan de servicio por ID
    /// </summary>
    [HttpGet("{id}")]
    [Authorize]
    [ProducesResponseType(200)]
    [ProducesResponseType(404)]
    [ProducesResponseType(401)]
    [ProducesResponseType(500)]
    public async Task<ActionResult<ServicePlanDto>> GetById(int id)
    {
        try
        {
            var plan = await _service.GetByIdAsync(id);
            return Ok(plan);
        }
        catch (InvalidOperationException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error al obtener plan {id}");
            return StatusCode(500, new { message = "Error interno del servidor" });
        }
    }

    /// <summary>
    /// Obtiene todos los planes de un negocio
    /// </summary>
    [HttpGet("business/{businessId}")]
    [Authorize]
    [ProducesResponseType(200)]
    [ProducesResponseType(401)]
    [ProducesResponseType(500)]
    public async Task<ActionResult<IEnumerable<ServicePlanDto>>> GetByBusiness(int businessId)
    {
        try
        {
            var plans = await _service.GetAllAsync(businessId);
            return Ok(plans);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error al obtener planes para business {businessId}");
            return StatusCode(500, new { message = "Error interno del servidor" });
        }
    }

    /// <summary>
    /// Obtiene planes activos de un negocio
    /// </summary>
    [HttpGet("business/{businessId}/active")]
    [Authorize]
    [ProducesResponseType(200)]
    [ProducesResponseType(401)]
    [ProducesResponseType(500)]
    public async Task<ActionResult<IEnumerable<ServicePlanDto>>> GetActivePlans(int businessId)
    {
        try
        {
            var plans = await _service.GetActiveAsync(businessId);
            return Ok(plans);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error al obtener planes activos para business {businessId}");
            return StatusCode(500, new { message = "Error interno del servidor" });
        }
    }

    /// <summary>
    /// Obtiene planes por servicio
    /// </summary>
    [HttpGet("service/{serviceId}")]
    [Authorize]
    [ProducesResponseType(200)]
    [ProducesResponseType(401)]
    [ProducesResponseType(500)]
    public async Task<ActionResult<IEnumerable<ServicePlanDto>>> GetByService(int serviceId)
    {
        try
        {
            var plans = await _service.GetByServiceAsync(serviceId);
            return Ok(plans);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error al obtener planes para servicio {serviceId}");
            return StatusCode(500, new { message = "Error interno del servidor" });
        }
    }

    /// <summary>
    /// Obtiene planes por categoría
    /// </summary>
    [HttpGet("category/{categoryId}")]
    [Authorize]
    [ProducesResponseType(200)]
    [ProducesResponseType(401)]
    [ProducesResponseType(500)]
    public async Task<ActionResult<IEnumerable<ServicePlanDto>>> GetByCategory(int categoryId)
    {
        try
        {
            var plans = await _service.GetByCategoryAsync(categoryId);
            return Ok(plans);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error al obtener planes para categoría {categoryId}");
            return StatusCode(500, new { message = "Error interno del servidor" });
        }
    }

    /// <summary>
    /// Crea un nuevo plan de servicio
    /// </summary>
    [HttpPost]
    [Authorize]
    [ProducesResponseType(201)]
    [ProducesResponseType(400)]
    [ProducesResponseType(401)]
    [ProducesResponseType(500)]
    public async Task<ActionResult<ServicePlanDto>> Create([FromBody] CreateServicePlanDto dto)
    {
        try
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var plan = await _service.CreateAsync(dto);
            return CreatedAtAction(nameof(GetById), new { id = plan.Id }, plan);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al crear plan de servicio");
            return StatusCode(500, new { message = "Error interno del servidor" });
        }
    }

    /// <summary>
    /// Actualiza un plan de servicio
    /// </summary>
    [HttpPut("{id}")]
    [Authorize]
    [ProducesResponseType(200)]
    [ProducesResponseType(400)]
    [ProducesResponseType(404)]
    [ProducesResponseType(401)]
    [ProducesResponseType(500)]
    public async Task<ActionResult<ServicePlanDto>> Update(int id, [FromBody] UpdateServicePlanDto dto)
    {
        try
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var plan = await _service.UpdateAsync(id, dto);
            return Ok(plan);
        }
        catch (InvalidOperationException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error al actualizar plan {id}");
            return StatusCode(500, new { message = "Error interno del servidor" });
        }
    }

    /// <summary>
    /// Cambia el estado activo/inactivo de un plan
    /// </summary>
    [HttpPatch("{id}/toggle-active")]
    [Authorize]
    [ProducesResponseType(200)]
    [ProducesResponseType(404)]
    [ProducesResponseType(401)]
    [ProducesResponseType(500)]
    public async Task<ActionResult<ServicePlanDto>> ToggleActive(int id)
    {
        try
        {
            var plan = await _service.ToggleActiveAsync(id);
            return Ok(plan);
        }
        catch (InvalidOperationException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error al cambiar estado del plan {id}");
            return StatusCode(500, new { message = "Error interno del servidor" });
        }
    }

    /// <summary>
    /// Elimina un plan de servicio
    /// </summary>
    [HttpDelete("{id}")]
    [Authorize]
    [ProducesResponseType(204)]
    [ProducesResponseType(400)]
    [ProducesResponseType(404)]
    [ProducesResponseType(401)]
    [ProducesResponseType(500)]
    public async Task<ActionResult> Delete(int id)
    {
        try
        {
            await _service.DeleteAsync(id);
            return NoContent();
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error al eliminar plan {id}");
            return StatusCode(500, new { message = "Error interno del servidor" });
        }
    }
}
