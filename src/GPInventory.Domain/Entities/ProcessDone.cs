using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace GPInventory.Domain.Entities;

public class ProcessDone : BaseEntity
{
    public int ProcessId { get; set; }
    
    public int Quantity { get; set; }
    
    [Column(TypeName = "decimal(10,2)")]
    public decimal TotalCost { get; set; }
    
    public DateTime CompletedAt { get; set; } = DateTime.UtcNow;
    
    [StringLength(500)]
    public string? Notes { get; set; }

    // Navigation properties
    public Process Process { get; set; } = null!;
    
    // Collection navigation properties
    public ICollection<SupplyEntry> SupplyEntries { get; set; } = new List<SupplyEntry>();

    public ProcessDone()
    {
    }

    public ProcessDone(int processId, int quantity, decimal totalCost, 
                      DateTime? completedAt = null, string? notes = null)
    {
        ProcessId = processId;
        Quantity = quantity;
        TotalCost = totalCost;
        CompletedAt = completedAt ?? DateTime.UtcNow;
        Notes = notes;
    }
}
