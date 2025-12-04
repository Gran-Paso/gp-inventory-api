namespace GPInventory.Application.DTOs.Budgets;

public class BudgetSummaryDto
{
    public int BudgetId { get; set; }
    public string BudgetName { get; set; } = string.Empty;
    public int Year { get; set; }
    public decimal TotalBudget { get; set; }
    public decimal TotalUsed { get; set; }
    public decimal RemainingAmount { get; set; }
    public decimal UsagePercentage { get; set; }
    
    public List<CategorySummaryDto> CategorySummary { get; set; } = new();
    public List<MonthlySummaryDto> MonthlySummary { get; set; } = new();
}

public class CategorySummaryDto
{
    public int ExpenseTypeId { get; set; }
    public string ExpenseTypeName { get; set; } = string.Empty;
    public string ExpenseTypeCode { get; set; } = string.Empty;
    public decimal AllocatedAmount { get; set; }
    public decimal UsedAmount { get; set; }
    public decimal RemainingAmount { get; set; }
    public decimal Percentage { get; set; }
    public decimal UsagePercentage { get; set; }
}

public class MonthlySummaryDto
{
    public int Month { get; set; }
    public string MonthName { get; set; } = string.Empty;
    public decimal AllocatedAmount { get; set; }
    public decimal UsedAmount { get; set; }
    public decimal RemainingAmount { get; set; }
    public decimal UsagePercentage { get; set; }
}
