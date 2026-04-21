using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace GPInventory.Domain.Entities;

[Table("shop_collection")]
public class ShopCollection
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Required]
    [Column("business_id")]
    public int BusinessId { get; set; }

    /// <summary>Canal: "webadas", etc. NULL = todos los canales.</summary>
    [Column("channel")]
    [StringLength(50)]
    public string? Channel { get; set; }

    [Required]
    [Column("name")]
    [StringLength(200)]
    public string Name { get; set; } = null!;

    /// <summary>URL-friendly slug (ej: "ofertas-verano").</summary>
    [Required]
    [Column("slug")]
    [StringLength(200)]
    public string Slug { get; set; } = null!;

    [Column("description")]
    [StringLength(1000)]
    public string? Description { get; set; }

    [Column("cover_image_url")]
    [StringLength(500)]
    public string? CoverImageUrl { get; set; }

    /// <summary>Color de fondo del encabezado en el storefront (ej: "#FF6B6B"). NULL = color por defecto.</summary>
    [Column("header_color")]
    [StringLength(20)]
    public string? HeaderColor { get; set; }

    /// <summary>
    /// Regla de ordenamiento automático por defecto para productos sin display_order manual.
    /// Valores: manual | best_sellers | highest_margin | newest | price_asc | price_desc
    /// </summary>
    [Column("sort_rule")]
    [StringLength(50)]
    public string SortRule { get; set; } = "manual";

    [Column("display_order")]
    public int DisplayOrder { get; set; } = 0;

    [Column("active")]
    public bool Active { get; set; } = true;

    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [Column("updated_at")]
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>Temporada a la que pertenece esta colección. NULL = siempre visible.</summary>
    [Column("season_id")]
    public int? SeasonId { get; set; }

    /// <summary>
    /// Modo de visualización: "catalog" (filtros en /catalogo, por defecto) o "featured"
    /// (sección de destacados en el home u otras páginas, mostrada como carrusel horizontal).
    /// </summary>
    [Column("display_as")]
    [StringLength(20)]
    public string DisplayAs { get; set; } = "catalog";

    [ForeignKey("BusinessId")]
    public virtual Business Business { get; set; } = null!;

    [ForeignKey("SeasonId")]
    public virtual ShopSeason? Season { get; set; }

    public virtual ICollection<ShopCollectionItem> Items { get; set; } = new List<ShopCollectionItem>();
}
