using GPInventory.Application.DTOs.Production;
using GPInventory.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GPInventory.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class ProcessesController : ControllerBase
{
    private readonly IProcessService _processService;
    private readonly ILogger<ProcessesController> _logger;

    public ProcessesController(IProcessService processService, ILogger<ProcessesController> logger)
    {
        _processService = processService;
        _logger = logger;
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<ProcessDto>>> GetProcesses([FromQuery] int[]? storeIds = null, [FromQuery] int? businessId = null)
    {
        try
        {
            var processes = await _processService.GetProcessesWithDetailsAsync(storeIds, businessId);
            return Ok(processes);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving processes");
            return StatusCode(500, "Internal server error");
        }
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<ProcessDto>> GetProcess(int id)
    {
        try
        {
            var process = await _processService.GetProcessByIdAsync(id);
            return Ok(process);
        }
        catch (KeyNotFoundException ex)
        {
            _logger.LogWarning(ex, "Process not found: {ProcessId}", id);
            return NotFound(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving process {ProcessId}", id);
            return StatusCode(500, "Internal server error");
        }
    }

    [HttpGet("store/{storeId}")]
    public async Task<ActionResult<IEnumerable<ProcessDto>>> GetProcessesByStore(int storeId)
    {
        try
        {
            var processes = await _processService.GetProcessesByStoreIdAsync(storeId);
            return Ok(processes);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving processes for store {StoreId}", storeId);
            return StatusCode(500, "Internal server error");
        }
    }

    [HttpGet("product/{productId}")]
    public async Task<ActionResult<IEnumerable<ProcessDto>>> GetProcessesByProduct(int productId)
    {
        try
        {
            var processes = await _processService.GetProcessesByProductIdAsync(productId);
            return Ok(processes);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving processes for product {ProductId}", productId);
            return StatusCode(500, "Internal server error");
        }
    }

    [HttpGet("store/{storeId}/active")]
    public async Task<ActionResult<IEnumerable<ProcessDto>>> GetActiveProcesses(int storeId)
    {
        try
        {
            var processes = await _processService.GetActiveProcessesAsync(storeId);
            return Ok(processes);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving active processes for store {StoreId}", storeId);
            return StatusCode(500, "Internal server error");
        }
    }

    [HttpPost]
    public async Task<ActionResult<ProcessDto>> CreateProcess([FromBody] CreateProcessDto createProcessDto)
    {
        try
        {
            var process = await _processService.CreateProcessAsync(createProcessDto);
            return Ok(process);
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "Invalid operation when creating process");
            return BadRequest(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating process");
            return StatusCode(500, "Internal server error");
        }
    }

    [HttpPut("{id}")]
    public async Task<ActionResult<ProcessDto>> UpdateProcess(int id, [FromBody] UpdateProcessDto updateProcessDto)
    {
        try
        {
            var process = await _processService.UpdateProcessAsync(id, updateProcessDto);
            return Ok(process);
        }
        catch (KeyNotFoundException ex)
        {
            _logger.LogWarning(ex, "Process not found for update: {ProcessId}", id);
            return NotFound(ex.Message);
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "Invalid operation when updating process {ProcessId}", id);
            return BadRequest(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating process {ProcessId}", id);
            return StatusCode(500, "Internal server error");
        }
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteProcess(int id)
    {
        try
        {
            await _processService.DeleteProcessAsync(id);
            return NoContent();
        }
        catch (KeyNotFoundException ex)
        {
            _logger.LogWarning(ex, "Process not found for deletion: {ProcessId}", id);
            return NotFound(ex.Message);
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "Cannot delete process: {Message}", ex.Message);
            return BadRequest(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting process {ProcessId}", id);
            return StatusCode(500, "Internal server error");
        }
    }

    [HttpPatch("{id}/deactivate")]
    public async Task<ActionResult<ProcessDto>> DeactivateProcess(int id)
    {
        try
        {
            var process = await _processService.DeactivateProcessAsync(id);
            return Ok(process);
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "Process not found for deactivation: {ProcessId}", id);
            return NotFound(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deactivating process {ProcessId}", id);
            return StatusCode(500, "Internal server error");
        }
    }
}
