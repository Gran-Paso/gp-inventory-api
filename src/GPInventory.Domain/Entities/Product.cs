using GPInventory.Domain.Entities;
using System.ComponentModel.DataAnnotations.Schema;

namespace GPInventory.Domain.Entities;

public class Product : BaseEntity
{
    public string Name { get; set; } = string.Empty;
    public string? Image { get; set; }
    public int ProductTypeId { get; set; }
    public decimal Price { get; set; }
    public decimal Cost { get; set; }
    
    public string? Sku { get; set; }
    public DateTime Date { get; set; }
    public int BusinessId { get; set; }
    public int MinimumStock { get; set; } = 0;

    // Navigation properties
    public ProductType ProductType { get; set; } = null!;
    public Business Business { get; set; } = null!;
    public ICollection<Stock> Stocks { get; set; } = new List<Stock>();

    public Product()
    {
        Date = DateTime.UtcNow;
    }

    public Product(string name, int productTypeId, decimal price, decimal cost, int businessId)
    {
        Name = name;
        ProductTypeId = productTypeId;
        Price = price;
        Cost = cost;
        BusinessId = businessId;
        Date = DateTime.UtcNow;
    }
}
