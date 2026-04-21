using GPInventory.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GPInventory.Infrastructure.Data.Configurations;

public class ShopCampaignConfiguration : IEntityTypeConfiguration<ShopCampaign>
{
    public void Configure(EntityTypeBuilder<ShopCampaign> builder)
    {
        builder.ToTable("shop_campaign");
        builder.HasKey(e => e.Id);

        builder.Property(e => e.Id).HasColumnName("id").ValueGeneratedOnAdd();
        builder.Property(e => e.BusinessId).HasColumnName("business_id").IsRequired();
        builder.Property(e => e.Channel).HasColumnName("channel").HasMaxLength(50);
        builder.Property(e => e.Name).HasColumnName("name").HasMaxLength(200).IsRequired();
        builder.Property(e => e.Description).HasColumnName("description").HasMaxLength(1000);
        builder.Property(e => e.StartsAt).HasColumnName("starts_at");
        builder.Property(e => e.EndsAt).HasColumnName("ends_at");
        builder.Property(e => e.Active).HasColumnName("active");
        builder.Property(e => e.CreatedAt).HasColumnName("created_at");
        builder.Property(e => e.UpdatedAt).HasColumnName("updated_at");
        builder.Property(e => e.SeasonId).HasColumnName("season_id");

        builder.HasOne(e => e.Business)
            .WithMany()
            .HasForeignKey(e => e.BusinessId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(e => e.CampaignPromotions)
            .WithOne(cp => cp.Campaign)
            .HasForeignKey(cp => cp.CampaignId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(e => e.CampaignBanners)
            .WithOne(cb => cb.Campaign)
            .HasForeignKey(cb => cb.CampaignId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(e => new { e.BusinessId, e.Active }).HasDatabaseName("idx_campaign_business_active");
        builder.HasIndex(e => new { e.Active, e.StartsAt, e.EndsAt }).HasDatabaseName("idx_campaign_schedule");
        builder.HasIndex(e => new { e.BusinessId, e.SeasonId }).HasDatabaseName("idx_campaign_season");
    }
}

public class ShopCampaignPromotionConfiguration : IEntityTypeConfiguration<ShopCampaignPromotion>
{
    public void Configure(EntityTypeBuilder<ShopCampaignPromotion> builder)
    {
        builder.ToTable("shop_campaign_promotion");
        builder.HasKey(e => e.Id);

        builder.Property(e => e.Id).HasColumnName("id").ValueGeneratedOnAdd();
        builder.Property(e => e.CampaignId).HasColumnName("campaign_id").IsRequired();
        builder.Property(e => e.PromotionId).HasColumnName("promotion_id").IsRequired();

        builder.HasOne(e => e.Campaign)
            .WithMany(c => c.CampaignPromotions)
            .HasForeignKey(e => e.CampaignId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(e => e.Promotion)
            .WithMany()
            .HasForeignKey(e => e.PromotionId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(e => new { e.CampaignId, e.PromotionId }).HasDatabaseName("idx_camp_promo_unique").IsUnique();
    }
}

public class ShopCampaignBannerConfiguration : IEntityTypeConfiguration<ShopCampaignBanner>
{
    public void Configure(EntityTypeBuilder<ShopCampaignBanner> builder)
    {
        builder.ToTable("shop_campaign_banner");
        builder.HasKey(e => e.Id);

        builder.Property(e => e.Id).HasColumnName("id").ValueGeneratedOnAdd();
        builder.Property(e => e.CampaignId).HasColumnName("campaign_id").IsRequired();
        builder.Property(e => e.BannerId).HasColumnName("banner_id").IsRequired();

        builder.HasOne(e => e.Campaign)
            .WithMany(c => c.CampaignBanners)
            .HasForeignKey(e => e.CampaignId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(e => e.Banner)
            .WithMany()
            .HasForeignKey(e => e.BannerId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(e => new { e.CampaignId, e.BannerId }).HasDatabaseName("idx_camp_banner_unique").IsUnique();
    }
}
