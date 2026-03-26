using GPInventory.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GPInventory.Infrastructure.Data.Configurations;

public class ExpenseTagAssignmentConfiguration : IEntityTypeConfiguration<ExpenseTagAssignment>
{
    public void Configure(EntityTypeBuilder<ExpenseTagAssignment> builder)
    {
        builder.ToTable("expense_tag_assignment");

        builder.HasKey(e => new { e.ExpenseId, e.TagId });

        builder.Property(e => e.ExpenseId).HasColumnName("expense_id");
        builder.Property(e => e.TagId).HasColumnName("tag_id");

        builder.HasOne(e => e.Expense)
            .WithMany(e => e.TagAssignments)
            .HasForeignKey(e => e.ExpenseId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(e => e.Tag)
            .WithMany(t => t.Assignments)
            .HasForeignKey(e => e.TagId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
