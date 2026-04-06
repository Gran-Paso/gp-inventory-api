using GPInventory.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GPInventory.Infrastructure.Data.Configurations;

public class PlanBillingPeriodConfiguration : IEntityTypeConfiguration<PlanBillingPeriod>
{
    public void Configure(EntityTypeBuilder<PlanBillingPeriod> builder)
    {
        builder.ToTable("plan_billing_period");

        builder.HasKey(e => e.Id);

        builder.Property(e => e.AmountDue)
            .HasPrecision(12, 2)
            .IsRequired();

        builder.Property(e => e.Status)
            .HasColumnName("status")
            .HasMaxLength(20)
            .IsRequired();

        // Un plan solo puede tener un período por mes/año
        builder.HasIndex(e => new { e.ClientServicePlanId, e.BillingYear, e.BillingMonth })
            .IsUnique()
            .HasDatabaseName("uq_plan_period");

        builder.HasIndex(e => e.ClientServicePlanId)
            .HasDatabaseName("idx_pbp_plan");

        builder.HasIndex(e => e.ServiceClientId)
            .HasDatabaseName("idx_pbp_client");

        builder.HasIndex(e => new { e.BusinessId, e.BillingYear, e.BillingMonth })
            .HasDatabaseName("idx_pbp_business");

        builder.HasIndex(e => new { e.Status, e.DueDate })
            .HasDatabaseName("idx_pbp_status");

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

        builder.HasOne(e => e.PlanTransaction)
            .WithMany()
            .HasForeignKey(e => e.PlanTransactionId)
            .OnDelete(DeleteBehavior.SetNull);

        // CommittedPercent no se mapea a la DB
        builder.Ignore(e => e.CommittedPercent);
    }
}
