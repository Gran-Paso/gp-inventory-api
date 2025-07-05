using GPInventory.Domain.Entities;

namespace GPInventory.Domain.Entities;

public class Stock : BaseEntity
{
    public int ProductId { get; set; }
    public DateTime Date { get; set; }
    public int FlowId { get; set; }
    public int Amount { get; set; }
    public int? AuctionPrice { get; set; }

    // Navigation properties
    public Product Product { get; set; } = null!;
    public FlowType Flow { get; set; } = null!;

    public Stock()
    {
        Date = DateTime.UtcNow;
    }

    public Stock(int productId, int flowId, int amount, int? auctionPrice = null)
    {
        ProductId = productId;
        FlowId = flowId;
        Amount = amount;
        AuctionPrice = auctionPrice;
        Date = DateTime.UtcNow;
    }
}
