using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace GPInventory.Domain.Entities;

public class ExpenseSubcategory
{
    [Key]
    public int Id { get; set; }

    [Required]
    [StringLength(200)]
    public string Name { get; set; } = string.Empty;

    [Required]
    [ForeignKey(nameof(ExpenseCategory))]
    public int ExpenseCategoryId { get; set; }

    // Navigation properties
    public ExpenseCategory ExpenseCategory { get; set; } = null!;

    public ExpenseSubcategory()
    {
    }

    public ExpenseSubcategory(string name, int expenseCategoryId)
    {
        Name = name;
        ExpenseCategoryId = expenseCategoryId;
    }
}
