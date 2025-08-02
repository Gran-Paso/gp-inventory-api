using GPInventory.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GPInventory.Infrastructure.Data.Configurations;

public class SaleConfiguration : IEntityTypeConfiguration<Sale>
{
    public void Configure(EntityTypeBuilder<Sale> builder)
    {
        builder.ToTable("sales");
        builder.HasKey(e => e.Id);
        
        builder.Property(e => e.Id)
            .HasColumnName("id")
            .ValueGeneratedOnAdd();
            
        builder.Property(e => e.StoreId)
            .HasColumnName("id_store")
            .IsRequired();
            
        builder.Property(e => e.Date)
            .HasColumnName("date")
            .IsRequired();
            
        builder.Property(e => e.CustomerName)
            .HasColumnName("customer_name")
            .HasMaxLength(255);
            
        builder.Property(e => e.CustomerRut)
            .HasColumnName("customer_rut")
            .HasMaxLength(255);
            
        builder.Property(e => e.Total)
            .HasColumnName("total");
            
        builder.Property(e => e.PaymentMethodId)
            .HasColumnName("payment_method");
            
        builder.Property(e => e.Notes)
            .HasColumnName("notes")
            .HasColumnType("text");

        // Relationships
        builder.HasOne(e => e.Store)
            .WithMany(s => s.Sales)
            .HasForeignKey(e => e.StoreId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("idx_store_sale");

        builder.HasOne(e => e.PaymentMethod)
            .WithMany(pm => pm.Sales)
            .HasForeignKey(e => e.PaymentMethodId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("idx_payment_method_sale");

        // BaseEntity properties - ignore since they don't exist in the database
        builder.Ignore(e => e.CreatedAt);
        builder.Ignore(e => e.UpdatedAt);
        builder.Ignore(e => e.IsActive);

        // Indexes for performance
        builder.HasIndex(e => e.StoreId).HasDatabaseName("idx_store_sale");
        builder.HasIndex(e => e.PaymentMethodId).HasDatabaseName("idx_payment_method_sale");
        builder.HasIndex(e => e.Date).HasDatabaseName("IX_Sale_Date");
    }
}
