using GPInventory.Application.DTOs.Production;
using GPInventory.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GPInventory.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class ManufacturesController : ControllerBase
{
    private readonly IManufactureService _manufactureService;
    private readonly ILogger<ManufacturesController> _logger;

    public ManufacturesController(IManufactureService manufactureService, ILogger<ManufacturesController> logger)
    {
        _manufactureService = manufactureService;
        _logger = logger;
    }

    /// <summary>
    /// Obtener todos los lotes manufacturados
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<IEnumerable<ManufactureDto>>> GetAll()
    {
        try
        {
            var manufactures = await _manufactureService.GetAllAsync();
            return Ok(manufactures);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting all manufactures");
            return StatusCode(500, "Error interno del servidor");
        }
    }

    /// <summary>
    /// Obtener un lote manufacturado por ID
    /// </summary>
    [HttpGet("{id}")]
    public async Task<ActionResult<ManufactureDto>> GetById(int id)
    {
        try
        {
            var manufacture = await _manufactureService.GetByIdAsync(id);
            return Ok(manufacture);
        }
        catch (ArgumentException ex)
        {
            return NotFound(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting manufacture by id {Id}", id);
            return StatusCode(500, "Error interno del servidor");
        }
    }

    /// <summary>
    /// Obtener lotes manufacturados por negocio
    /// </summary>
    [HttpGet("business/{businessId}")]
    public async Task<ActionResult<IEnumerable<ManufactureDto>>> GetByBusiness(int businessId)
    {
        try
        {
            var manufactures = await _manufactureService.GetByBusinessIdAsync(businessId);
            return Ok(manufactures);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting manufactures for business {BusinessId}", businessId);
            return StatusCode(500, "Error interno del servidor");
        }
    }

    /// <summary>
    /// Obtener lotes manufacturados por proceso completado
    /// </summary>
    [HttpGet("process-done/{processDoneId}")]
    public async Task<ActionResult<IEnumerable<ManufactureDto>>> GetByProcessDone(int processDoneId)
    {
        try
        {
            var manufactures = await _manufactureService.GetByProcessDoneIdAsync(processDoneId);
            return Ok(manufactures);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting manufactures for process done {ProcessDoneId}", processDoneId);
            return StatusCode(500, "Error interno del servidor");
        }
    }

    /// <summary>
    /// Obtener lotes manufacturados pendientes (no enviados)
    /// </summary>
    [HttpGet("pending/{businessId}")]
    public async Task<ActionResult<IEnumerable<ManufactureDto>>> GetPending(int businessId)
    {
        try
        {
            var manufactures = await _manufactureService.GetPendingAsync(businessId);
            return Ok(manufactures);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting pending manufactures for business {BusinessId}", businessId);
            return StatusCode(500, "Error interno del servidor");
        }
    }

    /// <summary>
    /// Obtener resumen de process dones con sus lotes
    /// </summary>
    [HttpGet("summaries/{businessId}")]
    public async Task<ActionResult<IEnumerable<ProcessDoneSummaryDto>>> GetProcessDoneSummaries(int businessId)
    {
        try
        {
            var summaries = await _manufactureService.GetProcessDoneSummariesAsync(businessId);
            return Ok(summaries);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting process done summaries for business {BusinessId}", businessId);
            return StatusCode(500, "Error interno del servidor");
        }
    }

    /// <summary>
    /// Crear un nuevo lote manufacturado
    /// </summary>
    [HttpPost]
    public async Task<ActionResult<ManufactureDto>> Create([FromBody] CreateManufactureDto createDto)
    {
        try
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var manufacture = await _manufactureService.CreateAsync(createDto);
            return CreatedAtAction(nameof(GetById), new { id = manufacture.Id }, manufacture);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating manufacture");
            return StatusCode(500, "Error interno del servidor");
        }
    }

    /// <summary>
    /// Actualizar un lote manufacturado
    /// </summary>
    [HttpPut("{id}")]
    public async Task<ActionResult<ManufactureDto>> Update(int id, [FromBody] UpdateManufactureDto updateDto)
    {
        try
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var manufacture = await _manufactureService.UpdateAsync(id, updateDto);
            return Ok(manufacture);
        }
        catch (ArgumentException ex)
        {
            return NotFound(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating manufacture {Id}", id);
            return StatusCode(500, "Error interno del servidor");
        }
    }

    /// <summary>
    /// Eliminar un lote manufacturado (soft delete)
    /// </summary>
    [HttpDelete("{id}")]
    public async Task<ActionResult> Delete(int id)
    {
        try
        {
            await _manufactureService.DeleteAsync(id);
            return NoContent();
        }
        catch (ArgumentException ex)
        {
            return NotFound(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting manufacture {Id}", id);
            return StatusCode(500, "Error interno del servidor");
        }
    }
}
