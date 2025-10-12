using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace GPInventory.Domain.Entities;

public class Expense
{
    public int Id { get; set; }
    
    [Required]
    public DateTime Date { get; set; }

    [Required]
    [ForeignKey(nameof(ExpenseSubcategory))]
    public int SubcategoryId { get; set; }

    [Required]
    public int Amount { get; set; }

    [Required]
    [StringLength(500)]
    public string Description { get; set; } = string.Empty;

    public bool? IsFixed { get; set; } = false;

    [Required]
    [ForeignKey(nameof(Business))]
    public int BusinessId { get; set; }

    [ForeignKey(nameof(Store))]
    public int? StoreId { get; set; }

    [ForeignKey(nameof(FixedExpense))]
    public int? FixedExpenseId { get; set; } // Reference to the fixed expense that generated this expense

    [ForeignKey(nameof(ExpenseType))]
    public int? ExpenseTypeId { get; set; } // Tipo de egreso: Gasto, Costo o Inversión

    // Navigation properties
    public ExpenseSubcategory ExpenseSubcategory { get; set; } = null!;
    public Business Business { get; set; } = null!;
    public Store? Store { get; set; }
    public FixedExpense? FixedExpense { get; set; } // Navigation to the fixed expense that generated this
    public ExpenseType? ExpenseType { get; set; } // Tipo de egreso
    public string Notes { get; set; } = string.Empty;
    public string CreatedAt { get; set; } = DateTime.UtcNow.ToString("o"); // ISO 8601 format for consistency

    public Expense()
    {
    }

    public Expense(DateTime date, int subcategoryId, int amount, string description, int businessId, int? storeId = null, string? notes = "", int? expenseTypeId = null)
    {
        Date = date;
        SubcategoryId = subcategoryId;
        Amount = amount;
        Description = description;
        BusinessId = businessId;
        StoreId = storeId;
        Notes = notes ?? string.Empty;
        ExpenseTypeId = expenseTypeId;
    }
}
