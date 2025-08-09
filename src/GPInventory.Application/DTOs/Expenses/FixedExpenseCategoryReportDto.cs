namespace GPInventory.Application.DTOs.Expenses;

public class FixedExpenseCategoryReportDto
{
    public int CategoryId { get; set; }
    public string CategoryName { get; set; } = string.Empty;
    public decimal TotalAmount { get; set; }
    public int Count { get; set; }
}
