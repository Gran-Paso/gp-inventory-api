using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using GPInventory.Application.DTOs.Payments;
using GPInventory.Application.DTOs.Production;

namespace GPInventory.Application.DTOs.Expenses;

// DTO ligero para listados de items individuales (no confundir con ExpenseSummaryDto que es para totales/reportes)
public class ExpenseListItemDto
{
    public int Id { get; set; }
    public DateTime Date { get; set; }
    public decimal Amount { get; set; }
    
    [JsonPropertyName("amount_net")]
    public decimal? AmountNet { get; set; }
    
    [JsonPropertyName("amount_iva")]
    public decimal? AmountIva { get; set; }
    
    [JsonPropertyName("amount_total")]
    public decimal? AmountTotal { get; set; }
    
    [JsonPropertyName("receipt_type_id")]
    public int? ReceiptTypeId { get; set; }
    
    public string Description { get; set; } = string.Empty;
    
    [JsonPropertyName("is_fixed")]
    public bool? IsFixed { get; set; }
    
    [JsonPropertyName("business_id")]
    public int BusinessId { get; set; }
    
    [JsonPropertyName("expense_type_id")]
    public int? ExpenseTypeId { get; set; }
    
    [JsonPropertyName("category_name")]
    public string? CategoryName { get; set; }
    
    [JsonPropertyName("subcategory_name")]
    public string? SubcategoryName { get; set; }
    
    [JsonPropertyName("has_provider")]
    public bool HasProvider { get; set; }
    
    [JsonPropertyName("provider_name")]
    public string? ProviderName { get; set; }
    
    [JsonPropertyName("paid_installments")]
    public int? PaidInstallments { get; set; }
    
    [JsonPropertyName("total_installments")]
    public int? TotalInstallments { get; set; }
    
    [JsonPropertyName("has_overdue_installments")]
    public bool? HasOverdueInstallments { get; set; }
    
    [JsonPropertyName("payment_plan_id")]
    public int? PaymentPlanId { get; set; }
    
    [JsonPropertyName("next_installment_due_date")]
    public DateTime? NextInstallmentDueDate { get; set; }

    [JsonPropertyName("service_sale_id")]
    public int? ServiceSaleId { get; set; }

    /// <summary>Moneda: "CLP" (default) o "USD".</summary>
    [JsonPropertyName("currency")]
    public string? Currency { get; set; }

    /// <summary>Monto original en USD. Null si Currency = "CLP".</summary>
    [JsonPropertyName("amount_usd")]
    public decimal? AmountUsd { get; set; }

    /// <summary>Tipo de cambio CLP/USD al momento del registro. Null si Currency = "CLP".</summary>
    [JsonPropertyName("usd_exchange_rate")]
    public decimal? UsdExchangeRate { get; set; }

    /// <summary>Etiquetas libres definidas por el usuario para este egreso</summary>
    [JsonPropertyName("tags")]
    public List<TagInfo> Tags { get; set; } = new();

    public class TagInfo
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Color { get; set; } = "#6b7280";
    }
}

public class CreateExpenseDto
{
    [Required]
    [JsonPropertyName("subcategory_id")]
    public int SubcategoryId { get; set; }

    [Required]
    [Range(0, double.MaxValue, ErrorMessage = "El monto no puede ser negativo")]
    public decimal Amount { get; set; }

    // IVA fields for invoices with tax (Factura Afecta)
    [JsonPropertyName("amount_net")]
    public decimal? AmountNet { get; set; }

    [JsonPropertyName("amount_iva")]
    public decimal? AmountIva { get; set; }

    [JsonPropertyName("amount_total")]
    public decimal? AmountTotal { get; set; }

    // Receipt type: 1=Boleta, 2=Factura Exenta, 3=Factura Afecta, 4=Sin Documento
    [JsonPropertyName("receipt_type_id")]
    public int? ReceiptTypeId { get; set; }

    [Required]
    [StringLength(500, ErrorMessage = "La descripción no puede exceder 500 caracteres")]
    public string Description { get; set; } = string.Empty;

    [Required]
    public DateTime Date { get; set; }

    [Required]
    [JsonPropertyName("business_id")]
    public int BusinessId { get; set; }

    [JsonPropertyName("store_id")]
    public int? StoreId { get; set; }

    [JsonPropertyName("is_fixed")]
    public bool? IsFixed { get; set; }

    [JsonPropertyName("fixed_expense_id")]
    public int? FixedExpenseId { get; set; }

    [JsonPropertyName("expense_type_id")]
    public int? ExpenseTypeId { get; set; }

    [JsonPropertyName("provider_id")]
    public int? ProviderId { get; set; }
    
    // Payment Plan fields (optional - for financing)
    [JsonPropertyName("payment_type_id")]
    public int? PaymentTypeId { get; set; }
    
    [JsonPropertyName("installments_count")]
    public int? InstallmentsCount { get; set; }
    
    [JsonPropertyName("expressed_in_uf")]
    public bool? ExpressedInUf { get; set; }
    
    [JsonPropertyName("bank_entity_id")]
    public int? BankEntityId { get; set; }
    
    [JsonPropertyName("payment_start_date")]
    public DateTime? PaymentStartDate { get; set; }

    [JsonPropertyName("service_sale_id")]
    public int? ServiceSaleId { get; set; }

