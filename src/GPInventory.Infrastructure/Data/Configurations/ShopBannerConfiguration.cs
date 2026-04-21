using GPInventory.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GPInventory.Infrastructure.Data.Configurations;

public class ShopBannerConfiguration : IEntityTypeConfiguration<ShopBanner>
{
    public void Configure(EntityTypeBuilder<ShopBanner> builder)
    {
        builder.ToTable("shop_banner");
        builder.HasKey(e => e.Id);

        builder.Property(e => e.Id).HasColumnName("id").ValueGeneratedOnAdd();
        builder.Property(e => e.BusinessId).HasColumnName("business_id").IsRequired();
        builder.Property(e => e.Channel).HasColumnName("channel").HasMaxLength(50);
        builder.Property(e => e.Title).HasColumnName("title").HasMaxLength(200).IsRequired();
        builder.Property(e => e.Subtitle).HasColumnName("subtitle").HasMaxLength(400);
        builder.Property(e => e.ImageUrl).HasColumnName("image_url").HasMaxLength(500);
        builder.Property(e => e.RedirectUrl).HasColumnName("redirect_url").HasMaxLength(500);
        builder.Property(e => e.DisplayOrder).HasColumnName("display_order");
        builder.Property(e => e.Active).HasColumnName("active");
        builder.Property(e => e.StartsAt).HasColumnName("starts_at");
        builder.Property(e => e.EndsAt).HasColumnName("ends_at");
        builder.Property(e => e.CreatedAt).HasColumnName("created_at");
        builder.Property(e => e.UpdatedAt).HasColumnName("updated_at");
        builder.Property(e => e.SeasonId).HasColumnName("season_id");

        builder.HasOne(e => e.Business)
            .WithMany()
            .HasForeignKey(e => e.BusinessId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(e => new { e.BusinessId, e.Channel }).HasDatabaseName("idx_banner_business_channel");
        builder.HasIndex(e => new { e.Active, e.StartsAt, e.EndsAt }).HasDatabaseName("idx_banner_active");
        builder.HasIndex(e => e.DisplayOrder).HasDatabaseName("idx_banner_order");
        builder.HasIndex(e => new { e.BusinessId, e.SeasonId }).HasDatabaseName("idx_banner_season");
    }
}
