using GPInventory.Domain.Entities;
using GPInventory.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GPInventory.Infrastructure.Data.Configurations;

public class ServicePlanConfiguration : IEntityTypeConfiguration<ServicePlan>
{
    public void Configure(EntityTypeBuilder<ServicePlan> builder)
    {
        builder.ToTable("service_plan");

        builder.HasKey(e => e.Id);

        builder.Property(e => e.Name)
            .IsRequired()
            .HasMaxLength(255);

        builder.Property(e => e.Price)
            .HasPrecision(12, 2)
            .IsRequired();

        builder.Property(e => e.ClassCount)
            .IsRequired();

        builder.Property(e => e.ValidityDays)
            .HasDefaultValue(30);

        // MySQL almacena payment_timing como ENUM string
        builder.Property(e => e.PaymentTiming)
            .HasColumnName("payment_timing")
            .HasConversion(
                v => v == PlanPaymentTiming.PrePay ? "pre_pay"
                   : v == PlanPaymentTiming.Both   ? "both"
                   : "deferred",
                v => v == "pre_pay" ? PlanPaymentTiming.PrePay
                   : v == "both"    ? PlanPaymentTiming.Both
                   : PlanPaymentTiming.Deferred)
            .HasDefaultValue(PlanPaymentTiming.Deferred);

        // Índices
        builder.HasIndex(e => new { e.BusinessId, e.Active })
            .HasDatabaseName("idx_business_plan");

        builder.HasIndex(e => e.ServiceId)
            .HasDatabaseName("idx_service");

        builder.HasIndex(e => e.ServiceCategoryId)
            .HasDatabaseName("idx_category");

        // Relaciones
        builder.HasOne(e => e.Business)
            .WithMany()
            .HasForeignKey(e => e.BusinessId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(e => e.Store)
            .WithMany()
            .HasForeignKey(e => e.StoreId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasOne(e => e.Service)
            .WithMany()
            .HasForeignKey(e => e.ServiceId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasOne(e => e.ServiceCategory)
            .WithMany()
            .HasForeignKey(e => e.ServiceCategoryId)
            .OnDelete(DeleteBehavior.SetNull);

        // DefaultPaymentMethodId: plain FK column — no C# nav since PaymentMethod entity is not in this context
        builder.Property(e => e.DefaultPaymentMethodId)
            .HasColumnName("default_payment_method_id")
            .IsRequired(false);
    }
}
