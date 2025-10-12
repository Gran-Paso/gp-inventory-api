using GPInventory.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GPInventory.Infrastructure.Data.Configurations;

public class ExpenseTypeConfiguration : IEntityTypeConfiguration<ExpenseType>
{
    public void Configure(EntityTypeBuilder<ExpenseType> builder)
    {
        builder.ToTable("expense_type");
        
        builder.HasKey(et => et.Id);
        builder.Property(et => et.Id)
            .HasColumnName("id")
            .ValueGeneratedOnAdd();

        builder.Property(et => et.Name)
            .HasColumnName("name")
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(et => et.Code)
            .HasColumnName("code")
            .HasMaxLength(50)
            .IsRequired();
        
        builder.HasIndex(et => et.Code)
            .IsUnique();

        builder.Property(et => et.Description)
            .HasColumnName("description")
            .HasColumnType("TEXT");

        builder.Property(et => et.IsActive)
            .HasColumnName("is_active")
            .HasDefaultValue(true);

        builder.Property(et => et.CreatedAt)
            .HasColumnName("created_at")
            .HasDefaultValueSql("CURRENT_TIMESTAMP");

        // Relationships
        builder.HasMany(et => et.Expenses)
            .WithOne(e => e.ExpenseType)
            .HasForeignKey(e => e.ExpenseTypeId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasMany(et => et.FixedExpenses)
            .WithOne(fe => fe.ExpenseType)
            .HasForeignKey(fe => fe.ExpenseTypeId)
            .OnDelete(DeleteBehavior.SetNull);

        // Seed data
        builder.HasData(
            new ExpenseType
            {
                Id = 1,
                Name = "Gasto Operacional",
                Code = "expense",
                Description = "Egresos operacionales que no generan ingresos directos (luz, agua, gastos comunes, sueldos administrativos)",
                IsActive = true,
                CreatedAt = DateTime.Parse("2025-01-01T00:00:00")
            },
            new ExpenseType
            {
                Id = 2,
                Name = "Costo de Producción",
                Code = "cost",
                Description = "Egresos directamente relacionados con la producción de bienes o servicios (insumos, materias primas)",
                IsActive = true,
                CreatedAt = DateTime.Parse("2025-01-01T00:00:00")
            },
            new ExpenseType
            {
                Id = 3,
                Name = "Inversión",
                Code = "investment",
                Description = "Activos que generarán valor a largo plazo (equipos, muebles, maquinaria, mejoras al local)",
                IsActive = true,
                CreatedAt = DateTime.Parse("2025-01-01T00:00:00")
            }
        );
    }
}
