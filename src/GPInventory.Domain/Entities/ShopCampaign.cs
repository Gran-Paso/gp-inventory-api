using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace GPInventory.Domain.Entities;

[Table("shop_campaign")]
public class ShopCampaign
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Required]
    [Column("business_id")]
    public int BusinessId { get; set; }

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

    [Column("starts_at")]
    public DateTime? StartsAt { get; set; }

    [Column("ends_at")]
    public DateTime? EndsAt { get; set; }

    [Column("active")]
    public bool Active { get; set; } = true;

    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [Column("updated_at")]
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>Temporada a la que pertenece esta campaña. NULL = siempre visible.</summary>
    [Column("season_id")]
    public int? SeasonId { get; set; }

    [ForeignKey("BusinessId")]
    public virtual Business Business { get; set; } = null!;

    [ForeignKey("SeasonId")]
    public virtual ShopSeason? Season { get; set; }

    public virtual ICollection<ShopCampaignPromotion> CampaignPromotions { get; set; } = new List<ShopCampaignPromotion>();
    public virtual ICollection<ShopCampaignBanner> CampaignBanners { get; set; } = new List<ShopCampaignBanner>();
}
