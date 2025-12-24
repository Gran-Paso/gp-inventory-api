namespace GPInventory.Domain.Entities;

public class Component
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public int BusinessId { get; set; }
    public int? StoreId { get; set; }
    public int UnitMeasureId { get; set; }
    public int? PreparationTime { get; set; }
    public int? TimeUnitId { get; set; }
    public decimal YieldAmount { get; set; }
    public bool Active { get; set; } = true;
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }

    // Non-persisted properties for display
    public string? UnitMeasureName { get; set; }
    public string? UnitMeasureSymbol { get; set; }
    
    // Navigation properties
    public virtual ICollection<ComponentSupply> Supplies { get; set; } = new List<ComponentSupply>();
    public virtual ICollection<ComponentSupply> UsedInComponents { get; set; } = new List<ComponentSupply>();
    public virtual ICollection<ComponentProduction> Productions { get; set; } = new List<ComponentProduction>();
}
