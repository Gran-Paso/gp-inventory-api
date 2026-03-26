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
    [Column(TypeName = "decimal(18,2)")]
    public decimal Amount { get; set; }

    // IVA fields for invoices with tax (Factura Afecta - receipt_type_id = 3)
    [Column(TypeName = "decimal(18,2)")]
    public decimal? AmountNet { get; set; } // Monto neto (sin IVA)

    [Column(TypeName = "decimal(18,2)")]
    public decimal? AmountIva { get; set; } // Monto IVA (19%)

    [Column(TypeName = "decimal(18,2)")]
    public decimal? AmountTotal { get; set; } // Monto total (neto + IVA o monto único)

    // Receipt type: 1=Boleta, 2=Factura Exenta, 3=Factura Afecta, 4=Sin Documento
    [Column("receipt_type_id")]
    public int? ReceiptTypeId { get; set; }

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

    [ForeignKey(nameof(Provider))]
    [Column("provider_id")]
    public int? ProviderId { get; set; } // Proveedor asociado al gasto (cuando viene de un supply entry)

    [ForeignKey(nameof(PaymentPlan))]
    [Column("payment_plan_id")]
    public int? PaymentPlanId { get; set; } // Plan de pagos asociado (para crédito o financiamiento)

    [Column("service_sale_id")]
    public int? ServiceSaleId { get; set; } // Venta de servicio que originó este gasto (gp-services)

    /// <summary>Moneda en que fue registrado el egreso. "CLP" (default) o "USD".</summary>
    [Column("currency")]
    [StringLength(3)]
    public string Currency { get; set; } = "CLP";

    /// <summary>Monto original en USD si Currency = "USD". El campo Amount/AmountTotal siempre almacena el equivalente en CLP.</summary>
    [Column("amount_usd", TypeName = "decimal(18,4)")]
    public decimal? AmountUsd { get; set; }

    /// <summary>Tipo de cambio CLP/USD usado al registrar el egreso. Null si Currency = "CLP".</summary>
    [Column("usd_exchange_rate", TypeName = "decimal(18,4)")]
    public decimal? UsdExchangeRate { get; set; }

    // Navigation properties
    public ExpenseSubcategory ExpenseSubcategory { get; set; } = null!;
    public Business Business { get; set; } = null!;
    public Store? Store { get; set; }
    public FixedExpense? FixedExpense { get; set; } // Navigation to the fixed expense that generated this
    public ExpenseType? ExpenseType { get; set; } // Tipo de egreso
    public Provider? Provider { get; set; } // Proveedor asociado
    public ICollection<ExpenseTagAssignment> TagAssignments { get; set; } = new List<ExpenseTagAssignment>();
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
