using GPInventory.Application.DTOs.Services;
using GPInventory.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Cors;
using Microsoft.AspNetCore.Mvc;

namespace GPInventory.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[EnableCors("AllowFrontend")]
public class ServiceClientsController : ControllerBase
{
    private readonly IServiceClientService _service;
    private readonly ILogger<ServiceClientsController> _logger;

    public ServiceClientsController(IServiceClientService service, ILogger<ServiceClientsController> logger)
    {
        _service = service;
        _logger = logger;
    }

    /// <summary>
    /// Obtiene todos los clientes de un negocio
    /// </summary>
    [HttpGet("business/{businessId}")]
    [Authorize]
    [ProducesResponseType(200)]
    [ProducesResponseType(401)]
    [ProducesResponseType(500)]
    public async Task<ActionResult<IEnumerable<ServiceClientDto>>> GetByBusiness(int businessId)
    {
        try
        {
            _logger.LogInformation($"Obteniendo clientes para business {businessId}");
            var clients = await _service.GetAllAsync(businessId);
            return Ok(clients);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error al obtener clientes para business {businessId}");
            return StatusCode(500, new { message = "Error interno del servidor" });
        }
    }

    /// <summary>
    /// Obtiene clientes activos de un negocio
    /// </summary>
    [HttpGet("business/{businessId}/active")]
    [Authorize]
    [ProducesResponseType(200)]
    [ProducesResponseType(401)]
    [ProducesResponseType(500)]
    public async Task<ActionResult<IEnumerable<ServiceClientDto>>> GetActiveClients(int businessId)
    {
        try
        {
            _logger.LogInformation($"Obteniendo clientes activos para business {businessId}");
            var clients = await _service.GetActiveClientsAsync(businessId);
            return Ok(clients);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error al obtener clientes activos para business {businessId}");
            return StatusCode(500, new { message = "Error interno del servidor" });
        }
    }

    /// <summary>
    /// Obtiene todos los clientes de una tienda
    /// </summary>
    [HttpGet("store/{storeId}")]
    [Authorize]
    [ProducesResponseType(200)]
    [ProducesResponseType(401)]
    [ProducesResponseType(500)]
    public async Task<ActionResult<IEnumerable<ServiceClientDto>>> GetByStore(int storeId)
    {
        try
        {
            _logger.LogInformation($"Obteniendo clientes para store {storeId}");
            var clients = await _service.GetByStoreIdAsync(storeId);
            return Ok(clients);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error al obtener clientes para store {storeId}");
            return StatusCode(500, new { message = "Error interno del servidor" });
        }
    }

    /// <summary>
    /// Obtiene un cliente por su ID
    /// </summary>
    [HttpGet("{id}")]
    [Authorize]
    [ProducesResponseType(200)]
    [ProducesResponseType(404)]
    [ProducesResponseType(401)]
    [ProducesResponseType(500)]
    public async Task<ActionResult<ServiceClientDto>> GetById(int id)
    {
        try
        {
            _logger.LogInformation($"Obteniendo cliente {id}");
            var client = await _service.GetByIdAsync(id);
            return Ok(client);
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, $"Cliente {id} no encontrado");
            return NotFound(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error al obtener cliente {id}");
            return StatusCode(500, new { message = "Error interno del servidor" });
        }
    }

    /// <summary>
    /// Obtiene el historial de compras de un cliente
    /// </summary>
    [HttpGet("{id}/history")]
    [Authorize]
    [ProducesResponseType(200)]
    [ProducesResponseType(404)]
    [ProducesResponseType(401)]
    [ProducesResponseType(500)]
    public async Task<ActionResult<IEnumerable<ServiceSaleDto>>> GetClientHistory(int id)
    {
        try
        {
            _logger.LogInformation($"Obteniendo historial de cliente {id}");
            var history = await _service.GetClientHistoryAsync(id);
            return Ok(history);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error al obtener historial de cliente {id}");
            return StatusCode(500, new { message = "Error interno del servidor" });
        }
    }

