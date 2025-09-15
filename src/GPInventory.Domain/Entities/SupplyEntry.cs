using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace GPInventory.Domain.Entities;

public class SupplyEntry : BaseEntity
{
    [Required]
    [Column(TypeName = "decimal(18,4)")]
    public decimal UnitCost { get; set; }
    
    [Required]
    public int Amount { get; set; }
    
    public int ProviderId { get; set; }
    
    public int SupplyId { get; set; }
    
    public int? ProcessDoneId { get; set; }
    
    /// <summary>
    /// Referencia al SupplyEntry original cuando esta es una entrada de consumo (negativa)
    /// </summary>
    public int? ReferenceToSupplyEntry { get; set; }

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
        Amount = (int)amount; // Cast decimal to int
        ProviderId = providerId;
        SupplyId = supplyId;
        ProcessDoneId = processDoneId;
        // Para entradas originales (positivas), IsActive = true por defecto desde BaseEntity
        IsActive = amount > 0;
    }

    // Constructor para autoreferencing (consumo con referencia a supply entry original)
    public SupplyEntry(decimal unitCost, decimal amount, 
                      int providerId, int supplyId, int? processDoneId, 
                      int referencedSupplyEntryId)
    {
        UnitCost = unitCost;
        Amount = (int)amount;
        ProviderId = providerId;
        SupplyId = supplyId;
        ProcessDoneId = processDoneId;
        ReferenceToSupplyEntry = referencedSupplyEntryId; // ⭐ Guardar la referencia
        // Para consumos (negativos), IsActive = false
        IsActive = amount > 0;
    }
}
