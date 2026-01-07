using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace GPInventory.Domain.Entities;

/// <summary>
/// Entidad que representa un proveedor
/// </summary>
[Table("provider")]
public class Provider : BaseEntity
{
    /// <summary>
    /// Nombre del proveedor
    /// </summary>
    [Column("name")]
    [Required]
    [StringLength(200)]
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// ID de la tienda a la que pertenece
    /// </summary>
    [Column("id_store")]
    public int? StoreId { get; set; }

    /// <summary>
    /// ID del negocio al que pertenece
    /// </summary>
    [Column("id_business")]
    public int BusinessId { get; set; }

    /// <summary>
    /// Información de contacto del proveedor (número de teléfono)
    /// </summary>
    [Column("contact")]
    public int? Contact { get; set; }

    /// <summary>
    /// Dirección del proveedor
    /// </summary>
    [Column("address")]
    [StringLength(500)]
    public string? Address { get; set; }

    /// <summary>
    /// Correo electrónico del proveedor
    /// </summary>
    [Column("mail")]
    [StringLength(200)]
    [EmailAddress]
    public string? Mail { get; set; }

    /// <summary>
    /// Prefijo o código corto del proveedor
    /// </summary>
    [Column("prefix")]
    [StringLength(20)]
    public string? Prefix { get; set; }

    /// <summary>
    /// Indica si el proveedor está activo
    /// </summary>
    [Column("active")]
    public bool Active { get; set; } = true;

    // Propiedades de navegación
    /// <summary>
    /// Tienda a la que pertenece el proveedor
    /// </summary>
    [ForeignKey("StoreId")]
    public virtual Store? Store { get; set; }

    /// <summary>
    /// Negocio al que pertenece el proveedor
    /// </summary>
    [ForeignKey("BusinessId")]
    public virtual Business? Business { get; set; }

    /// <summary>
    /// Movimientos de stock asociados a este proveedor
    /// </summary>
    public virtual ICollection<Stock> StockMovements { get; set; } = new List<Stock>();

    /// <summary>
    /// Entradas de suministro asociadas a este proveedor
    /// </summary>
    public virtual ICollection<SupplyEntry> SupplyEntries { get; set; } = new List<SupplyEntry>();

    /// <summary>
    /// Suministros que tienen a este proveedor como preferido
    /// </summary>
    public virtual ICollection<Supply> PreferredForSupplies { get; set; } = new List<Supply>();

    public Provider()
    {
    }

    public Provider(string name, int businessId, int? storeId = null)
    {
        Name = name;
        BusinessId = businessId;
        StoreId = storeId;
    }
}
