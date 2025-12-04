namespace GPInventory.Application.DTOs.Budgets;

public class BudgetDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public int Year { get; set; }
    public decimal TotalAmount { get; set; }
    public int? BusinessId { get; set; }
    public int? StoreId { get; set; }
    public string Status { get; set; } = "DRAFT";
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    
    // Calculated fields
    public decimal TotalAllocated { get; set; }
    public decimal TotalUsed { get; set; }
    public decimal RemainingAmount { get; set; }
    public decimal UsagePercentage { get; set; }
    
    // Related data
    public List<BudgetAllocationDto> Allocations { get; set; } = new();
    public List<MonthlyDistributionDto> MonthlyDistribution { get; set; } = new();
}
