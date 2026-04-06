using System.ComponentModel.DataAnnotations;
using GPInventory.Domain.Enums;

namespace GPInventory.Application.DTOs.Services;

// ============================================================================
// SERVICE PLAN DTOs
// ============================================================================

/// <summary>
/// DTO para crear un plan de servicio
/// </summary>
public class CreateServicePlanDto
{
    [Required]
    public int BusinessId { get; set; }

    public int? StoreId { get; set; }

    [Required]
    [StringLength(255)]
    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }

    /// <summary>
    /// ID del servicio específico (NULL = aplica a todos)
    /// </summary>
    public int? ServiceId { get; set; }

    /// <summary>
    /// ID de categoría de servicios incluidos
    /// </summary>
    public int? ServiceCategoryId { get; set; }

    /// <summary>Sesiones incluidas por período de facturación</summary>
    [Required]
    [Range(1, 1000)]
    public int ClassCount { get; set; }

    // ── Precios por frecuencia ──────────────────────────────────────────

    /// <summary>Precio mensual (base obligatorio)</summary>
    [Required]
    [Range(0.01, 10_000_000)]
    public decimal Price { get; set; }

    /// <summary>Precio trimestral (null = no ofrece esta opción)</summary>
    [Range(0.01, 10_000_000)]
    public decimal? PriceQuarterly { get; set; }

    /// <summary>Precio semestral (null = no ofrece esta opción)</summary>
    [Range(0.01, 10_000_000)]
    public decimal? PriceSemiannual { get; set; }

    /// <summary>Precio anual (null = no ofrece esta opción)</summary>
    [Range(0.01, 10_000_000)]
    public decimal? PriceAnnual { get; set; }

    // ── Configuración de cobro ──────────────────────────────────────────

    /// <summary>Pre-pago, diferido o ambos permitidos</summary>
    public PlanPaymentTiming PaymentTiming { get; set; } = PlanPaymentTiming.Deferred;

    /// <summary>Método de pago sugerido por defecto</summary>
    public int? DefaultPaymentMethodId { get; set; }
}

/// <summary>
/// DTO para actualizar un plan de servicio
/// </summary>
public class UpdateServicePlanDto
{
    [Required]
    [StringLength(255)]
    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }

    public int? ServiceId { get; set; }

    public int? ServiceCategoryId { get; set; }

    [Required]
    [Range(1, 1000)]
    public int ClassCount { get; set; }

    [Required]
    [Range(0.01, 10_000_000)]
    public decimal Price { get; set; }

    [Range(0.01, 10_000_000)]
    public decimal? PriceQuarterly { get; set; }

    [Range(0.01, 10_000_000)]
    public decimal? PriceSemiannual { get; set; }

    [Range(0.01, 10_000_000)]
    public decimal? PriceAnnual { get; set; }

    public PlanPaymentTiming PaymentTiming { get; set; } = PlanPaymentTiming.Deferred;

    public int? DefaultPaymentMethodId { get; set; }

    public bool Active { get; set; }
}

/// <summary>
/// DTO de respuesta para plan de servicio
/// </summary>
public class ServicePlanDto
{
    public int Id { get; set; }
    public int BusinessId { get; set; }
    public int? StoreId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public int? ServiceId { get; set; }
    public string? ServiceName { get; set; }
    public int? ServiceCategoryId { get; set; }
    public string? ServiceCategoryName { get; set; }
    public int ClassCount { get; set; }
    public decimal Price { get; set; }
    public decimal? PriceQuarterly { get; set; }
    public decimal? PriceSemiannual { get; set; }
    public decimal? PriceAnnual { get; set; }
    public PlanPaymentTiming PaymentTiming { get; set; }
    public string PaymentTimingDisplay { get; set; } = string.Empty;
    public int? DefaultPaymentMethodId { get; set; }
    public decimal PricePerClass { get; set; }
    public bool Active { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    // Estadísticas
    public int ActiveClientsCount { get; set; }
    public int TotalPurchasesCount { get; set; }
}

// ============================================================================
// CLIENT SERVICE PLAN DTOs
// ============================================================================

/// <summary>
/// DTO para comprar/asignar un plan a un cliente
/// </summary>
public class PurchaseServicePlanDto
{
    [Required]
    public int ClientId { get; set; }

