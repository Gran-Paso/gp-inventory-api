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
public class ClientServicePlansController : ControllerBase
{
    private readonly IClientServicePlanService _service;
    private readonly ILogger<ClientServicePlansController> _logger;

    public ClientServicePlansController(IClientServicePlanService service, ILogger<ClientServicePlansController> logger)
    {
        _service = service;
        _logger = logger;
    }

    /// <summary>
    /// Obtiene un plan de cliente por ID
    /// </summary>
    [HttpGet("{id}")]
    [Authorize]
    [ProducesResponseType(200)]
    [ProducesResponseType(404)]
    [ProducesResponseType(401)]
    [ProducesResponseType(500)]
    public async Task<ActionResult<ClientServicePlanDto>> GetById(int id)
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
            _logger.LogError(ex, $"Error al obtener plan de cliente {id}");
            return StatusCode(500, new { message = "Error interno del servidor" });
        }
    }

    /// <summary>
    /// Obtiene todos los planes de un cliente
    /// </summary>
    [HttpGet("client/{clientId}")]
    [Authorize]
    [ProducesResponseType(200)]
    [ProducesResponseType(401)]
    [ProducesResponseType(500)]
    public async Task<ActionResult<IEnumerable<ClientServicePlanDto>>> GetByClient(int clientId)
    {
        try
        {
            var plans = await _service.GetByClientAsync(clientId);
            return Ok(plans);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error al obtener planes del cliente {clientId}");
            return StatusCode(500, new { message = "Error interno del servidor" });
        }
    }

    /// <summary>
    /// Obtiene los planes activos de un cliente
    /// </summary>
    [HttpGet("client/{clientId}/active")]
    [Authorize]
    [ProducesResponseType(200)]
    [ProducesResponseType(401)]
    [ProducesResponseType(500)]
    public async Task<ActionResult<IEnumerable<ClientServicePlanDto>>> GetActiveByClient(int clientId)
    {
        try
        {
            var plans = await _service.GetActiveByClientAsync(clientId);
            return Ok(plans);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error al obtener planes activos del cliente {clientId}");
            return StatusCode(500, new { message = "Error interno del servidor" });
        }
    }

    /// <summary>
    /// Obtiene todos los planes activos de un negocio
    /// </summary>
    [HttpGet("business/{businessId}/active")]
    [Authorize]
    [ProducesResponseType(200)]
    [ProducesResponseType(401)]
    [ProducesResponseType(500)]
    public async Task<ActionResult<IEnumerable<ClientServicePlanDto>>> GetActiveByBusiness(int businessId)
    {
        try
        {
            var plans = await _service.GetActiveByBusinessAsync(businessId);
            return Ok(plans);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error al obtener planes activos para business {businessId}");
            return StatusCode(500, new { message = "Error interno del servidor" });
        }
    }

    /// <summary>
    /// Obtiene planes por vencer (parámetro: días de anticipación)
    /// </summary>
    [HttpGet("business/{businessId}/expiring")]
    [Authorize]
    [ProducesResponseType(200)]
    [ProducesResponseType(401)]
    [ProducesResponseType(500)]
    public async Task<ActionResult<IEnumerable<ClientServicePlanDto>>> GetExpiringPlans(int businessId, [FromQuery] int days = 7)
    {
        try
        {
            var plans = await _service.GetExpiringPlansAsync(businessId, days);
            return Ok(plans);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error al obtener planes por vencer para business {businessId}");
            return StatusCode(500, new { message = "Error interno del servidor" });
        }
    }

    /// <summary>
    /// Obtiene planes de alto riesgo (pocos clases, próximos a vencer)
    /// </summary>
    [HttpGet("business/{businessId}/high-risk")]
    [Authorize]
    [ProducesResponseType(200)]
    [ProducesResponseType(401)]
    [ProducesResponseType(500)]
    public async Task<ActionResult<IEnumerable<ClientServicePlanDto>>> GetHighRiskPlans(int businessId)
    {
        try
        {
            var plans = await _service.GetHighRiskPlansAsync(businessId);
            return Ok(plans);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error al obtener planes de alto riesgo para business {businessId}");
            return StatusCode(500, new { message = "Error interno del servidor" });
        }
    }

    /// <summary>
    /// Compra un plan para un cliente
    /// </summary>
    [HttpPost("purchase")]
    [Authorize]
    [ProducesResponseType(201)]
    [ProducesResponseType(400)]
    [ProducesResponseType(401)]
    [ProducesResponseType(500)]
    public async Task<ActionResult<ClientServicePlanDto>> PurchasePlan([FromBody] PurchaseServicePlanDto dto)
    {
        try
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            // Obtener el userId del token JWT (mismo orden que HrAuthorizeAttribute)
            var userIdClaim = User.FindFirst("sub")
                           ?? User.FindFirst("user_id")
                           ?? User.FindFirst("userId")
                           ?? User.FindFirst("id")
                           ?? User.FindFirst(ClaimTypes.NameIdentifier);

            _logger.LogDebug("[PurchasePlan] Claims en token: {Claims}",
                string.Join(", ", User.Claims.Select(c => $"{c.Type}={c.Value}")));

            if (!int.TryParse(userIdClaim?.Value, out var userId))
            {
                _logger.LogWarning("[PurchasePlan] No se pudo extraer userId. Claim encontrado: {Claim}", userIdClaim?.Type ?? "ninguno");
                return Unauthorized(new { message = "No se pudo identificar al usuario" });
            }

            _logger.LogDebug("[PurchasePlan] DTO recibido: ClientId={ClientId}, PlanId={PlanId}, Notes={Notes}",
                dto.ClientId, dto.PlanId, dto.Notes);
            _logger.LogDebug("[PurchasePlan] UserId extraído del token: {UserId}", userId);

            var plan = await _service.PurchasePlanAsync(dto, userId);

            _logger.LogDebug("[PurchasePlan] Plan creado exitosamente: Id={Id}, Status={Status}, StartDate={StartDate}, EndDate={EndDate}, TotalClasses={TotalClasses}",
                plan.Id, plan.Status, plan.StartDate, plan.EndDate, plan.TotalClasses);

            return CreatedAtAction(nameof(GetById), new { id = plan.Id }, plan);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al comprar plan");
            return StatusCode(500, new { message = "Error interno del servidor" });
        }
    }

    /// <summary>
    /// Congela un plan (no descuenta días mientras está congelado)
    /// </summary>
    [HttpPatch("{id}/freeze")]
    [Authorize]
    [ProducesResponseType(200)]
    [ProducesResponseType(400)]
    [ProducesResponseType(404)]
    [ProducesResponseType(401)]
    [ProducesResponseType(500)]
    public async Task<ActionResult<ClientServicePlanDto>> FreezePlan(int id, [FromBody] FreezePlanDto dto)
    {
        try
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var plan = await _service.FreezePlanAsync(id, dto);
            return Ok(plan);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error al congelar plan {id}");
            return StatusCode(500, new { message = "Error interno del servidor" });
        }
    }

    /// <summary>
    /// Descongela un plan
    /// </summary>
    [HttpPatch("{id}/unfreeze")]
    [Authorize]
    [ProducesResponseType(200)]
    [ProducesResponseType(404)]
    [ProducesResponseType(401)]
    [ProducesResponseType(500)]
    public async Task<ActionResult<ClientServicePlanDto>> UnfreezePlan(int id)
    {
        try
        {
            var plan = await _service.UnfreezePlanAsync(id);
            return Ok(plan);
        }
        catch (InvalidOperationException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error al descongelar plan {id}");
            return StatusCode(500, new { message = "Error interno del servidor" });
        }
    }

    /// <summary>
    /// Cancela un plan
    /// </summary>
    [HttpPatch("{id}/cancel")]
    [Authorize]
    [ProducesResponseType(200)]
    [ProducesResponseType(400)]
    [ProducesResponseType(404)]
    [ProducesResponseType(401)]
    [ProducesResponseType(500)]
    public async Task<ActionResult<ClientServicePlanDto>> CancelPlan(int id, [FromBody] CancelPlanDto dto)
    {
        try
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var plan = await _service.CancelPlanAsync(id, dto);
            return Ok(plan);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error al cancelar plan {id}");
            return StatusCode(500, new { message = "Error interno del servidor" });
        }
    }

    /// <summary>
    /// Expira manualmente los planes vencidos (para cron job)
    /// </summary>
    [HttpPost("business/{businessId}/expire-old")]
    [Authorize]
    [ProducesResponseType(200)]
    [ProducesResponseType(401)]
    [ProducesResponseType(500)]
    public async Task<ActionResult<object>> ExpireOldPlans(int businessId)
    {
        try
        {
            await _service.ExpireOldPlansAsync(businessId);
            return Ok(new { message = "Planes expirados procesados" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error al expirar planes para business {businessId}");
            return StatusCode(500, new { message = "Error interno del servidor" });
        }
    }

    /// <summary>
    /// Obtiene el dashboard de un cliente (planes activos + asistencias recientes)
    /// </summary>
    [HttpGet("client/{clientId}/dashboard")]
    [Authorize]
    [ProducesResponseType(200)]
    [ProducesResponseType(401)]
    [ProducesResponseType(500)]
    public async Task<ActionResult<ClientDashboardDto>> GetClientDashboard(int clientId)
    {
        try
        {
            var dashboard = await _service.GetClientDashboardAsync(clientId);
            return Ok(dashboard);
        }
        catch (InvalidOperationException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error al obtener dashboard del cliente {clientId}");
            return StatusCode(500, new { message = "Error interno del servidor" });
        }
    }

    /// <summary>
    /// Obtiene el reporte de ingresos diferidos de un negocio
    /// </summary>
    [HttpGet("business/{businessId}/deferred-revenue")]
    [Authorize]
    [ProducesResponseType(200)]
    [ProducesResponseType(401)]
    [ProducesResponseType(500)]
    public async Task<ActionResult<DeferredRevenueReportDto>> GetDeferredRevenueReport(int businessId)
    {
        try
        {
            var report = await _service.GetDeferredRevenueReportAsync(businessId);
            return Ok(report);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error al obtener reporte de ingresos diferidos para business {businessId}");
            return StatusCode(500, new { message = "Error interno del servidor" });
        }
    }

    /// <summary>
    /// Registra una matrícula grupal (combo): varios beneficiarios, un solo contrato con precio negociado
    /// </summary>
    [HttpPost("purchase-group")]
    [Authorize]
    [ProducesResponseType(201)]
    [ProducesResponseType(400)]
    [ProducesResponseType(401)]
    [ProducesResponseType(500)]
    public async Task<ActionResult<PlanEnrollmentGroupDto>> PurchaseGroupEnrollment([FromBody] PurchaseGroupEnrollmentDto dto)
    {
        try
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);

            var userIdClaim = User.FindFirst("sub")
                           ?? User.FindFirst("user_id")
                           ?? User.FindFirst("userId")
                           ?? User.FindFirst("id")
                           ?? User.FindFirst(ClaimTypes.NameIdentifier);

            if (!int.TryParse(userIdClaim?.Value, out var userId))
                return Unauthorized(new { message = "No se pudo identificar al usuario" });

            var group = await _service.PurchaseGroupEnrollmentAsync(dto, userId);
            return CreatedAtAction(nameof(GetGroupById), new { groupId = group.Id }, group);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al registrar matrícula grupal");
            return StatusCode(500, new { message = "Error interno del servidor" });
        }
    }

    /// <summary>
    /// Obtiene una matrícula grupal por ID
    /// </summary>
    [HttpGet("group/{groupId}")]
    [Authorize]
    [ProducesResponseType(200)]
    [ProducesResponseType(404)]
    [ProducesResponseType(401)]
    [ProducesResponseType(500)]
    public async Task<ActionResult<PlanEnrollmentGroupDto>> GetGroupById(int groupId)
    {
        try
        {
            var group = await _service.GetGroupByIdAsync(groupId);
            return Ok(group);
        }
        catch (InvalidOperationException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error al obtener grupo {groupId}");
            return StatusCode(500, new { message = "Error interno del servidor" });
        }
    }

    /// <summary>
    /// Obtiene todas las matrículas grupales de un cliente pagador
    /// </summary>
    [HttpGet("client/{clientId}/groups")]
    [Authorize]
    [ProducesResponseType(200)]
    [ProducesResponseType(401)]
    [ProducesResponseType(500)]
    public async Task<ActionResult<IEnumerable<PlanEnrollmentGroupDto>>> GetGroupsByPayer(int clientId)
    {
        try
        {
            var groups = await _service.GetGroupsByPayerAsync(clientId);
            return Ok(groups);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error al obtener grupos del cliente {clientId}");
            return StatusCode(500, new { message = "Error interno del servidor" });
        }
    }
}
