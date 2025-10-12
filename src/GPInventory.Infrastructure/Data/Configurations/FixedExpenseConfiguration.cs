using GPInventory.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GPInventory.Infrastructure.Data.Configurations;

public class FixedExpenseConfiguration : IEntityTypeConfiguration<FixedExpense>
{
    public void Configure(EntityTypeBuilder<FixedExpense> builder)
    {
        builder.ToTable("fixed_expense");
        
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).HasColumnName("id").ValueGeneratedOnAdd();
        
        builder.Property(e => e.BusinessId)
            .HasColumnName("business_id")
            .IsRequired();
            
        builder.Property(e => e.StoreId)
            .HasColumnName("store_id");
            
        builder.Property(e => e.AdditionalNote)
            .HasColumnName("additional_note")
            .HasMaxLength(255)
            .IsRequired();
            
        builder.Property(e => e.Amount)
            .HasColumnName("amount")
            .IsRequired();
            
        builder.Property(e => e.SubcategoryId)
            .HasColumnName("subcategory_id");
            
        builder.Property(e => e.RecurrenceTypeId)
            .HasColumnName("recurrence_id")
            .IsRequired();
        
        builder.Property(e => e.ExpenseTypeId)
            .HasColumnName("expense_type_id");
            
        builder.Property(e => e.EndDate)
            .HasColumnName("end_date");
            
        builder.Property(e => e.PaymentDate)
            .HasColumnName("payment_date");

        builder.Property(e => e.CreatedAt)
            .HasColumnName("created_at")
            .IsRequired();
            
        builder.Property(e => e.UpdatedAt)
            .HasColumnName("updated_at");

        builder.Property(e => e.IsActive)
            .HasColumnName("is_active")
            .IsRequired()
            .HasDefaultValue(true)
            .ValueGeneratedNever(); // Forzar que siempre se incluya en el INSERT

        // Relations
        builder.HasOne(e => e.Business)
            .WithMany(b => b.FixedExpenses)
            .HasForeignKey(e => e.BusinessId)
            .OnDelete(DeleteBehavior.Cascade);
            
        builder.HasOne(e => e.Store)
            .WithMany(s => s.FixedExpenses)
            .HasForeignKey(e => e.StoreId)
            .OnDelete(DeleteBehavior.Restrict);
            
        builder.HasOne(e => e.Subcategory)
            .WithMany()
            .HasForeignKey(e => e.SubcategoryId)
            .OnDelete(DeleteBehavior.Restrict);
            
        builder.HasOne(e => e.RecurrenceType)
            .WithMany(rt => rt.FixedExpenses)
            .HasForeignKey(e => e.RecurrenceTypeId)
            .OnDelete(DeleteBehavior.Restrict);
        
        builder.HasOne(e => e.ExpenseType)
            .WithMany(et => et.FixedExpenses)
            .HasForeignKey(e => e.ExpenseTypeId)
            .OnDelete(DeleteBehavior.SetNull);
            
        builder.HasMany(e => e.GeneratedExpenses)
            .WithOne(exp => exp.FixedExpense)
            .HasForeignKey(exp => exp.FixedExpenseId)
            .OnDelete(DeleteBehavior.SetNull);

        // Indexes
        builder.HasIndex(e => e.BusinessId);
        builder.HasIndex(e => e.StoreId);
        builder.HasIndex(e => e.SubcategoryId);
        builder.HasIndex(e => e.RecurrenceTypeId);
        builder.HasIndex(e => e.ExpenseTypeId);
        builder.HasIndex(e => e.PaymentDate);
    }
}
