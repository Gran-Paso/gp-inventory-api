using GPInventory.Application.DTOs.Components;
using GPInventory.Application.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace GPInventory.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ComponentsController : ControllerBase
{
    private readonly IComponentService _service;

    public ComponentsController(IComponentService service)
    {
        _service = service;
    }

    /// <summary>
    /// Get all components for a business
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<IEnumerable<ComponentDto>>> GetAll([FromQuery] int businessId, [FromQuery] bool? activeOnly = true)
    {
        if (businessId <= 0)
            return BadRequest("businessId is required");

        var components = await _service.GetAllAsync(businessId, activeOnly);
        return Ok(components);
    }

    /// <summary>
    /// Get component by id
    /// </summary>
    [HttpGet("{id}")]
    public async Task<ActionResult<ComponentDto>> GetById(int id)
    {
        var component = await _service.GetByIdAsync(id);
        if (component == null)
            return NotFound($"Component with id {id} not found");

        return Ok(component);
    }

    /// <summary>
    /// Get component by id with supplies
    /// </summary>
    [HttpGet("{id}/with-supplies")]
    public async Task<ActionResult<ComponentWithSuppliesDto>> GetByIdWithSupplies(int id)
    {
        var component = await _service.GetByIdWithSuppliesAsync(id);
        if (component == null)
            return NotFound($"Component with id {id} not found");

        return Ok(component);
    }

    /// <summary>
    /// Create new component
    /// </summary>
    [HttpPost]
    public async Task<ActionResult<ComponentDto>> Create([FromBody] CreateComponentDto dto)
    {
        try
        {
            var component = await _service.CreateAsync(dto);
            return CreatedAtAction(nameof(GetById), new { id = component.Id }, component);
        }
        catch (Exception ex)
        {
            return BadRequest(ex.Message);
        }
    }

    /// <summary>
    /// Update existing component
    /// </summary>
    [HttpPut("{id}")]
    public async Task<ActionResult<ComponentDto>> Update(int id, [FromBody] UpdateComponentDto dto)
    {
        try
        {
            var component = await _service.UpdateAsync(id, dto);
            return Ok(component);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(ex.Message);
        }
        catch (Exception ex)
        {
            return BadRequest(ex.Message);
        }
    }

    /// <summary>
    /// Delete component (soft delete)
    /// </summary>
    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(int id)
    {
        try
        {
            var deleted = await _service.DeleteAsync(id);
            if (!deleted)
                return NotFound($"Component with id {id} not found");

            return NoContent();
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(ex.Message);
        }
    }

    /// <summary>
    /// Add supplies to component
    /// </summary>
    [HttpPost("{id}/supplies")]
    public async Task<ActionResult<ComponentWithSuppliesDto>> AddSupplies(int id, [FromBody] List<CreateComponentSupplyDto> supplies)
    {
        try
        {
            var component = await _service.AddSuppliesAsync(id, supplies);
            return Ok(component);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(ex.Message);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }
    }

    /// <summary>
    /// Update component supplies (replaces all existing supplies)
    /// </summary>
    [HttpPut("{id}/supplies")]
    public async Task<ActionResult<ComponentWithSuppliesDto>> UpdateSupplies(int id, [FromBody] List<CreateComponentSupplyDto> supplies)
    {
        try
        {
            var component = await _service.UpdateSuppliesAsync(id, supplies);
            return Ok(component);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(ex.Message);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }
    }

    /// <summary>
    /// Remove supply from component
    /// </summary>
    [HttpDelete("{componentId}/supplies/{supplyId}")]
    public async Task<IActionResult> RemoveSupply(int componentId, int supplyId)
    {
        try
        {
            var deleted = await _service.RemoveSupplyAsync(componentId, supplyId);
            if (!deleted)
                return NotFound($"Supply with id {supplyId} not found");

            return NoContent();
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(ex.Message);
        }
    }

    /// <summary>
    /// Get BOM (Bill of Materials) tree for component
    /// </summary>
    [HttpGet("{id}/bom")]
    public async Task<ActionResult<BOMTreeNodeDto>> GetBOMTree(int id)
    {
        try
        {
            var tree = await _service.GetBOMTreeAsync(id);
            return Ok(tree);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(ex.Message);
        }
    }

    /// <summary>
    /// Calculate total cost for component (including sub-components)
    /// </summary>
    [HttpGet("{id}/cost")]
    public async Task<ActionResult<decimal>> CalculateCost(int id)
    {
        var cost = await _service.CalculateTotalCostAsync(id);
        return Ok(cost);
    }

    /// <summary>
    /// Get productions by component
    /// </summary>
    [HttpGet("{id}/productions")]
    public async Task<ActionResult<IEnumerable<ComponentProductionDto>>> GetProductions(int id)
    {
        var productions = await _service.GetProductionsByComponentAsync(id);
        return Ok(productions);
    }

    /// <summary>
    /// Get active productions for a business
    /// </summary>
    [HttpGet("productions/active")]
    public async Task<ActionResult<IEnumerable<ComponentProductionDto>>> GetActiveProductions([FromQuery] int businessId)
    {
        if (businessId <= 0)
            return BadRequest("businessId is required");

        var productions = await _service.GetActiveProductionsAsync(businessId);
        return Ok(productions);
    }

    /// <summary>
    /// Get expiring productions for a business
    /// </summary>
    [HttpGet("productions/expiring")]
    public async Task<ActionResult<IEnumerable<ComponentProductionDto>>> GetExpiringProductions(
        [FromQuery] int businessId, 
        [FromQuery] int daysAhead = 3)
    {
        if (businessId <= 0)
            return BadRequest("businessId is required");

        var productions = await _service.GetExpiringProductionsAsync(businessId, daysAhead);
        return Ok(productions);
    }

    /// <summary>
    /// Create new production
    /// </summary>
    [HttpPost("productions")]
    public async Task<ActionResult<ComponentProductionDto>> CreateProduction([FromBody] Application.DTOs.Components.CreateComponentProductionDto dto)
    {
        try
        {
            var production = await _service.CreateProductionAsync(dto);
            return Ok(production);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(ex.Message);
        }
    }

    /// <summary>
    /// Update production
    /// </summary>
    [HttpPut("productions/{id}")]
    public async Task<ActionResult<ComponentProductionDto>> UpdateProduction(int id, [FromBody] UpdateComponentProductionDto dto)
    {
        try
        {
            var production = await _service.UpdateProductionAsync(id, dto);
            return Ok(production);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(ex.Message);
        }
    }

    /// <summary>
    /// Consume production (decrement produced_amount)
    /// </summary>
    [HttpPost("productions/{id}/consume")]
    public async Task<IActionResult> ConsumeProduction(int id, [FromBody] decimal amountConsumed)
    {
        try
        {
            var consumed = await _service.ConsumeProductionAsync(id, amountConsumed);
            if (!consumed)
                return BadRequest("Failed to consume production");

            return NoContent();
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(ex.Message);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }
    }
}
