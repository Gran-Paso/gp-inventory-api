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

    /// <summary>
    /// ID del cliente CRM (service_client) que originó esta venta desde GP CRM
    /// </summary>
    [Column("crm_client_id")]
    public int? CrmClientId { get; set; }

    /// <summary>
    /// Canal de origen de la venta: null = interna, "webadas" = tienda online, etc.
    /// </summary>
    [Column("channel")]
    [StringLength(50)]
    public string? Channel { get; set; }

    // ─── Campos de envío (e-commerce) ───────────────────────────────────────

    /// <summary>Nombre del destinatario del envío.</summary>
    [Column("shipping_name")]
    [StringLength(100)]
    public string? ShippingName { get; set; }

    /// <summary>Teléfono de contacto para el despacho.</summary>
    [Column("shipping_phone")]
    [StringLength(20)]
    public string? ShippingPhone { get; set; }

    /// <summary>Dirección de despacho completa.</summary>
    [Column("shipping_address")]
    [StringLength(255)]
    public string? ShippingAddress { get; set; }

    /// <summary>Ciudad de despacho.</summary>
    [Column("shipping_city")]
    [StringLength(100)]
    public string? ShippingCity { get; set; }

    /// <summary>Región de despacho.</summary>
    [Column("shipping_region")]
    [StringLength(100)]
    public string? ShippingRegion { get; set; }

    /// <summary>
    /// Estado del despacho.
    /// Valores: pending | processing | shipped | delivered | cancelled
    /// </summary>
    [Column("shipping_status")]
    [StringLength(50)]
    public string? ShippingStatus { get; set; }

    /// <summary>Número de seguimiento del courier.</summary>
    [Column("tracking_number")]
    [StringLength(100)]
    public string? TrackingNumber { get; set; }

    /// <summary>Empresa de courier (ej: Starken, Chilexpress, etc.).</summary>
    [Column("shipping_carrier")]
    [StringLength(50)]
    public string? ShippingCarrier { get; set; }

    /// <summary>Notas de despacho internas.</summary>
    [Column("shipping_notes")]
    public string? ShippingNotes { get; set; }

    // ─── Propiedades de navegación ──────────────────────────────────────────
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
