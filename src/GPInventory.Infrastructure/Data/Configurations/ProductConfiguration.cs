using GPInventory.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GPInventory.Infrastructure.Data.Configurations;

public class ProductConfiguration : IEntityTypeConfiguration<Product>
{
    public void Configure(EntityTypeBuilder<Product> builder)
    {
        builder.ToTable("product");
        builder.HasKey(e => e.Id);
        
        builder.Property(e => e.Id)
            .HasColumnName("id")
            .ValueGeneratedOnAdd();
            
        builder.Property(e => e.Name)
            .HasColumnName("name")
            .HasMaxLength(255)
            .IsRequired();
            
        builder.Property(e => e.Image)
            .HasColumnName("image")
            .HasMaxLength(255);
            
        builder.Property(e => e.ProductTypeId)
            .HasColumnName("product_type")
            .IsRequired();
            
        builder.Property(e => e.Price)
            .HasColumnName("price")
            .HasMaxLength(255)
            .IsRequired();
            
        builder.Property(e => e.Cost)
            .HasColumnName("cost")
            .HasMaxLength(255)
            .IsRequired();
            
        builder.Property(e => e.Sku)
            .HasColumnName("sku")
            .HasMaxLength(255);
            
        builder.Property(e => e.Date)
            .HasColumnName("date")
            .IsRequired();
            
        builder.Property(e => e.BusinessId)
            .HasColumnName("business")
            .IsRequired();

        // Relationships
        builder.HasOne(e => e.ProductType)
            .WithMany(pt => pt.Products)
            .HasForeignKey(e => e.ProductTypeId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("FK_Product_ProductType");

        builder.HasOne(e => e.Business)
            .WithMany(b => b.Products)
            .HasForeignKey(e => e.BusinessId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("FK_Product_Business");
                
        // BaseEntity properties - ignore since they don't exist in the database
        builder.Ignore(e => e.CreatedAt);
        builder.Ignore(e => e.UpdatedAt);
        builder.Ignore(e => e.IsActive);
        
        // Indexes for performance
        builder.HasIndex(e => e.ProductTypeId).HasDatabaseName("IX_Product_ProductType");
        builder.HasIndex(e => e.BusinessId).HasDatabaseName("IX_Product_Business");
        builder.HasIndex(e => e.Sku).HasDatabaseName("IX_Product_Sku");
    }
}
