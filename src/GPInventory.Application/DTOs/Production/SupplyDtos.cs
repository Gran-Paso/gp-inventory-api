using System.ComponentModel.DataAnnotations;
using GPInventory.Application.DTOs.Expenses;
using GPInventory.Domain.Enums;

namespace GPInventory.Application.DTOs.Production;

public class CreateSupplyDto
{
    [Required]
    [StringLength(255)]
    public string Name { get; set; } = string.Empty;
    
    [StringLength(100)]
    public string? Sku { get; set; }
    
    [StringLength(500)]
    public string? Description { get; set; }
    
    [Required]
    public int UnitMeasureId { get; set; }
    
    // Datos para crear el gasto fijo automáticamente
    [Required]
    public int FixedExpenseAmount { get; set; }
    
    public int? SubcategoryId { get; set; }
    
    public DateTime? PaymentDate { get; set; }
    
    public bool Active { get; set; } = true;
    
    public int? SupplyCategoryId { get; set; }
    
    public SupplyType Type { get; set; } = SupplyType.Both;
    
    public int MinimumStock { get; set; } = 0;
    
    public int? PreferredProviderId { get; set; }
    
    [Required]
    public int BusinessId { get; set; }
    
    [Required]
    public int StoreId { get; set; }
}

public class SupplyDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Sku { get; set; }
    public string? Description { get; set; }
    public int UnitMeasureId { get; set; }
    public int? FixedExpenseId { get; set; }
    public int? ExpenseTypeId { get; set; }
    public bool Active { get; set; }
    public int BusinessId { get; set; }
    public int StoreId { get; set; }
    public int? SupplyCategoryId { get; set; }
    public SupplyType Type { get; set; }
    public int UsageCount { get; set; } = 0; // Total usage (legacy)
    public int ComponentUsageCount { get; set; } = 0; // Usage in components
    public int ProcessUsageCount { get; set; } = 0; // Usage in processes
    public int MinimumStock { get; set; } = 0;
    public int? PreferredProviderId { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    
    // Stock information
    public int CurrentStock { get; set; } = 0;
    public StockStatus StockStatus { get; set; } = StockStatus.OutOfStock;
    
    // Navigation properties
    public UnitMeasureDto? UnitMeasure { get; set; }
    public FixedExpenseDto? FixedExpense { get; set; }
    public ExpenseTypeDto? ExpenseType { get; set; }
    public int? SubcategoryId => FixedExpense?.SubcategoryId; // Subcategory from fixed expense
    public BusinessDto? Business { get; set; }
    public StoreDto? Store { get; set; }
    public SupplyCategoryDto? SupplyCategory { get; set; }
    public ProviderDto? PreferredProvider { get; set; }
    
    // Collection properties
    public ICollection<SupplyEntryDto> SupplyEntries { get; set; } = new List<SupplyEntryDto>();
}

public class UpdateSupplyDto
{
    [Required]
    [StringLength(255)]
    public string Name { get; set; } = string.Empty;
    
    [StringLength(100)]
    public string? Sku { get; set; }
    
    [StringLength(500)]
    public string? Description { get; set; }
    
    [Required]
    public int UnitMeasureId { get; set; }
    
    // Datos para actualizar el gasto fijo
    [Required]
    public int FixedExpenseAmount { get; set; }
    
    public int? SubcategoryId { get; set; }
    
    public DateTime? PaymentDate { get; set; }
    
    public int? ExpenseTypeId { get; set; }
    
    public bool Active { get; set; } = true;
    
    public int? SupplyCategoryId { get; set; }
    
    public SupplyType Type { get; set; } = SupplyType.Both;
    
    public int MinimumStock { get; set; } = 0;
    
    public int? PreferredProviderId { get; set; }
    
    [Required]
    public int StoreId { get; set; }
}

// DTOs auxiliares para referencias
public class BusinessDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
}

public class StoreDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Location { get; set; }
}

public class SupplyCategoryDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public bool Active { get; set; }
}

public class CreateSupplyCategoryDto
{
    [Required]
    [StringLength(100)]
    public string Name { get; set; } = string.Empty;
    
    [StringLength(500)]
    public string? Description { get; set; }
    
    public bool Active { get; set; } = true;
    
    [Required]
    public int BusinessId { get; set; }
}

public class FixedExpenseDto
{
    public int Id { get; set; }
    public string AdditionalNote { get; set; } = string.Empty;
    public int Amount { get; set; }
    public int? SubcategoryId { get; set; }
}
