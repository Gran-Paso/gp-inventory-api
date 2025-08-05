using GPInventory.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GPInventory.Infrastructure.Data.Configurations
{
    public class NotificationConfiguration : IEntityTypeConfiguration<Notification>
    {
        public void Configure(EntityTypeBuilder<Notification> builder)
        {
            builder.ToTable("notifications");
            
            builder.HasKey(n => n.Id);
            
            builder.Property(n => n.Id)
                .HasColumnName("id")
                .ValueGeneratedOnAdd();
                
            builder.Property(n => n.TitleTemplate)
                .HasColumnName("title") // Cambiado para coincidir con tu tabla
                .HasMaxLength(255)
                .IsRequired();
                
            builder.Property(n => n.MessageTemplate)
                .HasColumnName("message") // Cambiado para coincidir con tu tabla
                .IsRequired();
                
            builder.Property(n => n.Type)
                .HasColumnName("type")
                .HasMaxLength(50)
                .IsRequired();
                
            builder.Property(n => n.CreatedAt)
                .HasColumnName("created_at")
                .HasDefaultValueSql("NOW()");
                
            builder.Property(n => n.IsActive)
                .HasColumnName("is_active")
                .HasDefaultValue(true);

            // Relación con UserNotifications
            builder.HasMany(n => n.UserNotifications)
                .WithOne(un => un.Notification)
                .HasForeignKey(un => un.NotificationId)
                .OnDelete(DeleteBehavior.Cascade);
        }
    }
}
