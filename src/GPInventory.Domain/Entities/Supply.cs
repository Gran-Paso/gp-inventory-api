using System.ComponentModel.DataAnnotations;
using GPInventory.Domain.Enums;

namespace GPInventory.Domain.Entities;

public class Supply : BaseEntity
{
    [Required]
    [StringLength(255)]
    public string Name { get; set; } = string.Empty;
    
    [StringLength(500)]
    public string? Description { get; set; }
    
    public int UnitMeasureId { get; set; } = 1; // Default to first unit measure
    
    public int? FixedExpenseId { get; set; }
    
    public bool Active { get; set; } = true;
    
    public int BusinessId { get; set; }
    
    public int StoreId { get; set; }
    
    public int? SupplyCategoryId { get; set; }
    
    public SupplyType Type { get; set; } = SupplyType.Both;
    
    public int MinimumStock { get; set; } = 0;
    
    // Usage tracking (not mapped to database)
    public int ComponentUsageCount { get; set; }
    public int ProcessUsageCount { get; set; }

    // Navigation properties
    public UnitMeasure? UnitMeasure { get; set; }
    public FixedExpense? FixedExpense { get; set; }
    public Business? Business { get; set; }
    public Store? Store { get; set; }
    public SupplyCategory? SupplyCategory { get; set; }
    
    // Collection navigation properties
    public ICollection<SupplyEntry> SupplyEntries { get; set; } = new List<SupplyEntry>();
    public ICollection<ProcessSupply> ProcessSupplies { get; set; } = new List<ProcessSupply>();

    public Supply()
    {
    }

    public Supply(string name, int businessId, int storeId, int unitMeasureId = 1, 
                 string? description = null, int? fixedExpenseId = null, bool active = true,
                 int? supplyCategoryId = null, SupplyType type = SupplyType.Both)
    {
        Name = name;
        BusinessId = businessId;
        StoreId = storeId;
        UnitMeasureId = unitMeasureId;
        Description = description;
        FixedExpenseId = fixedExpenseId;
        Active = active;
        SupplyCategoryId = supplyCategoryId;
        Type = type;
    }
}
