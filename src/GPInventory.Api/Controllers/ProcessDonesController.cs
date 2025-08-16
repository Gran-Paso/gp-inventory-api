using GPInventory.Application.DTOs.Production;
using GPInventory.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

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
