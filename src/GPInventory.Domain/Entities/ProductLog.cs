using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace GPInventory.Domain.Entities
{
    [Table("product_log")]
    public class ProductLog
    {
        [Key]
        public int Id { get; set; }

        [Column("product_id")]
        public int ProductId { get; set; }

        [Column("user_id")]
        public int? UserId { get; set; }

        [Column("action_type")]
        [Required, MaxLength(50)]
        public string ActionType { get; set; } = string.Empty;

        [Column("table_name")]
        [Required, MaxLength(100)]
        public string TableName { get; set; } = "Products";

        [Column("timestamp")]
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;

        [Column("changes")]
        public string? Changes { get; set; }

        [Column("OldValues")]
        public string? OldValues { get; set; }

        [Column("NewValues")]
        public string? NewValues { get; set; }

        // Navigation properties
        public virtual Product? Product { get; set; }
        public virtual User? User { get; set; }
    }

    // Tipos de acción para el log
    public static class ProductLogActionTypes
    {
        public const string CREATE = "CREATE";
        public const string UPDATE = "UPDATE";
        public const string DELETE = "DELETE";
        public const string STOCK_CHANGE = "STOCK_CHANGE";
    }
}
