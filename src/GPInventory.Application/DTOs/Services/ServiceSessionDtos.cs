using System.ComponentModel.DataAnnotations;
using GPInventory.Domain.Enums;

namespace GPInventory.Application.DTOs.Services;

// ============================================================================
// SERVICE SESSION DTOs
// ============================================================================

/// <summary>
/// Datos de una sesión/clase planificada (vista resumen para listas)
/// </summary>
public class ServiceSessionSummaryDto
{
    public int Id { get; set; }
    public int BusinessId { get; set; }
    public int? StoreId { get; set; }
    public int ServiceId { get; set; }
    public string ServiceName { get; set; } = string.Empty;
    public int? ServicePlanId { get; set; }
    public string? PlanName { get; set; }
    public DateTime SessionDate { get; set; }
    public TimeSpan? StartTime { get; set; }
    public TimeSpan? EndTime { get; set; }
    public int? Capacity { get; set; }
    public int AttendeeCount { get; set; }
    public int? AvailableSpots { get; set; }
    public string? InstructorName { get; set; }
    public string? Location { get; set; }
    public ServiceSessionStatus Status { get; set; }
    public string StatusDisplay { get; set; } = string.Empty;
    public bool IsFull { get; set; }
    public bool AllowWalkIns { get; set; } = true;
}

/// <summary>
/// Datos completos de una sesión incluyendo asistentes
/// </summary>
public class ServiceSessionDto : ServiceSessionSummaryDto
{
    public List<SessionAttendeeDto> Attendees { get; set; } = new();
    public string? Notes { get; set; }
    public DateTime CreatedAt { get; set; }
}

/// <summary>
/// Asistente a una sesión
/// </summary>
public class SessionAttendeeDto
{
    public int AttendanceId { get; set; }
    public int? ServiceClientId { get; set; }
    public string DisplayName { get; set; } = string.Empty;
    public string? ClientEmail { get; set; }
    public int? LinkedUserId { get; set; }
    public int? ClientServicePlanId { get; set; }
    public AttendanceType AttendanceType { get; set; }
    public AttendanceStatus Status { get; set; }
    public string StatusDisplay { get; set; } = string.Empty;
    public string? Notes { get; set; }
    /// <summary>Nombre del titular (pagador) cuando el asistente es un sub-cliente/beneficiario.</summary>
    public string? ParentClientName { get; set; }
}

/// <summary>
/// Crear una sesión individual
/// </summary>
public class CreateServiceSessionDto
{
    [Required]
    public int BusinessId { get; set; }

    public int? StoreId { get; set; }

    [Required]
    public int ServiceId { get; set; }

    /// <summary>
    /// Plan requerido para asistir. NULL = sesión abierta (validado por AllowedPlanIds o AllowWalkIns).
    /// </summary>
    public int? ServicePlanId { get; set; }

    /// <summary>
    /// IDs de ClientServicePlan a pre-registrar en la sesión (selección explícita).
    /// </summary>
    public List<int>? PreRegisterClientPlanIds { get; set; }

    /// <summary>
    /// Nombres de asistentes particulares (sin plan) a pre-registrar.
    /// </summary>
    public List<string>? PreRegisterWalkIns { get; set; }

    [Required]
    public DateTime SessionDate { get; set; }

    /// <summary>Hora en formato HH:mm o HH:mm:ss</summary>
    public string? StartTime { get; set; }
    public string? EndTime { get; set; }

    public int? Capacity { get; set; }

    [StringLength(255)]
    public string? InstructorName { get; set; }

    public int? InstructorUserId { get; set; }

    [StringLength(255)]
    public string? Location { get; set; }

    public string? Notes { get; set; }

    /// <summary>
    /// Si true, permite registrar asistentes sin plan (walk-ins, pagos sueltos).
    /// </summary>
    public bool AllowWalkIns { get; set; } = true;
}

/// <summary>
/// Crear múltiples sesiones de una vez (por patrón semanal en un rango de fechas)
/// </summary>
public class CreateBulkServiceSessionsDto
{
    [Required]
    public int BusinessId { get; set; }

    public int? StoreId { get; set; }

    [Required]
    public int ServiceId { get; set; }

    public int? ServicePlanId { get; set; }

    /// <summary>
    /// IDs de ClientServicePlan a pre-registrar en cada sesión generada.
    /// </summary>
    public List<int>? PreRegisterClientPlanIds { get; set; }

    /// <summary>
    /// Nombres de asistentes particulares a pre-registrar en cada sesión.
    /// </summary>
    public List<string>? PreRegisterWalkIns { get; set; }

    [Required]
    public DateTime RangeStart { get; set; }

    [Required]
    public DateTime RangeEnd { get; set; }

    /// <summary>
    /// Días de la semana (0=Dom, 1=Lun … 6=Sáb)
    /// </summary>
    [Required]
    public List<int> DaysOfWeek { get; set; } = new();

    /// <summary>Hora en formato HH:mm o HH:mm:ss</summary>
    public string? StartTime { get; set; }
    public string? EndTime { get; set; }
    public int? Capacity { get; set; }

    [StringLength(255)]
    public string? InstructorName { get; set; }

    public int? InstructorUserId { get; set; }

    [StringLength(255)]
    public string? Location { get; set; }

    public string? Notes { get; set; }

    /// <summary>
    /// Permitir walk-ins (asistentes sin plan) en las sesiones generadas.
    /// </summary>
    public bool AllowWalkIns { get; set; } = true;
}

/// <summary>
/// Actualizar una sesión
/// </summary>
public class UpdateServiceSessionDto
{
    public DateTime? SessionDate { get; set; }
    /// <summary>Hora en formato HH:mm o HH:mm:ss</summary>
    public string? StartTime { get; set; }
    public string? EndTime { get; set; }
    public int? Capacity { get; set; }

    [StringLength(255)]
    public string? InstructorName { get; set; }

    public int? InstructorUserId { get; set; }

    [StringLength(255)]
    public string? Location { get; set; }

    public string? Notes { get; set; }

    public bool? AllowWalkIns { get; set; }
}

/// <summary>
/// Registrar asistencia a una sesión (plan client o walk-in)
/// </summary>
public class RegisterSessionAttendanceDto
{
    [Required]
    public int SessionId { get; set; }

    /// <summary>
    /// Cliente registrado con plan. Si se provee, se descuenta una clase del plan.
    /// </summary>
    public int? ClientServicePlanId { get; set; }

    /// <summary>
    /// Walk-in: nombre libre si no hay cliente registrado
    /// </summary>
    [StringLength(255)]
    public string? WalkInName { get; set; }

    [StringLength(255)]
    public string? WalkInEmail { get; set; }

    public AttendanceType AttendanceType { get; set; } = AttendanceType.Plan;

    public string? Notes { get; set; }
}
