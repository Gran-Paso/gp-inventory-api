using GPInventory.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GPInventory.Infrastructure.Data.Configurations;

public class ServiceCategoryConfiguration : IEntityTypeConfiguration<ServiceCategory>
{
    public void Configure(EntityTypeBuilder<ServiceCategory> builder)
    {
        builder.ToTable("service_category");

        // created_at and updated_at do not exist in the current DB schema.
        // They are ignored here so EF does not try to SELECT / INSERT them.
        // Run migration 20260322_add_timestamps_service_category.sql to add
        // the columns, then remove these two Ignore calls.
        builder.Ignore(e => e.CreatedAt);
        builder.Ignore(e => e.UpdatedAt);
    }
}
