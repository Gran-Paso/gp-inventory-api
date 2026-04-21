using GPInventory.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GPInventory.Infrastructure.Data.Configurations;

public class ShopCollectionItemConfiguration : IEntityTypeConfiguration<ShopCollectionItem>
{
    public void Configure(EntityTypeBuilder<ShopCollectionItem> builder)
    {
        builder.ToTable("shop_collection_item");
        builder.HasKey(e => e.Id);

        builder.Property(e => e.Id).HasColumnName("id").ValueGeneratedOnAdd();
        builder.Property(e => e.CollectionId).HasColumnName("collection_id").IsRequired();
        builder.Property(e => e.ProductId).HasColumnName("product_id").IsRequired();
        builder.Property(e => e.DisplayOrder).HasColumnName("display_order");
        builder.Property(e => e.Pinned).HasColumnName("pinned");
        builder.Property(e => e.CreatedAt).HasColumnName("created_at");
        builder.Property(e => e.UpdatedAt).HasColumnName("updated_at");

        builder.HasOne(e => e.Collection)
            .WithMany(c => c.Items)
            .HasForeignKey(e => e.CollectionId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(e => e.Product)
            .WithMany()
            .HasForeignKey(e => e.ProductId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(e => new { e.CollectionId, e.ProductId }).HasDatabaseName("idx_collection_item_unique").IsUnique();
        builder.HasIndex(e => new { e.CollectionId, e.DisplayOrder }).HasDatabaseName("idx_collection_item_order");
        builder.HasIndex(e => new { e.CollectionId, e.Pinned }).HasDatabaseName("idx_collection_item_pinned");
    }
}
