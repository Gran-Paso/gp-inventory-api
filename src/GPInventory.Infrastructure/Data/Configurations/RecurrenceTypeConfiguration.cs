using GPInventory.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GPInventory.Infrastructure.Data.Configurations;

public class RecurrenceTypeConfiguration : IEntityTypeConfiguration<RecurrenceType>
{
    public void Configure(EntityTypeBuilder<RecurrenceType> builder)
    {
        builder.ToTable("recurrence_type");
        
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).HasColumnName("id").ValueGeneratedOnAdd();
        
        builder.Property(e => e.Value)
            .HasColumnName("value")
            .HasMaxLength(255)
            .IsRequired();
            
        builder.Property(e => e.Description)
            .HasColumnName("description")
            .HasMaxLength(500);
        
        // Relations
        builder.HasMany(e => e.FixedExpenses)
            .WithOne(fe => fe.RecurrenceType)
            .HasForeignKey(fe => fe.RecurrenceTypeId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
