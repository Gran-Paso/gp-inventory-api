using GPInventory.Application.DTOs.Services;
using GPInventory.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Cors;
using Microsoft.AspNetCore.Mvc;

namespace GPInventory.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[EnableCors("AllowFrontend")]
public class ServiceSalesController : ControllerBase
{
    private readonly IServiceSaleService _service;
    private readonly ILogger<ServiceSalesController> _logger;

    public ServiceSalesController(IServiceSaleService service, ILogger<ServiceSalesController> logger)
    {
        _service = service;
        _logger = logger;
    }

    /// <summary>
    /// Obtiene todas las ventas de servicio de un negocio
    /// </summary>
    [HttpGet("business/{businessId}")]
    [Authorize]
    [ProducesResponseType(200)]
    [ProducesResponseType(401)]
    [ProducesResponseType(500)]
    public async Task<ActionResult<IEnumerable<ServiceSaleDto>>> GetByBusiness(int businessId)
    {
        try
        {
            _logger.LogInformation($"Obteniendo ventas de servicio para business {businessId}");
            var sales = await _service.GetAllAsync(businessId);
            return Ok(sales);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error al obtener ventas de servicio para business {businessId}");
            return StatusCode(500, new { message = "Error interno del servidor" });
        }
    }

    /// <summary>
    /// Obtiene todas las ventas de servicio de una tienda
    /// </summary>
    [HttpGet("store/{storeId}")]
    [Authorize]
    [ProducesResponseType(200)]
    [ProducesResponseType(401)]
    [ProducesResponseType(500)]
    public async Task<ActionResult<IEnumerable<ServiceSaleDto>>> GetByStore(int storeId)
    {
        try
        {
            _logger.LogInformation($"Obteniendo ventas de servicio para store {storeId}");
            var sales = await _service.GetByStoreIdAsync(storeId);
            return Ok(sales);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error al obtener ventas de servicio para store {storeId}");
            return StatusCode(500, new { message = "Error interno del servidor" });
        }
    }

    /// <summary>
    /// Obtiene una venta de servicio por su ID
    /// </summary>
    [HttpGet("{id}")]
    [Authorize]
    [ProducesResponseType(200)]
    [ProducesResponseType(404)]
    [ProducesResponseType(401)]
    [ProducesResponseType(500)]
    public async Task<ActionResult<ServiceSaleDto>> GetById(int id)
    {
        try
        {
            _logger.LogInformation($"Obteniendo venta de servicio {id}");
            var sale = await _service.GetByIdAsync(id);
            return Ok(sale);
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, $"Venta de servicio {id} no encontrada");
            return NotFound(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error al obtener venta de servicio {id}");
            return StatusCode(500, new { message = "Error interno del servidor" });
        }
    }

    /// <summary>
    /// Obtiene ventas de servicio pendientes de un negocio
    /// </summary>
    [HttpGet("business/{businessId}/pending")]
    [Authorize]
    [ProducesResponseType(200)]
    [ProducesResponseType(401)]
    [ProducesResponseType(500)]
    public async Task<ActionResult<IEnumerable<ServiceSaleDto>>> GetPending(int businessId)
    {
        try
        {
            _logger.LogInformation($"Obteniendo ventas de servicio pendientes para business {businessId}");
            var sales = await _service.GetPendingSalesAsync(businessId);
            return Ok(sales);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error al obtener ventas pendientes para business {businessId}");
            return StatusCode(500, new { message = "Error interno del servidor" });
        }
    }

    /// <summary>
    /// Obtiene el historial de ventas de un cliente
    /// </summary>
    [HttpGet("client/{clientId}")]
    [Authorize]
    [ProducesResponseType(200)]
    [ProducesResponseType(401)]
    [ProducesResponseType(500)]
    public async Task<ActionResult<IEnumerable<ServiceSaleDto>>> GetByClient(int clientId)
    {
        try
        {
            _logger.LogInformation($"Obteniendo historial de ventas para client {clientId}");
            var sales = await _service.GetSalesByClientIdAsync(clientId);
            return Ok(sales);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error al obtener historial para client {clientId}");
            return StatusCode(500, new { message = "Error interno del servidor" });
        }
    }

