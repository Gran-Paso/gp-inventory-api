using GPInventory.Application.DTOs.Budgets;
using GPInventory.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GPInventory.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class BudgetsController : ControllerBase
{
    private readonly IBudgetService _budgetService;
    private readonly ILogger<BudgetsController> _logger;

    public BudgetsController(IBudgetService budgetService, ILogger<BudgetsController> logger)
    {
        _budgetService = budgetService;
        _logger = logger;
    }

    /// <summary>
    /// Get all budgets with optional filters
    /// </summary>
    /// <param name="storeId">Filter by store ID</param>
    /// <param name="businessId">Filter by business ID</param>
    /// <param name="year">Filter by year</param>
    /// <param name="status">Filter by status (DRAFT, ACTIVE, COMPLETED, CANCELLED)</param>
    [HttpGet]
    public async Task<IActionResult> GetBudgets(
        [FromQuery] int? storeId,
        [FromQuery] int? businessId,
        [FromQuery] int? year,
        [FromQuery] string? status)
    {
        try
        {
            var budgets = await _budgetService.GetBudgetsAsync(storeId, businessId, year, status);
            return Ok(budgets);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving budgets");
            return StatusCode(500, new { message = "Error al obtener los presupuestos" });
        }
    }

    /// <summary>
    /// Get a specific budget by ID
    /// </summary>
    /// <param name="id">Budget ID</param>
    [HttpGet("{id}")]
    public async Task<IActionResult> GetBudget(int id)
    {
        try
        {
            var budget = await _budgetService.GetBudgetByIdAsync(id);
            return Ok(budget);
        }
        catch (KeyNotFoundException ex)
        {
            _logger.LogWarning(ex, "Budget not found: {Id}", id);
            return NotFound(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving budget: {Id}", id);
            return StatusCode(500, new { message = "Error al obtener el presupuesto" });
        }
    }

    /// <summary>
    /// Create a new budget
    /// </summary>
    /// <param name="createDto">Budget creation data</param>
    [HttpPost]
    public async Task<IActionResult> CreateBudget([FromBody] CreateBudgetDto createDto)
    {
        try
        {
            var budget = await _budgetService.CreateBudgetAsync(createDto);
            return CreatedAtAction(nameof(GetBudget), new { id = budget.Id }, budget);
        }
        catch (ArgumentException ex)
        {
            _logger.LogWarning(ex, "Invalid budget data");
            return BadRequest(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating budget");
            return StatusCode(500, new { message = "Error al crear el presupuesto" });
        }
    }

    /// <summary>
    /// Update an existing budget
    /// </summary>
    /// <param name="id">Budget ID</param>
    /// <param name="updateDto">Budget update data</param>
    [HttpPut("{id}")]
    public async Task<IActionResult> UpdateBudget(int id, [FromBody] UpdateBudgetDto updateDto)
    {
        try
        {
            var budget = await _budgetService.UpdateBudgetAsync(id, updateDto);
            return Ok(budget);
        }
        catch (KeyNotFoundException ex)
        {
            _logger.LogWarning(ex, "Budget not found: {Id}", id);
            return NotFound(new { message = ex.Message });
        }
        catch (ArgumentException ex)
        {
            _logger.LogWarning(ex, "Invalid budget data");
            return BadRequest(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating budget: {Id}", id);
            return StatusCode(500, new { message = "Error al actualizar el presupuesto" });
        }
    }

    /// <summary>
    /// Delete a budget
    /// </summary>
    /// <param name="id">Budget ID</param>
    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteBudget(int id)
    {
        try
        {
            await _budgetService.DeleteBudgetAsync(id);
            return NoContent();
        }
        catch (KeyNotFoundException ex)
        {
            _logger.LogWarning(ex, "Budget not found: {Id}", id);
            return NotFound(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting budget: {Id}", id);
            return StatusCode(500, new { message = "Error al eliminar el presupuesto" });
        }
    }

    /// <summary>
    /// Get budget summary with usage statistics
    /// </summary>
    /// <param name="id">Budget ID</param>
    [HttpGet("{id}/summary")]
    public async Task<IActionResult> GetBudgetSummary(int id)
    {
        try
        {
            var summary = await _budgetService.GetBudgetSummaryAsync(id);
            return Ok(summary);
        }
        catch (KeyNotFoundException ex)
        {
            _logger.LogWarning(ex, "Budget not found: {Id}", id);
            return NotFound(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving budget summary: {Id}", id);
            return StatusCode(500, new { message = "Error al obtener el resumen del presupuesto" });
        }
    }

    /// <summary>
    /// Get budget allocations
    /// </summary>
    /// <param name="id">Budget ID</param>
    [HttpGet("{id}/allocations")]
    public async Task<IActionResult> GetBudgetAllocations(int id)
    {
        try
        {
            var allocations = await _budgetService.GetBudgetAllocationsAsync(id);
            return Ok(allocations);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving budget allocations: {Id}", id);
            return StatusCode(500, new { message = "Error al obtener las asignaciones del presupuesto" });
        }
    }

    /// <summary>
    /// Get monthly distribution
    /// </summary>
    /// <param name="id">Budget ID</param>
    [HttpGet("{id}/monthly-distribution")]
    public async Task<IActionResult> GetMonthlyDistribution(int id)
    {
        try
        {
            var distribution = await _budgetService.GetMonthlyDistributionAsync(id);
            return Ok(distribution);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving monthly distribution: {Id}", id);
            return StatusCode(500, new { message = "Error al obtener la distribución mensual" });
        }
    }
}
