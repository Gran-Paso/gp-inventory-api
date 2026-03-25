using GPInventory.Application.DTOs.Services;
using GPInventory.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Cors;
using Microsoft.AspNetCore.Mvc;

namespace GPInventory.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[EnableCors("AllowFrontend")]
public class ServiceAttendancesController : ControllerBase
{
    private readonly IServiceAttendanceService _service;
    private readonly ILogger<ServiceAttendancesController> _logger;

    public ServiceAttendancesController(IServiceAttendanceService service, ILogger<ServiceAttendancesController> logger)
    {
        _service = service;
        _logger = logger;
    }

    /// <summary>
    /// Obtiene una asistencia por ID
    /// </summary>
    [HttpGet("{id}")]
    [Authorize]
    [ProducesResponseType(200)]
    [ProducesResponseType(404)]
    [ProducesResponseType(401)]
    [ProducesResponseType(500)]
    public async Task<ActionResult<ServiceAttendanceDto>> GetById(int id)
    {
        try
        {
            var attendance = await _service.GetByIdAsync(id);
            return Ok(attendance);
        }
        catch (InvalidOperationException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error al obtener asistencia {id}");
            return StatusCode(500, new { message = "Error interno del servidor" });
        }
    }

    /// <summary>
    /// Obtiene asistencias de un cliente (con filtros opcionales de fecha)
    /// </summary>
    [HttpGet("client/{clientId}")]
    [Authorize]
    [ProducesResponseType(200)]
    [ProducesResponseType(401)]
    [ProducesResponseType(500)]
    public async Task<ActionResult<IEnumerable<ServiceAttendanceDto>>> GetByClient(
        int clientId,
        [FromQuery] DateTime? startDate = null,
        [FromQuery] DateTime? endDate = null)
    {
        try
        {
            var attendances = await _service.GetByClientAsync(clientId, startDate, endDate);
            return Ok(attendances);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error al obtener asistencias del cliente {clientId}");
            return StatusCode(500, new { message = "Error interno del servidor" });
        }
    }

    /// <summary>
    /// Obtiene asistencias de un servicio (con filtros opcionales de fecha)
    /// </summary>
    [HttpGet("service/{serviceId}")]
    [Authorize]
    [ProducesResponseType(200)]
    [ProducesResponseType(401)]
    [ProducesResponseType(500)]
    public async Task<ActionResult<IEnumerable<ServiceAttendanceDto>>> GetByService(
        int serviceId,
        [FromQuery] DateTime? startDate = null,
        [FromQuery] DateTime? endDate = null)
    {
        try
        {
            var attendances = await _service.GetByServiceAsync(serviceId, startDate, endDate);
            return Ok(attendances);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error al obtener asistencias del servicio {serviceId}");
            return StatusCode(500, new { message = "Error interno del servidor" });
        }
    }

    /// <summary>
    /// Obtiene asistencias de un negocio para una fecha determinada
    /// </summary>
    [HttpGet("business/{businessId}/date")]
    [Authorize]
    [ProducesResponseType(200)]
    [ProducesResponseType(401)]
    [ProducesResponseType(500)]
    public async Task<ActionResult<IEnumerable<ServiceAttendanceDto>>> GetByDate(
        int businessId,
        [FromQuery] DateTime? date = null)
    {
        try
        {
            var targetDate = date ?? DateTime.Today;
            var attendances = await _service.GetByDateAsync(businessId, targetDate);
            return Ok(attendances);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error al obtener asistencias para business {businessId}");
            return StatusCode(500, new { message = "Error interno del servidor" });
        }
    }

    /// <summary>
    /// Registra un check-in usando el plan activo del cliente
    /// </summary>
    [HttpPost("check-in")]
    [Authorize]
    [ProducesResponseType(200)]
    [ProducesResponseType(400)]
    [ProducesResponseType(401)]
    [ProducesResponseType(500)]
    public async Task<ActionResult<CheckInResultDto>> CheckIn([FromBody] CheckInAttendanceDto dto)
    {
        try
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            // Obtener el userId del token JWT
            var userIdClaim = User.FindFirst("UserId") ?? User.FindFirst("sub") ?? User.FindFirst("id");
            if (!int.TryParse(userIdClaim?.Value, out var userId))
            {
                return Unauthorized(new { message = "No se pudo identificar al usuario" });
            }

            var result = await _service.CheckInAsync(dto, userId);

            // Si el resultado indica que se requiere pago, retornar 402
            if (!result.Success && result.ResultType == "no_plan")
            {
                return StatusCode(402, result);
            }

            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error en check-in");
            return StatusCode(500, new { message = "Error interno del servidor" });
        }
    }

    /// <summary>
    /// Registra una asistencia con pago directo (sin plan)
    /// </summary>
    [HttpPost("paid")]
    [Authorize]
    [ProducesResponseType(200)]
    [ProducesResponseType(400)]
    [ProducesResponseType(401)]
    [ProducesResponseType(500)]
    public async Task<ActionResult<CheckInResultDto>> RegisterPaidAttendance([FromBody] PaidAttendanceDto dto)
    {
        try
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var userIdClaim = User.FindFirst("UserId") ?? User.FindFirst("sub") ?? User.FindFirst("id");
            if (!int.TryParse(userIdClaim?.Value, out var userId))
            {
                return Unauthorized(new { message = "No se pudo identificar al usuario" });
            }

            var result = await _service.RegisterPaidAttendanceAsync(dto, userId);
            return Ok(result);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al registrar asistencia pagada");
            return StatusCode(500, new { message = "Error interno del servidor" });
        }
    }

    /// <summary>
    /// Actualiza el estado de una asistencia
    /// </summary>
    [HttpPatch("{id}/status")]
    [Authorize]
    [ProducesResponseType(200)]
    [ProducesResponseType(400)]
    [ProducesResponseType(404)]
    [ProducesResponseType(401)]
    [ProducesResponseType(500)]
    public async Task<ActionResult<ServiceAttendanceDto>> UpdateStatus(int id, [FromBody] UpdateAttendanceStatusDto dto)
    {
        try
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var attendance = await _service.UpdateStatusAsync(id, dto);
            return Ok(attendance);
        }
        catch (InvalidOperationException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error al actualizar estado de asistencia {id}");
            return StatusCode(500, new { message = "Error interno del servidor" });
        }
    }

    /// <summary>
    /// Elimina una asistencia (revierte el consumo de clase del plan)
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
            await _service.DeleteAsync(id);
            return NoContent();
        }
        catch (InvalidOperationException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error al eliminar asistencia {id}");
            return StatusCode(500, new { message = "Error interno del servidor" });
        }
    }

    /// <summary>
    /// Obtiene reporte de ocupación de un servicio
    /// </summary>
    [HttpGet("service/{serviceId}/occupancy")]
    [Authorize]
    [ProducesResponseType(200)]
    [ProducesResponseType(401)]
    [ProducesResponseType(500)]
    public async Task<ActionResult<ClassOccupancyReportDto>> GetOccupancyReport(
        int serviceId,
        [FromQuery] DateTime? startDate = null,
        [FromQuery] DateTime? endDate = null)
    {
        try
        {
            var start = startDate ?? DateTime.Today.AddMonths(-1);
            var end = endDate ?? DateTime.Today;

            var report = await _service.GetClassOccupancyReportAsync(serviceId, start, end);
            return Ok(report);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error al obtener reporte de ocupación para servicio {serviceId}");
            return StatusCode(500, new { message = "Error interno del servidor" });
        }
    }
}
