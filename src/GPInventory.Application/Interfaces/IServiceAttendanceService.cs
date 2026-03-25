using GPInventory.Application.DTOs.Services;
using GPInventory.Domain.Enums;

namespace GPInventory.Application.Interfaces;

/// <summary>
/// Servicio para registro de asistencias
/// </summary>
public interface IServiceAttendanceService
{
    Task<ServiceAttendanceDto> GetByIdAsync(int id);
    Task<IEnumerable<ServiceAttendanceDto>> GetByClientAsync(int clientId, DateTime? startDate = null, DateTime? endDate = null);
    Task<IEnumerable<ServiceAttendanceDto>> GetByServiceAsync(int serviceId, DateTime? startDate = null, DateTime? endDate = null);
    Task<IEnumerable<ServiceAttendanceDto>> GetByDateAsync(int businessId, DateTime date);
    Task<CheckInResultDto> CheckInAsync(CheckInAttendanceDto dto, int userId);
    Task<CheckInResultDto> ScheduleAttendanceAsync(ScheduleAttendanceDto dto, int userId);
    Task<CheckInResultDto> ConfirmAttendanceAsync(int attendanceId, int userId);
    Task<CheckInResultDto> CancelScheduledAttendanceAsync(int attendanceId, CancelScheduledAttendanceDto dto);
    Task<CheckInResultDto> RegisterPaidAttendanceAsync(PaidAttendanceDto dto, int userId);
    Task<ServiceAttendanceDto> UpdateStatusAsync(int id, UpdateAttendanceStatusDto dto);
    Task DeleteAsync(int id);
    Task<ClassOccupancyReportDto> GetClassOccupancyReportAsync(int serviceId, DateTime startDate, DateTime endDate);
}
