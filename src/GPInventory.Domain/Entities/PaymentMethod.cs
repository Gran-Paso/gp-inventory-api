using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace GPInventory.Domain.Entities;

/// <summary>
/// Entidad que representa un método de pago
/// </summary>
[Table("payment_methods")]
public class PaymentMethod : BaseEntity
{
    /// <summary>
    /// Nombre del método de pago
    /// </summary>
    [Column("name")]
    [Required]
    [StringLength(255)]
    public string Name { get; set; } = string.Empty;

    // Propiedades de navegación
    /// <summary>
    /// Ventas asociadas a este método de pago
    /// </summary>
    public virtual ICollection<Sale> Sales { get; set; } = new List<Sale>();

    public PaymentMethod()
    {
    }

    public PaymentMethod(string name)
    {
        Name = name;
    }
}
