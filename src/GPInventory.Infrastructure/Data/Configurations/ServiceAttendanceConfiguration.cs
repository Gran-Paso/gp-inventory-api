using GPInventory.Domain.Entities;
using GPInventory.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GPInventory.Infrastructure.Data.Configurations;

public class ServiceAttendanceConfiguration : IEntityTypeConfiguration<ServiceAttendance>
{
    public void Configure(EntityTypeBuilder<ServiceAttendance> builder)
    {
        builder.ToTable("service_attendance");

        builder.HasKey(e => e.Id);

        builder.Property(e => e.ClientName)
            .HasMaxLength(255);

        // MySQL almacena attendance_type como ENUM string
        builder.Property(e => e.AttendanceType)
            .HasColumnName("attendance_type")
            .HasConversion(
                v => v == AttendanceType.Paid  ? "paid"
                   : v == AttendanceType.Free  ? "free"
                   : v == AttendanceType.Trial ? "trial"
                   : "plan",
                v => v == "paid"  ? AttendanceType.Paid
                   : v == "free"  ? AttendanceType.Free
                   : v == "trial" ? AttendanceType.Trial
                   : AttendanceType.Plan)
            .IsRequired();

        // MySQL almacena status como ENUM string
        builder.Property(e => e.Status)
            .HasColumnName("status")
            .HasConversion(
                v => v == AttendanceStatus.Confirmed  ? "confirmed"
                   : v == AttendanceStatus.Attended   ? "attended"
                   : v == AttendanceStatus.Absent     ? "absent"
                   : v == AttendanceStatus.Cancelled  ? "cancelled"
                   : "scheduled",
                v => v == "confirmed"  ? AttendanceStatus.Confirmed
                   : v == "attended"   ? AttendanceStatus.Attended
                   : v == "absent"     ? AttendanceStatus.Absent
                   : v == "cancelled"  ? AttendanceStatus.Cancelled
                   : AttendanceStatus.Scheduled)
            .IsRequired();

        // Índices
        builder.HasIndex(e => new { e.ServiceId, e.AttendanceDate })
            .HasDatabaseName("idx_service_date");

        builder.HasIndex(e => new { e.ServiceClientId, e.AttendanceDate })
            .HasDatabaseName("idx_client");

        builder.HasIndex(e => e.ClientServicePlanId)
            .HasDatabaseName("idx_plan");

        builder.HasIndex(e => new { e.BusinessId, e.AttendanceDate })
            .HasDatabaseName("idx_business_date");

        builder.HasIndex(e => new { e.Status, e.AttendanceDate })
            .HasDatabaseName("idx_status");

        builder.HasIndex(e => e.ServiceSessionId)
            .HasDatabaseName("idx_attendance_session");

        builder.HasIndex(e => e.PlanBillingPeriodId)
            .HasDatabaseName("idx_attendance_period");

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
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(e => e.ServiceClient)
            .WithMany()
            .HasForeignKey(e => e.ServiceClientId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasOne(e => e.ClientServicePlan)
            .WithMany(csp => csp.Attendances)
            .HasForeignKey(e => e.ClientServicePlanId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasOne(e => e.PlanBillingPeriod)
            .WithMany(p => p.Attendances)
            .HasForeignKey(e => e.PlanBillingPeriodId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasOne(e => e.ServiceSale)
            .WithMany()
            .HasForeignKey(e => e.ServiceSaleId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasOne(e => e.RegisteredByUser)
            .WithMany()
            .HasForeignKey(e => e.RegisteredByUserId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasOne(e => e.ServiceSession)
            .WithMany(s => s.Attendances)
            .HasForeignKey(e => e.ServiceSessionId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
