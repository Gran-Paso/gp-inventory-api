using GPInventory.Domain.Entities;
using GPInventory.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GPInventory.Infrastructure.Data.Configurations;

public class ClientServicePlanConfiguration : IEntityTypeConfiguration<ClientServicePlan>
{
    public void Configure(EntityTypeBuilder<ClientServicePlan> builder)
    {
        builder.ToTable("client_service_plan");

        builder.HasKey(e => e.Id);

        builder.Property(e => e.TotalPaid)
            .HasPrecision(12, 2)
            .IsRequired();

        builder.Property(e => e.RevenueRecognized)
            .HasPrecision(12, 2)
            .IsRequired()
            .HasDefaultValue(0);

        // Columnas generadas por la base de datos
        builder.Property(e => e.ClassesRemaining)
            .HasComputedColumnSql("(total_classes - classes_used)", stored: true);

        builder.Property(e => e.DeferredRevenue)
            .HasComputedColumnSql("(total_paid - revenue_recognized)", stored: true);

        // MySQL almacena status como ENUM string ('active'|'expired'|'cancelled')
        builder.Property(e => e.Status)
            .HasColumnName("status")
            .HasConversion(
                v => v == ClientServicePlanStatus.Expired   ? "expired"
                   : v == ClientServicePlanStatus.Cancelled ? "cancelled"
                   : "active",
                v => v == "expired"   ? ClientServicePlanStatus.Expired
                   : v == "cancelled" ? ClientServicePlanStatus.Cancelled
                   : ClientServicePlanStatus.Active)
            .IsRequired();

        // billing_frequency ENUM string
        builder.Property(e => e.BillingFrequency)
            .HasColumnName("billing_frequency")
            .HasConversion(
                v => v == BillingFrequency.Quarterly  ? "quarterly"
                   : v == BillingFrequency.Semiannual ? "semiannual"
                   : v == BillingFrequency.Annual     ? "annual"
                   : "monthly",
                v => v == "quarterly"  ? BillingFrequency.Quarterly
                   : v == "semiannual" ? BillingFrequency.Semiannual
                   : v == "annual"     ? BillingFrequency.Annual
                   : BillingFrequency.Monthly)
            .HasDefaultValue(BillingFrequency.Monthly);

        // payment_timing ENUM string
        builder.Property(e => e.PaymentTiming)
            .HasColumnName("payment_timing")
            .HasConversion(
                v => v == PlanPaymentTiming.PrePay ? "pre_pay" : "deferred",
                v => v == "pre_pay" ? PlanPaymentTiming.PrePay : PlanPaymentTiming.Deferred)
            .HasDefaultValue(PlanPaymentTiming.Deferred);

        // PaymentMethodId: stored as plain FK column
        builder.Property(e => e.PaymentMethodId)
            .HasColumnName("payment_method_id")
            .IsRequired(false);

        // Índices
        builder.HasIndex(e => new { e.ServiceClientId, e.Status })
            .HasDatabaseName("idx_client_active");

        builder.HasIndex(e => new { e.BusinessId, e.Status })
            .HasDatabaseName("idx_business_status");

        builder.HasIndex(e => new { e.EndDate, e.Status })
            .HasDatabaseName("idx_expiration");

        builder.HasIndex(e => e.ServiceSaleId)
            .HasDatabaseName("idx_sale");

        // Relaciones
        builder.HasOne(e => e.Business)
            .WithMany()
            .HasForeignKey(e => e.BusinessId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(e => e.Store)
            .WithMany()
            .HasForeignKey(e => e.StoreId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasOne(e => e.ServiceClient)
            .WithMany()
            .HasForeignKey(e => e.ServiceClientId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(e => e.ServicePlan)
            .WithMany(sp => sp.ClientServicePlans)
            .HasForeignKey(e => e.ServicePlanId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(e => e.ServiceSale)
            .WithMany()
            .HasForeignKey(e => e.ServiceSaleId)
            .IsRequired(false)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
