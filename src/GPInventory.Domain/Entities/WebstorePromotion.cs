using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace GPInventory.Domain.Entities;

[Table("webstore_promotion")]
public class WebstorePromotion
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Required]
    [Column("business_id")]
    public int BusinessId { get; set; }

    /// <summary>Canal externo: "webadas", "instagram", etc.</summary>
    [Required]
    [Column("channel")]
    [StringLength(50)]
    public string Channel { get; set; } = null!;

    [Required]
    [Column("product_id")]
    public int ProductId { get; set; }

    [Column("discount_pct")]
    public decimal DiscountPct { get; set; } = 0;

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

    [ForeignKey("BusinessId")]
    public virtual Business Business { get; set; } = null!;

    [ForeignKey("ProductId")]
    public virtual Product Product { get; set; } = null!;
}
