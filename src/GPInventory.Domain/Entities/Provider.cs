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
    /// ID del negocio al que pertenece
    /// </summary>
    [Column("business")]
    public int BusinessId { get; set; }

    // Propiedades de navegación
    /// <summary>
    /// Negocio al que pertenece el proveedor
    /// </summary>
    [ForeignKey("BusinessId")]
    public virtual Business Business { get; set; } = null!;

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
