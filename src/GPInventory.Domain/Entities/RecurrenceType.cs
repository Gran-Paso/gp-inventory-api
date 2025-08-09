using System.ComponentModel.DataAnnotations;

namespace GPInventory.Domain.Entities;

public class RecurrenceType
{
    public int Id { get; set; }
    
    [Required]
    [StringLength(50)]
    public string Value { get; set; } = string.Empty; // 'mensual', 'bimestral', 'trimestral', 'semestral', 'anual', 'único'

    [Required]
    [StringLength(200)]
    public string Description { get; set; } = string.Empty; // 'Cada mes', 'Cada 2 meses', etc.

    // Navigation properties
    public ICollection<FixedExpense> FixedExpenses { get; set; } = new List<FixedExpense>();

    public RecurrenceType()
    {
    }

    public RecurrenceType(string value, string description)
    {
        Value = value;
        Description = description;
    }
}
