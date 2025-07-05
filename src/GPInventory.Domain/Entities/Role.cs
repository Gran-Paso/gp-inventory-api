using System.ComponentModel.DataAnnotations;

namespace GPInventory.Domain.Entities;

public class Role : BaseEntity
{
    [Required]
    [StringLength(100)]
    public string Name { get; set; } = string.Empty;

    [StringLength(500)]
    public string? Description { get; set; }

    // Navigation properties
    public ICollection<UserHasBusiness> UserBusinesses { get; set; } = new List<UserHasBusiness>();

    public Role()
    {
    }

    public Role(string name, string? description = null)
    {
        Name = name;
        Description = description;
    }
}