    [Required]
    public int PlanId { get; set; }

    /// <summary>Frecuencia de facturación para este cliente</summary>
    public BillingFrequency BillingFrequency { get; set; } = BillingFrequency.Monthly;

    /// <summary>Pre-pago o pago diferido para este cliente</summary>
    public PlanPaymentTiming PaymentTiming { get; set; } = PlanPaymentTiming.Deferred;

    /// <summary>Método de pago del cliente (sobreescribe el del plan)</summary>
    public int? PaymentMethodId { get; set; }

    /// <summary>Duración del contrato en meses. Default: 12</summary>
    [Range(1, 120)]
    public int ContractMonths { get; set; } = 12;

    /// <summary>Fecha de inicio. Default: hoy</summary>
    public DateTime? StartDate { get; set; }

    public DocumentType DocumentType { get; set; } = DocumentType.None;

    public string? Notes { get; set; }
}

/// <summary>
/// DTO de respuesta para plan del cliente
/// </summary>
public class ClientServicePlanDto
{
    public int Id { get; set; }
    public int BusinessId { get; set; }
    public int? StoreId { get; set; }
    public int ServiceClientId { get; set; }
    public string ClientName { get; set; } = string.Empty;
    public string? ClientEmail { get; set; }
    /// <summary>Si el cliente es un sub-cliente, nombre del cliente raíz (pagador)</summary>
    public string? ParentClientName { get; set; }
    public int ServicePlanId { get; set; }
    public string PlanName { get; set; } = string.Empty;
    public int? ServiceSaleId { get; set; }
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public int TotalClasses { get; set; }
    public int ClassesUsed { get; set; }
    /// <summary>Cupos reservados en sesiones futuras agendadas</summary>
    public int ClassesReserved { get; set; }
    public int ClassesRemaining { get; set; }
    public ClientServicePlanStatus Status { get; set; }
    public string StatusDisplay { get; set; } = string.Empty;
    // ── Suscripción
    public BillingFrequency BillingFrequency { get; set; }
    public string BillingFrequencyDisplay { get; set; } = string.Empty;
    public PlanPaymentTiming PaymentTiming { get; set; }
    public string PaymentTimingDisplay { get; set; } = string.Empty;
    public int? PaymentMethodId { get; set; }
    public int ContractMonths { get; set; }
    public decimal AmountPerPeriod { get; set; }
    // ──
    public DateTime? CancelledAt { get; set; }
    public string? CancellationReason { get; set; }
    public decimal TotalPaid { get; set; }
    public decimal RevenueRecognized { get; set; }
    public decimal DeferredRevenue { get; set; }
    public DateTime? FrozenUntil { get; set; }
    public string? Notes { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    // Computed properties
    public decimal CostPerClass { get; set; }
    public int DaysRemaining { get; set; }
    public double UtilizationPercent { get; set; }
    /// <summary>% de cupos usados + reservados sobre el total</summary>
    public double CommittedPercent { get; set; }
    public bool IsExpiringSoon { get; set; }
    public bool IsHighRisk { get; set; }
    public string AlertLevel { get; set; } = "OK";

    // ── Período de facturación actual (el del mes en curso)
    /// <summary>Clases incluidas en el período actual (0 si no existe período creado)</summary>
    public int CurrentPeriodSessionsAllowed { get; set; }
    /// <summary>Clases ya asistidas en el período actual</summary>
    public int CurrentPeriodSessionsAttended { get; set; }
    /// <summary>Clases reservadas (agendadas) en el período actual</summary>
    public int CurrentPeriodSessionsReserved { get; set; }
    /// <summary>True cuando asistidas + reservadas >= permitidas</summary>
    public bool CurrentPeriodIsFull { get; set; }

    /// <summary>Si forma parte de una matrícula grupal (combo), ID del grupo</summary>
    public int? EnrollmentGroupId { get; set; }
}

// ── MATRÍCULA GRUPAL (COMBO) ─────────────────────────────────────────────────

/// <summary>
/// Un miembro dentro de una matrícula grupal: qué cliente y qué plan
/// </summary>
public class GroupEnrollmentMemberDto
{
    [Required]
    public int ClientId { get; set; }

