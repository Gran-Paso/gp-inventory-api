using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace GPInventory.Domain.Entities;

public class ProcessDone : BaseEntity
{
    public int ProcessId { get; set; }
    
    public int Stage { get; set; } = 0; // 0 = no iniciado, 1+ = etapa actual del insumo
    
    public DateTime? StartDate { get; set; }
    
    public DateTime? EndDate { get; set; }
    
    public int? StockId { get; set; }
    
    public int Amount { get; set; }
    
    /// <summary>
    /// Costo unitario del producto producido (costo total de insumos / cantidad producida)
    /// </summary>
    [Column(TypeName = "decimal(18,4)")]
    public decimal Cost { get; set; } = 0;
    
    public DateTime CompletedAt { get; set; } = DateTime.UtcNow;
    
    [StringLength(500)]
    public string? Notes { get; set; }

    // Navigation properties
    public Process Process { get; set; } = null!;
    public Stock? Stock { get; set; }
    
    // Collection navigation properties
    public ICollection<SupplyEntry> SupplyEntries { get; set; } = new List<SupplyEntry>();

    public ProcessDone()
    {
    }

    public ProcessDone(int processId, int amount, int stage = 0, 
                      DateTime? startDate = null, DateTime? endDate = null,
                      DateTime? completedAt = null, string? notes = null)
    {
        ProcessId = processId;
        Amount = amount;
        Stage = stage;
        StartDate = startDate;
        EndDate = endDate;
        CompletedAt = completedAt ?? DateTime.UtcNow;
        Notes = notes;
    }
}
