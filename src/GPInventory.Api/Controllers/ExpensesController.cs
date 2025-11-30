using GPInventory.Application.DTOs.Expenses;
using GPInventory.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GPInventory.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class ExpensesController : ControllerBase
{
    private readonly IExpenseService _expenseService;
    private readonly ILogger<ExpensesController> _logger;

    public ExpensesController(IExpenseService expenseService, ILogger<ExpensesController> logger)
    {
        _expenseService = expenseService;
        _logger = logger;
    }

    // GET: api/expenses
    [HttpGet]
    public async Task<IActionResult> GetExpenses([FromQuery] ExpenseFiltersDto filters)
    {
        try
        {
            // Construir array de business IDs para el filtrado
            int[]? targetBusinessIds = null;
            
            if (filters.BusinessIds != null && filters.BusinessIds.Length > 0)
            {
                targetBusinessIds = filters.BusinessIds;
            }
            else if (filters.BusinessId.HasValue)
            {
                targetBusinessIds = new[] { filters.BusinessId.Value };
            }

            // Validar que se proporcione al menos un business ID
            if (targetBusinessIds == null || targetBusinessIds.Length == 0)
            {
                return BadRequest(new { message = "Se debe proporcionar al menos un ID de negocio" });
            }

            // Actualizar filters con los business IDs procesados
            filters.BusinessIds = targetBusinessIds;
            filters.BusinessId = null; // Limpiar BusinessId individual para evitar conflictos

            var expenses = await _expenseService.GetExpensesAsync(filters);
            return Ok(expenses);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving expenses");
            return StatusCode(500, new { message = "Error al obtener los gastos" });
        }
    }

    // GET: api/expenses/{id}
    [HttpGet("{id}")]
    public async Task<IActionResult> GetExpense(int id)
    {
        try
        {
            var expense = await _expenseService.GetExpenseByIdAsync(id);
            return Ok(expense);
        }
        catch (KeyNotFoundException ex)
        {
            _logger.LogWarning(ex, "Expense not found: {Id}", id);
            return NotFound(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving expense: {Id}", id);
            return StatusCode(500, new { message = "Error al obtener el gasto" });
        }
    }

    // POST: api/expenses
    [HttpPost]
    public async Task<IActionResult> CreateExpense([FromBody] CreateExpenseDto createExpenseDto)
    {
        try
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var expense = await _expenseService.CreateExpenseAsync(createExpenseDto);
            return CreatedAtAction(nameof(GetExpense), new { id = expense.Id }, expense);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating expense");
            return StatusCode(500, new { message = "Error al crear el gasto" });
        }
    }

    // PUT: api/expenses/{id}
    [HttpPut("{id}")]
    public async Task<IActionResult> UpdateExpense(int id, [FromBody] UpdateExpenseDto updateExpenseDto)
    {
        try
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var expense = await _expenseService.UpdateExpenseAsync(id, updateExpenseDto);
            return Ok(expense);
        }
        catch (KeyNotFoundException ex)
        {
            _logger.LogWarning(ex, "Expense not found for update: {Id}", id);
            return NotFound(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating expense: {Id}", id);
            return StatusCode(500, new { message = "Error al actualizar el gasto" });
        }
    }

    // DELETE: api/expenses/{id}
    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteExpense(int id)
    {
        try
        {
            await _expenseService.DeleteExpenseAsync(id);
            return NoContent();
        }
        catch (KeyNotFoundException ex)
        {
            _logger.LogWarning(ex, "Expense not found for deletion: {Id}", id);
            return NotFound(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting expense: {Id}", id);
            return StatusCode(500, new { message = "Error al eliminar el gasto" });
        }
    }

    // GET: api/expenses/summary
    [HttpGet("summary")]
    public async Task<IActionResult> GetExpenseSummary([FromQuery] ExpenseFiltersDto filters)
    {
        try
        {
            // Validar que se proporcionen business IDs como en otros endpoints
            int[]? targetBusinessIds = null;
            
            if (filters.BusinessIds != null && filters.BusinessIds.Length > 0)
            {
                targetBusinessIds = filters.BusinessIds;
            }
            else if (filters.BusinessId.HasValue)
            {
                targetBusinessIds = new[] { filters.BusinessId.Value };
            }
            else
            {
                return BadRequest(new { message = "Se debe proporcionar al menos un ID de negocio" });
            }

            // Actualizar el servicio para manejar múltiples business IDs
            var summary = await _expenseService.GetExpenseSummaryAsync(targetBusinessIds, filters);
            return Ok(summary);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving expense summary for filters: {@Filters}", filters);
            return StatusCode(500, new { message = "Error al obtener el resumen de gastos" });
        }
    }

    // GET: api/expenses/monthly-kpis
    [HttpGet("monthly-kpis")]
    public async Task<IActionResult> GetMonthlyKPIs([FromQuery] int businessId)
    {
        try
        {
            var kpis = await _expenseService.GetMonthlyKPIsAsync(businessId);
            return Ok(kpis);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving monthly KPIs for business: {BusinessId}", businessId);
            return StatusCode(500, new { message = "Error al obtener los KPIs mensuales" });
        }
    }

    // GET: api/expenses/type-kpis
    [HttpGet("type-kpis")]
    public async Task<IActionResult> GetExpenseTypeKPIs([FromQuery] int businessId, [FromQuery] int expenseTypeId)
    {
        try
        {
            var kpis = await _expenseService.GetExpenseTypeKPIsAsync(businessId, expenseTypeId);
            return Ok(kpis);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving expense type KPIs for business: {BusinessId}, type: {ExpenseTypeId}", businessId, expenseTypeId);
            return StatusCode(500, new { message = "Error al obtener los KPIs por tipo" });
        }
    }

    // GET: api/expenses/export
    [HttpGet("export")]
    public async Task<IActionResult> ExportExpenses([FromQuery] ExpenseFiltersDto filters)
    {
        try
        {
            var csvData = await _expenseService.ExportExpensesAsync(filters);
            return File(csvData, "text/csv", $"gastos_{DateTime.Now:yyyyMMdd_HHmmss}.csv");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error exporting expenses");
            return StatusCode(500, new { message = "Error al exportar los gastos" });
        }
    }
}
