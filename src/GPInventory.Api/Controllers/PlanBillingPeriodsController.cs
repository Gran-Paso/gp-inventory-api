using GPInventory.Application.DTOs.Services;
using GPInventory.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Cors;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace GPInventory.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[EnableCors("AllowFrontend")]
public class PlanBillingPeriodsController : ControllerBase
{
    private readonly IPlanBillingPeriodService _service;
    private readonly ILogger<PlanBillingPeriodsController> _logger;

    public PlanBillingPeriodsController(
        IPlanBillingPeriodService service,
        ILogger<PlanBillingPeriodsController> logger)
    {
        _service = service;
        _logger  = logger;
    }

    // ── Queries ───────────────────────────────────────────────────────────────

    /// <summary>
    /// Obtiene todos los períodos de facturación de un plan de cliente.
    /// </summary>
    [HttpGet("plan/{clientServicePlanId}")]
    [Authorize]
    public async Task<ActionResult<IEnumerable<PlanBillingPeriodDto>>> GetByPlan(int clientServicePlanId)
    {
        try
        {
            var periods = await _service.GetByPlanAsync(clientServicePlanId);
            return Ok(periods);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al obtener períodos del plan {PlanId}", clientServicePlanId);
            return StatusCode(500, new { message = "Error interno del servidor" });
        }
    }

    /// <summary>
    /// Obtiene todos los períodos de facturación de un cliente (todos sus planes).
    /// </summary>
    [HttpGet("client/{serviceClientId}")]
    [Authorize]
    public async Task<ActionResult<IEnumerable<PlanBillingPeriodDto>>> GetByClient(int serviceClientId)
    {
        try
        {
            var periods = await _service.GetByClientAsync(serviceClientId);
            return Ok(periods);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al obtener períodos del cliente {ClientId}", serviceClientId);
            return StatusCode(500, new { message = "Error interno del servidor" });
        }
    }

    /// <summary>
    /// Obtiene los períodos pendientes/vencidos de cobro para un negocio.
    /// </summary>
    [HttpGet("business/{businessId}/pending")]
    [Authorize]
    public async Task<ActionResult<IEnumerable<PendingBillingPeriodDto>>> GetPendingByBusiness(int businessId)
    {
        try
        {
            var periods = await _service.GetPendingByBusinessAsync(businessId);
            return Ok(periods);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al obtener períodos pendientes para business {BusinessId}", businessId);
            return StatusCode(500, new { message = "Error interno del servidor" });
        }
    }

    /// <summary>
    /// Obtiene un período de facturación por su ID.
    /// </summary>
    [HttpGet("{id}")]
    [Authorize]
    public async Task<ActionResult<PlanBillingPeriodDto>> GetById(int id)
    {
        try
        {
            var period = await _service.GetByIdAsync(id);
            return Ok(period);
        }
        catch (InvalidOperationException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al obtener período {PeriodId}", id);
            return StatusCode(500, new { message = "Error interno del servidor" });
        }
    }

    // ── Mutations ─────────────────────────────────────────────────────────────

    /// <summary>
    /// Crea un nuevo período de facturación mensual para un plan.
    /// Si el período ya existe para ese mes, devuelve el existente.
    /// </summary>
    [HttpPost]
    [Authorize]
    public async Task<ActionResult<PlanBillingPeriodDto>> CreatePeriod([FromBody] CreateBillingPeriodDto dto)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        try
        {
            var userId = ExtractUserId();
            if (userId == null) return Unauthorized(new { message = "No se pudo identificar al usuario" });

            var period = await _service.CreatePeriodAsync(dto, userId.Value);
            return StatusCode(201, period);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al crear período de facturación");
            return StatusCode(500, new { message = "Error interno del servidor" });
        }
    }

    /// <summary>
    /// Registra el pago de un período: crea la transacción financiera y
    /// marca el período como pagado.
    /// </summary>
    [HttpPost("{id}/pay")]
    [Authorize]
    public async Task<ActionResult<PlanBillingPeriodDto>> PayPeriod(
        int id, [FromBody] PayBillingPeriodDto dto)
    {
        try
        {
            var userId = ExtractUserId();
            if (userId == null) return Unauthorized(new { message = "No se pudo identificar al usuario" });

            var period = await _service.PayPeriodAsync(id, dto, userId.Value);
            return Ok(period);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al registrar pago del período {PeriodId}", id);
            return StatusCode(500, new { message = "Error interno del servidor" });
        }
    }

    /// <summary>
    /// Recalcula sessions_attended y sessions_reserved leyendo service_attendance.
    /// Útil para sincronizar si hubo cambios manuales en asistencias.
    /// </summary>
    [HttpPost("{id}/recalculate")]
    [Authorize]
    public async Task<ActionResult<PlanBillingPeriodDto>> Recalculate(int id)
    {
        try
        {
            var period = await _service.RecalculateAttendanceAsync(id);
            return Ok(period);
        }
        catch (InvalidOperationException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al recalcular período {PeriodId}", id);
            return StatusCode(500, new { message = "Error interno del servidor" });
        }
    }

    /// <summary>
    /// Marca todos los períodos vencidos (pending + due_date en el pasado) como 'overdue'
    /// para un negocio. Puede llamarse desde un cron job.
    /// </summary>
    [HttpPost("business/{businessId}/mark-overdue")]
    [Authorize]
    public async Task<ActionResult> MarkOverdue(int businessId)
    {
        try
        {
            await _service.MarkOverdueAsync(businessId);
            return Ok(new { message = "Períodos vencidos actualizados" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al marcar vencidos para business {BusinessId}", businessId);
            return StatusCode(500, new { message = "Error interno del servidor" });
        }
    }

    /// <summary>
    /// Condona/perdona el cobro de un período (status = waived).
    /// </summary>
    [HttpPost("{id}/waive")]
    [Authorize]
    public async Task<ActionResult<PlanBillingPeriodDto>> Waive(int id, [FromBody] WaivePeriodDto dto)
    {
        try
        {
            var period = await _service.WaivePeriodAsync(id, dto.Reason);
            return Ok(period);
        }
        catch (InvalidOperationException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al condonar período {PeriodId}", id);
            return StatusCode(500, new { message = "Error interno del servidor" });
        }
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private int? ExtractUserId()
    {
        var claim = User.FindFirst("sub")
                 ?? User.FindFirst("user_id")
                 ?? User.FindFirst("userId")
                 ?? User.FindFirst("id")
                 ?? User.FindFirst(ClaimTypes.NameIdentifier);

        return int.TryParse(claim?.Value, out var userId) ? userId : null;
    }
}

/// <summary>DTO para condonar un período</summary>
public class WaivePeriodDto
{
    public string? Reason { get; set; }
}