    [Required]
    public int PlanId { get; set; }

    /// <summary>
    /// Precio por período para este miembro (opcional).
    /// Si null, se prorratea el total automáticamente entre todos los miembros.
    /// </summary>
    public decimal? AmountPerPeriodOverride { get; set; }
}

/// <summary>
/// DTO para registrar una matrícula grupal (combo de planes)
/// </summary>
public class PurchaseGroupEnrollmentDto
{
    /// <summary>Cliente que paga / firma el contrato (títular)</summary>
    [Required]
    public int PayerClientId { get; set; }

    [Required]
    public BillingFrequency BillingFrequency { get; set; } = BillingFrequency.Monthly;

    public PlanPaymentTiming PaymentTiming { get; set; } = PlanPaymentTiming.Deferred;

    public int? PaymentMethodId { get; set; }

    [Range(1, 120)]
    public int ContractMonths { get; set; } = 12;

    public DateTime? StartDate { get; set; }

    /// <summary>
    /// Precio combo total acordado por período de facturación.
    /// Puede ser menor que la suma de planes individuales.
    /// </summary>
    [Required]
    [Range(0, double.MaxValue)]
    public decimal TotalAmountPerPeriod { get; set; }

    public DocumentType DocumentType { get; set; } = DocumentType.None;

    public string? Notes { get; set; }

    /// <summary>Beneficiarios: cada uno con su plan asignado</summary>
    [Required]
    [MinLength(2)]
    public List<GroupEnrollmentMemberDto> Members { get; set; } = new();
}

/// <summary>
/// DTO de respuesta para una matrícula grupal
/// </summary>
public class PlanEnrollmentGroupDto
{
    public int Id { get; set; }
    public int BusinessId { get; set; }
    public int PayerClientId { get; set; }
    public string PayerClientName { get; set; } = string.Empty;
    public BillingFrequency BillingFrequency { get; set; }
    public string BillingFrequencyDisplay { get; set; } = string.Empty;
    public PlanPaymentTiming PaymentTiming { get; set; }
    public int ContractMonths { get; set; }
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public decimal TotalAmountPerPeriod { get; set; }
    public string Status { get; set; } = string.Empty;
    public string? Notes { get; set; }
    public DateTime CreatedAt { get; set; }
    /// <summary>Planes individuales de cada miembro del combo</summary>
    public List<ClientServicePlanDto> MemberPlans { get; set; } = new();
}

/// <summary>
/// DTO para congelar/descongelar un plan
/// </summary>
public class FreezePlanDto
{
    [Required]
    public DateTime FrozenUntil { get; set; }

    public string? Reason { get; set; }
}

/// <summary>
/// DTO para cancelar un plan
/// </summary>
public class CancelPlanDto
{
    [Required]
    [StringLength(500)]
    public string Reason { get; set; } = string.Empty;

    /// <summary>
    /// Si true, se reembolsa el valor proporcional de clases no usadas
    /// </summary>
    public bool RefundUnusedClasses { get; set; } = false;
}

// ============================================================================
// SERVICE ATTENDANCE DTOs
// ============================================================================

/// <summary>
/// DTO para registrar asistencia (check-in)
/// </summary>
public class CheckInAttendanceDto
{
    [Required]
    public int ServiceId { get; set; }

    [Required]
    public int ClientId { get; set; }

    [Required]
    public DateTime AttendanceDate { get; set; }

    public TimeSpan? AttendanceTime { get; set; }

    public string? Notes { get; set; }
}

/// <summary>
/// DTO para AGENDAR un cliente en una sesión futura.
/// Reserva un cupo del plan activo sin consumirlo.
/// </summary>
public class ScheduleAttendanceDto
{
    [Required]
    public int ServiceId { get; set; }

    [Required]
    public int ClientId { get; set; }

    /// <summary>Fecha de la sesión futura</summary>
    [Required]
    public DateTime SessionDate { get; set; }

