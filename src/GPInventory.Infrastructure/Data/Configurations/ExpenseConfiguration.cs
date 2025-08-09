using GPInventory.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GPInventory.Infrastructure.Data.Configurations;

public class ExpenseConfiguration : IEntityTypeConfiguration<Expense>
{
    public void Configure(EntityTypeBuilder<Expense> builder)
    {
        builder.ToTable("expenses");
        
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).HasColumnName("id").ValueGeneratedOnAdd();
        
        builder.Property(e => e.Description)
            .HasColumnName("description")
            .HasMaxLength(255)
            .IsRequired();
            
        builder.Property(e => e.Amount)
            .HasColumnName("amount")
            .IsRequired();
            
        builder.Property(e => e.Date)
            .HasColumnName("date")
            .IsRequired();
            
        builder.Property(e => e.SubcategoryId)
            .HasColumnName("subcategory_id")
            .IsRequired();
            
        builder.Property(e => e.BusinessId)
            .HasColumnName("business_id")
            .IsRequired();
            
        builder.Property(e => e.StoreId)
            .HasColumnName("store_id");
            
        builder.Property(e => e.FixedExpenseId)
            .HasColumnName("fixed_expense_id");
            
        builder.Property(e => e.IsFixed)
            .HasColumnName("is_fixed")
            .HasDefaultValue(false)
            .HasConversion(
                v => v ?? false,  // Al guardar: NULL -> false
                v => v);          // Al leer: mantiene el valor
            
        builder.Property(e => e.Notes)
            .HasColumnName("notes")
            .HasMaxLength(1000);
            
        // BaseEntity properties - ignore since they don't exist in the database
        builder.Ignore(e => e.CreatedAt);
        
        // Ignore problematic navigation properties that cause BusinessId1/StoreId1 issues
        builder.Ignore(e => e.Business);
        builder.Ignore(e => e.Store);
        builder.Ignore(e => e.FixedExpense);
        
        // Relations - only the one that works properly
        builder.HasOne(e => e.ExpenseSubcategory)
            .WithMany()
            .HasForeignKey(e => e.SubcategoryId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
