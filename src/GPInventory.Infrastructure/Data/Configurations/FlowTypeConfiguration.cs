using GPInventory.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GPInventory.Infrastructure.Data.Configurations;

public class FlowTypeConfiguration : IEntityTypeConfiguration<FlowType>
{
    public void Configure(EntityTypeBuilder<FlowType> builder)
    {
        builder.ToTable("flow_type");
        builder.HasKey(e => e.Id);
        
        builder.Property(e => e.Id)
            .HasColumnName("id")
            .ValueGeneratedOnAdd();
            
        builder.Property(e => e.Name)
            .HasColumnName("type")
            .HasMaxLength(100)
            .IsRequired();
        
        // BaseEntity properties - ignore since they don't exist in the database
        builder.Ignore(e => e.CreatedAt);
        builder.Ignore(e => e.UpdatedAt);
        builder.Ignore(e => e.IsActive);
        
        // Seed data
        builder.HasData(
            new FlowType("Compra") { Id = 1 },
            new FlowType("Producción") { Id = 2 },
            new FlowType("Devolución de Cliente") { Id = 3 },
            new FlowType("Transferencia Ingresada") { Id = 4 },
            new FlowType("Ajuste Positivo") { Id = 5 },
            new FlowType("Donación Recibida") { Id = 6 },
            new FlowType("Recepción de Muestra") { Id = 7 },
            new FlowType("Transformación Positiva") { Id = 8 },
            new FlowType("Importación") { Id = 9 },
            new FlowType("Consignación Recibida") { Id = 10 },
            new FlowType("Venta") { Id = 11 }
        );
    }
}
