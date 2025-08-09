using GPInventory.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GPInventory.Api.Controllers;

[ApiController]
[Route("api/expense-categories")]
[Authorize]
public class ExpenseCategoriesController : ControllerBase
{
    private readonly IExpenseService _expenseService;
    private readonly ILogger<ExpenseCategoriesController> _logger;

    public ExpenseCategoriesController(IExpenseService expenseService, ILogger<ExpenseCategoriesController> logger)
    {
        _expenseService = expenseService;
        _logger = logger;
    }

    // GET: api/expense-categories
    [HttpGet]
    public async Task<IActionResult> GetCategories()
    {
        try
        {
            var categories = await _expenseService.GetCategoriesAsync();
            return Ok(categories);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving expense categories");
            return StatusCode(500, new { message = "Error al obtener las categorías de gastos" });
        }
    }
}

[ApiController]
[Route("api/expense-subcategories")]
[Authorize]
public class ExpenseSubcategoriesController : ControllerBase
{
    private readonly IExpenseService _expenseService;
    private readonly ILogger<ExpenseSubcategoriesController> _logger;

    public ExpenseSubcategoriesController(IExpenseService expenseService, ILogger<ExpenseSubcategoriesController> logger)
    {
        _expenseService = expenseService;
        _logger = logger;
    }

    // GET: api/expense-subcategories
    [HttpGet]
    public async Task<IActionResult> GetSubcategories([FromQuery] int? categoryId)
    {
        try
        {
            var subcategories = await _expenseService.GetSubcategoriesAsync(categoryId);
            return Ok(subcategories);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving expense subcategories");
            return StatusCode(500, new { message = "Error al obtener las subcategorías de gastos" });
        }
    }
}
