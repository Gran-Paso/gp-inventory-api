using GPInventory.Application.DTOs.Payments;
using GPInventory.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GPInventory.Api.Controllers;

[ApiController]
[Route("api/payment-plans")]
[Authorize]
public class PaymentPlansController : ControllerBase
{
    private readonly IPaymentPlanService _paymentPlanService;
    private readonly ILogger<PaymentPlansController> _logger;

    public PaymentPlansController(
        IPaymentPlanService paymentPlanService, 
        ILogger<PaymentPlansController> logger)
    {
        _paymentPlanService = paymentPlanService;
        _logger = logger;
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(int id)
    {
        try
        {
            var paymentPlan = await _paymentPlanService.GetPaymentPlanByIdAsync(id);
            return Ok(paymentPlan);
        }
        catch (KeyNotFoundException ex)
        {
            _logger.LogWarning(ex, "Payment plan not found: {Id}", id);
            return NotFound(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving payment plan: {Id}", id);
            return StatusCode(500, new { message = "Error al obtener el plan de pago" });
        }
    }

    [HttpGet("fixed-expense/{fixedExpenseId}")]
    public async Task<IActionResult> GetByFixedExpense(int fixedExpenseId)
    {
        try
        {
            var paymentPlans = await _paymentPlanService.GetPaymentPlansByFixedExpenseAsync(fixedExpenseId);
            return Ok(paymentPlans);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving payment plans for fixed expense: {FixedExpenseId}", fixedExpenseId);
            return StatusCode(500, new { message = "Error al obtener los planes de pago" });
        }
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreatePaymentPlanDto createDto)
    {
        try
        {
            // Log detallado de lo que recibe el backend
            Console.WriteLine($"[PaymentPlan CREATE] Received DTO:");
            Console.WriteLine($"  ExpenseId: {createDto.ExpenseId}");
            Console.WriteLine($"  FixedExpenseId: {createDto.FixedExpenseId}");
            Console.WriteLine($"  PaymentTypeId: {createDto.PaymentTypeId}");
            Console.WriteLine($"  ExpressedInUf: {createDto.ExpressedInUf}");
            Console.WriteLine($"  BankEntityId: {createDto.BankEntityId}");
            Console.WriteLine($"  InstallmentsCount: {createDto.InstallmentsCount}");
            Console.WriteLine($"  StartDate: {createDto.StartDate}");
            
            if (!ModelState.IsValid)
            {
                Console.WriteLine($"[PaymentPlan CREATE] ModelState is invalid:");
                foreach (var error in ModelState)
                {
                    Console.WriteLine($"  {error.Key}: {string.Join(", ", error.Value.Errors.Select(e => e.ErrorMessage))}");
                }
                return BadRequest(ModelState);
            }

            var paymentPlan = await _paymentPlanService.CreatePaymentPlanAsync(createDto);
            Console.WriteLine($"[PaymentPlan CREATE] Created successfully with ID: {paymentPlan.Id}");
            
            return CreatedAtAction(nameof(GetById), new { id = paymentPlan.Id }, paymentPlan);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[PaymentPlan CREATE] ERROR: {ex.Message}");
            Console.WriteLine($"[PaymentPlan CREATE] Stack trace: {ex.StackTrace}");
            _logger.LogError(ex, "Error creating payment plan");
            return StatusCode(500, new { message = "Error al crear el plan de pago" });
        }
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(int id)
    {
        try
        {
            await _paymentPlanService.DeletePaymentPlanAsync(id);
            return NoContent();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting payment plan: {Id}", id);
            return StatusCode(500, new { message = "Error al eliminar el plan de pago" });
        }
    }
}