    /// <summary>Moneda: "CLP" (default) o "USD".</summary>
    [JsonPropertyName("currency")]
    public string? Currency { get; set; }

    /// <summary>Monto original en USD si Currency = "USD". El Amount/AmountTotal almacena el equivalente en CLP.</summary>
    [JsonPropertyName("amount_usd")]
    public decimal? AmountUsd { get; set; }

    /// <summary>Tipo de cambio CLP/USD al momento del registro. Null si Currency = "CLP".</summary>
    [JsonPropertyName("usd_exchange_rate")]
    public decimal? UsdExchangeRate { get; set; }
}

public class UpdateExpenseDto
{
    [JsonPropertyName("subcategory_id")]
    public int? SubcategoryId { get; set; }

    [Range(0.01, double.MaxValue, ErrorMessage = "El monto debe ser mayor a 0")]
    public decimal? Amount { get; set; }

    [StringLength(500, ErrorMessage = "La descripción no puede exceder 500 caracteres")]
    public string? Description { get; set; }

    public DateTime? Date { get; set; }

    [JsonPropertyName("store_id")]
    public int? StoreId { get; set; }
}

public class ExpenseDto
{
    public int Id { get; set; }
    public DateTime Date { get; set; }
    [JsonPropertyName("subcategory_id")]
    public int SubcategoryId { get; set; }
    public decimal Amount { get; set; }
    
    // IVA fields
    [JsonPropertyName("amount_net")]
    public decimal? AmountNet { get; set; }
    
    [JsonPropertyName("amount_iva")]
    public decimal? AmountIva { get; set; }
    
    [JsonPropertyName("amount_total")]
    public decimal? AmountTotal { get; set; }
    
    public string Description { get; set; } = string.Empty;
    [JsonPropertyName("is_fixed")]
    public bool? IsFixed { get; set; }
    [JsonPropertyName("fixed_expense_id")]
    public int? FixedExpenseId { get; set; }
    [JsonPropertyName("business_id")]
    public int BusinessId { get; set; }
    [JsonPropertyName("store_id")]
    public int? StoreId { get; set; }
    [JsonPropertyName("provider_id")]
    public int? ProviderId { get; set; }
    [JsonPropertyName("created_at")]
    public DateTime CreatedAt { get; set; }
    [JsonPropertyName("service_sale_id")]
    public int? ServiceSaleId { get; set; }

    /// <summary>Moneda: "CLP" (default) o "USD".</summary>
    [JsonPropertyName("currency")]
    public string? Currency { get; set; }

    /// <summary>Monto original en USD. Null si Currency = "CLP".</summary>
    [JsonPropertyName("amount_usd")]
    public decimal? AmountUsd { get; set; }

    /// <summary>Tipo de cambio CLP/USD al momento del registro. Null si Currency = "CLP".</summary>
    [JsonPropertyName("usd_exchange_rate")]
    public decimal? UsdExchangeRate { get; set; }
}

public class ExpenseWithDetailsDto
{
    public int Id { get; set; }
    public DateTime Date { get; set; }
    public decimal Amount { get; set; }
    
    // IVA fields
    [JsonPropertyName("amount_net")]
    public decimal? AmountNet { get; set; }
    
    [JsonPropertyName("amount_iva")]
    public decimal? AmountIva { get; set; }
    
    [JsonPropertyName("amount_total")]
    public decimal? AmountTotal { get; set; }

    [JsonPropertyName("receipt_type_id")]
    public int? ReceiptTypeId { get; set; }

    /// <summary>Moneda: "CLP" (default) o "USD".</summary>
    [JsonPropertyName("currency")]
    public string? Currency { get; set; }

    /// <summary>Monto original en USD. Null si Currency = "CLP".</summary>
    [JsonPropertyName("amount_usd")]
    public decimal? AmountUsd { get; set; }

    /// <summary>Tipo de cambio CLP/USD al momento del registro. Null si Currency = "CLP".</summary>
    [JsonPropertyName("usd_exchange_rate")]
    public decimal? UsdExchangeRate { get; set; }
    
    public string Description { get; set; } = string.Empty;
    [JsonPropertyName("is_fixed")]
    public bool? IsFixed { get; set; }
    [JsonPropertyName("fixed_expense_id")]
    public int? FixedExpenseId { get; set; }
    [JsonPropertyName("business_id")]
    public int BusinessId { get; set; }
    [JsonPropertyName("store_id")]
    public int? StoreId { get; set; }
    [JsonPropertyName("created_at")]
    public DateTime CreatedAt { get; set; }
    [JsonPropertyName("expense_type_id")]
    public int? ExpenseTypeId { get; set; }
    [JsonPropertyName("provider_id")]
    public int? ProviderId { get; set; }
    
    // Detalles relacionados
    public ExpenseSubcategoryDto Subcategory { get; set; } = null!;
    public ExpenseCategoryDto Category { get; set; } = null!;
    [JsonPropertyName("store_name")]
    public string? StoreName { get; set; }
    
    // NUEVO: Payment Plan con cuotas
    [JsonPropertyName("payment_plan")]
    public PaymentPlanWithInstallmentsDto? PaymentPlan { get; set; }
    
    // NUEVO: Provider (para costos asociados a supply entries)
    public ProviderDto? Provider { get; set; }

    [JsonPropertyName("service_sale_id")]
    public int? ServiceSaleId { get; set; }
}
