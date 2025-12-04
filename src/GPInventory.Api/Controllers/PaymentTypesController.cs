using GPInventory.Application.DTOs.Payments;
using GPInventory.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GPInventory.Api.Controllers;

[ApiController]
[Route("api/payment-types")]
[Authorize]
public class PaymentTypesController : ControllerBase
{
    private readonly IPaymentCatalogService _catalogService;
    private readonly ILogger<PaymentTypesController> _logger;

    public PaymentTypesController(
        IPaymentCatalogService catalogService, 
        ILogger<PaymentTypesController> logger)
    {
        _catalogService = catalogService;
        _logger = logger;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        try
        {
            var paymentTypes = await _catalogService.GetPaymentTypesAsync();
            return Ok(paymentTypes);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving payment types");
            return StatusCode(500, new { message = "Error al obtener los tipos de pago" });
        }
    }
}
