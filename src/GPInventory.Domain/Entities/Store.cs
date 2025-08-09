using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace GPInventory.Domain.Entities;

/// <summary>
/// Entidad que representa una tienda o local
/// </summary>
[Table("store")]
public class Store : BaseEntity
{
    /// <summary>
    /// Nombre de la tienda
    /// </summary>
    [Column("name")]
    [StringLength(255)]
    public string? Name { get; set; }

    /// <summary>
    /// Ubicación de la tienda
    /// </summary>
    [Column("location")]
    [StringLength(255)]
    public string? Location { get; set; }

    /// <summary>
    /// ID del negocio al que pertenece
    /// </summary>
    [Column("id_business")]
    public int? BusinessId { get; set; }

    /// <summary>
    /// ID del manager de la tienda
    /// </summary>
    [Column("id_manager")]
    public int? ManagerId { get; set; }

    /// <summary>
    /// Hora de apertura
    /// </summary>
    [Column("open_hour")]
    public TimeOnly? OpenHour { get; set; }

    /// <summary>
    /// Hora de cierre
    /// </summary>
    [Column("close_hour")]
    public TimeOnly? CloseHour { get; set; }

    /// <summary>
    /// Si la tienda está activa
    /// </summary>
    [Column("active")]
    public bool Active { get; set; } = true;

    // Propiedades de navegación
    /// <summary>
    /// Negocio al que pertenece la tienda
    /// </summary>
    [ForeignKey("BusinessId")]
    public virtual Business? Business { get; set; }

    /// <summary>
    /// Manager de la tienda
    /// </summary>
    [ForeignKey("ManagerId")]
    public virtual User? Manager { get; set; }

    /// <summary>
    /// Proveedores asociados a esta tienda
    /// </summary>
    public virtual ICollection<Provider> Providers { get; set; } = new List<Provider>();

    /// <summary>
    /// Ventas realizadas en esta tienda
    /// </summary>
    public virtual ICollection<Sale> Sales { get; set; } = new List<Sale>();

    /// <summary>
    /// Movimientos de stock de esta tienda
    /// </summary>
    public virtual ICollection<Stock> StockMovements { get; set; } = new List<Stock>();

    /// <summary>
    /// Gastos variables de esta tienda
    /// </summary>
    public virtual ICollection<Expense> Expenses { get; set; } = new List<Expense>();

    /// <summary>
    /// Gastos fijos de esta tienda
    /// </summary>
    public virtual ICollection<FixedExpense> FixedExpenses { get; set; } = new List<FixedExpense>();

    public Store()
    {
    }

    public Store(string name, string? location = null, int? businessId = null)
    {
        Name = name;
        Location = location;
        BusinessId = businessId;
    }
}
