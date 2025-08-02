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
    /// ID de la tienda a la que pertenece (mapeado desde BusinessId temporalmente)
    /// </summary>
    [Column("id_store")]
    public int? StoreId { get; set; }

    /// <summary>
    /// ID del negocio al que pertenece (compatibilidad temporal)
    /// </summary>
    [NotMapped]
    public int BusinessId { get; set; }

    // Propiedades de navegación
    /// <summary>
    /// Tienda a la que pertenece el proveedor
    /// </summary>
    [ForeignKey("StoreId")]
    public virtual Store? Store { get; set; }

    /// <summary>
    /// Negocio al que pertenece el proveedor (a través de Store - compatibilidad)
    /// </summary>
    [NotMapped]
    public virtual Business? Business => Store?.Business;

    /// <summary>
    /// Movimientos de stock asociados a este proveedor
    /// </summary>
    public virtual ICollection<Stock> StockMovements { get; set; } = new List<Stock>();

    public Provider()
    {
    }

    public Provider(string name, int businessId)
    {
        Name = name;
        BusinessId = businessId;
    }
}
