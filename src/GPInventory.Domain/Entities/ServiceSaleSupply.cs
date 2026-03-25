using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace GPInventory.Domain.Entities;

/// <summary>
/// Insumos consumidos durante la ejecución de una venta de servicio
/// </summary>
[Table("service_sale_supply")]
public class ServiceSaleSupply
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Required]
    [ForeignKey(nameof(ServiceSale))]
    [Column("service_sale_id")]
    public int ServiceSaleId { get; set; }

    [Required]
    [ForeignKey(nameof(Supply))]
    [Column("supply_id")]
    public int SupplyId { get; set; }

    [Required]
    [Column("quantity", TypeName = "decimal(10,2)")]
    public decimal Quantity { get; set; }

    [Column("notes", TypeName = "text")]
    public string? Notes { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Navigation properties
    public virtual ServiceSale? ServiceSale { get; set; }
    public virtual Supply? Supply { get; set; }
}
