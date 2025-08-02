using GPInventory.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GPInventory.Infrastructure.Data.Configurations;

public class StockConfiguration : IEntityTypeConfiguration<Stock>
{
    public void Configure(EntityTypeBuilder<Stock> builder)
    {
        builder.ToTable("stock");
        builder.HasKey(e => e.Id);

        builder.Property(e => e.Id)
            .HasColumnName("id")
            .ValueGeneratedOnAdd();

        builder.Property(e => e.ProductId)
            .HasColumnName("product")
            .IsRequired();

        builder.Property(e => e.Date)
            .HasColumnName("date")
            .IsRequired();

        builder.Property(e => e.FlowTypeId)
            .HasColumnName("flow")
            .IsRequired();

        builder.Property(e => e.Amount)
            .HasColumnName("amount")
            .IsRequired();

        builder.Property(e => e.AuctionPrice)
            .HasColumnName("auction_price");

        builder.Property(e => e.Cost)
            .HasColumnName("cost");

        builder.Property(e => e.ProviderId)
            .HasColumnName("provider");

        builder.Property(e => e.Notes)
            .HasColumnName("notes")
            .HasColumnType("text");

        builder.Property(e => e.StoreId)
            .HasColumnName("id_store")
            .IsRequired();

        // Relationships
        builder.HasOne(e => e.Product)
            .WithMany(p => p.Stocks)
            .HasForeignKey(e => e.ProductId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("FK_Stock_Product");

        builder.HasOne(e => e.FlowType)
            .WithMany(f => f.Stocks)
            .HasForeignKey(e => e.FlowTypeId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("FK_Stock_FlowType");

        builder.HasOne(e => e.Provider)
            .WithMany(p => p.StockMovements)
            .HasForeignKey(e => e.ProviderId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("FK_Stock_Provider");

        builder.HasOne(e => e.Store)
            .WithMany(s => s.StockMovements)
            .HasForeignKey(e => e.StoreId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("FK_Stock_Store");

        // BaseEntity properties - ignore since they don't exist in the database
        builder.Ignore(e => e.CreatedAt);
        builder.Ignore(e => e.UpdatedAt);
        builder.Ignore(e => e.IsActive);

        // Indexes for performance
        builder.HasIndex(e => e.ProductId).HasDatabaseName("IX_Stock_ProductId");
        builder.HasIndex(e => e.Date).HasDatabaseName("IX_Stock_Date");
        builder.HasIndex(e => e.FlowTypeId).HasDatabaseName("IX_Stock_FlowTypeId");
        builder.HasIndex(e => e.StoreId).HasDatabaseName("IX_Stock_StoreId");
        builder.HasIndex(e => new { e.ProductId, e.Date }).HasDatabaseName("IX_Stock_Product_Date");
    }
}