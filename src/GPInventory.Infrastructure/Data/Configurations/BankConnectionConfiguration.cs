using GPInventory.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GPInventory.Infrastructure.Data.Configurations;

public class BankConnectionConfiguration : IEntityTypeConfiguration<BankConnection>
{
    public void Configure(EntityTypeBuilder<BankConnection> builder)
    {
        builder.ToTable("bank_connections");

        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).HasColumnName("id").ValueGeneratedOnAdd();

        builder.Property(e => e.BusinessId)
            .HasColumnName("business_id")
            .IsRequired();

        builder.Property(e => e.LinkToken)
            .HasColumnName("link_token")
            .HasMaxLength(500)
            .IsRequired();

        builder.Property(e => e.AccountId)
            .HasColumnName("account_id")
            .HasMaxLength(200);

        builder.Property(e => e.BankEntityId)
            .HasColumnName("bank_entity_id");

        builder.Property(e => e.Label)
            .HasColumnName("label")
            .HasMaxLength(200);

        builder.Property(e => e.LastSyncAt)
            .HasColumnName("last_sync_at");

        builder.Property(e => e.IsActive)
            .HasColumnName("is_active")
            .HasDefaultValue(true);

        builder.Property(e => e.CreatedAt)
            .HasColumnName("created_at")
            .ValueGeneratedOnAdd()
            .HasDefaultValueSql("CURRENT_TIMESTAMP");

        builder.Property(e => e.UpdatedAt)
            .HasColumnName("updated_at")
            .ValueGeneratedOnAddOrUpdate()
            .HasDefaultValueSql("CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP");

        // Ignore navigations to avoid duplicate FK columns
        builder.Ignore(e => e.Business);

        // BankEntity relationship (FK via bank_entity_id column)
        builder.HasOne(e => e.BankEntity)
            .WithMany()
            .HasForeignKey(e => e.BankEntityId)
            .OnDelete(DeleteBehavior.SetNull)
            .IsRequired(false);

        builder.HasMany(e => e.Transactions)
            .WithOne(t => t.BankConnection)
            .HasForeignKey(t => t.BankConnectionId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
