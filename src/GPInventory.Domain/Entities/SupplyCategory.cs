using System.ComponentModel.DataAnnotations;

namespace GPInventory.Domain.Entities;

public class SupplyCategory : BaseEntity
{
    [Required]
    [StringLength(100)]
    public string Name { get; set; } = string.Empty;
    
    [StringLength(500)]
    public string? Description { get; set; }
    
    public bool Active { get; set; } = true;
    
    public int BusinessId { get; set; }
    
    // Navigation properties
    public Business? Business { get; set; }
    public ICollection<Supply> Supplies { get; set; } = new List<Supply>();
    public ICollection<Component> Components { get; set; } = new List<Component>();
    
    public SupplyCategory()
    {
    }
    
    public SupplyCategory(string name, int businessId, string? description = null, bool active = true)
    {
        Name = name;
        BusinessId = businessId;
        Description = description;
        Active = active;
    }
}
