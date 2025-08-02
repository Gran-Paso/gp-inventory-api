using GPInventory.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GPInventory.Infrastructure.Data.Configurations;

public class StoreConfiguration : IEntityTypeConfiguration<Store>
{
    public void Configure(EntityTypeBuilder<Store> builder)
    {
        builder.ToTable("store");
        builder.HasKey(e => e.Id);
        
        builder.Property(e => e.Id)
            .HasColumnName("id")
            .ValueGeneratedOnAdd();
            
        builder.Property(e => e.Name)
            .HasColumnName("name")
            .HasMaxLength(255);
            
        builder.Property(e => e.Location)
            .HasColumnName("location")
            .HasMaxLength(255);
            
        builder.Property(e => e.BusinessId)
            .HasColumnName("id_business");
            
        builder.Property(e => e.ManagerId)
            .HasColumnName("id_manager");
            
        builder.Property(e => e.OpenHour)
            .HasColumnName("open_hour");
            
        builder.Property(e => e.CloseHour)
            .HasColumnName("close_hour");
            
        builder.Property(e => e.Active)
            .HasColumnName("active");

        // Relationships
        builder.HasOne(e => e.Business)
            .WithMany()
            .HasForeignKey(e => e.BusinessId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("idx_business_store");
            
        builder.HasOne(e => e.Manager)
            .WithMany()
            .HasForeignKey(e => e.ManagerId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("idx_manager_store");

        // BaseEntity properties - ignore since they don't exist in the database
        builder.Ignore(e => e.CreatedAt);
        builder.Ignore(e => e.UpdatedAt);
        builder.Ignore(e => e.IsActive);

        // Indexes for performance
        builder.HasIndex(e => e.BusinessId).HasDatabaseName("idx_business_store");
        builder.HasIndex(e => e.ManagerId).HasDatabaseName("idx_manager_store");
    }
}
