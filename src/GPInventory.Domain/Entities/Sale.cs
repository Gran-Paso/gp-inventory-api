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
    /// ID de la tienda donde se realizó la venta
    /// </summary>
    [Column("id_store")]
    [Required]
    public int StoreId { get; set; }

    /// <summary>
    /// ID del negocio (propiedad temporal para compatibilidad)
    /// </summary>
    [NotMapped]
    public int BusinessId 
    { 
        get => Store?.BusinessId ?? 0; 
        set 
        {
            // Esta propiedad es solo para compatibilidad hacia atrás
            // El valor real se asigna a través de StoreId
        } 
    }

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

    /// <summary>
    /// ID del usuario vendedor que realizó la venta
    /// </summary>
    [Column("seller_user_id")]
    public int? SellerUserId { get; set; }

    // Propiedades de navegación
    /// <summary>
    /// Tienda donde se realizó la venta
    /// </summary>
    [ForeignKey("StoreId")]
    public virtual Store Store { get; set; } = null!;

    /// <summary>
    /// Negocio asociado a la venta (a través de Store - compatibilidad)
    /// </summary>
    [NotMapped]
    public virtual Business Business => Store?.Business!;

    /// <summary>
    /// Método de pago utilizado
    /// </summary>
    [ForeignKey("PaymentMethodId")]
    public virtual PaymentMethod? PaymentMethod { get; set; }

    /// <summary>
    /// Usuario vendedor que realizó la venta
    /// </summary>
    [ForeignKey("SellerUserId")]
    public virtual User? SellerUser { get; set; }

    /// <summary>
    /// Detalles de la venta
    /// </summary>
    public virtual ICollection<SaleDetail> SaleDetails { get; set; } = new List<SaleDetail>();

    public Sale()
    {
        Date = DateTime.UtcNow;
    }
}
