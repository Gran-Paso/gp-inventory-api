using System.ComponentModel.DataAnnotations;

namespace GPInventory.Domain.Entities;

public class ProcessSupply : BaseEntity
{
    public int ProcessId { get; set; }
    
    public int SupplyId { get; set; }
    
    public int Order { get; set; }

    // Navigation properties
    public Process Process { get; set; } = null!;
    public Supply Supply { get; set; } = null!;

    public ProcessSupply()
    {
    }

    public ProcessSupply(int processId, int supplyId, int order)
    {
        ProcessId = processId;
        SupplyId = supplyId;
        Order = order;
    }
}
