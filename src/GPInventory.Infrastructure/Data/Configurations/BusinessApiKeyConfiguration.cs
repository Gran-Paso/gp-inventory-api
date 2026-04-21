using GPInventory.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GPInventory.Infrastructure.Data.Configurations;

public class BusinessApiKeyConfiguration : IEntityTypeConfiguration<BusinessApiKey>
{
    public void Configure(EntityTypeBuilder<BusinessApiKey> builder)
    {
        builder.ToTable("business_api_keys");
        builder.HasKey(e => e.Id);

        builder.Property(e => e.Id).HasColumnName("id").ValueGeneratedOnAdd();
        builder.Property(e => e.BusinessId).HasColumnName("business_id").IsRequired();
        builder.Property(e => e.KeyHash).HasColumnName("key_hash").HasMaxLength(64).IsRequired();
        builder.Property(e => e.Label).HasColumnName("label").HasMaxLength(100);
        builder.Property(e => e.Scopes).HasColumnName("scopes").HasColumnType("json");
        builder.Property(e => e.ExpiresAt).HasColumnName("expires_at");
        builder.Property(e => e.Active).HasColumnName("active");
        builder.Property(e => e.CreatedAt).HasColumnName("created_at");
        builder.Property(e => e.UpdatedAt).HasColumnName("updated_at");

        builder.HasOne(e => e.Business)
            .WithMany()
            .HasForeignKey(e => e.BusinessId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(e => e.BusinessId).HasDatabaseName("idx_bak_business");
        builder.HasIndex(e => e.KeyHash).HasDatabaseName("idx_bak_hash");
    }
}
