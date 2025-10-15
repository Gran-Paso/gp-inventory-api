using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace GPInventory.Application.DTOs.Expenses;

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
    
    // Detalles relacionados
    public ExpenseSubcategoryDto Subcategory { get; set; } = null!;
    public ExpenseCategoryDto Category { get; set; } = null!;
    [JsonPropertyName("store_name")]
    public string? StoreName { get; set; }
}
