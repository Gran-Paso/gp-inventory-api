using GPInventory.Application.DTOs.Production;
using GPInventory.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Cors;
using Microsoft.AspNetCore.Mvc;

namespace GPInventory.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
[EnableCors("AllowFrontend")]
public class SuppliesController : ControllerBase
{
    private readonly ISupplyService _supplyService;

    public SuppliesController(ISupplyService supplyService)
    {
        _supplyService = supplyService;
    }

    /// <summary>
    /// Get all supplies
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<IEnumerable<SupplyDto>>> GetSupplies()
    {
        try
        {
            var supplies = await _supplyService.GetAllSuppliesAsync();
            return Ok(supplies);
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { message = "Error retrieving supplies", error = ex.Message });
        }
    }

    /// <summary>
    /// Get supply by ID
    /// </summary>
    [HttpGet("{id}")]
    public async Task<ActionResult<SupplyDto>> GetSupply(int id)
    {
        try
        {
            var supply = await _supplyService.GetSupplyByIdAsync(id);
            return Ok(supply);
        }
        catch (ArgumentException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { message = "Error retrieving supply", error = ex.Message });
        }
    }

    /// <summary>
    /// Get supplies by business ID
    /// </summary>
    [HttpGet("business/{businessId}")]
    public async Task<ActionResult<IEnumerable<SupplyDto>>> GetSuppliesByBusiness(int businessId)
    {
        try
        {
            var supplies = await _supplyService.GetSuppliesByBusinessIdAsync(businessId);
            return Ok(supplies);
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { message = "Error retrieving supplies", error = ex.Message });
        }
    }

    /// <summary>
    /// Get supplies by store ID
    /// </summary>
    [HttpGet("store/{storeId}")]
    public async Task<ActionResult<IEnumerable<SupplyDto>>> GetSuppliesByStore(int storeId)
    {
        try
        {
            var supplies = await _supplyService.GetSuppliesByStoreIdAsync(storeId);
            return Ok(supplies);
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { message = "Error retrieving supplies", error = ex.Message });
        }
    }

    /// <summary>
    /// Get active supplies by business ID
    /// </summary>
    [HttpGet("business/{businessId}/active")]
    public async Task<ActionResult<IEnumerable<SupplyDto>>> GetActiveSupplies(int businessId)
    {
        try
        {
            var supplies = await _supplyService.GetActiveSuppliesAsync(businessId);
            return Ok(supplies);
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { message = "Error retrieving active supplies", error = ex.Message });
        }
    }

    /// <summary>
    /// Create a new supply
    /// </summary>
    [HttpPost]
    public async Task<ActionResult<SupplyDto>> CreateSupply([FromBody] CreateSupplyDto createSupplyDto)
    {
        try
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var supply = await _supplyService.CreateSupplyAsync(createSupplyDto);
            return CreatedAtAction(nameof(GetSupply), new { id = supply.Id }, supply);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { message = "Error creating supply", error = ex.Message });
        }
    }

    /// <summary>
    /// Update an existing supply
    /// </summary>
    [HttpPut("{id}")]
    public async Task<ActionResult<SupplyDto>> UpdateSupply(int id, [FromBody] UpdateSupplyDto updateSupplyDto)
    {
        try
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var supply = await _supplyService.UpdateSupplyAsync(id, updateSupplyDto);
            return Ok(supply);
        }
        catch (ArgumentException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { message = "Error updating supply", error = ex.Message });
        }
    }

    /// <summary>
    /// Delete a supply
    /// </summary>
    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteSupply(int id)
    {
        try
        {
            await _supplyService.DeleteSupplyAsync(id);
            return NoContent();
        }
        catch (ArgumentException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { message = "Error deleting supply", error = ex.Message });
        }
    }

    /// <summary>
    /// Get supplies with details
    /// </summary>
    [HttpGet("details")]
    public async Task<ActionResult<IEnumerable<SupplyDto>>> GetSuppliesWithDetails([FromQuery] int[]? businessIds = null)
    {
        try
        {
            var supplies = await _supplyService.GetSuppliesWithDetailsAsync(businessIds);
            return Ok(supplies);
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { message = "Error retrieving supplies with details", error = ex.Message });
        }
    }
}
