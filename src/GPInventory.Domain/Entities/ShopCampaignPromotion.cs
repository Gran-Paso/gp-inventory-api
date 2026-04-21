using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace GPInventory.Domain.Entities;

[Table("shop_campaign_promotion")]
public class ShopCampaignPromotion
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Required]
    [Column("campaign_id")]
    public int CampaignId { get; set; }

    [Required]
    [Column("promotion_id")]
    public int PromotionId { get; set; }

    [ForeignKey("CampaignId")]
    public virtual ShopCampaign Campaign { get; set; } = null!;

    [ForeignKey("PromotionId")]
    public virtual WebstorePromotion Promotion { get; set; } = null!;
}
