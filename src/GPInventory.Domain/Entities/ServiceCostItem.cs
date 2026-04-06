using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace GPInventory.Domain.Entities;

/// <summary>
/// Costo/gasto asociado a un servicio (mano de obra, proveedor, etc.)
/// </summary>
[Table("service_cost_item")]
public class ServiceCostItem
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Required]
    [ForeignKey(nameof(Service))]
    [Column("service_id")]
    public int ServiceId { get; set; }

    [ForeignKey(nameof(Provider))]
    [Column("provider_id")]
    public int? ProviderId { get; set; }

    [Required]
    [StringLength(200)]
    [Column("name")]
    public string Name { get; set; } = string.Empty;

    [Column("description")]
    public string? Description { get; set; }

    [Required]
    [Column("amount", TypeName = "decimal(12,2)")]
    public decimal Amount { get; set; }

    [Column("quantity", TypeName = "decimal(10,3)")]
    public decimal Quantity { get; set; } = 1;

    [StringLength(50)]
    [Column("unit")]
    public string? Unit { get; set; }

    [StringLength(100)]
    [Column("cost_type")]
    public string? CostType { get; set; }

    [Column("position_id")]
    public int? PositionId { get; set; }

    [Column("is_externalized")]
    public bool IsExternalized { get; set; } = false;

    [StringLength(200)]
    [Column("provider_name")]
    public string? ProviderName { get; set; }

    [Column("sort_order")]
    public int SortOrder { get; set; } = 0;

    [Column("receipt_type_id")]
    public byte? ReceiptTypeId { get; set; }

    /// <summary>Empleado de RRHH asignado como mano de obra para este ítem.</summary>
    [Column("employee_id")]
    public int? EmployeeId { get; set; }

    [StringLength(200)]
    [Column("employee_name")]
    public string? EmployeeName { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Navigation properties
    public virtual Service? Service { get; set; }
    public virtual Provider? Provider { get; set; }
}
