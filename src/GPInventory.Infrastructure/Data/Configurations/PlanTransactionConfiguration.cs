using GPInventory.Domain.Entities;
using GPInventory.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GPInventory.Infrastructure.Data.Configurations;

public class PlanTransactionConfiguration : IEntityTypeConfiguration<PlanTransaction>
{
    public void Configure(EntityTypeBuilder<PlanTransaction> builder)
    {
        builder.ToTable("plan_transaction");

        builder.HasKey(e => e.Id);

        builder.Property(e => e.Amount)
            .HasPrecision(12, 2)
            .IsRequired();

        // MySQL almacena document_type como ENUM string
        builder.Property(e => e.DocumentType)
            .HasColumnName("document_type")
            .HasConversion(
                v => v == DocumentType.Boleta  ? "boleta"
                   : v == DocumentType.Factura ? "factura"
                   : "none",
                v => v == "boleta"  ? DocumentType.Boleta
                   : v == "factura" ? DocumentType.Factura
                   : DocumentType.None)
            .IsRequired();

        // Índices
        builder.HasIndex(e => e.ClientServicePlanId)
            .HasDatabaseName("idx_plan_tx_plan");

        builder.HasIndex(e => e.ServiceClientId)
            .HasDatabaseName("idx_plan_tx_client");

        builder.HasIndex(e => new { e.BusinessId, e.TransactionDate })
            .HasDatabaseName("idx_plan_tx_business");

        // Relaciones
        builder.HasOne(e => e.Business)
            .WithMany()
            .HasForeignKey(e => e.BusinessId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(e => e.Store)
            .WithMany()
            .HasForeignKey(e => e.StoreId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasOne(e => e.ClientServicePlan)
            .WithMany()
            .HasForeignKey(e => e.ClientServicePlanId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(e => e.ServiceClient)
            .WithMany()
            .HasForeignKey(e => e.ServiceClientId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(e => e.ServicePlan)
            .WithMany()
            .HasForeignKey(e => e.ServicePlanId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