    /// <summary>
    /// Crea un nuevo cliente
    /// </summary>
    [HttpPost]
    [Authorize]
    [ProducesResponseType(201)]
    [ProducesResponseType(400)]
    [ProducesResponseType(401)]
    [ProducesResponseType(500)]
    public async Task<ActionResult<ServiceClientDto>> Create([FromBody] CreateServiceClientDto dto)
    {
        _logger.LogInformation("=== INICIO Create Cliente ===");
        _logger.LogInformation("DTO recibido: {@Dto}", dto);
        
        if (!ModelState.IsValid)
        {
            _logger.LogWarning("ModelState inválido al crear cliente");
            
            foreach (var state in ModelState)
            {
                _logger.LogWarning("Campo: {Field}", state.Key);
                foreach (var error in state.Value.Errors)
                {
                    _logger.LogWarning("  - Error: {ErrorMessage}", error.ErrorMessage);
                    if (error.Exception != null)
                    {
                        _logger.LogWarning("  - Exception: {Exception}", error.Exception.Message);
                    }
                }
            }
            
            var errors = ModelState
                .Where(x => x.Value?.Errors.Count > 0)
                .Select(x => new
                {
                    Field = x.Key,
                    Errors = x.Value?.Errors.Select(e => string.IsNullOrEmpty(e.ErrorMessage) ? e.Exception?.Message : e.ErrorMessage).ToArray()
                })
                .ToList();
            
            return BadRequest(new { message = "Errores de validación", errors });
        }

        try
        {
            _logger.LogInformation("Creando nuevo cliente");
            var client = await _service.CreateAsync(dto);
            _logger.LogInformation("Cliente creado exitosamente con ID: {ClientId}", client.Id);
            return CreatedAtAction(nameof(GetById), new { id = client.Id }, client);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al crear cliente");
            return StatusCode(500, new { message = "Error interno del servidor", error = ex.Message });
        }
    }

    /// <summary>
    /// Actualiza un cliente existente
    /// </summary>
    [HttpPut("{id}")]
    [Authorize]
    [ProducesResponseType(200)]
    [ProducesResponseType(400)]
    [ProducesResponseType(404)]
    [ProducesResponseType(401)]
    [ProducesResponseType(500)]
    public async Task<ActionResult<ServiceClientDto>> Update(int id, [FromBody] UpdateServiceClientDto dto)
    {
        try
        {
            _logger.LogInformation($"Actualizando cliente {id}");
            var client = await _service.UpdateAsync(id, dto);
            return Ok(client);
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, $"Cliente {id} no encontrado");
            return NotFound(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error al actualizar cliente {id}");
            return StatusCode(500, new { message = "Error interno del servidor" });
        }
    }

    /// <summary>
    /// Elimina un cliente (soft delete si tiene ventas, hard delete si no)
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
            _logger.LogInformation($"Eliminando cliente {id}");
            await _service.DeleteAsync(id);
            return NoContent();
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, $"Cliente {id} no encontrado");
            return NotFound(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error al eliminar cliente {id}");
            return StatusCode(500, new { message = "Error interno del servidor" });
        }
    }

    /// <summary>
    /// Obtiene los sub-clientes (beneficiarios) de un cliente raíz
    /// </summary>
    [HttpGet("{parentId}/sub-clients")]
    [Authorize]
    [ProducesResponseType(200)]
    [ProducesResponseType(401)]
    [ProducesResponseType(500)]
    public async Task<ActionResult<IEnumerable<ServiceClientDto>>> GetSubClients(int parentId)
    {
        try
        {
            var sub = await _service.GetSubClientsAsync(parentId);
            return Ok(sub);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error al obtener sub-clientes de {parentId}");
            return StatusCode(500, new { message = "Error interno del servidor" });
        }
    }

    /// <summary>
    /// Devuelve el catálogo de tipos de relación para sub-clientes
    /// </summary>
    [HttpGet("relationship-types")]
    [Authorize]
    [ProducesResponseType(200)]
    [ProducesResponseType(401)]
    [ProducesResponseType(500)]
    public async Task<ActionResult<IEnumerable<RelationshipTypeDto>>> GetRelationshipTypes()
    {
        try
        {
            var types = await _service.GetRelationshipTypesAsync();
            return Ok(types);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al obtener tipos de relación");
            return StatusCode(500, new { message = "Error interno del servidor" });
        }
    }
}
