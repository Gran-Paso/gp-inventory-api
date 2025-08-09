using GPInventory.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GPInventory.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class RecurrenceTypesController : ControllerBase
{
    private readonly IRecurrenceTypeRepository _recurrenceTypeRepository;

    public RecurrenceTypesController(IRecurrenceTypeRepository recurrenceTypeRepository)
    {
        _recurrenceTypeRepository = recurrenceTypeRepository;
    }

    /// <summary>
    /// Obtiene todos los tipos de recurrencia activos
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        try
        {
            var recurrenceTypes = await _recurrenceTypeRepository.GetAllActiveAsync();
            var result = recurrenceTypes.Select(rt => new
            {
                rt.Id,
                rt.Value,
                rt.Description
            });

            return Ok(result);
        }
        catch (Exception ex)
        {
            // Log detailed error information
            Console.WriteLine($"RecurrenceTypes GetAll Error: {ex.Message}");
            Console.WriteLine($"Stack Trace: {ex.StackTrace}");
            if (ex.InnerException != null)
            {
                Console.WriteLine($"Inner Exception: {ex.InnerException.Message}");
            }
            
            return StatusCode(500, new { 
                message = "Error interno del servidor", 
                details = ex.Message,
                innerException = ex.InnerException?.Message,
                type = ex.GetType().Name
            });
        }
    }

    /// <summary>
    /// Obtiene un tipo de recurrencia por ID
    /// </summary>
    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(int id)
    {
        try
        {
            var recurrenceType = await _recurrenceTypeRepository.GetByIdAsync(id);
            if (recurrenceType == null)
                return NotFound(new { message = "Tipo de recurrencia no encontrado" });

            var result = new
            {
                recurrenceType.Id,
                recurrenceType.Value,
                recurrenceType.Description
            };

            return Ok(result);
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { message = "Error interno del servidor", details = ex.Message });
        }
    }
}
