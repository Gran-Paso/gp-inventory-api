using GPInventory.Application.DTOs.Production;
using GPInventory.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GPInventory.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class SupplyEntriesController : ControllerBase
{
    private readonly ISupplyEntryService _supplyEntryService;

    public SupplyEntriesController(ISupplyEntryService supplyEntryService)
    {
        _supplyEntryService = supplyEntryService;
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<SupplyEntryDto>>> GetAllSupplyEntries()
    {
        try
        {
            var supplyEntries = await _supplyEntryService.GetAllAsync();
            return Ok(supplyEntries);
        }
        catch (Exception ex)
        {
            return StatusCode(500, $"Error retrieving supply entries: {ex.Message}");
        }
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<SupplyEntryDto>> GetSupplyEntry(int id)
    {
        try
        {
            var supplyEntry = await _supplyEntryService.GetByIdAsync(id);
            if (supplyEntry == null)
            {
                return NotFound($"Supply entry with ID {id} not found");
            }
            return Ok(supplyEntry);
        }
        catch (Exception ex)
        {
            return StatusCode(500, $"Error retrieving supply entry: {ex.Message}");
        }
    }

    [HttpGet("by-supply/{supplyId}")]
    public async Task<ActionResult<IEnumerable<SupplyEntryDto>>> GetSupplyEntriesBySupplyId(int supplyId)
    {
        try
        {
            var supplyEntries = await _supplyEntryService.GetBySupplyIdAsync(supplyId);
            return Ok(supplyEntries);
        }
        catch (Exception ex)
        {
            return StatusCode(500, $"Error retrieving supply entries: {ex.Message}");
        }
    }

    [HttpGet("by-process/{processDoneId}")]
    public async Task<ActionResult<IEnumerable<SupplyEntryDto>>> GetSupplyEntriesByProcessDoneId(int processDoneId)
    {
        try
        {
            var supplyEntries = await _supplyEntryService.GetByProcessDoneIdAsync(processDoneId);
            return Ok(supplyEntries);
        }
        catch (Exception ex)
        {
            return StatusCode(500, $"Error retrieving supply entries: {ex.Message}");
        }
    }

    [HttpGet("stock/{supplyId}")]
    public async Task<ActionResult<SupplyStockDto>> GetSupplyStock(int supplyId)
    {
        try
        {
            var stock = await _supplyEntryService.GetSupplyStockAsync(supplyId);
            if (stock == null)
            {
                return NotFound($"Supply with ID {supplyId} not found");
            }
            return Ok(stock);
        }
        catch (Exception ex)
        {
            return StatusCode(500, $"Error retrieving supply stock: {ex.Message}");
        }
    }

    [HttpGet("stocks")]
    public async Task<ActionResult<IEnumerable<SupplyStockDto>>> GetAllSupplyStocks([FromQuery] int? businessId = null)
    {
        try
        {
            var stocks = await _supplyEntryService.GetAllSupplyStocksAsync(businessId);
            return Ok(stocks);
        }
        catch (Exception ex)
        {
            return StatusCode(500, $"Error retrieving supply stocks: {ex.Message}");
        }
    }

    [HttpGet("history/{supplyId}")]
    public async Task<ActionResult<IEnumerable<SupplyEntryDto>>> GetSupplyHistory(int supplyId, [FromQuery] int supplyEntryId)
    {
        try
        {
            var history = await _supplyEntryService.GetSupplyHistoryAsync(supplyEntryId, supplyId);
            return Ok(history);
        }
        catch (Exception ex)
        {
            return StatusCode(500, $"Error retrieving supply history: {ex.Message}");
        }
    }

    [HttpPost]
    public async Task<ActionResult<SupplyEntryDto>> CreateSupplyEntry([FromBody] CreateSupplyEntryDto createSupplyEntryDto)
    {
        try
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var supplyEntry = await _supplyEntryService.CreateAsync(createSupplyEntryDto);
            return CreatedAtAction(nameof(GetSupplyEntry), new { id = supplyEntry.Id }, supplyEntry);
        }
        catch (Exception ex)
        {
            return StatusCode(500, $"Error creating supply entry: {ex.Message}");
        }
    }

    [HttpPut("{id}")]
    public async Task<ActionResult<SupplyEntryDto>> UpdateSupplyEntry(int id, [FromBody] UpdateSupplyEntryDto updateSupplyEntryDto)
    {
        try
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var supplyEntry = await _supplyEntryService.UpdateAsync(id, updateSupplyEntryDto);
            return Ok(supplyEntry);
        }
        catch (InvalidOperationException ex)
        {
            return NotFound(ex.Message);
        }
        catch (Exception ex)
        {
            return StatusCode(500, $"Error updating supply entry: {ex.Message}");
        }
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteSupplyEntry(int id)
    {
        try
        {
            await _supplyEntryService.DeleteAsync(id);
            return NoContent();
        }
        catch (Exception ex)
        {
            return StatusCode(500, $"Error deleting supply entry: {ex.Message}");
        }
    }
}
