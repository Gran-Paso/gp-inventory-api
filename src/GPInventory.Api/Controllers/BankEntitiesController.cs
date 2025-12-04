using GPInventory.Application.DTOs.Payments;
using GPInventory.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GPInventory.Api.Controllers;

[ApiController]
[Route("api/bank-entities")]
[Authorize]
public class BankEntitiesController : ControllerBase
{
    private readonly IPaymentCatalogService _catalogService;
    private readonly ILogger<BankEntitiesController> _logger;

    public BankEntitiesController(
        IPaymentCatalogService catalogService, 
        ILogger<BankEntitiesController> logger)
    {
        _catalogService = catalogService;
        _logger = logger;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        try
        {
            var bankEntities = await _catalogService.GetBankEntitiesAsync();
            return Ok(bankEntities);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving bank entities");
            return StatusCode(500, new { message = "Error al obtener las entidades bancarias" });
        }
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateBankEntityDto createDto)
    {
        try
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var bankEntity = await _catalogService.CreateBankEntityAsync(createDto);
            return CreatedAtAction(nameof(GetAll), new { id = bankEntity.Id }, bankEntity);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating bank entity");
            return StatusCode(500, new { message = "Error al crear la entidad bancaria" });
        }
    }
}
