using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace GPInventory.Domain.Entities;

[Table("shop_collection_item")]
public class ShopCollectionItem
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Required]
    [Column("collection_id")]
    public int CollectionId { get; set; }

    [Required]
    [Column("product_id")]
    public int ProductId { get; set; }

    /// <summary>Orden dentro de la colección (drag-and-drop).</summary>
    [Column("display_order")]
    public int DisplayOrder { get; set; } = 0;

    /// <summary>Si true, el producto aparece siempre al tope independiente del sort_rule.</summary>
    [Column("pinned")]
    public bool Pinned { get; set; } = false;

    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [Column("updated_at")]
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    [ForeignKey("CollectionId")]
    public virtual ShopCollection Collection { get; set; } = null!;

    [ForeignKey("ProductId")]
    public virtual Product Product { get; set; } = null!;
}
