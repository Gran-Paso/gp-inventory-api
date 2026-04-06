using GPInventory.Domain.Entities;
using GPInventory.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GPInventory.Infrastructure.Data.Configurations;

public class ServiceSessionConfiguration : IEntityTypeConfiguration<ServiceSession>
{
    public void Configure(EntityTypeBuilder<ServiceSession> builder)
    {
        builder.ToTable("service_session");

        builder.HasKey(e => e.Id);

        builder.Property(e => e.InstructorName)
            .HasMaxLength(255);

        builder.Property(e => e.Location)
            .HasMaxLength(255);

        // MySQL almacena status como ENUM string
        builder.Property(e => e.Status)
            .HasColumnName("status")
            .HasConversion(
                v => v == ServiceSessionStatus.InProgress ? "in_progress"
                   : v == ServiceSessionStatus.Completed  ? "completed"
                   : v == ServiceSessionStatus.Cancelled  ? "cancelled"
                   : "scheduled",
                v => v == "in_progress" ? ServiceSessionStatus.InProgress
                   : v == "completed"   ? ServiceSessionStatus.Completed
                   : v == "cancelled"   ? ServiceSessionStatus.Cancelled
                   : ServiceSessionStatus.Scheduled)
            .IsRequired();

        // Índices
        builder.HasIndex(e => new { e.ServiceId, e.SessionDate })
            .HasDatabaseName("idx_session_service_date");

        builder.HasIndex(e => new { e.BusinessId, e.SessionDate })
            .HasDatabaseName("idx_session_business_date");

        builder.HasIndex(e => e.ServicePlanId)
            .HasDatabaseName("idx_session_plan");

        builder.HasIndex(e => new { e.Status, e.SessionDate })
            .HasDatabaseName("idx_session_status");

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

        builder.HasOne(e => e.ServicePlan)
            .WithMany()
            .HasForeignKey(e => e.ServicePlanId)
            .OnDelete(DeleteBehavior.SetNull);

        // InstructorUserId y CreatedByUserId: sin FK navegable para evitar conflictos múltiples con User
        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(e => e.InstructorUserId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(e => e.CreatedByUserId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
