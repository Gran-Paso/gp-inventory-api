using System.ComponentModel.DataAnnotations;

namespace GPInventory.Domain.Entities;

public class ProcessComponent : BaseEntity
{
    public int ProcessId { get; set; }
    
    public int ComponentId { get; set; }
    
    public int Order { get; set; }

    // Navigation properties
    public Process Process { get; set; } = null!;
    public Component Component { get; set; } = null!;

    public ProcessComponent()
    {
    }

    public ProcessComponent(int processId, int componentId, int order)
    {
        ProcessId = processId;
        ComponentId = componentId;
        Order = order;
    }
}
