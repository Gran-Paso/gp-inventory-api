using GPInventory.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GPInventory.Infrastructure.Data.Configurations;

public class ServiceSessionExpenseConfiguration : IEntityTypeConfiguration<ServiceSessionExpense>
{
    public void Configure(EntityTypeBuilder<ServiceSessionExpense> builder)
    {
        builder.ToTable("service_session_expense");
        builder.HasKey(e => e.Id);

        builder.Property(e => e.Id).HasColumnName("id").ValueGeneratedOnAdd();
        builder.Property(e => e.Description).HasMaxLength(500).IsRequired();
        builder.Property(e => e.Amount).HasColumnType("decimal(12,2)").IsRequired();
        builder.Property(e => e.Status).HasMaxLength(20).IsRequired().HasDefaultValue("pending");
        builder.Property(e => e.PayeeType).HasMaxLength(20);
        builder.Property(e => e.PayeeEmployeeName).HasMaxLength(255);
        builder.Property(e => e.PayeeExternalName).HasMaxLength(255);

        builder.HasIndex(e => e.ServiceSessionId).HasDatabaseName("idx_sse_session");
        builder.HasIndex(e => new { e.BusinessId, e.Status }).HasDatabaseName("idx_sse_business_status");
        builder.HasIndex(e => e.ServiceCostItemId).HasDatabaseName("idx_sse_cost_item");

        builder.HasOne(e => e.Business)
            .WithMany()
            .HasForeignKey(e => e.BusinessId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(e => e.Store)
            .WithMany()
            .HasForeignKey(e => e.StoreId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasOne(e => e.ServiceSession)
            .WithMany()
            .HasForeignKey(e => e.ServiceSessionId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(e => e.ServiceCostItem)
            .WithMany()
            .HasForeignKey(e => e.ServiceCostItemId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
