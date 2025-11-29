namespace GPInventory.Application.DTOs.Budgets;

public class BudgetAllocationDto
{
    public int Id { get; set; }
    public int BudgetId { get; set; }
    public int ExpenseTypeId { get; set; }
    public decimal? Percentage { get; set; }
    public decimal? FixedAmount { get; set; }
    public decimal AllocatedAmount { get; set; }
    
    // Related data
    public string? ExpenseTypeName { get; set; }
    public string? ExpenseTypeCode { get; set; }
}
