using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace GPInventory.Domain.Entities;

/// <summary>
/// Entidad que representa un tipo de flujo (entrada/salida)
/// </summary>
[Table("flow_type")]
public class FlowType : BaseEntity
{
    /// <summary>
    /// Nombre del tipo de flujo
    /// </summary>
    [Column("type")]
    [Required]
    [StringLength(100)]
    public string Name { get; set; } = string.Empty;

    // Propiedades de navegación
    /// <summary>
    /// Movimientos de stock asociados a este tipo de flujo
    /// </summary>
    public virtual ICollection<Stock> Stocks { get; set; } = new List<Stock>();

    public FlowType()
    {
    }

    public FlowType(string name)
    {
        Name = name;
    }
}
