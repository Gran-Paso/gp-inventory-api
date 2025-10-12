namespace GPInventory.Domain.Entities;

/// <summary>
/// Representa el tipo de egreso: Gasto, Costo o Inversión
/// </summary>
public class ExpenseType
{
    /// <summary>
    /// ID único del tipo de egreso
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    /// Nombre del tipo (ej: "Gasto Operacional", "Costo de Producción", "Inversión")
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Código único para identificar el tipo (ej: "expense", "cost", "investment")
    /// </summary>
    public string Code { get; set; } = string.Empty;

    /// <summary>
    /// Descripción detallada del tipo de egreso
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// Indica si el tipo está activo
    /// </summary>
    public bool IsActive { get; set; } = true;

    /// <summary>
    /// Fecha de creación
    /// </summary>
    public DateTime CreatedAt { get; set; }

    // Navigation properties
    /// <summary>
    /// Gastos asociados a este tipo
    /// </summary>
    public virtual ICollection<Expense> Expenses { get; set; } = new List<Expense>();

    /// <summary>
    /// Gastos fijos asociados a este tipo
    /// </summary>
    public virtual ICollection<FixedExpense> FixedExpenses { get; set; } = new List<FixedExpense>();
}
