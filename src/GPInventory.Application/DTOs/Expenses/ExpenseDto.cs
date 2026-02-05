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
    public string Description { get; set; } = string.Empty;
    public bool? IsFixed { get; set; }
    public int BusinessId { get; set; }
    public int? ExpenseTypeId { get; set; }
    public string? CategoryName { get; set; }
    public string? SubcategoryName { get; set; }
    public bool HasProvider { get; set; }
    public string? ProviderName { get; set; }
    public int? PaidInstallments { get; set; }
    public int? TotalInstallments { get; set; }
    public bool? HasOverdueInstallments { get; set; }
    public int? PaymentPlanId { get; set; }
    public DateTime? NextInstallmentDueDate { get; set; }
}

public class CreateExpenseDto
{
    [Required]
    [JsonPropertyName("subcategory_id")]
    public int SubcategoryId { get; set; }

    [Required]
    [Range(0.01, double.MaxValue, ErrorMessage = "El monto debe ser mayor a 0")]
    public decimal Amount { get; set; }

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
}

public class ExpenseWithDetailsDto
{
    public int Id { get; set; }
    public DateTime Date { get; set; }
    public decimal Amount { get; set; }
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
}
