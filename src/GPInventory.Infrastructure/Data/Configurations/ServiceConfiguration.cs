using GPInventory.Domain.Entities;
using GPInventory.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GPInventory.Infrastructure.Data.Configurations;

public class ServiceConfiguration : IEntityTypeConfiguration<Service>
{
    public void Configure(EntityTypeBuilder<Service> builder)
    {
        builder.ToTable("service");

        // The Service.Sales collection implies a direct FK on service_sale, but
        // service_sale has no service_id column — sales are linked via service_sale_item.
        // Ignoring the navigation prevents EF from creating the shadow ServiceId property.
        builder.Ignore(e => e.Sales);

        // created_at and updated_at exist on the service table per DATABASE.md.
        builder.Property(e => e.CreatedAt).HasColumnName("created_at");
        builder.Property(e => e.UpdatedAt).HasColumnName("updated_at");

        // MySQL stores pricing_type as a string ENUM ('fixed' | 'per_enrollment').
        // EF would default to int; we need explicit string conversion.
        builder.Property(e => e.PricingType)
            .HasColumnName("pricing_type")
            .HasConversion(
                v => v == PricingType.Fixed ? "fixed" : "per_enrollment",
                v => v == "fixed" ? PricingType.Fixed : PricingType.PerEnrollment);
    }
}
