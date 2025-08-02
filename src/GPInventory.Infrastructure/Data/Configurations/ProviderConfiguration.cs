using GPInventory.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GPInventory.Infrastructure.Data.Configurations;

public class ProviderConfiguration : IEntityTypeConfiguration<Provider>
{
    public void Configure(EntityTypeBuilder<Provider> builder)
    {
        builder.ToTable("provider");
        builder.HasKey(e => e.Id);
        
        builder.Property(e => e.Id)
            .HasColumnName("id")
            .ValueGeneratedOnAdd();
            
        builder.Property(e => e.Name)
            .HasColumnName("name")
            .HasMaxLength(255)
            .IsRequired();
            
        builder.Property(e => e.StoreId)
            .HasColumnName("id_store");

        // Relationships - usar Store como relación principal
        builder.HasOne(e => e.Store)
            .WithMany(s => s.Providers)
            .HasForeignKey(e => e.StoreId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("FK_Provider_Store");
                
        // BaseEntity properties - ignore since they don't exist in the database
        builder.Ignore(e => e.CreatedAt);
        builder.Ignore(e => e.UpdatedAt);
        builder.Ignore(e => e.IsActive);
        
        // Indexes for performance
        builder.HasIndex(e => e.StoreId).HasDatabaseName("IX_Provider_Store");
        builder.HasIndex(e => e.Name).HasDatabaseName("IX_Provider_Name");
    }
}
