using System.ComponentModel.DataAnnotations;

namespace GPInventory.Domain.Entities;

public class ProductType : BaseEntity
{
    [Required]
    [StringLength(200)]
    public string Name { get; set; } = string.Empty;

    [StringLength(500)]
    public string? Description { get; set; }

    // Navigation properties
    public ICollection<Product> Products { get; set; } = new List<Product>();

    public ProductType()
    {
    }

    public ProductType(string name, string? description = null)
    {
        Name = name;
        Description = description;
    }
}
