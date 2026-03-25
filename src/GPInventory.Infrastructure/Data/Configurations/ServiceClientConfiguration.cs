using GPInventory.Domain.Entities;
using GPInventory.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GPInventory.Infrastructure.Data.Configurations;

public class ServiceClientConfiguration : IEntityTypeConfiguration<ServiceClient>
{
    public void Configure(EntityTypeBuilder<ServiceClient> builder)
    {
        builder.ToTable("service_client");

        // Configurar el enum ClientType para que se guarde como INT en lugar de string
        builder.Property(e => e.ClientType)
            .HasColumnName("client_type")
            .HasConversion<int>()
            .IsRequired();

        // Auto-referencia: un sub-cliente apunta a su cliente raíz
        builder.HasOne(e => e.ParentClient)
            .WithMany(e => e.SubClients)
            .HasForeignKey(e => e.ParentClientId)
            .OnDelete(DeleteBehavior.SetNull);

        // Tipo de relación con el cliente raíz
        builder.HasOne(e => e.RelationshipType)
            .WithMany(e => e.SubClients)
            .HasForeignKey(e => e.RelationshipTypeId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
