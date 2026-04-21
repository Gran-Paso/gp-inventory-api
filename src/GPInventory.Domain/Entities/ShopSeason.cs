using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace GPInventory.Domain.Entities;

/// <summary>
/// Temporada de catálogo. Agrupa banners, colecciones y campañas bajo un mismo "tema".
/// Al activar una temporada todo el catálogo visible en el canal cambia a esa configuración.
/// Solo puede haber una temporada activa por (business_id, channel) a la vez.
/// Contenido sin season_id (NULL) se considera "siempre visible" y se muestra en cualquier estado.
/// </summary>
[Table("shop_season")]
public class ShopSeason
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Required]
    [Column("business_id")]
    public int BusinessId { get; set; }

    /// <summary>Canal al que aplica la temporada. NULL = todos los canales.</summary>
    [Column("channel")]
    [StringLength(50)]
    public string? Channel { get; set; }

    [Required]
    [Column("name")]
    [StringLength(200)]
    public string Name { get; set; } = null!;

    [Column("description")]
    [StringLength(1000)]
    public string? Description { get; set; }

    [Column("cover_image_url")]
    [StringLength(500)]
    public string? CoverImageUrl { get; set; }

    /// <summary>¿Es la temporada actualmente activa para su canal?</summary>
    [Column("is_active")]
    public bool IsActive { get; set; } = false;

    [Column("starts_at")]
    public DateTime? StartsAt { get; set; }

    [Column("ends_at")]
    public DateTime? EndsAt { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [Column("updated_at")]
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    [ForeignKey("BusinessId")]
    public virtual Business Business { get; set; } = null!;

    public virtual ICollection<ShopBanner> Banners { get; set; } = new List<ShopBanner>();
    public virtual ICollection<ShopCollection> Collections { get; set; } = new List<ShopCollection>();
    public virtual ICollection<ShopCampaign> Campaigns { get; set; } = new List<ShopCampaign>();
}