    public TimeSpan? SessionTime { get; set; }

    /// <summary>service_session.id si la sesión ya fue creada</summary>
    public int? SessionId { get; set; }

    public string? Notes { get; set; }
}

/// <summary>
/// DTO para cancelar una cita agendada y liberar el cupo reservado
/// </summary>
public class CancelScheduledAttendanceDto
{
    public string? Reason { get; set; }
}

/// <summary>
/// DTO para registrar asistencia con pago directo
/// </summary>
public class PaidAttendanceDto
{
    [Required]
    public int ServiceId { get; set; }

    [Required]
    public int ClientId { get; set; }

    [Required]
    public DateTime AttendanceDate { get; set; }

    public TimeSpan? AttendanceTime { get; set; }

    [Required]
    [Range(0.01, 10000000)]
    public decimal Price { get; set; }

    [Required]
    public int PaymentMethodId { get; set; }

    public string? Notes { get; set; }
}

/// <summary>
/// DTO para respuesta de check-in
/// </summary>
public class CheckInResultDto
{
    public bool Success { get; set; }
    public string ResultType { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public int? AttendanceId { get; set; }
    public int? ClassesRemaining { get; set; }
    public decimal? AmountCharged { get; set; }
}

/// <summary>
/// DTO de respuesta para asistencia
/// </summary>
public class ServiceAttendanceDto
{
    public int Id { get; set; }
    public int BusinessId { get; set; }
    public int? StoreId { get; set; }
    public int ServiceId { get; set; }
    public string ServiceName { get; set; } = string.Empty;
    public DateTime AttendanceDate { get; set; }
    public TimeSpan? AttendanceTime { get; set; }
    public int? ServiceClientId { get; set; }
    public string? ClientName { get; set; }
    public int? ClientServicePlanId { get; set; }
    public string? PlanName { get; set; }
    public int? PlanBillingPeriodId { get; set; }
    public int? ServiceSaleId { get; set; }
    public AttendanceType AttendanceType { get; set; }
    public string AttendanceTypeDisplay { get; set; } = string.Empty;
    public AttendanceStatus Status { get; set; }
    public string StatusDisplay { get; set; } = string.Empty;
    public string? Notes { get; set; }
    public int? RegisteredByUserId { get; set; }
    public string? RegisteredByUserName { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    // Información adicional
    public decimal? AmountPaid { get; set; }
    public bool IsPlanUsage { get; set; }
}

/// <summary>
/// DTO para actualizar estado de asistencia
/// </summary>
public class UpdateAttendanceStatusDto
{
    [Required]
    public AttendanceStatus Status { get; set; }

    public string? Notes { get; set; }
}

// ============================================================================
// REPORTES DTOs
// ============================================================================

/// <summary>
/// DTO para dashboard del cliente
/// </summary>
public class ClientDashboardDto
{
    public int ClientId { get; set; }
    public string ClientName { get; set; } = string.Empty;
    public List<ClientServicePlanDto> ActivePlans { get; set; } = new();
    public List<ServiceAttendanceDto> RecentAttendances { get; set; } = new();
    public int TotalClassesAttended { get; set; }
    public int TotalClassesAvailable { get; set; }
    public decimal TotalSpent { get; set; }
}

/// <summary>
/// DTO para reporte de ingresos diferidos
/// </summary>
public class DeferredRevenueReportDto
{
    public int BusinessId { get; set; }
    public string BusinessName { get; set; } = string.Empty;
    public int ActivePlansCount { get; set; }
    public decimal TotalPaid { get; set; }
    public decimal TotalRevenueRecognized { get; set; }
    public decimal TotalDeferredRevenue { get; set; }
    public double RecognitionPercent { get; set; }
    public List<ClientServicePlanDto> Plans { get; set; } = new();
}

/// <summary>
/// DTO para reporte de ocupación de clases
/// </summary>
public class ClassOccupancyReportDto
{
    public int ServiceId { get; set; }
    public string ServiceName { get; set; } = string.Empty;
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public int TotalSessions { get; set; }
    public int TotalAttendees { get; set; }
    public int AttendeesWithPlan { get; set; }
    public int AttendeesWithoutPlan { get; set; }
    public decimal AverageAttendancePerSession { get; set; }
    public decimal RevenueFromPlans { get; set; }
    public decimal RevenueFromDirectPay { get; set; }
    public decimal TotalRevenue { get; set; }
}

// ============================================================================
// PLAN BILLING PERIOD DTOs
// ============================================================================

/// <summary>
/// DTO de respuesta para un período de facturación mensual
/// </summary>
public class PlanBillingPeriodDto
{
    public int Id { get; set; }
    public int BusinessId { get; set; }
    public int ClientServicePlanId { get; set; }
    public int ServiceClientId { get; set; }
    public string ClientName { get; set; } = string.Empty;
    public string PlanName { get; set; } = string.Empty;

