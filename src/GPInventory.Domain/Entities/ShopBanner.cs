using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace GPInventory.Domain.Entities;

[Table("shop_banner")]
public class ShopBanner
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Required]
    [Column("business_id")]
    public int BusinessId { get; set; }

    /// <summary>Canal: "webadas", "instagram", etc. NULL = todos los canales.</summary>
    [Column("channel")]
    [StringLength(50)]
    public string? Channel { get; set; }

    [Required]
    [Column("title")]
    [StringLength(200)]
    public string Title { get; set; } = null!;

    [Column("subtitle")]
    [StringLength(400)]
    public string? Subtitle { get; set; }

    [Column("image_url")]
    [StringLength(500)]
    public string? ImageUrl { get; set; }

    /// <summary>URL de redirección (producto, colección, URL externa, etc.).</summary>
    [Column("redirect_url")]
    [StringLength(500)]
    public string? RedirectUrl { get; set; }

    [Column("display_order")]
    public int DisplayOrder { get; set; } = 0;

    [Column("active")]
    public bool Active { get; set; } = true;

    [Column("starts_at")]
    public DateTime? StartsAt { get; set; }

    [Column("ends_at")]
    public DateTime? EndsAt { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [Column("updated_at")]
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>Temporada a la que pertenece este banner. NULL = siempre visible.</summary>
    [Column("season_id")]
    public int? SeasonId { get; set; }

    /// <summary>
    /// Slot de ubicación en el storefront. Valores: "hero" (por defecto), o cualquier nombre libre
    /// que el storefront reconozca (ej: "mid-page", "promo-strip").
    /// </summary>
    [Column("slot")]
    [StringLength(50)]
    public string Slot { get; set; } = "hero";

    [ForeignKey("BusinessId")]
    public virtual Business Business { get; set; } = null!;

    [ForeignKey("SeasonId")]
    public virtual ShopSeason? Season { get; set; }
}
