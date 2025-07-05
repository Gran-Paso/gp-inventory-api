using GPInventory.Domain.Entities;

namespace GPInventory.Domain.Entities;

public class Product : BaseEntity
{
    public string Name { get; set; } = string.Empty;
    public string? Image { get; set; }
    public int ProductTypeId { get; set; }
    public int Price { get; set; }
    public int Cost { get; set; }
    public string? Sku { get; set; }
    public DateTime Date { get; set; }
    public int BusinessId { get; set; }

    // Navigation properties
    public ProductType ProductType { get; set; } = null!;
    public Business Business { get; set; } = null!;
    public ICollection<Stock> Stocks { get; set; } = new List<Stock>();

    public Product()
    {
        Date = DateTime.UtcNow;
    }

    public Product(string name, int productTypeId, int price, int cost, int businessId)
    {
        Name = name;
        ProductTypeId = productTypeId;
        Price = price;
        Cost = cost;
        BusinessId = businessId;
        Date = DateTime.UtcNow;
    }
}