    /// <summary>Año del período, ej: 2026</summary>
    public int BillingYear { get; set; }

    /// <summary>Mes del período 1-12</summary>
    public int BillingMonth { get; set; }

    /// <summary>Nombre legible del mes, ej: "Marzo 2026"</summary>
    public string PeriodLabel { get; set; } = string.Empty;

    public DateTime PeriodStartDate { get; set; }
    public DateTime PeriodEndDate { get; set; }

    public int SessionsAllowed { get; set; }
    public int SessionsAttended { get; set; }
    public int SessionsReserved { get; set; }
    public int SessionsRemaining { get; set; }

    /// <summary>% de sesiones comprometidas (asistidas + agendadas) sobre el total</summary>
    public double CommittedPercent { get; set; }

    /// <summary>pending | paid | overdue | waived</summary>
    public string Status { get; set; } = "pending";
    public string StatusDisplay { get; set; } = string.Empty;

    public decimal AmountDue { get; set; }
    public int? PlanTransactionId { get; set; }
    public DateTime? PaidAt { get; set; }
    public DateTime DueDate { get; set; }
    public bool IsOverdue { get; set; }
    public string? Notes { get; set; }
    public DateTime CreatedAt { get; set; }
}

/// <summary>
/// DTO para crear un nuevo período de facturación (al abrir/registrar el mes)
/// </summary>
public class CreateBillingPeriodDto
{
    [Required]
    public int ClientServicePlanId { get; set; }

    /// <summary>Año del período a crear, ej: 2026</summary>
    [Required]
    [Range(2020, 2100)]
    public int BillingYear { get; set; }

    /// <summary>Mes del período 1-12</summary>
    [Required]
    [Range(1, 12)]
    public int BillingMonth { get; set; }

    /// <summary>Monto a cobrar este mes. Si es null, se toma el precio del plan.</summary>
    public decimal? AmountDue { get; set; }

    /// <summary>Fecha límite de pago. Si null, se calcula como último día del mes.</summary>
    public DateTime? DueDate { get; set; }

    public string? Notes { get; set; }
}

/// <summary>
/// DTO para registrar el pago de un período
/// </summary>
public class PayBillingPeriodDto
{
    public int? PaymentMethodId { get; set; }

    public DocumentType DocumentType { get; set; } = DocumentType.None;

    /// <summary>Monto realmente cobrado (puede diferir de amount_due si hay descuento/recargo)</summary>
    public decimal? AmountPaid { get; set; }

    public DateTime? PaidAt { get; set; }

    public string? Notes { get; set; }
}

/// <summary>
/// DTO resumido para listados de períodos pendientes de cobro
/// </summary>
public class PendingBillingPeriodDto
{
    public int PeriodId { get; set; }
    public int ServiceClientId { get; set; }
    public string ClientName { get; set; } = string.Empty;
    public int ClientServicePlanId { get; set; }
    public string PlanName { get; set; } = string.Empty;
    public int BillingYear { get; set; }
    public int BillingMonth { get; set; }
    public string PeriodLabel { get; set; } = string.Empty;
    public decimal AmountDue { get; set; }
    public DateTime DueDate { get; set; }
    public string Status { get; set; } = "pending";
    public bool IsOverdue { get; set; }
    public int SessionsAttended { get; set; }
    public int SessionsReserved { get; set; }
    public int SessionsAllowed { get; set; }
}
