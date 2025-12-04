namespace GPInventory.Application.DTOs.Budgets;

public class MonthlyDistributionDto
{
    public int Id { get; set; }
    public int BudgetId { get; set; }
    public int Month { get; set; }
    public decimal? Percentage { get; set; }
    public decimal? FixedAmount { get; set; }
    public decimal AllocatedAmount { get; set; }
}
