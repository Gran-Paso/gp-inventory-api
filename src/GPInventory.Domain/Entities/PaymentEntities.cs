using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace GPInventory.Domain.Entities;

[Table("receipt_types")]
public class ReceiptType : BaseEntity
{
    [Column("name")]
    [Required]
    [StringLength(100)]
    public string Name { get; set; } = string.Empty;

    [Column("description")]
    [StringLength(255)]
    public string? Description { get; set; }
}

[Table("payment_types")]
public class PaymentType : BaseEntity
{
    [Column("name")]
    [Required]
    [StringLength(100)]
    public string Name { get; set; } = string.Empty;
}

[Table("bank_entities")]
public class BankEntity : BaseEntity
{
    [Column("name")]
    [Required]
    [StringLength(200)]
    public string Name { get; set; } = string.Empty;
}

[Table("payment_plan")]
public class PaymentPlan : BaseEntity
{
    [Column("expense_id")]
    [ForeignKey(nameof(Expense))]
    public int? ExpenseId { get; set; }

    [Column("fixed_expense_id")]
    [ForeignKey(nameof(FixedExpense))]
    public int? FixedExpenseId { get; set; }

    [Column("payment_type_id")]
    [Required]
    [ForeignKey(nameof(PaymentType))]
    public int PaymentTypeId { get; set; }

    [Column("expressed_in_uf")]
    public bool ExpressedInUf { get; set; } = false;

    [Column("bank_entity_id")]
    [ForeignKey(nameof(BankEntity))]
    public int? BankEntityId { get; set; }

    [Column("installments_count")]
    [Required]
    public int InstallmentsCount { get; set; }

    [Column("start_date")]
    [Required]
    public DateTime StartDate { get; set; }

    // Navigation properties
    public virtual Expense? Expense { get; set; }
    public virtual FixedExpense? FixedExpense { get; set; }
    public virtual PaymentType PaymentType { get; set; } = null!;
    public virtual BankEntity? BankEntity { get; set; }
    public virtual ICollection<PaymentInstallment> Installments { get; set; } = new List<PaymentInstallment>();
}

[Table("payment_installment")]
public class PaymentInstallment : BaseEntity
{
    [Column("payment_plan_id")]
    [Required]
    [ForeignKey(nameof(PaymentPlan))]
    public int PaymentPlanId { get; set; }

    [Column("installment_number")]
    [Required]
    public int InstallmentNumber { get; set; }

    [Column("due_date")]
    [Required]
    public DateTime DueDate { get; set; }

    [Column("amount_clp", TypeName = "decimal(18,2)")]
    [Required]
    public decimal AmountClp { get; set; }

    [Column("amount_uf", TypeName = "decimal(18,4)")]
    public decimal? AmountUf { get; set; }

    [Column("status")]
    [Required]
    [StringLength(50)]
    public string Status { get; set; } = "pendiente"; // pendiente, pagado, vencido

    [Column("paid_date")]
    public DateTime? PaidDate { get; set; }

    [Column("payment_method_id")]
    [ForeignKey(nameof(PaymentMethod))]
    public int? PaymentMethodId { get; set; }

    [Column("expense_id")]
    [ForeignKey(nameof(Expense))]
    public int? ExpenseId { get; set; } // Reference to expense created when paid

    // Navigation properties
    public virtual PaymentPlan PaymentPlan { get; set; } = null!;
    public virtual PaymentMethod? PaymentMethod { get; set; }
    public virtual Expense? Expense { get; set; }
}
