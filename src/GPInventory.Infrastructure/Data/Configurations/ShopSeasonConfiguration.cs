using GPInventory.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GPInventory.Infrastructure.Data.Configurations;

public class ShopSeasonConfiguration : IEntityTypeConfiguration<ShopSeason>
{
    public void Configure(EntityTypeBuilder<ShopSeason> builder)
    {
        builder.ToTable("shop_season");
        builder.HasKey(e => e.Id);

        builder.Property(e => e.Id).HasColumnName("id").ValueGeneratedOnAdd();
        builder.Property(e => e.BusinessId).HasColumnName("business_id").IsRequired();
        builder.Property(e => e.Channel).HasColumnName("channel").HasMaxLength(50);
        builder.Property(e => e.Name).HasColumnName("name").HasMaxLength(200).IsRequired();
        builder.Property(e => e.Description).HasColumnName("description").HasMaxLength(1000);
        builder.Property(e => e.CoverImageUrl).HasColumnName("cover_image_url").HasMaxLength(500);
        builder.Property(e => e.IsActive).HasColumnName("is_active");
        builder.Property(e => e.StartsAt).HasColumnName("starts_at");
        builder.Property(e => e.EndsAt).HasColumnName("ends_at");
        builder.Property(e => e.CreatedAt).HasColumnName("created_at");
        builder.Property(e => e.UpdatedAt).HasColumnName("updated_at");

        builder.HasOne(e => e.Business)
            .WithMany()
            .HasForeignKey(e => e.BusinessId)
            .OnDelete(DeleteBehavior.Cascade);

        // Banners, Collections, Campaigns relacionados (FK inversa en cada uno)
        builder.HasMany(e => e.Banners)
            .WithOne(b => b.Season)
            .HasForeignKey(b => b.SeasonId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasMany(e => e.Collections)
            .WithOne(c => c.Season)
            .HasForeignKey(c => c.SeasonId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasMany(e => e.Campaigns)
            .WithOne(c => c.Season)
            .HasForeignKey(c => c.SeasonId)
            .OnDelete(DeleteBehavior.SetNull);

        // Solo una temporada activa por (business_id, channel) a la vez — se fuerza en el controller
        builder.HasIndex(e => new { e.BusinessId, e.Channel, e.IsActive }).HasDatabaseName("idx_season_business_channel_active");
        builder.HasIndex(e => new { e.BusinessId, e.StartsAt, e.EndsAt }).HasDatabaseName("idx_season_schedule");
    }
}
