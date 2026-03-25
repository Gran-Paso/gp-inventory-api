using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace GPInventory.Domain.Entities;

/// <summary>
/// Línea de servicio dentro de una venta
/// </summary>
[Table("service_sale_item")]
public class ServiceSaleItem
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Required]
    [ForeignKey(nameof(ServiceSale))]
    [Column("service_sale_id")]
    public int ServiceSaleId { get; set; }

    [Required]
    [ForeignKey(nameof(Service))]
    [Column("service_id")]
    public int ServiceId { get; set; }

    [Column("price", TypeName = "decimal(12,2)")]
    public decimal? Price { get; set; }

    [Column("is_completed")]
    public bool IsCompleted { get; set; } = false;

    [Column("notes", TypeName = "text")]
    public string? Notes { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [Column("updated_at")]
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    // Navigation properties
    public virtual ServiceSale? ServiceSale { get; set; }
    public virtual Service? Service { get; set; }
}
