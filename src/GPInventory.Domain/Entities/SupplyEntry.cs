using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace GPInventory.Domain.Entities;

public class SupplyEntry : BaseEntity
{
    [Required]
    [Column(TypeName = "decimal(10,2)")]
    public decimal UnitCost { get; set; }
    
    [Required]
    [Column(TypeName = "decimal(10,2)")]
    public decimal Amount { get; set; }
    
    public int ProviderId { get; set; }
    
    public int SupplyId { get; set; }
    
    public int? ProcessDoneId { get; set; }

    // Navigation properties
    public Provider Provider { get; set; } = null!;
    public Supply Supply { get; set; } = null!;
    public ProcessDone? ProcessDone { get; set; }

    public SupplyEntry()
    {
    }

    public SupplyEntry(decimal unitCost, decimal amount, 
                      int providerId, int supplyId, int? processDoneId = null)
    {
        UnitCost = unitCost;
        Amount = amount;
        ProviderId = providerId;
        SupplyId = supplyId;
        ProcessDoneId = processDoneId;
    }
}
