using GPInventory.Domain.Entities;
using GPInventory.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GPInventory.Infrastructure.Data.Configurations;

public class ServiceSaleConfiguration : IEntityTypeConfiguration<ServiceSale>
{
    public void Configure(EntityTypeBuilder<ServiceSale> builder)
    {
        builder.ToTable("service_sale");

        // created_at and updated_at do not exist yet in the DB schema.
        // They are ignored here so EF does not try to SELECT / INSERT them.
        // Run migration 20260322_add_timestamps_service_sale.sql to add
        // the columns, then remove these two Ignore calls.
        builder.Ignore(e => e.CreatedAt);
        builder.Ignore(e => e.UpdatedAt);

        // MySQL stores status as string ENUM ('pending'|'in_progress'|'completed'|'cancelled').
        builder.Property(e => e.Status)
            .HasColumnName("status")
            .HasConversion(
                v => v == ServiceSaleStatus.InProgress ? "in_progress"
                   : v == ServiceSaleStatus.Completed  ? "completed"
                   : v == ServiceSaleStatus.Cancelled  ? "cancelled"
                   : "pending",
                v => v == "in_progress" ? ServiceSaleStatus.InProgress
                   : v == "completed"   ? ServiceSaleStatus.Completed
                   : v == "cancelled"   ? ServiceSaleStatus.Cancelled
                   : ServiceSaleStatus.Pending);

        // MySQL stores document_type as string ENUM ('none'|'boleta'|'factura').
        builder.Property(e => e.DocumentType)
            .HasColumnName("document_type")
            .HasConversion(
                v => v == DocumentType.Boleta  ? "boleta"
                   : v == DocumentType.Factura ? "factura"
                   : "none",
                v => v == "boleta"  ? DocumentType.Boleta
                   : v == "factura" ? DocumentType.Factura
                   : DocumentType.None);
    }
}
