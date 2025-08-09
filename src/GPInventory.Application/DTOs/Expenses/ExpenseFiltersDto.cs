namespace GPInventory.Application.DTOs.Expenses;

public class ExpenseFiltersDto
{
    public int? BusinessId { get; set; }
    public int[]? BusinessIds { get; set; }
    public int? StoreId { get; set; }
    public int? CategoryId { get; set; }
    public int? SubcategoryId { get; set; }
    public DateTime? StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public int? MinAmount { get; set; }
    public int? MaxAmount { get; set; }
    public bool? IsFixed { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 10;
    public string? OrderBy { get; set; } = "Date";
    public bool OrderDescending { get; set; } = true;
}

public class ExpenseSummaryDto
{
    public decimal TotalAmount { get; set; }
    public int TotalCount { get; set; }
    public decimal ExpensesAmount { get; set; }
    public int ExpensesCount { get; set; }
    public decimal VariableExpensesAmount { get; set; }
    public int VariableExpensesCount { get; set; }
    public DateTime? PeriodStart { get; set; }
    public DateTime? PeriodEnd { get; set; }
    public List<ExpenseByCategoryDto> ExpensesByCategory { get; set; } = new List<ExpenseByCategoryDto>();
    public List<MonthlyExpenseDto> MonthlyExpenses { get; set; } = new List<MonthlyExpenseDto>();
}

public class ExpenseByCategoryDto
{
    public int CategoryId { get; set; }
    public string CategoryName { get; set; } = string.Empty;
    public decimal TotalAmount { get; set; }
    public int Count { get; set; }
    public decimal Percentage { get; set; }
}

public class MonthlyExpenseDto
{
    public int Year { get; set; }
    public int Month { get; set; }
    public string MonthName { get; set; } = string.Empty;
    public decimal TotalAmount { get; set; }
    public int Count { get; set; }
}
