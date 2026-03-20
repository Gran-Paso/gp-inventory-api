namespace GPInventory.Application.DTOs.Expenses;

public class ExpenseSubcategoryDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public int ExpenseCategoryId { get; set; }
}