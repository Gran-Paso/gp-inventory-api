using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace GPInventory.Domain.Entities;

/// <summary>
/// Catálogo de tipos de relación entre cliente raíz y sub-cliente
/// (e.g. hijo, hija, pareja, hermano/a, etc.)
/// </summary>
[Table("service_client_relationship_type")]
public class ServiceClientRelationshipType
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Required]
    [StringLength(50)]
    [Column("code")]
    public string Code { get; set; } = string.Empty;

    [Required]
    [StringLength(100)]
    [Column("label")]
    public string Label { get; set; } = string.Empty;

    [StringLength(255)]
    [Column("description")]
    public string? Description { get; set; }

    [Column("sort_order")]
    public int SortOrder { get; set; } = 0;

    [Column("active")]
    public bool Active { get; set; } = true;

    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [Column("updated_at")]
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    // Navigation
    public virtual ICollection<ServiceClient> SubClients { get; set; } = [];
}
