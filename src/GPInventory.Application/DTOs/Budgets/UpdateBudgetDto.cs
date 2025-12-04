namespace GPInventory.Application.DTOs.Budgets;

public class UpdateBudgetDto
{
    public string? Name { get; set; }
    public int? Year { get; set; }
    public decimal? TotalAmount { get; set; }
    public int? BusinessId { get; set; }
    public int? StoreId { get; set; }
    public string? Status { get; set; }
    
    public List<CreateBudgetAllocationDto>? Allocations { get; set; }
    public List<CreateMonthlyDistributionDto>? MonthlyDistributions { get; set; }
}
