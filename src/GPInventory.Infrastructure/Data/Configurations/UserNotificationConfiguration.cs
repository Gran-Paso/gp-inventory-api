using GPInventory.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GPInventory.Infrastructure.Data.Configurations
{
    public class UserNotificationConfiguration : IEntityTypeConfiguration<UserNotification>
    {
        public void Configure(EntityTypeBuilder<UserNotification> builder)
        {
            builder.ToTable("user_notifications"); // Tabla correcta para notificaciones de usuarios
            
            builder.HasKey(un => un.Id);
            
            builder.Property(un => un.Id)
                .HasColumnName("id")
                .ValueGeneratedOnAdd();
                
            builder.Property(un => un.UserId)
                .HasColumnName("user_id")
                .IsRequired();
                
            builder.Property(un => un.NotificationId)
                .HasColumnName("notification_id")
                .IsRequired();
                
            // Estas propiedades no existen en tu tabla, las vamos a quitar temporalmente
            // builder.Property(un => un.RenderedTitle)
            // builder.Property(un => un.RenderedMessage)
                
            builder.Property(un => un.IsRead)
                .HasColumnName("is_read")
                .HasDefaultValue(false)
                .IsRequired(false);
                
            builder.Property(un => un.ReadAt)
                .HasColumnName("seen_at")
                .IsRequired(false);
                
            builder.Property(un => un.CreatedAt)
                .HasColumnName("created_at")
                .IsRequired(false);

            // Mensajes renderizados específicos para cada usuario
            builder.Property(un => un.RenderedTitle)
                .HasColumnName("rendered_title")
                .HasMaxLength(500)
                .IsRequired(false);
                
            builder.Property(un => un.RenderedMessage)
                .HasColumnName("rendered_message")
                .HasMaxLength(2000)
                .IsRequired(false);
                
            builder.Property(un => un.Variables)
                .HasColumnName("variables")
                .HasMaxLength(1000)
                .IsRequired(false);

            // Relaciones
            builder.HasOne(un => un.User)
                .WithMany()
                .HasForeignKey(un => un.UserId)
                .OnDelete(DeleteBehavior.Cascade);
                
            builder.HasOne(un => un.Notification)
                .WithMany(n => n.UserNotifications)
                .HasForeignKey(un => un.NotificationId)
                .OnDelete(DeleteBehavior.Cascade);

            // Índices para optimizar consultas
            builder.HasIndex(un => un.UserId);
            builder.HasIndex(un => un.IsRead);
            builder.HasIndex(un => un.CreatedAt);
            builder.HasIndex(un => new { un.UserId, un.IsRead });
        }
    }
}
