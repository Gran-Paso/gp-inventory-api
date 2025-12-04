namespace GPInventory.Application.DTOs.Budgets;

public class CreateBudgetDto
{
    public string Name { get; set; } = string.Empty;
    public int Year { get; set; }
    public decimal TotalAmount { get; set; }
    public int? BusinessId { get; set; }
    public int? StoreId { get; set; }
    public string? Description { get; set; }
    
    public List<CreateBudgetAllocationDto> Allocations { get; set; } = new();
    public List<CreateMonthlyDistributionDto> MonthlyDistributions { get; set; } = new();
}

public class CreateBudgetAllocationDto
{
    public int ExpenseTypeId { get; set; }
    public decimal? AllocationPercentage { get; set; }
    public decimal? Percentage { get; set; } // Alias for compatibility
    public decimal? FixedAmount { get; set; }
    public string? AllocationType { get; set; }
    
    // Computed property to get the percentage value
    public decimal? GetPercentage() => AllocationPercentage ?? Percentage;
}

public class CreateMonthlyDistributionDto
{
    public int Month { get; set; }
    public decimal? DistributionPercentage { get; set; }
    public decimal? Percentage { get; set; } // Alias for compatibility
    public decimal? FixedAmount { get; set; }
    
    // Computed property to get the percentage value
    public decimal? GetPercentage() => DistributionPercentage ?? Percentage;
}
