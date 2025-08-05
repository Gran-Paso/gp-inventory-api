using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace GPInventory.Domain.Entities
{
    [Table("user_notifications")]
    public class UserNotification
    {
        [Key]
        [Column("id")]
        public int Id { get; set; }

        [Required]
        [Column("user_id")]
        public int UserId { get; set; }

        [Required]
        [Column("notification_id")]
        public int NotificationId { get; set; }

        [Column("is_read")]
        public bool? IsRead { get; set; } = false;

        [Column("seen_at")]
        public DateTime? ReadAt { get; set; }

        [Column("created_at")]
        public DateTime? CreatedAt { get; set; } = DateTime.UtcNow;

        // Mensajes renderizados específicos para cada usuario
        [Column("rendered_title")]
        public string? RenderedTitle { get; set; }

        [Column("rendered_message")]
        public string? RenderedMessage { get; set; }

        [Column("variables")]
        public string? Variables { get; set; }

        // Navegación
        [ForeignKey("UserId")]
        public virtual User User { get; set; } = null!;

        [ForeignKey("NotificationId")]
        public virtual Notification Notification { get; set; } = null!;
    }
}
