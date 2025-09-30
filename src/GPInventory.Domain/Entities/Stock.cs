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

    /// <summary>
    /// ID de la tienda donde se realizó el movimiento
    /// </summary>
    [Column("id_store")]
    [Required]
    public int StoreId { get; set; }

    /// <summary>
    /// ID de la venta asociada (opcional, para movimientos de salida por venta)
    /// </summary>
    [Column("sale_id")]
    public int? SaleId { get; set; }

    /// <summary>
    /// ID del stock padre (para relaciones FIFO entre entrada y salida)
    /// </summary>
    [Column("stock_id")]
    public int? StockId { get; set; }



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

    /// <summary>
    /// Tienda donde se realizó el movimiento
    /// </summary>
    [ForeignKey("StoreId")]
    public virtual Store Store { get; set; } = null!;

    /// <summary>
    /// Venta asociada (opcional, para movimientos de salida por venta)
    /// </summary>
    [ForeignKey("SaleId")]
    public virtual Sale? Sale { get; set; }

    public Stock()
    {
        Date = DateTime.UtcNow;
        IsActive = true; // ✅ Por defecto activo
    }

    public Stock(int productId, int flowTypeId, int amount, int storeId, int? auctionPrice = null, int? cost = null, int? providerId = null, string? notes = null, int? saleId = null, int? stockId = null)
    {
        ProductId = productId;
        FlowTypeId = flowTypeId;
        Amount = amount;
        StoreId = storeId;
        AuctionPrice = auctionPrice;
        Cost = cost;
        ProviderId = providerId;
        Notes = notes;
        SaleId = saleId;
        StockId = stockId;
        Date = DateTime.UtcNow;
        IsActive = true; // ✅ Por defecto activo
    }
}
