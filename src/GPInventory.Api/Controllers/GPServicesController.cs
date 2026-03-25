using GPInventory.Application.DTOs.Services;
using GPInventory.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Cors;
using Microsoft.AspNetCore.Mvc;

namespace GPInventory.Api.Controllers;

[ApiController]
[Route("api/gp-services")]
[EnableCors("AllowFrontend")]
public class GPServicesController : ControllerBase
{
    private readonly IServiceService _service;
    private readonly ILogger<GPServicesController> _logger;

    public GPServicesController(IServiceService service, ILogger<GPServicesController> logger)
    {
        _service = service;
        _logger = logger;
    }

    /// <summary>
    /// Obtiene todos los servicios de un negocio
    /// </summary>
    [HttpGet("business/{businessId}")]
    [Authorize]
    [ProducesResponseType(200)]
    [ProducesResponseType(401)]
    [ProducesResponseType(500)]
    public async Task<ActionResult<IEnumerable<ServiceDto>>> GetByBusiness(int businessId)
    {
        try
        {
            _logger.LogInformation($"Obteniendo servicios para business {businessId}");
            var services = await _service.GetAllAsync(businessId);
            return Ok(services);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error al obtener servicios para business {businessId}");
            return StatusCode(500, new { message = "Error interno del servidor" });
        }
    }

    /// <summary>
    /// Obtiene servicios activos de un negocio
    /// </summary>
    [HttpGet("business/{businessId}/active")]
    [Authorize]
    [ProducesResponseType(200)]
    [ProducesResponseType(401)]
    [ProducesResponseType(500)]
    public async Task<ActionResult<IEnumerable<ServiceDto>>> GetActiveServices(int businessId)
    {
        try
        {
            _logger.LogInformation($"Obteniendo servicios activos para business {businessId}");
            var services = await _service.GetActiveServicesAsync(businessId);
            return Ok(services);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error al obtener servicios activos para business {businessId}");
            return StatusCode(500, new { message = "Error interno del servidor" });
        }
    }

    /// <summary>
    /// Obtiene todos los servicios de una tienda
    /// </summary>
    [HttpGet("store/{storeId}")]
    [Authorize]
    [ProducesResponseType(200)]
    [ProducesResponseType(401)]
    [ProducesResponseType(500)]
    public async Task<ActionResult<IEnumerable<ServiceDto>>> GetByStore(int storeId)
    {
        try
        {
            _logger.LogInformation($"Obteniendo servicios para store {storeId}");
            var services = await _service.GetByStoreIdAsync(storeId);
            return Ok(services);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error al obtener servicios para store {storeId}");
            return StatusCode(500, new { message = "Error interno del servidor" });
        }
    }

    /// <summary>
    /// Obtiene todos los servicios de una categoría
    /// </summary>
    [HttpGet("category/{categoryId}")]
    [Authorize]
    [ProducesResponseType(200)]
    [ProducesResponseType(401)]
    [ProducesResponseType(500)]
    public async Task<ActionResult<IEnumerable<ServiceDto>>> GetByCategory(int categoryId)
    {
        try
        {
            _logger.LogInformation($"Obteniendo servicios para categoría {categoryId}");
            var services = await _service.GetByCategoryIdAsync(categoryId);
            return Ok(services);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error al obtener servicios para categoría {categoryId}");
            return StatusCode(500, new { message = "Error interno del servidor" });
        }
    }

    /// <summary>
    /// Obtiene un servicio por su ID
    /// </summary>
    [HttpGet("{id}")]
    [Authorize]
    [ProducesResponseType(200)]
    [ProducesResponseType(404)]
    [ProducesResponseType(401)]
    [ProducesResponseType(500)]
    public async Task<ActionResult<ServiceDto>> GetById(int id)
    {
        try
        {
            _logger.LogInformation($"Obteniendo servicio {id}");
            var service = await _service.GetByIdAsync(id);
            return Ok(service);
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, $"Servicio {id} no encontrado");
            return NotFound(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error al obtener servicio {id}");
            return StatusCode(500, new { message = "Error interno del servidor" });
        }
    }

    /// <summary>
    /// Crea un nuevo servicio
    /// </summary>
    [HttpPost]
    [Authorize]
    [ProducesResponseType(201)]
    [ProducesResponseType(400)]
    [ProducesResponseType(401)]
    [ProducesResponseType(500)]
    public async Task<ActionResult<ServiceDto>> Create([FromBody] CreateServiceDto dto)
    {
        try
        {
            _logger.LogInformation("Creando nuevo servicio");
            var service = await _service.CreateAsync(dto);
            return CreatedAtAction(nameof(GetById), new { id = service.Id }, service);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al crear servicio");
            return StatusCode(500, new { message = "Error interno del servidor", error = ex.Message });
        }
    }

    /// <summary>
    /// Actualiza un servicio existente
    /// </summary>
    [HttpPut("{id}")]
    [Authorize]
    [ProducesResponseType(200)]
    [ProducesResponseType(400)]
    [ProducesResponseType(404)]
    [ProducesResponseType(401)]
    [ProducesResponseType(500)]
    public async Task<ActionResult<ServiceDto>> Update(int id, [FromBody] UpdateServiceDto dto)
    {
        try
        {
            _logger.LogInformation($"Actualizando servicio {id}");
            var service = await _service.UpdateAsync(id, dto);
            return Ok(service);
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, $"Servicio {id} no encontrado");
            return NotFound(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error al actualizar servicio {id}");
            return StatusCode(500, new { message = "Error interno del servidor" });
        }
    }

    /// <summary>
    /// Elimina un servicio
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
            _logger.LogInformation($"Eliminando servicio {id}");
            await _service.DeleteAsync(id);
            return NoContent();
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, $"Servicio {id} no encontrado");
            return NotFound(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error al eliminar servicio {id}");
            return StatusCode(500, new { message = "Error interno del servidor" });
        }
    }
}
