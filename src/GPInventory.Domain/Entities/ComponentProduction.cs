namespace GPInventory.Domain.Entities;

public class ComponentProduction
{
    public int Id { get; set; }
    public int ComponentId { get; set; }
    public int? ProcessDoneId { get; set; }
    public int BusinessId { get; set; }
    public int StoreId { get; set; }
    public decimal ProducedAmount { get; set; }
    public DateTime? ProductionDate { get; set; }
    public DateTime? ExpirationDate { get; set; }
    public string? BatchNumber { get; set; }
    public decimal Cost { get; set; }
    public string? Notes { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; }
    
    // Navigation properties
    public virtual Component Component { get; set; } = null!;
    public virtual ProcessDone? ProcessDone { get; set; }
    public virtual Business? Business { get; set; }
    public virtual Store? Store { get; set; }
}
