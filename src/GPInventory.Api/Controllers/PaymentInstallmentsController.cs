using GPInventory.Application.DTOs.Payments;
using GPInventory.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GPInventory.Api.Controllers;

[ApiController]
[Route("api/payment-installments")]
[Authorize]
public class PaymentInstallmentsController : ControllerBase
{
    private readonly IPaymentInstallmentService _installmentService;
    private readonly ILogger<PaymentInstallmentsController> _logger;

    public PaymentInstallmentsController(
        IPaymentInstallmentService installmentService, 
        ILogger<PaymentInstallmentsController> logger)
    {
        _installmentService = installmentService;
        _logger = logger;
    }

    [HttpGet("payment-plan/{paymentPlanId}")]
    public async Task<IActionResult> GetByPaymentPlan(int paymentPlanId)
    {
        try
        {
            var installments = await _installmentService.GetInstallmentsByPaymentPlanAsync(paymentPlanId);
            return Ok(installments);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving installments for payment plan: {PaymentPlanId}", paymentPlanId);
            return StatusCode(500, new { message = "Error al obtener las cuotas" });
        }
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateInstallmentDto createDto)
    {
        try
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var installment = await _installmentService.CreateInstallmentAsync(createDto);
            return Ok(installment);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating installment");
            return StatusCode(500, new { message = "Error al crear la cuota" });
        }
    }

    [HttpPost("bulk")]
    public async Task<IActionResult> CreateBulk([FromBody] CreateInstallmentsBulkDto createDto)
    {
        try
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            // Log para debug
            _logger.LogInformation($"Creating {createDto.Installments.Count} installments");
            foreach (var inst in createDto.Installments)
            {
                _logger.LogInformation($"Installment #{inst.InstallmentNumber}: PaymentPlanId={inst.PaymentPlanId}, Amount={inst.AmountClp}");
            }

            var installments = await _installmentService.CreateInstallmentsBulkAsync(createDto);
            return Ok(installments);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating installments in bulk");
            return StatusCode(500, new { message = "Error al crear las cuotas" });
        }
    }

    [HttpPatch("{id}/status")]
    public async Task<IActionResult> UpdateStatus(int id, [FromBody] UpdateInstallmentStatusDto updateDto)
    {
        try
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var installment = await _installmentService.UpdateInstallmentStatusAsync(id, updateDto);
            return Ok(installment);
        }
        catch (KeyNotFoundException ex)
        {
            _logger.LogWarning(ex, "Installment not found: {Id}", id);
            return NotFound(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating installment status: {Id}", id);
            return StatusCode(500, new { message = "Error al actualizar el estado de la cuota" });
        }
    }

    [HttpPut("{id}/pay")]
    public async Task<IActionResult> PayInstallment(int id, [FromBody] PayInstallmentDto payDto)
    {
        try
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            _logger.LogInformation($"Paying installment {id} with payment method {payDto.PaymentMethodId}");

            var installment = await _installmentService.PayInstallmentAsync(id, payDto);
            
            return Ok(new
            {
                message = "Cuota pagada exitosamente",
                installment
            });
        }
        catch (KeyNotFoundException ex)
        {
            _logger.LogWarning(ex, "Installment not found: {Id}", id);
            return NotFound(new { message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "Invalid operation on installment: {Id}", id);
            return BadRequest(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error paying installment: {Id}", id);
            return StatusCode(500, new { message = $"Error al pagar la cuota: {ex.Message}" });
        }
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(int id)
    {
        try
        {
            await _installmentService.DeleteInstallmentAsync(id);
            return NoContent();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting installment: {Id}", id);
            return StatusCode(500, new { message = "Error al eliminar la cuota" });
        }
    }

    [HttpGet("summary")]
    public async Task<IActionResult> GetSummary([FromQuery] List<int>? businessIds = null)
    {
        try
        {
            var summary = await _installmentService.GetInstallmentsSummaryAsync(businessIds);
            return Ok(summary);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting installments summary");
            return StatusCode(500, new { message = "Error al obtener el resumen de cuotas" });
        }
    }
}
