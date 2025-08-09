namespace GPInventory.Application.DTOs.Expenses;

public class RecurrenceTypeDto
{
    public int Id { get; set; }
    public string Value { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public bool IsActive { get; set; }
}
