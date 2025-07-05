using GPInventory.Domain.Entities;

namespace GPInventory.Domain.Entities;

public class FlowType : BaseEntity
{
    public string Type { get; set; } = string.Empty;

    // Navigation properties
    public ICollection<Stock> Stocks { get; set; } = new List<Stock>();

    public FlowType()
    {
    }

    public FlowType(string type)
    {
        Type = type;
    }
}
