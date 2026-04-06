namespace GPInventory.Domain.Entities;

/// <summary>
/// Tabla pivot que relaciona un egreso con una etiqueta de usuario (many-to-many).
/// </summary>
public class ExpenseTagAssignment
{
    public int ExpenseId { get; set; }
    public int TagId { get; set; }

    // Navigation properties
    public Expense? Expense { get; set; }
    public ExpenseTag? Tag { get; set; }
}
