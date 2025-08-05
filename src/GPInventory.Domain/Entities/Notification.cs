using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace GPInventory.Domain.Entities
{
    [Table("notifications")]
    public class Notification
    {
        [Key]
        [Column("id")]
        public int Id { get; set; }

        [Required]
        [MaxLength(255)]
        [Column("title_template")]
        public string TitleTemplate { get; set; } = string.Empty;

        [Required]
        [Column("message_template")]
        public string MessageTemplate { get; set; } = string.Empty;

        [Required]
        [MaxLength(50)]
        [Column("type")]
        public string Type { get; set; } = string.Empty;

        [Column("created_at")]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        [Column("is_active")]
        public bool IsActive { get; set; } = true;

        // Navegación a las notificaciones de usuario
        public virtual ICollection<UserNotification> UserNotifications { get; set; } = new List<UserNotification>();
    }
}
