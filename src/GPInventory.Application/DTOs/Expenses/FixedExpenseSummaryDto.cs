namespace GPInventory.Application.DTOs.Expenses;

public class FixedExpenseSummaryDto
{
    public decimal TotalActiveAmount { get; set; }
    public decimal TotalInactiveAmount { get; set; }
    public decimal TotalAmount { get; set; }
    public List<FixedExpenseCategoryReportDto> CategoryBreakdown { get; set; } = new();
}
