using GPInventory.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GPInventory.Infrastructure.Data.Configurations;

public class ShopCollectionConfiguration : IEntityTypeConfiguration<ShopCollection>
{
    public void Configure(EntityTypeBuilder<ShopCollection> builder)
    {
        builder.ToTable("shop_collection");
        builder.HasKey(e => e.Id);

        builder.Property(e => e.Id).HasColumnName("id").ValueGeneratedOnAdd();
        builder.Property(e => e.BusinessId).HasColumnName("business_id").IsRequired();
        builder.Property(e => e.Channel).HasColumnName("channel").HasMaxLength(50);
        builder.Property(e => e.Name).HasColumnName("name").HasMaxLength(200).IsRequired();
        builder.Property(e => e.Slug).HasColumnName("slug").HasMaxLength(200).IsRequired();
        builder.Property(e => e.Description).HasColumnName("description").HasMaxLength(1000);
        builder.Property(e => e.CoverImageUrl).HasColumnName("cover_image_url").HasMaxLength(500);
        builder.Property(e => e.SortRule).HasColumnName("sort_rule").HasMaxLength(50).HasDefaultValue("manual");
        builder.Property(e => e.DisplayOrder).HasColumnName("display_order");
        builder.Property(e => e.Active).HasColumnName("active");
        builder.Property(e => e.CreatedAt).HasColumnName("created_at");
        builder.Property(e => e.UpdatedAt).HasColumnName("updated_at");
        builder.Property(e => e.SeasonId).HasColumnName("season_id");

        builder.HasOne(e => e.Business)
            .WithMany()
            .HasForeignKey(e => e.BusinessId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(e => e.Items)
            .WithOne(i => i.Collection)
            .HasForeignKey(i => i.CollectionId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(e => new { e.BusinessId, e.Slug }).HasDatabaseName("idx_collection_business_slug").IsUnique();
        builder.HasIndex(e => new { e.BusinessId, e.Channel }).HasDatabaseName("idx_collection_business_channel");
        builder.HasIndex(e => e.Active).HasDatabaseName("idx_collection_active");
        builder.HasIndex(e => new { e.BusinessId, e.SeasonId }).HasDatabaseName("idx_collection_season");
    }
}
