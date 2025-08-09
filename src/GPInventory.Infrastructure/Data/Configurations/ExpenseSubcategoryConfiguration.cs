using GPInventory.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GPInventory.Infrastructure.Data.Configurations;

public class ExpenseSubcategoryConfiguration : IEntityTypeConfiguration<ExpenseSubcategory>
{
    public void Configure(EntityTypeBuilder<ExpenseSubcategory> builder)
    {
        builder.ToTable("expense_subcategory");
        
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).HasColumnName("id").ValueGeneratedOnAdd();
        
        builder.Property(e => e.Name)
            .HasColumnName("name")
            .HasMaxLength(255)
            .IsRequired();
            
        builder.Property(e => e.ExpenseCategoryId)
            .HasColumnName("expense_category_id")
            .IsRequired();
        
        // Relations
        builder.HasOne(e => e.ExpenseCategory)
            .WithMany()
            .HasForeignKey(e => e.ExpenseCategoryId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
