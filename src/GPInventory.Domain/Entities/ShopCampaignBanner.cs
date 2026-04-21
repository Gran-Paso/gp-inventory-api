using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace GPInventory.Domain.Entities;

[Table("shop_campaign_banner")]
public class ShopCampaignBanner
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Required]
    [Column("campaign_id")]
    public int CampaignId { get; set; }

    [Required]
    [Column("banner_id")]
    public int BannerId { get; set; }

    [ForeignKey("CampaignId")]
    public virtual ShopCampaign Campaign { get; set; } = null!;

    [ForeignKey("BannerId")]
    public virtual ShopBanner Banner { get; set; } = null!;
}
