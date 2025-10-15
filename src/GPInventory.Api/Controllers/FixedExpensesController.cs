using GPInventory.Application.DTOs.Expenses;
using GPInventory.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GPInventory.Api.Controllers;

[ApiController]
[Route("api/fixed-expenses")]
[Authorize]
public class FixedExpensesController : ControllerBase
{
    private readonly IExpenseService _expenseService;
    private readonly ILogger<FixedExpensesController> _logger;

    public FixedExpensesController(IExpenseService expenseService, ILogger<FixedExpensesController> logger)
    {
        _expenseService = expenseService;
        _logger = logger;
    }

    // GET: api/fixed-expenses
    [HttpGet]
    public async Task<IActionResult> GetFixedExpenses([FromQuery] int? businessId, [FromQuery] int[]? businessIds, [FromQuery] int? expenseTypeId)
    {
        try
        {
            _logger.LogInformation("GetFixedExpenses called with businessId: {BusinessId}, businessIds: {BusinessIds}, expenseTypeId: {ExpenseTypeId}", 
                businessId, businessIds != null ? string.Join(",", businessIds) : "null", expenseTypeId);
            
            // Si se proporcionan múltiples businessIds, usar esos; si no, usar el businessId único
            var targetBusinessIds = businessIds?.Length > 0 ? businessIds : (businessId.HasValue ? new[] { businessId.Value } : null);
            
            // Validar que al menos se proporcione un businessId
            if (targetBusinessIds == null || targetBusinessIds.Length == 0)
            {
                return BadRequest(new { message = "Se debe proporcionar al menos un ID de negocio" });
            }
            
            _logger.LogInformation("Using targetBusinessIds: {TargetBusinessIds}", 
                targetBusinessIds != null ? string.Join(",", targetBusinessIds) : "null");
            
            var fixedExpenses = await _expenseService.GetFixedExpensesAsync(targetBusinessIds, expenseTypeId);
            
            _logger.LogInformation("Retrieved {Count} fixed expenses", fixedExpenses.Count());
            
            return Ok(fixedExpenses);
        }
        catch (ArgumentException ex)
        {
            _logger.LogWarning(ex, "Invalid arguments for GetFixedExpenses");
            return BadRequest(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving fixed expenses");
            return StatusCode(500, new { message = "Error al obtener los gastos fijos" });
        }
    }

    // GET: api/fixed-expenses/{id}
    [HttpGet("{id}")]
    public async Task<IActionResult> GetFixedExpense(int id)
    {
        try
        {
            var fixedExpense = await _expenseService.GetFixedExpenseByIdAsync(id);
            return Ok(fixedExpense);
        }
        catch (KeyNotFoundException ex)
        {
            _logger.LogWarning(ex, "Fixed expense not found: {Id}", id);
            return NotFound(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving fixed expense: {Id}", id);
            return StatusCode(500, new { message = "Error al obtener el gasto fijo" });
        }
    }

    // POST: api/fixed-expenses
    [HttpPost]
    public async Task<IActionResult> CreateFixedExpense([FromBody] CreateFixedExpenseDto createFixedExpenseDto)
    {
        try
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var fixedExpense = await _expenseService.CreateFixedExpenseAsync(createFixedExpenseDto);
            return CreatedAtAction(nameof(GetFixedExpense), new { id = fixedExpense.Id }, fixedExpense);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating fixed expense");
            return StatusCode(500, new { message = "Error al crear el gasto fijo" });
        }
    }

    // PUT: api/fixed-expenses/{id}
    [HttpPut("{id}")]
    public async Task<IActionResult> UpdateFixedExpense(int id, [FromBody] UpdateFixedExpenseDto updateFixedExpenseDto)
    {
        try
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var fixedExpense = await _expenseService.UpdateFixedExpenseAsync(id, updateFixedExpenseDto);
            return Ok(fixedExpense);
        }
        catch (KeyNotFoundException ex)
        {
            _logger.LogWarning(ex, "Fixed expense not found for update: {Id}", id);
            return NotFound(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating fixed expense: {Id}", id);
            return StatusCode(500, new { message = "Error al actualizar el gasto fijo" });
        }
    }

    // DELETE: api/fixed-expenses/{id}
    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteFixedExpense(int id)
    {
        try
        {
            await _expenseService.DeleteFixedExpenseAsync(id);
            return NoContent();
        }
        catch (KeyNotFoundException ex)
        {
            _logger.LogWarning(ex, "Fixed expense not found for deletion: {Id}", id);
            return NotFound(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting fixed expense: {Id}", id);
            return StatusCode(500, new { message = "Error al eliminar el gasto fijo" });
        }
    }
}
