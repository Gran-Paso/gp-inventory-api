using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace GPInventory.Domain.Entities;

/// <summary>
/// Entidad que representa un movimiento de stock
/// </summary>
[Table("stock")]
public class Stock : BaseEntity
{
    /// <summary>
    /// ID del producto asociado
    /// </summary>
    [Column("product")]
    public int ProductId { get; set; }

    /// <summary>
    /// Fecha del movimiento
    /// </summary>
    [Column("date")]
    public DateTime Date { get; set; }

    /// <summary>
    /// ID del tipo de flujo (entrada/salida)
    /// </summary>
    [Column("flow")]
    public int FlowTypeId { get; set; }

    /// <summary>
    /// Cantidad del movimiento (positivo para entrada, negativo para salida)
    /// </summary>
    [Column("amount")]
    public int Amount { get; set; }

    /// <summary>
    /// Precio de subasta (opcional)
    /// </summary>
    [Column("auction_price")]
    public int? AuctionPrice { get; set; }

    /// <summary>
    /// Costo (opcional)
    /// </summary>
    [Column("cost")]
    public int? Cost { get; set; }

    /// <summary>
    /// ID del proveedor (opcional)
    /// </summary>
    [Column("provider")]
    public int? ProviderId { get; set; }

    /// <summary>
    /// Notas adicionales
    /// </summary>
    [Column("notes")]
    public string? Notes { get; set; }

    // Propiedades de navegación
    /// <summary>
    /// Producto asociado
    /// </summary>
    [ForeignKey("ProductId")]
    public virtual Product Product { get; set; } = null!;

    /// <summary>
    /// Tipo de flujo asociado
    /// </summary>
    [ForeignKey("FlowTypeId")]
    public virtual FlowType FlowType { get; set; } = null!;

    /// <summary>
    /// Proveedor asociado (opcional)
    /// </summary>
    [ForeignKey("ProviderId")]
    public virtual Provider? Provider { get; set; }

    public Stock()
    {
        Date = DateTime.UtcNow;
    }

    public Stock(int productId, int flowTypeId, int amount, int? auctionPrice = null, int? cost = null, int? providerId = null, string? notes = null)
    {
        ProductId = productId;
        FlowTypeId = flowTypeId;
        Amount = amount;
        AuctionPrice = auctionPrice;
        Cost = cost;
        ProviderId = providerId;
        Notes = notes;
        Date = DateTime.UtcNow;
    }
}
