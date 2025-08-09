namespace GPInventory.Application.DTOs.Expenses;

public class FixedExpenseFilterDto
{
    public int? BusinessId { get; set; }
    public int? StoreId { get; set; }
    public int? CategoryId { get; set; }
    public int? SubcategoryId { get; set; }
    public int? RecurrenceTypeId { get; set; }
    public bool? IsActive { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 10;
    public string OrderBy { get; set; } = "Name";
    public bool OrderDescending { get; set; } = false;
}
