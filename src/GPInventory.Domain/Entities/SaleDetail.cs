using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace GPInventory.Domain.Entities;

/// <summary>
/// Entidad que representa el detalle de una venta
/// </summary>
[Table("sales_detail")]
public class SaleDetail : BaseEntity
{
    /// <summary>
    /// ID del producto
    /// </summary>
    [Column("product")]
    [Required]
    public int ProductId { get; set; }

    /// <summary>
    /// Cantidad vendida
    /// </summary>
    [Column("amount")]
    [Required]
    [StringLength(255)]
    public string Amount { get; set; } = string.Empty;

    /// <summary>
    /// Precio unitario al momento de la venta
    /// </summary>
    [Column("price")]
    [Required]
    public int Price { get; set; }

    /// <summary>
    /// Descuento aplicado
    /// </summary>
    [Column("discount")]
    public int? Discount { get; set; }

    /// <summary>
    /// ID de la venta
    /// </summary>
    [Column("sale")]
    [Required]
    public int SaleId { get; set; }

    /// <summary>
    /// ID del stock específico del cual se está vendiendo el producto
    /// </summary>
    [Column("stock_id")]
    public int? StockId { get; set; }

    // Propiedades de navegación
    /// <summary>
    /// Producto vendido
    /// </summary>
    [ForeignKey("ProductId")]
    public virtual Product Product { get; set; } = null!;

    /// <summary>
    /// Venta a la que pertenece este detalle
    /// </summary>
    [ForeignKey("SaleId")]
    public virtual Sale Sale { get; set; } = null!;

    /// <summary>
    /// Stock específico del cual se está vendiendo (opcional)
    /// </summary>
    [ForeignKey("StockId")]
    public virtual Stock? Stock { get; set; }

    // Propiedades calculadas
    /// <summary>
    /// Cantidad como número entero
    /// </summary>
    [NotMapped]
    public int AmountAsInt
    {
        get => int.TryParse(Amount, out int result) ? result : 0;
        set => Amount = value.ToString();
    }

    /// <summary>
    /// Subtotal (precio * cantidad - descuento)
    /// </summary>
    [NotMapped]
    public int Subtotal => (Price * AmountAsInt) - (Discount ?? 0);

    public SaleDetail()
    {
    }

    public SaleDetail(int productId, int amount, int price, int saleId, int? discount = null, int? stockId = null)
    {
        ProductId = productId;
        Amount = amount.ToString();
        Price = price;
        SaleId = saleId;
        Discount = discount;
        StockId = stockId;
    }
}
