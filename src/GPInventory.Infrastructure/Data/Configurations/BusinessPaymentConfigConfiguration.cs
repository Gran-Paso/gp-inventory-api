using GPInventory.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GPInventory.Infrastructure.Data.Configurations;

public class BusinessPaymentConfigConfiguration : IEntityTypeConfiguration<BusinessPaymentConfig>
{
    public void Configure(EntityTypeBuilder<BusinessPaymentConfig> builder)
    {
        builder.ToTable("business_payment_config");
        builder.HasKey(e => e.Id);

        builder.Property(e => e.Id).HasColumnName("id").ValueGeneratedOnAdd();
        builder.Property(e => e.BusinessId).HasColumnName("business_id").IsRequired();

        builder.Property(e => e.MpAccessTokenEncrypted).HasColumnName("mp_access_token_encrypted").HasMaxLength(512);
        builder.Property(e => e.MpPublicKey).HasColumnName("mp_public_key").HasMaxLength(100);
        builder.Property(e => e.MpPointDeviceId).HasColumnName("mp_point_device_id").HasMaxLength(100);
        builder.Property(e => e.MpWebhookSecretEncrypted).HasColumnName("mp_webhook_secret_encrypted").HasMaxLength(512);
        builder.Property(e => e.MpEnabled).HasColumnName("mp_enabled").HasDefaultValue(false);

        builder.Property(e => e.CreatedAt).HasColumnName("created_at");
        builder.Property(e => e.UpdatedAt).HasColumnName("updated_at");

        builder.HasOne(e => e.Business)
            .WithMany()
            .HasForeignKey(e => e.BusinessId)
            .OnDelete(DeleteBehavior.Cascade);

        // Un solo registro por negocio
        builder.HasIndex(e => e.BusinessId)
            .IsUnique()
            .HasDatabaseName("idx_bpc_business_unique");
    }
}
