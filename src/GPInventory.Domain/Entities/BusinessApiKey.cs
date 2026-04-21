using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace GPInventory.Domain.Entities;

[Table("business_api_keys")]
public class BusinessApiKey
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Required]
    [Column("business_id")]
    public int BusinessId { get; set; }

    /// <summary>SHA-256 hex de la clave real. Nunca almacenar en texto plano.</summary>
    [Required]
    [Column("key_hash")]
    [StringLength(64)]
    public string KeyHash { get; set; } = null!;

    [Column("label")]
    [StringLength(100)]
    public string? Label { get; set; }

    /// <summary>JSON array de scopes, ej: ["products:read","sales:write"]</summary>
    [Column("scopes", TypeName = "json")]
    public string? Scopes { get; set; }

    [Column("expires_at")]
    public DateTime? ExpiresAt { get; set; }

    [Column("active")]
    public bool Active { get; set; } = true;

    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [Column("updated_at")]
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    [ForeignKey("BusinessId")]
    public virtual Business Business { get; set; } = null!;
}
