using System.ComponentModel.DataAnnotations;

namespace GPInventory.Domain.Entities;

/// <summary>
/// Etiqueta libre definida por el usuario para organizar egresos dentro de un negocio.
/// Independiente de las categorías/subcategorías del sistema.
/// </summary>
public class ExpenseTag
{
    public int Id { get; set; }

    [Required]
    public int BusinessId { get; set; }

    [Required]
    [StringLength(100)]
    public string Name { get; set; } = string.Empty;

    /// <summary>Color hexadecimal (ej: "#ef4444")</summary>
    [StringLength(7)]
    public string Color { get; set; } = "#6b7280";

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Navigation properties
    public Business? Business { get; set; }
    public ICollection<ExpenseTagAssignment> Assignments { get; set; } = new List<ExpenseTagAssignment>();
}
