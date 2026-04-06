using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using GPInventory.Domain.Enums;

namespace GPInventory.Domain.Entities;

/// <summary>
/// Registro de asistencia a una clase/servicio
/// Puede ser con plan o con pago directo
/// </summary>
[Table("service_attendance")]
public class ServiceAttendance
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

    /// <summary>
    /// Sesión planificada a la que pertenece esta asistencia (null si es registro suelto)
    /// </summary>
    [ForeignKey(nameof(ServiceSession))]
    [Column("service_session_id")]
    public int? ServiceSessionId { get; set; }

    [Required]
    [ForeignKey(nameof(Service))]
    [Column("service_id")]
    public int ServiceId { get; set; }

    [Required]
    [Column("attendance_date", TypeName = "date")]
    public DateTime AttendanceDate { get; set; }

    [Column("attendance_time", TypeName = "time")]
    public TimeSpan? AttendanceTime { get; set; }

    /// <summary>
    /// Cliente registrado (puede ser NULL para walk-ins)
    /// </summary>
    [ForeignKey(nameof(ServiceClient))]
    [Column("service_client_id")]
    public int? ServiceClientId { get; set; }

    /// <summary>
    /// Nombre del cliente si no está registrado
    /// </summary>
    [StringLength(255)]
    [Column("client_name")]
    public string? ClientName { get; set; }

    /// <summary>
    /// Si usó un plan, referencia al plan activo
    /// </summary>
    [ForeignKey(nameof(ClientServicePlan))]
    [Column("client_service_plan_id")]
    public int? ClientServicePlanId { get; set; }

    /// <summary>
    /// Período de facturación mensual al que pertenece esta asistencia (null si es pago directo o el período aún no fue creado)
    /// </summary>
    [ForeignKey(nameof(PlanBillingPeriod))]
    [Column("plan_billing_period_id")]
    public int? PlanBillingPeriodId { get; set; }

    /// <summary>
    /// Si pagó directamente, referencia a la venta
    /// </summary>
    [ForeignKey(nameof(ServiceSale))]
    [Column("service_sale_id")]
    public int? ServiceSaleId { get; set; }

    [Required]
    [Column("attendance_type")]
    public AttendanceType AttendanceType { get; set; } = AttendanceType.Paid;

    [Required]
    [Column("status")]
    public AttendanceStatus Status { get; set; } = AttendanceStatus.Confirmed;

    [Column("notes", TypeName = "text")]
    public string? Notes { get; set; }

    [ForeignKey(nameof(RegisteredByUser))]
    [Column("registered_by_user_id")]
    public int? RegisteredByUserId { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [Column("updated_at")]
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    // Navigation properties
    public virtual Business? Business { get; set; }
    public virtual Store? Store { get; set; }
    public virtual Service? Service { get; set; }
    public virtual ServiceClient? ServiceClient { get; set; }
    public virtual ClientServicePlan? ClientServicePlan { get; set; }
    public virtual PlanBillingPeriod? PlanBillingPeriod { get; set; }
    public virtual ServiceSale? ServiceSale { get; set; }
    public virtual User? RegisteredByUser { get; set; }
    public virtual ServiceSession? ServiceSession { get; set; }

    // Computed properties
    [NotMapped]
    public string DisplayName => ClientName ?? ServiceClient?.Name ?? "Walk-in";

    [NotMapped]
    public bool IsPlanUsage => AttendanceType == AttendanceType.Plan && ClientServicePlanId.HasValue;

    [NotMapped]
    public bool IsPaidDirect => AttendanceType == AttendanceType.Paid && ServiceSaleId.HasValue;
}
