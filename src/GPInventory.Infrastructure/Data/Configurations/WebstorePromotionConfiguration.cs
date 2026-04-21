using GPInventory.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GPInventory.Infrastructure.Data.Configurations;

public class WebstorePromotionConfiguration : IEntityTypeConfiguration<WebstorePromotion>
{
    public void Configure(EntityTypeBuilder<WebstorePromotion> builder)
    {
        builder.ToTable("webstore_promotion");
        builder.HasKey(e => e.Id);

        builder.Property(e => e.Id).HasColumnName("id").ValueGeneratedOnAdd();
        builder.Property(e => e.BusinessId).HasColumnName("business_id").IsRequired();
        builder.Property(e => e.Channel).HasColumnName("channel").HasMaxLength(50).IsRequired();
        builder.Property(e => e.ProductId).HasColumnName("product_id").IsRequired();
        builder.Property(e => e.DiscountPct).HasColumnName("discount_pct").HasColumnType("decimal(5,2)");
        builder.Property(e => e.DisplayOrder).HasColumnName("display_order");
        builder.Property(e => e.Active).HasColumnName("active");
        builder.Property(e => e.StartsAt).HasColumnName("starts_at");
        builder.Property(e => e.EndsAt).HasColumnName("ends_at");
        builder.Property(e => e.CreatedAt).HasColumnName("created_at");
        builder.Property(e => e.UpdatedAt).HasColumnName("updated_at");

        builder.HasOne(e => e.Business)
            .WithMany()
            .HasForeignKey(e => e.BusinessId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(e => e.Product)
            .WithMany()
            .HasForeignKey(e => e.ProductId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(e => new { e.BusinessId, e.Channel }).HasDatabaseName("idx_wp_business_channel");
        builder.HasIndex(e => new { e.Active, e.StartsAt, e.EndsAt }).HasDatabaseName("idx_wp_active");
    }
}
