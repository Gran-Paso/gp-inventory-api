using System.ComponentModel.DataAnnotations;

namespace GPInventory.Application.DTOs.Production;

public class CreateSupplyDto
{
    [Required]
    [StringLength(255)]
    public string Name { get; set; } = string.Empty;
    
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
    
    [Required]
    public int BusinessId { get; set; }
    
    [Required]
    public int StoreId { get; set; }
}

public class SupplyDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public int UnitMeasureId { get; set; }
    public int? FixedExpenseId { get; set; }
    public bool Active { get; set; }
    public int BusinessId { get; set; }
    public int StoreId { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    
    // Stock information
    public int CurrentStock { get; set; } = 0;
    
    // Navigation properties
    public UnitMeasureDto? UnitMeasure { get; set; }
    public FixedExpenseDto? FixedExpense { get; set; }
    public BusinessDto? Business { get; set; }
    public StoreDto? Store { get; set; }
    
    // Collection properties
    public ICollection<SupplyEntryDto> SupplyEntries { get; set; } = new List<SupplyEntryDto>();
}

public class UpdateSupplyDto
{
    [Required]
    [StringLength(255)]
    public string Name { get; set; } = string.Empty;
    
    [StringLength(500)]
    public string? Description { get; set; }
    
    [Required]
    public int UnitMeasureId { get; set; }
    
    // Datos para actualizar el gasto fijo
    [Required]
    public int FixedExpenseAmount { get; set; }
    
    public int? SubcategoryId { get; set; }
    
    public DateTime? PaymentDate { get; set; }
    
    public bool Active { get; set; } = true;
    
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

public class FixedExpenseDto
{
    public int Id { get; set; }
    public string AdditionalNote { get; set; } = string.Empty;
    public int Amount { get; set; }
}
