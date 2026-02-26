using GPInventory.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GPInventory.Infrastructure.Data.Configurations;

public class BankTransactionConfiguration : IEntityTypeConfiguration<BankTransaction>
{
    public void Configure(EntityTypeBuilder<BankTransaction> builder)
    {
        builder.ToTable("bank_transactions");

        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).HasColumnName("id").ValueGeneratedOnAdd();

        builder.Property(e => e.BankConnectionId)
            .HasColumnName("bank_connection_id")
            .IsRequired();

        builder.Property(e => e.BusinessId)
            .HasColumnName("business_id")
            .IsRequired();

        builder.Property(e => e.FintocId)
            .HasColumnName("fintoc_id")
            .HasMaxLength(200)
            .IsRequired();

        // Unique index to prevent duplicate imports
        builder.HasIndex(e => e.FintocId).IsUnique();

        builder.Property(e => e.Amount)
            .HasColumnName("amount")
            .HasColumnType("decimal(18,2)")
            .IsRequired();

        builder.Property(e => e.Description)
            .HasColumnName("description")
            .HasMaxLength(500);

        builder.Property(e => e.TransactionDate)
            .HasColumnName("transaction_date")
            .IsRequired();

        builder.Property(e => e.TransactionType)
            .HasColumnName("transaction_type")
            .HasMaxLength(50);

        builder.Property(e => e.Status)
            .HasColumnName("status")
            .HasMaxLength(50)
            .HasDefaultValue("pending")
            .IsRequired();

        builder.Property(e => e.ExpenseId)
            .HasColumnName("expense_id");

        builder.Property(e => e.SuggestedSubcategoryId)
            .HasColumnName("suggested_subcategory_id");

        builder.Property(e => e.IsActive)
            .HasColumnName("is_active")
            .HasDefaultValue(true);

        builder.Property(e => e.CreatedAt)
            .HasColumnName("created_at")
            .ValueGeneratedOnAdd()
            .HasDefaultValueSql("CURRENT_TIMESTAMP");

        builder.Property(e => e.UpdatedAt)
            .HasColumnName("updated_at")
            .ValueGeneratedOnAddOrUpdate()
            .HasDefaultValueSql("CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP");

        // Ignore navigations that would cause duplicate FK column issues
        builder.Ignore(e => e.Business);
        builder.Ignore(e => e.Expense);
        builder.Ignore(e => e.ExpenseSubcategory);
    }
}
