using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using GPInventory.Domain.Enums;

namespace GPInventory.Domain.Entities;

/// <summary>
/// Clase / sesión planificada de un servicio.
/// Agrupa las asistencias de múltiples participantes para una fecha y horario.
/// Un servicio per_enrollment tiene sesiones; un servicio fixed se vende directamente.
/// </summary>
[Table("service_session")]
public class ServiceSession
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
    [ForeignKey(nameof(Service))]
    [Column("service_id")]
    public int ServiceId { get; set; }

    /// <summary>
    /// Plan requerido para asistir. NULL = sesión abierta (walk-ins o cualquier plan).
    /// </summary>
    [ForeignKey(nameof(ServicePlan))]
    [Column("service_plan_id")]
    public int? ServicePlanId { get; set; }

    [Required]
    [Column("session_date", TypeName = "date")]
    public DateTime SessionDate { get; set; }

    [Column("start_time", TypeName = "time")]
    public TimeSpan? StartTime { get; set; }

    [Column("end_time", TypeName = "time")]
    public TimeSpan? EndTime { get; set; }

    /// <summary>
    /// Capacidad máxima de participantes. NULL = sin límite.
    /// </summary>
    [Column("capacity")]
    public int? Capacity { get; set; }

    [StringLength(255)]
    [Column("instructor_name")]
    public string? InstructorName { get; set; }

    [Column("instructor_user_id")]
    public int? InstructorUserId { get; set; }

    [StringLength(255)]
    [Column("location")]
    public string? Location { get; set; }

    [Required]
    [Column("status")]
    public ServiceSessionStatus Status { get; set; } = ServiceSessionStatus.Scheduled;

    [Column("notes", TypeName = "text")]
    public string? Notes { get; set; }

    /// <summary>
    /// Si false, solo pueden asistir clientes con plan activo (se bloquea walk-in / pago suelto).
    /// </summary>
    [Column("allow_walk_ins")]
    public bool AllowWalkIns { get; set; } = true;

    [Column("created_by_user_id")]
    public int? CreatedByUserId { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [Column("updated_at")]
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    // Navigation properties
    public virtual Business? Business { get; set; }
    public virtual Store? Store { get; set; }
    public virtual Service? Service { get; set; }
    public virtual ServicePlan? ServicePlan { get; set; }
    public virtual ICollection<ServiceAttendance>? Attendances { get; set; }

    // Computed
    [NotMapped]
    public int ConfirmedCount => Attendances?.Count(a =>
        a.Status == AttendanceStatus.Attended || a.Status == AttendanceStatus.Confirmed) ?? 0;

    [NotMapped]
    public bool IsFull => Capacity.HasValue && ConfirmedCount >= Capacity.Value;

    [NotMapped]
    public int? AvailableSpots => Capacity.HasValue ? Math.Max(0, Capacity.Value - ConfirmedCount) : null;
}
