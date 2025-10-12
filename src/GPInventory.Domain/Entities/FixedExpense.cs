using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace GPInventory.Domain.Entities;

public class FixedExpense
{
    public int Id { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public bool IsActive { get; set; } = true;
    public int BusinessId { get; set; }
    public int? StoreId { get; set; }
    
    [Required]
    [StringLength(255)]
    public string AdditionalNote { get; set; } = string.Empty;
    
    [Required]
    public int Amount { get; set; }
    
    public int? SubcategoryId { get; set; }
    public int RecurrenceTypeId { get; set; }
    
    [ForeignKey(nameof(ExpenseType))]
    public int? ExpenseTypeId { get; set; } // Tipo de egreso: Gasto, Costo o Inversión
    
    public DateTime? EndDate { get; set; }
    public DateTime? PaymentDate { get; set; }

    // Navigation properties
    public Business Business { get; set; } = null!;
    public Store? Store { get; set; }
    public ExpenseSubcategory? Subcategory { get; set; }
    public RecurrenceType RecurrenceType { get; set; } = null!;
    public ExpenseType? ExpenseType { get; set; } // Tipo de egreso
    
    // Related expenses generated from this fixed expense
    public ICollection<Expense> GeneratedExpenses { get; set; } = new List<Expense>();

    public FixedExpense()
    {
    }

    public FixedExpense(int businessId, string additionalNote, int amount, 
                       int recurrenceTypeId, int? storeId = null, 
                       int? subcategoryId = null, DateTime? endDate = null, 
                       DateTime? paymentDate = null, int? expenseTypeId = null)
    {
        BusinessId = businessId;
        StoreId = storeId;
        AdditionalNote = additionalNote;
        Amount = amount;
        SubcategoryId = subcategoryId;
        RecurrenceTypeId = recurrenceTypeId;
        EndDate = endDate;
        PaymentDate = paymentDate;
        ExpenseTypeId = expenseTypeId;
    }
}
