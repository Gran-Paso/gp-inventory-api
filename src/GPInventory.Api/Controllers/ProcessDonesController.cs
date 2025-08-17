using GPInventory.Application.DTOs.Production;
using GPInventory.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Text.Json;

namespace GPInventory.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class ProcessDonesController : ControllerBase
{
    private readonly IProcessDoneService _processDoneService;
    private readonly ILogger<ProcessDonesController> _logger;

    public ProcessDonesController(IProcessDoneService processDoneService, ILogger<ProcessDonesController> logger)
    {
        _processDoneService = processDoneService;
        _logger = logger;
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<ProcessDoneDto>>> GetProcessDones()
    {
        try
        {
            var processDones = await _processDoneService.GetAllProcessDonesAsync();
            return Ok(processDones);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving process dones");
            return StatusCode(500, "Internal server error");
        }
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<ProcessDoneDto>> GetProcessDone(int id)
    {
        try
        {
            var processDone = await _processDoneService.GetProcessDoneByIdAsync(id);
            return Ok(processDone);
        }
        catch (KeyNotFoundException ex)
        {
            _logger.LogWarning(ex, "ProcessDone not found: {ProcessDoneId}", id);
            return NotFound(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving process done {ProcessDoneId}", id);
            return StatusCode(500, "Internal server error");
        }
    }

    [HttpGet("process/{processId}")]
    public async Task<ActionResult<IEnumerable<ProcessDoneDto>>> GetProcessDonesByProcess(int processId)
    {
        try
        {
            var processDones = await _processDoneService.GetProcessDonesByProcessIdAsync(processId);
            return Ok(processDones);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving process dones for process {ProcessId}", processId);
            return StatusCode(500, "Internal server error");
        }
    }

    [HttpPost]
    public async Task<ActionResult<ProcessDoneDto>> CreateProcessDone([FromBody] CreateProcessDoneDto createProcessDoneDto)
    {
        try
        {
            var processDone = await _processDoneService.CreateProcessDoneAsync(createProcessDoneDto);
            return CreatedAtAction(nameof(GetProcessDone), new { id = processDone.Id }, processDone);
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "Invalid operation when creating process done");
            return BadRequest(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating process done");
            return StatusCode(500, "Internal server error");
        }
    }

    [HttpPut("{id}/stage")]
    public async Task<ActionResult<ProcessDoneDto>> UpdateProcessDoneStage(int id, [FromBody] int stage)
    {
        try
        {
            var processDone = await _processDoneService.UpdateProcessDoneStageAsync(id, stage);
            return Ok(processDone);
        }
        catch (KeyNotFoundException ex)
        {
            _logger.LogWarning(ex, "ProcessDone not found when updating stage");
            return NotFound(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating process done stage");
            return StatusCode(500, "Internal server error");
        }
    }

    [HttpPut("{id}/amount")]
    public async Task<ActionResult<ProcessDoneDto>> UpdateProcessDoneAmount(int id, [FromBody] object amountData, [FromQuery] bool isLastSupply = false)
    {
        try
        {
            int amount;
            
            // Try to parse as object first, then as direct value
            if (amountData is JsonElement jsonElement)
            {
                if (jsonElement.ValueKind == JsonValueKind.Number)
                {
                    // Direct number value
                    amount = jsonElement.GetInt32();
                }
                else if (jsonElement.ValueKind == JsonValueKind.Object && jsonElement.TryGetProperty("quantity", out var quantityProp))
                {
                    // Object with quantity property
                    amount = quantityProp.GetInt32();
                    
                    // Check if isLastSupply is specified in the body
                    if (jsonElement.TryGetProperty("isLastSupply", out var isLastProp))
                    {
                        isLastSupply = isLastProp.GetBoolean();
                    }
                }
                else if (jsonElement.ValueKind == JsonValueKind.Object && jsonElement.TryGetProperty("amount", out var amountProp))
                {
                    // Object with amount property
                    amount = amountProp.GetInt32();
                    
                    // Check if isLastSupply is specified in the body
                    if (jsonElement.TryGetProperty("isLastSupply", out var isLastProp))
                    {
                        isLastSupply = isLastProp.GetBoolean();
                    }
                }
                else
                {
                    return BadRequest("Invalid amount format. Expected a number or object with 'quantity' or 'amount' property.");
                }
            }
            else if (amountData is int directAmount)
            {
                amount = directAmount;
            }
            else
            {
                return BadRequest("Invalid amount format.");
            }
            
            var processDone = await _processDoneService.UpdateProcessDoneAmountAsync(id, amount, isLastSupply);
            return Ok(processDone);
        }
        catch (KeyNotFoundException ex)
        {
            _logger.LogWarning(ex, "ProcessDone not found when updating quantity");
            return NotFound(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating process done quantity");
            return StatusCode(500, "Internal server error");
        }
    }

    // Compatibility endpoint for frontend
    [HttpPut("{id}/quantity")]
    public async Task<ActionResult<ProcessDoneDto>> UpdateProcessDoneQuantity(int id, [FromBody] object quantityData, [FromQuery] bool isLastSupply = false)
    {
        // Redirect to the new method with flexible parsing
        return await UpdateProcessDoneAmount(id, quantityData, isLastSupply);
    }

    [HttpPost("{id}/supply-entry")]
    public async Task<ActionResult<ProcessDoneDto>> AddSupplyEntryToProcess(int id, [FromBody] CreateSupplyUsageDto supplyUsage)
    {
        try
        {
            var processDone = await _processDoneService.AddSupplyEntryAsync(id, supplyUsage);
            return Ok(processDone);
        }
        catch (KeyNotFoundException ex)
        {
            _logger.LogWarning(ex, "ProcessDone not found when adding supply entry");
            return NotFound(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error adding supply entry to process done");
            return StatusCode(500, "Internal server error");
        }
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteProcessDone(int id)
    {
        try
        {
            await _processDoneService.DeleteProcessDoneAsync(id);
            return NoContent();
        }
        catch (KeyNotFoundException ex)
        {
            _logger.LogWarning(ex, "ProcessDone not found for deletion: {ProcessDoneId}", id);
            return NotFound(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting process done {ProcessDoneId}", id);
            return StatusCode(500, "Internal server error");
        }
    }
}

// DTOs for request bodies
public class QuantityUpdateRequest
{
    public int Quantity { get; set; }
}
