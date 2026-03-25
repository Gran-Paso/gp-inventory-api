using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace GPInventory.Domain.Entities;

/// <summary>
/// Gasto pendiente generado automáticamente al crear una sesión de servicio.
/// Cada fila corresponde a un ítem de costo (service_cost_item) del servicio.
/// Una vez decidido el pago, se crea el registro en la tabla `expenses` y
/// esta fila queda marcada como `paid` con el FK al expense generado.
/// </summary>
[Table("service_session_expense")]
public class ServiceSessionExpense
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Required]
    [ForeignKey(nameof(Business))]
    [Column("business_id")]
    public int BusinessId { get; set; }

    [ForeignKey(nameof(Store))]
    [Column("store_id")]
    public int? StoreId { get; set; }

    [Required]
    [ForeignKey(nameof(ServiceSession))]
    [Column("service_session_id")]
    public int ServiceSessionId { get; set; }

    /// <summary>Ítem de costo del servicio que originó este gasto. NULL si se agregó manualmente.</summary>
    [ForeignKey(nameof(ServiceCostItem))]
    [Column("service_cost_item_id")]
    public int? ServiceCostItemId { get; set; }

    /// <summary>Ítem de costo específico de la sesión (session_cost_item.id). NULL si no aplica.</summary>
    [Column("session_cost_item_id")]
    public int? SessionCostItemId { get; set; }

    [Required]
    [StringLength(500)]
    [Column("description")]
    public string Description { get; set; } = string.Empty;

    [Required]
    [Column("amount", TypeName = "decimal(12,2)")]
    public decimal Amount { get; set; }

    /// <summary>pending | paid | cancelled</summary>
    [Required]
    [StringLength(20)]
    [Column("status")]
    public string Status { get; set; } = "pending";

    /// <summary>employee | external | null</summary>
    [StringLength(20)]
    [Column("payee_type")]
    public string? PayeeType { get; set; }

    /// <summary>ID del empleado de RRHH (de gp-hr).</summary>
    [Column("payee_employee_id")]
    public int? PayeeEmployeeId { get; set; }

    /// <summary>Nombre del empleado al momento de la asignación (desnormalizado).</summary>
    [StringLength(255)]
    [Column("payee_employee_name")]
    public string? PayeeEmployeeName { get; set; }

    /// <summary>Nombre de la persona externa (no registrada en RRHH).</summary>
    [StringLength(255)]
    [Column("payee_external_name")]
    public string? PayeeExternalName { get; set; }

    /// <summary>FK al registro de gasto creado en `expenses` cuando se paga.</summary>
    [Column("expense_id")]
    public int? ExpenseId { get; set; }

    [Column("paid_at")]
    public DateTime? PaidAt { get; set; }

    [Column("paid_by_user_id")]
    public int? PaidByUserId { get; set; }

    [Column("notes", TypeName = "text")]
    public string? Notes { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [Column("updated_at")]
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    // Navigation properties
    public virtual Business?         Business        { get; set; }
    public virtual Store?             Store           { get; set; }
    public virtual ServiceSession?    ServiceSession  { get; set; }
    public virtual ServiceCostItem?   ServiceCostItem { get; set; }
}
