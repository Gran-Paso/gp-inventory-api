using GPInventory.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GPInventory.Infrastructure.Data.Configurations;

/// <summary>
/// EF Core configuration for BankEntity (table: bank_entities).
/// The table only has id + name — ignore all BaseEntity audit columns.
/// </summary>
public class BankEntityConfiguration : IEntityTypeConfiguration<BankEntity>
{
    public void Configure(EntityTypeBuilder<BankEntity> builder)
    {
        builder.ToTable("bank_entities");

        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).HasColumnName("id").ValueGeneratedOnAdd();

        builder.Property(e => e.Name)
            .HasColumnName("name")
            .HasMaxLength(200)
            .IsRequired();

        // bank_entities only has id + name — these columns do not exist
        builder.Ignore(e => e.CreatedAt);
        builder.Ignore(e => e.UpdatedAt);
        builder.Ignore(e => e.IsActive);
    }
}
