using GPInventory.Application.DTOs.Services;

namespace GPInventory.Application.Interfaces;

public interface IServiceSessionService
{
    /// <summary>
    /// Obtiene una sesión por ID con lista de asistentes
    /// </summary>
    Task<ServiceSessionDto> GetByIdAsync(int id);

    /// <summary>
    /// Lista sesiones de un negocio con filtro opcional de rango de fechas
    /// </summary>
    Task<IEnumerable<ServiceSessionSummaryDto>> GetByBusinessAsync(int businessId, DateTime? from = null, DateTime? to = null);

    /// <summary>
    /// Lista sesiones de un servicio específico
    /// </summary>
    Task<IEnumerable<ServiceSessionSummaryDto>> GetByServiceAsync(int serviceId, DateTime? from = null, DateTime? to = null);

    /// <summary>
    /// Lista próximas sesiones de un plan (para mostrar al cliente)
    /// </summary>
    Task<IEnumerable<ServiceSessionSummaryDto>> GetUpcomingByPlanAsync(int servicePlanId, int days = 30);

    /// <summary>
    /// Crea una sesión individual. Si AutoAssignPlanClients=true, asigna en status Scheduled
    /// a todos los clientes con plan activo del plan indicado.
    /// </summary>
    Task<ServiceSessionDto> CreateAsync(CreateServiceSessionDto dto, int userId);

    /// <summary>
    /// Crea múltiples sesiones por patrón semanal dentro de un rango de fechas
    /// </summary>
    Task<IEnumerable<ServiceSessionSummaryDto>> CreateBulkAsync(CreateBulkServiceSessionsDto dto, int userId);

    /// <summary>
    /// Actualiza datos de una sesión (mientras esté en estado Scheduled)
    /// </summary>
    Task<ServiceSessionDto> UpdateAsync(int id, UpdateServiceSessionDto dto);

    /// <summary>
    /// Cambia el estado a InProgress
    /// </summary>
    Task<ServiceSessionDto> StartSessionAsync(int id);

    /// <summary>
    /// Cambia el estado a Completed y reconoce ingresos de los asistentes con plan
    /// </summary>
    Task<ServiceSessionDto> CompleteSessionAsync(int id);

    /// <summary>
    /// Cancela la sesión y revierte las clases descontadas a los planes
    /// </summary>
    Task<ServiceSessionDto> CancelSessionAsync(int id, string? reason = null);

    /// <summary>
    /// Registra la asistencia de un cliente (con plan) o walk-in a una sesión
    /// </summary>
    Task<SessionAttendeeDto> RegisterAttendanceAsync(RegisterSessionAttendanceDto dto, int userId);

    /// <summary>
    /// Marca un asistente como Attended (confirmando asistencia real)
    /// </summary>
    Task<SessionAttendeeDto> MarkAttendedAsync(int attendanceId);

    /// <summary>
    /// Marca un asistente como Absent
    /// </summary>
    Task<SessionAttendeeDto> MarkAbsentAsync(int attendanceId);
}
