using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace GPInventory.Application.DTOs.Expenses;

public class CreateFixedExpenseDto
{
    [Required]
    [StringLength(255, ErrorMessage = "La descripción no puede exceder 255 caracteres")]
    [JsonPropertyName("name")]
    public string Description { get; set; } = string.Empty;

    [Required]
    [Range(1, int.MaxValue, ErrorMessage = "El monto debe ser mayor a 0")]
    public int Amount { get; set; }

    [JsonPropertyName("subcategory_id")]
    public int? SubcategoryId { get; set; }

    [Required]
    [JsonPropertyName("recurrence_id")]
    public int RecurrenceTypeId { get; set; }

    [Required]
    [JsonPropertyName("payment_date")]
    public DateTime StartDate { get; set; }

    public DateTime? EndDate { get; set; }

    [StringLength(500, ErrorMessage = "Las notas no pueden exceder 500 caracteres")]
    [JsonPropertyName("additional_note")]
    public string? Notes { get; set; }

    [Required]
    [JsonPropertyName("business_id")]
    public int BusinessId { get; set; }

    [JsonPropertyName("store_id")]
    public int? StoreId { get; set; }
}

public class UpdateFixedExpenseDto
{
    [StringLength(255, ErrorMessage = "La descripción no puede exceder 255 caracteres")]
    public string? Description { get; set; }

    [Range(1, int.MaxValue, ErrorMessage = "El monto debe ser mayor a 0")]
    public int? Amount { get; set; }

    public int? SubcategoryId { get; set; }

    public int? RecurrenceTypeId { get; set; }

    public DateTime? StartDate { get; set; }

    public DateTime? EndDate { get; set; }

    [StringLength(500, ErrorMessage = "Las notas no pueden exceder 500 caracteres")]
    public string? Notes { get; set; }

    public int? StoreId { get; set; }
}

public class FixedExpenseDto
{
    public int Id { get; set; }
    public string Description { get; set; } = string.Empty;
    public int Amount { get; set; }
    public int? SubcategoryId { get; set; }
    public int RecurrenceTypeId { get; set; }
    public DateTime StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public string? Notes { get; set; }
    public int BusinessId { get; set; }
    public int? StoreId { get; set; }
    public bool IsActive { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    
    // Estado de pago
    public bool IsUpToDate { get; set; }
    public DateTime NextDueDate { get; set; }
    public DateTime? LastPaymentDate { get; set; }
}

public class FixedExpenseWithDetailsDto
{
    public int Id { get; set; }
    public string Description { get; set; } = string.Empty;
    public int Amount { get; set; }
    public int? SubcategoryId { get; set; }
    public int RecurrenceTypeId { get; set; }
    public DateTime StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public string? Notes { get; set; }
    public int BusinessId { get; set; }
    public int? StoreId { get; set; }
    public bool IsActive { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    
    // Estado de pago
    public bool IsUpToDate { get; set; }
    public DateTime NextDueDate { get; set; }
    public DateTime? LastPaymentDate { get; set; }
    
    // Detalles relacionados
    public ExpenseCategoryDto? Category { get; set; } // Obtenida a través de la subcategoría
    public ExpenseSubcategoryDto? Subcategory { get; set; }
    public RecurrenceTypeDto RecurrenceType { get; set; } = null!;
    public string? StoreName { get; set; }
    
    // Expenses asociados a este gasto fijo
    public List<ExpenseDto> AssociatedExpenses { get; set; } = new List<ExpenseDto>();
}
