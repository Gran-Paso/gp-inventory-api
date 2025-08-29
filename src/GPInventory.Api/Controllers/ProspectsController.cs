using GPInventory.Application.DTOs.Prospects;
using GPInventory.Application.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace GPInventory.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ProspectsController : ControllerBase
{
    private readonly IProspectService _prospectService;
    private readonly ILogger<ProspectsController> _logger;

    public ProspectsController(IProspectService prospectService, ILogger<ProspectsController> logger)
    {
        _prospectService = prospectService;
        _logger = logger;
    }

    // POST: api/prospects
    [HttpPost]
    public async Task<IActionResult> CreateProspect([FromBody] CreateProspectDto createProspectDto)
    {
        try
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var prospect = await _prospectService.CreateProspectAsync(createProspectDto);
            
            _logger.LogInformation("Nuevo prospect creado: {ProspectId} - {ProspectName} - {ProspectMail}", 
                prospect.Id, prospect.Name, prospect.Mail);

            return CreatedAtAction(
                nameof(GetProspectById), 
                new { id = prospect.Id }, 
                new { 
                    success = true, 
                    message = "Prospect creado exitosamente", 
                    data = prospect 
                });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al crear prospect: {ErrorMessage}", ex.Message);
            return StatusCode(500, new { 
                success = false, 
                message = "Error interno del servidor al crear el prospect", 
                error = ex.Message 
            });
        }
    }

    // GET: api/prospects
    [HttpGet]
    public async Task<IActionResult> GetProspects()
    {
        try
        {
            var prospects = await _prospectService.GetAllProspectsAsync();
            return Ok(new { 
                success = true, 
                data = prospects, 
                count = prospects.Count() 
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al obtener prospects: {ErrorMessage}", ex.Message);
            return StatusCode(500, new { 
                success = false, 
                message = "Error interno del servidor al obtener prospects", 
                error = ex.Message 
            });
        }
    }

    // GET: api/prospects/{id}
    [HttpGet("{id}")]
    public async Task<IActionResult> GetProspectById(int id)
    {
        try
        {
            var prospect = await _prospectService.GetProspectByIdAsync(id);
            
            if (prospect == null)
            {
                return NotFound(new { 
                    success = false, 
                    message = $"Prospect con ID {id} no encontrado" 
                });
            }

            return Ok(new { 
                success = true, 
                data = prospect 
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al obtener prospect por ID: {ErrorMessage}", ex.Message);
            return StatusCode(500, new { 
                success = false, 
                message = "Error interno del servidor al obtener el prospect", 
                error = ex.Message 
            });
        }
    }
}
