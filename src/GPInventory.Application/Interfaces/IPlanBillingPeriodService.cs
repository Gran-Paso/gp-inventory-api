using GPInventory.Application.DTOs.Services;

namespace GPInventory.Application.Interfaces;

/// <summary>
/// Servicio para gestionar los períodos de facturación mensual de planes de cliente.
/// Cada mes activo de un plan genera un período que registra las sesiones asistidas
/// y el estado de pago de ese mes.
/// </summary>
public interface IPlanBillingPeriodService
{
    /// <summary>Obtiene todos los períodos de un plan de cliente, ordenados de más reciente a más antiguo.</summary>
    Task<IEnumerable<PlanBillingPeriodDto>> GetByPlanAsync(int clientServicePlanId);

    /// <summary>Obtiene todos los períodos de un cliente (todos sus planes).</summary>
    Task<IEnumerable<PlanBillingPeriodDto>> GetByClientAsync(int serviceClientId);

    /// <summary>Obtiene períodos pendientes/vencidos de un negocio (para lista de cobros).</summary>
    Task<IEnumerable<PendingBillingPeriodDto>> GetPendingByBusinessAsync(int businessId);

    /// <summary>Obtiene un período por su ID.</summary>
    Task<PlanBillingPeriodDto> GetByIdAsync(int id);

    /// <summary>
    /// Abre/crea un nuevo período mensual para un plan.
    /// Si el período ya existe devuelve el existente sin duplicar.
    /// </summary>
    Task<PlanBillingPeriodDto> CreatePeriodAsync(CreateBillingPeriodDto dto, int userId);

    /// <summary>
    /// Registra el pago de un período: crea la plan_transaction correspondiente,
    /// vincula el plan_transaction_id y cambia status a 'paid'.
    /// </summary>
    Task<PlanBillingPeriodDto> PayPeriodAsync(int periodId, PayBillingPeriodDto dto, int userId);

    /// <summary>
    /// Recalcula sessions_attended y sessions_reserved de un período
    /// consultando service_attendance. Útil para sincronizar si hubo cambios manuales.
    /// </summary>
    Task<PlanBillingPeriodDto> RecalculateAttendanceAsync(int periodId);

    /// <summary>Marca un período como 'overdue' (llamado por un job o manualmente).</summary>
    Task MarkOverdueAsync(int businessId);

    /// <summary>Condona/perdona un período (status = waived).</summary>
    Task<PlanBillingPeriodDto> WaivePeriodAsync(int periodId, string? reason);
}
