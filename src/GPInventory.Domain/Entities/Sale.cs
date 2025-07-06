using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace GPInventory.Domain.Entities;

/// <summary>
/// Entidad que representa una venta
/// </summary>
[Table("sales")]
public class Sale : BaseEntity
{
    /// <summary>
    /// ID del negocio
    /// </summary>
    [Column("business")]
    [Required]
    public int BusinessId { get; set; }

    /// <summary>
    /// Fecha de la venta
    /// </summary>
    [Column("date")]
    [Required]
    public DateTime Date { get; set; }

    /// <summary>
    /// Nombre del cliente
    /// </summary>
    [Column("customer_name")]
    [StringLength(255)]
    public string? CustomerName { get; set; }

    /// <summary>
    /// RUT del cliente
    /// </summary>
    [Column("customer_rut")]
    [StringLength(255)]
    public string? CustomerRut { get; set; }

    /// <summary>
    /// Total de la venta
    /// </summary>
    [Column("total")]
    public int Total { get; set; }

    /// <summary>
    /// ID del método de pago
    /// </summary>
    [Column("payment_method")]
    public int? PaymentMethodId { get; set; }

    /// <summary>
    /// Notas adicionales
    /// </summary>
    [Column("notes")]
    public string? Notes { get; set; }

    // Propiedades de navegación
    /// <summary>
    /// Negocio asociado a la venta
    /// </summary>
    [ForeignKey("BusinessId")]
    public virtual Business Business { get; set; } = null!;

    /// <summary>
    /// Método de pago utilizado
    /// </summary>
    [ForeignKey("PaymentMethodId")]
    public virtual PaymentMethod? PaymentMethod { get; set; }

    /// <summary>
    /// Detalles de la venta
    /// </summary>
    public virtual ICollection<SaleDetail> SaleDetails { get; set; } = new List<SaleDetail>();

    public Sale()
    {
        Date = DateTime.UtcNow;
    }
}
