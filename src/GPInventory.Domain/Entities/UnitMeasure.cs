using System.ComponentModel.DataAnnotations;

namespace GPInventory.Domain.Entities;

public class UnitMeasure : BaseEntity
{
    [Required]
    [StringLength(100)]
    public string Name { get; set; } = string.Empty;
    
    [StringLength(10)]
    public string? Symbol { get; set; }
    
    [StringLength(200)]
    public string? Description { get; set; }

    // Collection navigation properties
    public ICollection<SupplyEntry> SupplyEntries { get; set; } = new List<SupplyEntry>();
    public ICollection<Supply> Supplies { get; set; } = new List<Supply>();

    public UnitMeasure()
    {
    }

    public UnitMeasure(string name, string? symbol = null, string? description = null)
    {
        Name = name;
        Symbol = symbol;
        Description = description;
    }
}
