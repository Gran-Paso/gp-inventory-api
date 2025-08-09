using System.ComponentModel.DataAnnotations;

namespace GPInventory.Domain.Entities;

public class ExpenseCategory
{
    public int Id { get; set; }
    
    [Required]
    [StringLength(200)]
    public string Name { get; set; } = string.Empty;

    public ExpenseCategory()
    {
    }

    public ExpenseCategory(string name)
    {
        Name = name;
    }
}
