using System.ComponentModel.DataAnnotations;

namespace GPInventory.Domain.Entities;

public class ExpenseCategory
{
    public int Id { get; set; }
    
    [Required]
    [StringLength(200)]
    public string Name { get; set; } = string.Empty;

    // Navigation property
    public ICollection<ExpenseSubcategory> Subcategories { get; set; } = new List<ExpenseSubcategory>();

    public ExpenseCategory()
    {
    }

    public ExpenseCategory(string name)
    {
        Name = name;
    }
}
