using GPInventory.Application.DTOs.Payments;
using GPInventory.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GPInventory.Api.Controllers;

[ApiController]
[Route("api/receipt-types")]
[Authorize]
public class ReceiptTypesController : ControllerBase
{
    private readonly IPaymentCatalogService _catalogService;
    private readonly ILogger<ReceiptTypesController> _logger;

    public ReceiptTypesController(
        IPaymentCatalogService catalogService, 
        ILogger<ReceiptTypesController> logger)
    {
        _catalogService = catalogService;
        _logger = logger;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        try
        {
            var receiptTypes = await _catalogService.GetReceiptTypesAsync();
            return Ok(receiptTypes);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving receipt types");
            return StatusCode(500, new { message = "Error al obtener los tipos de documento" });
        }
    }
}
