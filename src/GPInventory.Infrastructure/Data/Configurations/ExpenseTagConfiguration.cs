using GPInventory.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GPInventory.Infrastructure.Data.Configurations;

public class ExpenseTagConfiguration : IEntityTypeConfiguration<ExpenseTag>
{
    public void Configure(EntityTypeBuilder<ExpenseTag> builder)
    {
        builder.ToTable("expense_tag");

        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).HasColumnName("id").ValueGeneratedOnAdd();

        builder.Property(e => e.BusinessId)
            .HasColumnName("business_id")
            .IsRequired();

        builder.Property(e => e.Name)
            .HasColumnName("name")
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(e => e.Color)
            .HasColumnName("color")
            .HasMaxLength(7)
            .HasDefaultValue("#6b7280")
            .IsRequired();

        builder.Property(e => e.CreatedAt)
            .HasColumnName("created_at")
            .ValueGeneratedOnAdd();

        builder.HasIndex(e => new { e.BusinessId, e.Name }).IsUnique();

        builder.HasOne(e => e.Business)
            .WithMany()
            .HasForeignKey(e => e.BusinessId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
