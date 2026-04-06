using GPInventory.Application.DTOs.Expenses;
using GPInventory.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GPInventory.Api.Controllers;

/// <summary>
/// Gestión de etiquetas de usuario para egresos.
/// Las etiquetas son libres, definidas por negocio, independientes del sistema de categorías.
/// </summary>
[ApiController]
[Route("api/expense-tags")]
[Authorize]
public class ExpenseTagsController : ControllerBase
{
    private readonly IExpenseTagService _tagService;
    private readonly ILogger<ExpenseTagsController> _logger;

    public ExpenseTagsController(IExpenseTagService tagService, ILogger<ExpenseTagsController> logger)
    {
        _tagService = tagService;
        _logger = logger;
    }

    // GET api/expense-tags?businessId=1
    [HttpGet]
    public async Task<IActionResult> GetByBusiness([FromQuery] int businessId)
    {
        try
        {
            if (businessId <= 0)
                return BadRequest(new { message = "Se requiere un businessId válido." });

            var tags = await _tagService.GetByBusinessAsync(businessId);
            return Ok(tags);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving expense tags");
            return StatusCode(500, new { message = "Error al obtener las etiquetas." });
        }
    }

    // POST api/expense-tags
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateExpenseTagDto dto)
    {
        try
        {
            var created = await _tagService.CreateAsync(dto);
            return CreatedAtAction(nameof(GetByBusiness), new { businessId = created.BusinessId }, created);
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating expense tag");
            return StatusCode(500, new { message = "Error al crear la etiqueta." });
        }
    }

    // PUT api/expense-tags/5
    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, [FromBody] UpdateExpenseTagDto dto)
    {
        try
        {
            var updated = await _tagService.UpdateAsync(id, dto);
            return Ok(updated);
        }
        catch (KeyNotFoundException)
        {
            return NotFound(new { message = "Etiqueta no encontrada." });
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating expense tag {Id}", id);
            return StatusCode(500, new { message = "Error al actualizar la etiqueta." });
        }
    }

    // DELETE api/expense-tags/5
    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id)
    {
        try
        {
            await _tagService.DeleteAsync(id);
            return NoContent();
        }
        catch (KeyNotFoundException)
        {
            return NotFound(new { message = "Etiqueta no encontrada." });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting expense tag {Id}", id);
            return StatusCode(500, new { message = "Error al eliminar la etiqueta." });
        }
    }

    // GET api/expense-tags/by-expense/42
    [HttpGet("by-expense/{expenseId:int}")]
    public async Task<IActionResult> GetByExpense(int expenseId)
    {
        try
        {
            var tags = await _tagService.GetTagsByExpenseAsync(expenseId);
            return Ok(tags);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving tags for expense {ExpenseId}", expenseId);
            return StatusCode(500, new { message = "Error al obtener las etiquetas del egreso." });
        }
    }

    // PUT api/expense-tags/assign/42
    [HttpPut("assign/{expenseId:int}")]
    public async Task<IActionResult> AssignTags(int expenseId, [FromBody] AssignTagsDto dto)
    {
        try
        {
            await _tagService.SetTagsForExpenseAsync(expenseId, dto.TagIds);
            return NoContent();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error assigning tags to expense {ExpenseId}", expenseId);
            return StatusCode(500, new { message = "Error al asignar etiquetas al egreso." });
        }
    }
}
