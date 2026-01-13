using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace GPInventory.Domain.Entities;

/// <summary>
/// Entidad que representa productos manufacturados listos para ser enviados a tiendas
/// Similar a Stock pero para productos terminados en fábrica
/// </summary>
[Table("manufacture")]
public class Manufacture
{
    [Column("id")]
    public int Id { get; set; }

    /// <summary>
    /// ID del producto asociado
    /// </summary>
    [Column("product_id")]
    public int ProductId { get; set; }

    /// <summary>
    /// Fecha de manufactura
    /// </summary>
    [Column("date")]
    public DateTime Date { get; set; }

    /// <summary>
    /// Cantidad manufacturada
    /// </summary>
    [Column("amount")]
    public int Amount { get; set; }

    /// <summary>
    /// Costo de producción
    /// </summary>
    [Column("cost")]
    public decimal? Cost { get; set; }

    /// <summary>
    /// Notas adicionales
    /// </summary>
    [Column("notes")]
    [StringLength(500)]
    public string? Notes { get; set; }

    /// <summary>
    /// ID de la tienda destino (cuando se asigne a una tienda)
    /// </summary>
    [Column("store_id")]
    public int? StoreId { get; set; }

    /// <summary>
    /// ID del stock creado en tienda (cuando se envíe)
    /// </summary>
    [Column("stock_id")]
    public int? StockId { get; set; }

    /// <summary>
    /// Fecha de expiración del lote (opcional)
    /// </summary>
    [Column("expiration_date")]
    public DateTime? ExpirationDate { get; set; }

    /// <summary>
    /// ID del proceso completado que generó este lote
    /// </summary>
    [Column("process_done_id")]
    [Required]
    public int ProcessDoneId { get; set; }

    /// <summary>
    /// ID del negocio
    /// </summary>
    [Column("business_id")]
    [Required]
    public int BusinessId { get; set; }

    /// <summary>
    /// Estado: pending (en fábrica), sent (enviado a tienda), completed (recibido en tienda)
    /// </summary>
    [Column("status")]
    [StringLength(50)]
    public string Status { get; set; } = "pending";

    /// <summary>
    /// ID del usuario que creó el registro
    /// </summary>
    [Column("created_by_user_id")]
    public int? CreatedByUserId { get; set; }

    [Column("is_active")]
    public bool IsActive { get; set; } = true;

    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [Column("updated_at")]
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    // Propiedades de navegación
    /// <summary>
    /// Producto asociado
    /// </summary>
    [ForeignKey("ProductId")]
    public virtual Product Product { get; set; } = null!;

    /// <summary>
    /// Proceso completado que generó este lote
    /// </summary>
    [ForeignKey("ProcessDoneId")]
    public virtual ProcessDone ProcessDone { get; set; } = null!;

    /// <summary>
    /// Tienda destino (opcional)
    /// </summary>
    [ForeignKey("StoreId")]
    public virtual Store? Store { get; set; }

    /// <summary>
    /// Negocio asociado
    /// </summary>
    [ForeignKey("BusinessId")]
    public virtual Business Business { get; set; } = null!;

    /// <summary>
    /// Stock creado en tienda (opcional)
    /// </summary>
    [ForeignKey("StockId")]
    public virtual Stock? Stock { get; set; }

    public Manufacture()
    {
        Date = DateTime.UtcNow;
        Status = "pending";
        IsActive = true;
    }

    public Manufacture(int productId, int processDoneId, int businessId, int amount, decimal? cost = null, string? notes = null, DateTime? expirationDate = null)
    {
        ProductId = productId;
        ProcessDoneId = processDoneId;
        BusinessId = businessId;
        Amount = amount;
        Cost = cost;
        Notes = notes;
        ExpirationDate = expirationDate;
        Date = DateTime.UtcNow;
        Status = "pending";
        IsActive = true;
    }
}
