namespace GPInventory.Domain.Entities;

public class ComponentSupply
{
    public int Id { get; set; }
    public int ComponentId { get; set; }
    public int? SupplyId { get; set; }
    public int? SubComponentId { get; set; }
    public decimal Quantity { get; set; }
    public int Order { get; set; }
    public string ItemType { get; set; } = "supply"; // 'supply' | 'component'
    public bool IsOptional { get; set; } = false;
    
    // Navigation properties
    public virtual Component Component { get; set; } = null!;
    public virtual Supply? Supply { get; set; }
    public virtual Component? SubComponent { get; set; }
}
