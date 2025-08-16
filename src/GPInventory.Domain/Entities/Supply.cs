using System.ComponentModel.DataAnnotations;

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

    // Navigation properties
    // public UnitMeasure UnitMeasure { get; set; } = null!; // Temporarily removed to fix EF Core issue
    public FixedExpense? FixedExpense { get; set; }
    public Business Business { get; set; } = null!;
    public Store Store { get; set; } = null!;
    
    // Collection navigation properties
    public ICollection<SupplyEntry> SupplyEntries { get; set; } = new List<SupplyEntry>();
    public ICollection<ProcessSupply> ProcessSupplies { get; set; } = new List<ProcessSupply>();

    public Supply()
    {
    }

    public Supply(string name, int businessId, int storeId, int unitMeasureId = 1, 
                 string? description = null, int? fixedExpenseId = null, bool active = true)
    {
        Name = name;
        BusinessId = businessId;
        StoreId = storeId;
        UnitMeasureId = unitMeasureId;
        Description = description;
        FixedExpenseId = fixedExpenseId;
        Active = active;
    }
}