    /// <summary>
    /// Crea una nueva venta de servicio
    /// </summary>
    [HttpPost]
    [Authorize]
    [ProducesResponseType(201)]
    [ProducesResponseType(400)]
    [ProducesResponseType(401)]
    [ProducesResponseType(500)]
    public async Task<ActionResult<ServiceSaleDto>> Create([FromBody] CreateServiceSaleDto dto)
    {
        try
        {
            _logger.LogInformation("Creando nueva venta de servicio");
            var sale = await _service.CreateAsync(dto);
            return CreatedAtAction(nameof(GetById), new { id = sale.Id }, sale);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al crear venta de servicio");
            return StatusCode(500, new { message = "Error interno del servidor", error = ex.Message });
        }
    }

    /// <summary>
    /// Completa una venta de servicio (calcula montos y genera expense)
    /// </summary>
    [HttpPost("{id}/complete")]
    [Authorize]
    [ProducesResponseType(200)]
    [ProducesResponseType(400)]
    [ProducesResponseType(404)]
    [ProducesResponseType(401)]
    [ProducesResponseType(500)]
    public async Task<ActionResult<ServiceSaleDto>> Complete(int id, [FromBody] CompleteServiceSaleDto dto)
    {
        try
        {
            _logger.LogInformation($"Completando venta de servicio {id}");
            var sale = await _service.CompleteAsync(id, dto);
            return Ok(sale);
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, $"Error al completar venta {id}");
            return BadRequest(new { message = ex.Message });
        }
        catch (ArgumentException ex)
        {
            _logger.LogWarning(ex, $"Validación fallida para venta {id}");
            return BadRequest(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error al completar venta de servicio {id}");
            return StatusCode(500, new { message = "Error interno del servidor" });
        }
    }

    /// <summary>
    /// Inicia una venta de servicio (cambia status a InProgress)
    /// </summary>
    [HttpPost("{id}/start")]
    [Authorize]
    [ProducesResponseType(200)]
    [ProducesResponseType(400)]
    [ProducesResponseType(404)]
    [ProducesResponseType(401)]
    [ProducesResponseType(500)]
    public async Task<ActionResult<ServiceSaleDto>> Start(int id)
    {
        try
        {
            _logger.LogInformation($"Iniciando venta de servicio {id}");
            var sale = await _service.StartAsync(id);
            return Ok(sale);
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, $"Error al iniciar venta {id}");
            return BadRequest(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error al iniciar venta de servicio {id}");
            return StatusCode(500, new { message = "Error interno del servidor" });
        }
    }

    /// <summary>
    /// Cancela una venta de servicio
    /// </summary>
    [HttpPost("{id}/cancel")]
    [Authorize]
    [ProducesResponseType(200)]
    [ProducesResponseType(400)]
    [ProducesResponseType(404)]
    [ProducesResponseType(401)]
    [ProducesResponseType(500)]
    public async Task<ActionResult<ServiceSaleDto>> Cancel(int id)
    {
        try
        {
            _logger.LogInformation($"Cancelando venta de servicio {id}");
            var sale = await _service.CancelAsync(id);
            return Ok(sale);
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, $"Error al cancelar venta {id}");
            return BadRequest(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error al cancelar venta de servicio {id}");
            return StatusCode(500, new { message = "Error interno del servidor" });
        }
    }

    /// <summary>
    /// Elimina una venta de servicio
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
            _logger.LogInformation($"Eliminando venta de servicio {id}");
            await _service.DeleteAsync(id);
            return NoContent();
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, $"Error al eliminar venta {id}");
            return BadRequest(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error al eliminar venta de servicio {id}");
            return StatusCode(500, new { message = "Error interno del servidor" });
        }
    }
}
