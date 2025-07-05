using System.ComponentModel.DataAnnotations;

namespace GPInventory.Domain.Entities;

public class Business : BaseEntity
{
    [Required]
    [StringLength(200)]
    public string CompanyName { get; set; } = string.Empty;

    public int? Theme { get; set; }

    [StringLength(20)]
    public string? PrimaryColor { get; set; }

    // Navigation properties
    public ICollection<UserHasBusiness> UserBusinesses { get; set; } = new List<UserHasBusiness>();
    public ICollection<Product> Products { get; set; } = new List<Product>();

    public Business()
    {
    }

    public Business(string companyName, int? theme = null, string? primaryColor = null)
    {
        CompanyName = companyName;
        Theme = theme;
        PrimaryColor = primaryColor;
    }
}
