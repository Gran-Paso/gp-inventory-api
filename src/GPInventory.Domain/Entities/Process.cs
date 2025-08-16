using System.ComponentModel.DataAnnotations;

namespace GPInventory.Domain.Entities;

public class Process : BaseEntity
{
    public int ProductId { get; set; }
    
    [Required]
    [StringLength(255)]
    public string Name { get; set; } = string.Empty;
    
    [StringLength(500)]
    public string? Description { get; set; }
    
    public int ProductionTime { get; set; }
    
    public int TimeUnitId { get; set; }
    
    public int StoreId { get; set; }

    // Navigation properties
    public Product Product { get; set; } = null!;
    public TimeUnit TimeUnit { get; set; } = null!;
    public Store Store { get; set; } = null!;
    
    // Collection navigation properties
    public ICollection<ProcessSupply> ProcessSupplies { get; set; } = new List<ProcessSupply>();
    public ICollection<ProcessDone> ProcessDones { get; set; } = new List<ProcessDone>();

    public Process()
    {
    }

    public Process(int productId, string name, int productionTime, int timeUnitId, 
                  int storeId,
                  string? description = null)
    {
        ProductId = productId;
        Name = name;
        Description = description;
        ProductionTime = productionTime;
        TimeUnitId = timeUnitId;
        StoreId = storeId;
    }
}
