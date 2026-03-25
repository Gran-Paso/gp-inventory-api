using GPInventory.Application.DTOs.Services;
using GPInventory.Application.Interfaces;
using GPInventory.Domain.Entities;
using GPInventory.Domain.Enums;
using GPInventory.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace GPInventory.Infrastructure.Services;

public class ServiceAttendanceService : IServiceAttendanceService
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<ServiceAttendanceService> _logger;

    public ServiceAttendanceService(ApplicationDbContext context, ILogger<ServiceAttendanceService> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<ServiceAttendanceDto> GetByIdAsync(int id)
    {
        _logger.LogInformation("Obteniendo asistencia {AttendanceId}", id);

        var attendance = await _context.ServiceAttendances
            .Include(a => a.Service)
            .Include(a => a.ServiceClient)
            .Include(a => a.ClientServicePlan)
            .ThenInclude(cp => cp!.ServicePlan)
            .Include(a => a.ServiceSale)
            .Include(a => a.RegisteredByUser)
            .FirstOrDefaultAsync(a => a.Id == id);

        if (attendance == null)
        {
            throw new InvalidOperationException($"Asistencia con ID {id} no encontrado");
        }

        return MapToDto(attendance);
    }

    public async Task<IEnumerable<ServiceAttendanceDto>> GetByClientAsync(int clientId, DateTime? startDate = null, DateTime? endDate = null)
    {
        _logger.LogInformation("Obteniendo asistencias del cliente {ClientId}", clientId);

        var query = _context.ServiceAttendances
            .Include(a => a.Service)
            .Include(a => a.ServiceClient)
            .Include(a => a.ClientServicePlan)
            .ThenInclude(cp => cp!.ServicePlan)
            .Include(a => a.ServiceSale)
            .Include(a => a.RegisteredByUser)
            .Where(a => a.ServiceClientId == clientId);

        if (startDate.HasValue)
        {
            query = query.Where(a => a.AttendanceDate >= startDate.Value);
        }

        if (endDate.HasValue)
        {
            query = query.Where(a => a.AttendanceDate <= endDate.Value);
        }

        var attendances = await query
            .OrderByDescending(a => a.AttendanceDate)
            .ToListAsync();

        return attendances.Select(MapToDto).ToList();
    }

    public async Task<IEnumerable<ServiceAttendanceDto>> GetByServiceAsync(int serviceId, DateTime? startDate = null, DateTime? endDate = null)
    {
        _logger.LogInformation("Obteniendo asistencias del servicio {ServiceId}", serviceId);

        var query = _context.ServiceAttendances
            .Include(a => a.Service)
            .Include(a => a.ServiceClient)
            .Include(a => a.ClientServicePlan)
            .ThenInclude(cp => cp!.ServicePlan)
            .Include(a => a.ServiceSale)
            .Include(a => a.RegisteredByUser)
            .Where(a => a.ServiceId == serviceId);

        if (startDate.HasValue)
        {
            query = query.Where(a => a.AttendanceDate >= startDate.Value);
        }

        if (endDate.HasValue)
        {
            query = query.Where(a => a.AttendanceDate <= endDate.Value);
        }

        var attendances = await query
            .OrderByDescending(a => a.AttendanceDate)
            .ToListAsync();

        return attendances.Select(MapToDto).ToList();
    }

    public async Task<IEnumerable<ServiceAttendanceDto>> GetByDateAsync(int businessId, DateTime date)
    {
        _logger.LogInformation("Obteniendo asistencias para business {BusinessId} en fecha {Date}", businessId, date);

        var startOfDay = date.Date;
        var endOfDay = startOfDay.AddDays(1);

        var attendances = await _context.ServiceAttendances
            .Include(a => a.Service)
            .Include(a => a.ServiceClient)
            .Include(a => a.ClientServicePlan)
            .ThenInclude(cp => cp!.ServicePlan)
            .Include(a => a.ServiceSale)
            .Include(a => a.RegisteredByUser)
            .Where(a => a.BusinessId == businessId
                && a.AttendanceDate >= startOfDay
                && a.AttendanceDate < endOfDay)
            .OrderBy(a => a.AttendanceDate)
            .ToListAsync();

        return attendances.Select(MapToDto).ToList();
    }

    public async Task<CheckInResultDto> CheckInAsync(CheckInAttendanceDto dto, int userId)
    {
        _logger.LogInformation("Check-in de cliente {ClientId} en servicio {ServiceId}", 
            dto.ClientId, dto.ServiceId);

        using var transaction = await _context.Database.BeginTransactionAsync();
        try
        {
            var now = DateTime.UtcNow;

            // Buscar plan activo del cliente para este servicio
            var activePlan = await _context.ClientServicePlans
                .Include(cp => cp.ServicePlan)
                .ThenInclude(p => p!.Service)
                .Include(cp => cp.ServiceClient)
                .Where(cp => cp.ServiceClientId == dto.ClientId
                    && cp.ServicePlan != null && cp.ServicePlan.ServiceId == dto.ServiceId
                    && cp.Status == ClientServicePlanStatus.Active
                    && cp.EndDate > now
                    && (cp.FrozenUntil == null || cp.FrozenUntil <= now))
                .OrderBy(cp => cp.EndDate) // Usar el que expira primero
                .FirstOrDefaultAsync();

            if (activePlan == null || activePlan.ClassesRemaining <= 0)
            {
                // No tiene plan activo o se quedó sin clases
                return new CheckInResultDto
                {
                    Success = false,
                    ResultType = activePlan == null ? "no_plan" : "no_classes",
                    Message = activePlan == null 
                        ? "Cliente no tiene un plan activo para este servicio"
                        : "Cliente no tiene clases disponibles en su plan"
                };
            }

            // Verificar si ya hay una asistencia para hoy
            var today = DateTime.UtcNow.Date;
            var existingAttendance = await _context.ServiceAttendances
                .FirstOrDefaultAsync(a => a.ServiceClientId == dto.ClientId
                    && a.ClientServicePlanId == activePlan.Id
                    && a.AttendanceDate.Date == today);

            if (existingAttendance != null)
            {
                return new CheckInResultDto
                {
                    Success = false,
                    ResultType = "already_checked_in",
                    Message = "Cliente ya registró asistencia hoy para este servicio",
                    AttendanceId = existingAttendance.Id
                };
            }

            // Obtener información del servicio para BusinessId y StoreId
            var service = await _context.Services.FindAsync(dto.ServiceId);
            if (service == null)
            {
                throw new InvalidOperationException("Servicio no encontrado");
            }

            // Crear registro de asistencia
            var billingPeriodId = await FindBillingPeriodIdAsync(activePlan.Id, dto.AttendanceDate);

            var attendance = new ServiceAttendance
            {
                BusinessId = service.BusinessId,
                StoreId = service.StoreId,
                ServiceId = dto.ServiceId,
                ServiceClientId = dto.ClientId,
                ClientServicePlanId = activePlan.Id,
                PlanBillingPeriodId = billingPeriodId,
                AttendanceDate = dto.AttendanceDate,
                AttendanceTime = dto.AttendanceTime,
                AttendanceType = AttendanceType.Plan,
                Status = AttendanceStatus.Attended,
                Notes = dto.Notes,
                RegisteredByUserId = userId,
                CreatedAt = now,
                UpdatedAt = now
            };

            _context.ServiceAttendances.Add(attendance);

            // Calcular ingreso a reconocer (precio por clase del período)
            var revenuePerClass = activePlan.RevenuePerClass;

            // Actualizar el plan del cliente
            activePlan.ClassesUsed += 1;
            activePlan.RevenueRecognized += revenuePerClass;
            activePlan.UpdatedAt = now;

            // Actualizar contador del período de facturación
            if (billingPeriodId.HasValue)
            {
                var period = await _context.PlanBillingPeriods.FindAsync(billingPeriodId.Value);
                if (period != null)
                {
                    period.SessionsAttended += 1;
                    period.UpdatedAt = now;
                }
            }

            await _context.SaveChangesAsync();
            await transaction.CommitAsync();

            _logger.LogInformation("Check-in exitoso. Cliente {ClientId}, Plan {PlanId}, Asistencia {AttendanceId}. " +
                "Clases restantes: {ClassesRemaining}. Ingreso reconocido: ${Revenue}",
                dto.ClientId, activePlan.Id, attendance.Id, 
                activePlan.ClassesRemaining, revenuePerClass);

            return new CheckInResultDto
            {
                Success = true,
                ResultType = "success",
                Message = $"Check-in exitoso. Clases restantes: {activePlan.ClassesRemaining}",
                AttendanceId = attendance.Id,
                ClassesRemaining = activePlan.ClassesRemaining
            };
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync();
            _logger.LogError(ex, "Error en check-in de cliente {ClientId}", dto.ClientId);
            throw;
        }
    }

    public async Task<CheckInResultDto> ScheduleAttendanceAsync(ScheduleAttendanceDto dto, int userId)
    {
        _logger.LogInformation("Agendando cliente {ClientId} en servicio {ServiceId} para {SessionDate}",
            dto.ClientId, dto.ServiceId, dto.SessionDate);

        using var transaction = await _context.Database.BeginTransactionAsync();
        try
        {
            var service = await _context.Services.FindAsync(dto.ServiceId)
                ?? throw new InvalidOperationException("Servicio no encontrado");

            var sessionDate = dto.SessionDate.Date;

            // Buscar plan activo que cubra la fecha de la sesión (no necesariamente hoy)
            var activePlan = await _context.ClientServicePlans
                .Include(cp => cp.ServicePlan)
                .Where(cp => cp.ServiceClientId == dto.ClientId
                    && cp.Status == ClientServicePlanStatus.Active
                    && cp.EndDate.Date >= sessionDate
                    && (cp.FrozenUntil == null || cp.FrozenUntil.Value.Date < sessionDate)
                    && (cp.ServicePlan!.ServiceId == dto.ServiceId || cp.ServicePlan.ServiceId == null)
                    && cp.ClassesRemaining > 0)
                .OrderBy(cp => cp.EndDate)
                .FirstOrDefaultAsync();

            if (activePlan == null)
            {
                return new CheckInResultDto
                {
                    Success = false,
                    ResultType = "no_plan",
                    Message = "El cliente no tiene un plan activo con cupos disponibles para la fecha de la sesión."
                };
            }

            // Verificar si ya tiene una cita para ese día/servicio
            var existingSchedule = await _context.ServiceAttendances
                .FirstOrDefaultAsync(a => a.ServiceClientId == dto.ClientId
                    && a.ServiceId == dto.ServiceId
                    && a.AttendanceDate.Date == sessionDate
                    && (a.Status == AttendanceStatus.Scheduled || a.Status == AttendanceStatus.Confirmed));

            if (existingSchedule != null)
            {
                return new CheckInResultDto
                {
                    Success = false,
                    ResultType = "already_scheduled",
                    Message = "El cliente ya tiene una cita agendada para este servicio en esa fecha.",
                    AttendanceId = existingSchedule.Id
                };
            }

            // Reservar cupo
            activePlan.ClassesReserved += 1;
            activePlan.UpdatedAt = DateTime.UtcNow;

            var billingPeriodId = await FindBillingPeriodIdAsync(activePlan.Id, sessionDate);

            var attendance = new ServiceAttendance
            {
                BusinessId = service.BusinessId,
                StoreId = service.StoreId,
                ServiceId = dto.ServiceId,
                ServiceClientId = dto.ClientId,
                ClientServicePlanId = activePlan.Id,
                PlanBillingPeriodId = billingPeriodId,
                ServiceSessionId = dto.SessionId,
                AttendanceDate = sessionDate,
                AttendanceTime = dto.SessionTime,
                AttendanceType = AttendanceType.Plan,
                Status = AttendanceStatus.Scheduled,
                Notes = dto.Notes,
                RegisteredByUserId = userId,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            _context.ServiceAttendances.Add(attendance);

            // Actualizar contador del período de facturación
            if (billingPeriodId.HasValue)
            {
                var period = await _context.PlanBillingPeriods.FindAsync(billingPeriodId.Value);
                if (period != null)
                {
                    period.SessionsReserved += 1;
                    period.UpdatedAt = DateTime.UtcNow;
                }
            }

            await _context.SaveChangesAsync();
            await transaction.CommitAsync();

            _logger.LogInformation(
                "Cupo reservado. Cliente {ClientId}, Plan {PlanId}, Cita {AttendanceId}, Día {Date}. Cupos disponibles: {Remaining}",
                dto.ClientId, activePlan.Id, attendance.Id, sessionDate, activePlan.ClassesRemaining);

            return new CheckInResultDto
            {
                Success = true,
                ResultType = "reserved",
                Message = $"Cita agendada. Cupos disponibles restantes: {activePlan.ClassesRemaining}",
                AttendanceId = attendance.Id,
                ClassesRemaining = activePlan.ClassesRemaining
            };
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync();
            _logger.LogError(ex, "Error al agendar cliente {ClientId}", dto.ClientId);
            throw;
        }
    }

    public async Task<CheckInResultDto> ConfirmAttendanceAsync(int attendanceId, int userId)
    {
        _logger.LogInformation("Confirmando asistencia {AttendanceId}", attendanceId);

        using var transaction = await _context.Database.BeginTransactionAsync();
        try
        {
            var attendance = await _context.ServiceAttendances
                .Include(a => a.ClientServicePlan)
                .FirstOrDefaultAsync(a => a.Id == attendanceId)
                ?? throw new InvalidOperationException("Asistencia no encontrada");

            if (attendance.Status == AttendanceStatus.Attended)
            {
                return new CheckInResultDto
                {
                    Success = false,
                    ResultType = "already_confirmed",
                    Message = "La asistencia ya está confirmada.",
                    AttendanceId = attendanceId
                };
            }

            var wasScheduled = attendance.Status == AttendanceStatus.Scheduled;

            // Si el período de facturación no está vinculado, intentar encontrarlo ahora
            // para que el trigger MySQL pueda actualizar sessions_attended correctamente.
            if (attendance.ClientServicePlanId.HasValue && !attendance.PlanBillingPeriodId.HasValue)
            {
                var bpId = await FindBillingPeriodIdAsync(attendance.ClientServicePlanId.Value, attendance.AttendanceDate);
                if (bpId.HasValue)
                    attendance.PlanBillingPeriodId = bpId;
            }

            attendance.Status = AttendanceStatus.Attended;
            attendance.UpdatedAt = DateTime.UtcNow;

            if (attendance.ClientServicePlanId.HasValue && attendance.ClientServicePlan != null)
            {
                var plan = attendance.ClientServicePlan;
                var revenuePerClass = plan.RevenuePerClass;

                if (wasScheduled)
                {
                    // Mover reserved → used
                    plan.ClassesReserved = Math.Max(0, plan.ClassesReserved - 1);
                }

                plan.ClassesUsed += 1;
                plan.RevenueRecognized += revenuePerClass;
                plan.UpdatedAt = DateTime.UtcNow;

                _logger.LogInformation(
                    "Asistencia confirmada. Plan {PlanId}: reserved={Reserved}, used={Used}",
                    plan.Id, plan.ClassesReserved, plan.ClassesUsed);
            }

            // Actualizar contador del período de facturación
            if (attendance.PlanBillingPeriodId.HasValue)
            {
                var period = await _context.PlanBillingPeriods.FindAsync(attendance.PlanBillingPeriodId.Value);
                if (period != null)
                {
                    if (wasScheduled) period.SessionsReserved = Math.Max(0, period.SessionsReserved - 1);
                    period.SessionsAttended += 1;
                    period.UpdatedAt = DateTime.UtcNow;
                }
            }

            await _context.SaveChangesAsync();
            await transaction.CommitAsync();

            return new CheckInResultDto
            {
                Success = true,
                ResultType = "confirmed",
                Message = "Asistencia confirmada exitosamente.",
                AttendanceId = attendanceId,
                ClassesRemaining = attendance.ClientServicePlan?.ClassesRemaining
            };
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync();
            _logger.LogError(ex, "Error al confirmar asistencia {AttendanceId}", attendanceId);
            throw;
        }
    }

    public async Task<CheckInResultDto> CancelScheduledAttendanceAsync(int attendanceId, CancelScheduledAttendanceDto dto)
    {
        _logger.LogInformation("Cancelando cita agendada {AttendanceId}", attendanceId);

        using var transaction = await _context.Database.BeginTransactionAsync();
        try
        {
            var attendance = await _context.ServiceAttendances
                .Include(a => a.ClientServicePlan)
                .FirstOrDefaultAsync(a => a.Id == attendanceId)
                ?? throw new InvalidOperationException("Asistencia no encontrada");

            if (attendance.Status == AttendanceStatus.Attended)
            {
                return new CheckInResultDto
                {
                    Success = false,
                    ResultType = "already_attended",
                    Message = "No se puede cancelar una asistencia ya confirmada. Use actualizar estado."
                };
            }

            var wasScheduled = attendance.Status == AttendanceStatus.Scheduled;
            attendance.Status = AttendanceStatus.Cancelled;
            attendance.Notes = string.IsNullOrEmpty(dto.Reason)
                ? attendance.Notes
                : $"{attendance.Notes} | Cancelado: {dto.Reason}".TrimStart(' ', '|', ' ');
            attendance.UpdatedAt = DateTime.UtcNow;

            // Liberar cupo reservado solo si venía de 'Scheduled'
            if (wasScheduled && attendance.ClientServicePlanId.HasValue && attendance.ClientServicePlan != null)
            {
                var plan = attendance.ClientServicePlan;
                plan.ClassesReserved = Math.Max(0, plan.ClassesReserved - 1);
                plan.UpdatedAt = DateTime.UtcNow;

                _logger.LogInformation(
                    "Cupo liberado. Plan {PlanId}: reserved={Reserved}", plan.Id, plan.ClassesReserved);
            }

            // Actualizar contador del período de facturación
            if (attendance.PlanBillingPeriodId.HasValue)
            {
                var period = await _context.PlanBillingPeriods.FindAsync(attendance.PlanBillingPeriodId.Value);
                if (period != null)
                {
                    period.SessionsReserved = Math.Max(0, period.SessionsReserved - 1);
                    period.UpdatedAt = DateTime.UtcNow;
                }
            }

            await _context.SaveChangesAsync();
            await transaction.CommitAsync();

            return new CheckInResultDto
            {
                Success = true,
                ResultType = "released",
                Message = "Cita cancelada y cupo liberado.",
                AttendanceId = attendanceId,
                ClassesRemaining = attendance.ClientServicePlan?.ClassesRemaining
            };
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync();
            _logger.LogError(ex, "Error al cancelar cita {AttendanceId}", attendanceId);
            throw;
        }
    }

    public async Task<CheckInResultDto> RegisterPaidAttendanceAsync(PaidAttendanceDto dto, int userId)
    {
        _logger.LogInformation("Registrando asistencia pagada para cliente {ClientId} en servicio {ServiceId}", 
            dto.ClientId, dto.ServiceId);

        using var transaction = await _context.Database.BeginTransactionAsync();
        try
        {
            // Obtener información del servicio
            var service = await _context.Services.FindAsync(dto.ServiceId);
            if (service == null)
            {
                throw new InvalidOperationException("Servicio no encontrado");
            }

            // Crear la venta
            var sale = new ServiceSale
            {
                BusinessId = service.BusinessId,
                StoreId = service.StoreId,
                UserId = userId,
                ServiceClientId = dto.ClientId,
                Date = dto.AttendanceDate,
                TotalAmount = dto.Price,
                SaleType = "direct_service",
                Status = ServiceSaleStatus.Completed,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            _context.ServiceSales.Add(sale);
            await _context.SaveChangesAsync();

            // Crear registro de asistencia
            var attendance = new ServiceAttendance
            {
                BusinessId = service.BusinessId,
                StoreId = service.StoreId,
                ServiceId = dto.ServiceId,
                ServiceClientId = dto.ClientId,
                ServiceSaleId = sale.Id,
                AttendanceDate = dto.AttendanceDate,
                AttendanceTime = dto.AttendanceTime,
                AttendanceType = AttendanceType.Paid,
                Status = AttendanceStatus.Attended,
                Notes = dto.Notes,
                RegisteredByUserId = userId,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            _context.ServiceAttendances.Add(attendance);
            await _context.SaveChangesAsync();

            await transaction.CommitAsync();

            _logger.LogInformation("Asistencia pagada registrada. Cliente {ClientId}, Venta {SaleId}, Asistencia {AttendanceId}",
                dto.ClientId, sale.Id, attendance.Id);

            return new CheckInResultDto
            {
                Success = true,
                ResultType = "paid_attendance",
                Message = "Asistencia pagada registrada exitosamente",
                AttendanceId = attendance.Id,
                AmountCharged = dto.Price
            };
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync();
            _logger.LogError(ex, "Error al registrar asistencia pagada para cliente {ClientId}", dto.ClientId);
            throw;
        }
    }

    public async Task<ServiceAttendanceDto> UpdateStatusAsync(int id, UpdateAttendanceStatusDto dto)
    {
        _logger.LogInformation("Actualizando estado de asistencia {AttendanceId} a {Status}", id, dto.Status);

        var attendance = await _context.ServiceAttendances
            .Include(a => a.ClientServicePlan)
                .ThenInclude(cp => cp!.ServicePlan)
            .FirstOrDefaultAsync(a => a.Id == id);

        if (attendance == null)
        {
            throw new InvalidOperationException("Asistencia no encontrada");
        }

        var oldStatus = attendance.Status;
        attendance.Status = dto.Status;
        attendance.Notes = dto.Notes;
        attendance.UpdatedAt = DateTime.UtcNow;

        // Si se cancela una asistencia de plan, ajustar contadores
        if (oldStatus == AttendanceStatus.Attended && 
            dto.Status == AttendanceStatus.Cancelled &&
            attendance.ClientServicePlanId.HasValue &&
            attendance.ClientServicePlan != null)
        {
            var plan = attendance.ClientServicePlan;
            var revenuePerClass = plan.RevenuePerClass;
            
            plan.ClassesUsed -= 1;
            plan.RevenueRecognized -= revenuePerClass;
            plan.UpdatedAt = DateTime.UtcNow;

            _logger.LogInformation("Asistencia cancelada. Revirtiendo clase y ingreso reconocido en plan {PlanId}", plan.Id);

            // Actualizar contador del período de facturación
            if (attendance.PlanBillingPeriodId.HasValue)
            {
                var periodCancel = await _context.PlanBillingPeriods.FindAsync(attendance.PlanBillingPeriodId.Value);
                if (periodCancel != null)
                {
                    periodCancel.SessionsAttended = Math.Max(0, periodCancel.SessionsAttended - 1);
                    periodCancel.UpdatedAt = DateTime.UtcNow;
                }
            }
        }

        // Si se marca como asistida una que estaba programada
        if (oldStatus != AttendanceStatus.Attended && 
            dto.Status == AttendanceStatus.Attended &&
            attendance.ClientServicePlanId.HasValue &&
            attendance.ClientServicePlan != null)
        {
            var plan = attendance.ClientServicePlan;
            var revenuePerClass = plan.RevenuePerClass;
            
            plan.ClassesUsed += 1;
            plan.RevenueRecognized += revenuePerClass;
            plan.UpdatedAt = DateTime.UtcNow;

            _logger.LogInformation("Asistencia confirmada. Incrementando clase y reconociendo ingreso en plan {PlanId}", plan.Id);

            // Actualizar contador del período de facturación
            if (attendance.PlanBillingPeriodId.HasValue)
            {
                var periodConfirm = await _context.PlanBillingPeriods.FindAsync(attendance.PlanBillingPeriodId.Value);
                if (periodConfirm != null)
                {
                    if (oldStatus == AttendanceStatus.Scheduled) periodConfirm.SessionsReserved = Math.Max(0, periodConfirm.SessionsReserved - 1);
                    periodConfirm.SessionsAttended += 1;
                    periodConfirm.UpdatedAt = DateTime.UtcNow;
                }
            }
        }

        await _context.SaveChangesAsync();

        return await GetByIdAsync(id);
    }

    public async Task DeleteAsync(int id)
    {
        _logger.LogInformation("Eliminando asistencia {AttendanceId}", id);

        var attendance = await _context.ServiceAttendances
            .Include(a => a.ClientServicePlan)
                .ThenInclude(cp => cp!.ServicePlan)
            .FirstOrDefaultAsync(a => a.Id == id);

        if (attendance == null)
        {
            throw new InvalidOperationException("Asistencia no encontrada");
        }

        // Si era una asistencia de plan confirmada, revertir el contador
        if (attendance.Status == AttendanceStatus.Attended &&
            attendance.ClientServicePlanId.HasValue &&
            attendance.ClientServicePlan != null)
        {
            var plan = attendance.ClientServicePlan;
            var revenuePerClass = plan.RevenuePerClass;
            
            plan.ClassesUsed -= 1;
            plan.RevenueRecognized -= revenuePerClass;
            plan.UpdatedAt = DateTime.UtcNow;

            _logger.LogInformation("Eliminando asistencia. Revirtiendo clase en plan {PlanId}", plan.Id);

            // Actualizar contador del período de facturación
            if (attendance.PlanBillingPeriodId.HasValue)
            {
                var periodDel = await _context.PlanBillingPeriods.FindAsync(attendance.PlanBillingPeriodId.Value);
                if (periodDel != null)
                {
                    periodDel.SessionsAttended = Math.Max(0, periodDel.SessionsAttended - 1);
                    periodDel.UpdatedAt = DateTime.UtcNow;
                }
            }
        }

        // Si era una cita agendada, liberar cupo reservado
        if (attendance.Status == AttendanceStatus.Scheduled &&
            attendance.ClientServicePlanId.HasValue &&
            attendance.ClientServicePlan != null)
        {
            var plan = attendance.ClientServicePlan;
            plan.ClassesReserved = Math.Max(0, plan.ClassesReserved - 1);
            plan.UpdatedAt = DateTime.UtcNow;

            _logger.LogInformation("Eliminando cita agendada. Liberando cupo reservado en plan {PlanId}", plan.Id);

            // Actualizar contador del período de facturación
            if (attendance.PlanBillingPeriodId.HasValue)
            {
                var periodDelSched = await _context.PlanBillingPeriods.FindAsync(attendance.PlanBillingPeriodId.Value);
                if (periodDelSched != null)
                {
                    periodDelSched.SessionsReserved = Math.Max(0, periodDelSched.SessionsReserved - 1);
                    periodDelSched.UpdatedAt = DateTime.UtcNow;
                }
            }
        }

        _context.ServiceAttendances.Remove(attendance);
        await _context.SaveChangesAsync();

        _logger.LogInformation("Asistencia {AttendanceId} eliminada", id);
    }

    public async Task<ClassOccupancyReportDto> GetClassOccupancyReportAsync(int serviceId, DateTime startDate, DateTime endDate)
    {
        _logger.LogInformation("Generando reporte de ocupación para servicio {ServiceId} desde {Start} hasta {End}",
            serviceId, startDate, endDate);

        var attendances = await _context.ServiceAttendances
            .Include(a => a.ClientServicePlan)
            .ThenInclude(cp => cp!.ServicePlan)
            .Include(a => a.ServiceSale)
            .Where(a => a.ServiceId == serviceId
                && a.AttendanceDate >= startDate
                && a.AttendanceDate <= endDate
                && a.Status == AttendanceStatus.Attended)
            .ToListAsync();

        var service = await _context.Services.FindAsync(serviceId);

        var planAttendances = attendances.Count(a => a.AttendanceType == AttendanceType.Plan);
        var paidAttendances = attendances.Count(a => a.AttendanceType == AttendanceType.Paid);

        // Calcular ingresos
        var revenueFromPlans = attendances
            .Where(a => a.AttendanceType == AttendanceType.Plan && a.ClientServicePlan != null)
            .Sum(a => a.ClientServicePlan!.CostPerClass);

        var revenueFromDirectPay = attendances
            .Where(a => a.AttendanceType == AttendanceType.Paid && a.ServiceSale != null)
            .Sum(a => a.ServiceSale!.TotalAmount ?? 0);

        // Calcular sesiones únicas
        var uniqueDates = attendances.Select(a => a.AttendanceDate.Date).Distinct().Count();

        return new ClassOccupancyReportDto
        {
            ServiceId = serviceId,
            ServiceName = service?.Name ?? "Unknown",
            StartDate = startDate,
            EndDate = endDate,
            TotalSessions = uniqueDates,
            TotalAttendees = attendances.Count,
            AttendeesWithPlan = planAttendances,
            AttendeesWithoutPlan = paidAttendances,
            AverageAttendancePerSession = uniqueDates > 0 ? (decimal)attendances.Count / uniqueDates : 0,
            RevenueFromPlans = revenueFromPlans,
            RevenueFromDirectPay = revenueFromDirectPay,
            TotalRevenue = revenueFromPlans + revenueFromDirectPay
        };
    }

    /// <summary>
    /// Busca el período de facturación activo para un plan y fecha dados, usando rango
    /// de fechas. Funciona para cualquier frecuencia (mensual, trimestral, etc.).
    /// </summary>
    private async Task<int?> FindBillingPeriodIdAsync(int clientServicePlanId, DateTime attendanceDate)
    {
        // 1. Exact date-range match (rolling periods)
        var periodId = await _context.PlanBillingPeriods
            .Where(p => p.ClientServicePlanId == clientServicePlanId
                     && p.PeriodStartDate.Date <= attendanceDate.Date
                     && p.PeriodEndDate.Date   >= attendanceDate.Date)
            .Select(p => (int?)p.Id)
            .FirstOrDefaultAsync();

        if (periodId.HasValue) return periodId;

        // 2. Fallback: match by calendar month/year (covers cases where plan start
        //    date is AFTER the attendance date but billing_month matches)
        return await _context.PlanBillingPeriods
            .Where(p => p.ClientServicePlanId == clientServicePlanId
                     && p.BillingYear  == attendanceDate.Year
                     && p.BillingMonth == attendanceDate.Month)
            .Select(p => (int?)p.Id)
            .FirstOrDefaultAsync();
    }

    private ServiceAttendanceDto MapToDto(ServiceAttendance attendance)
    {
        return new ServiceAttendanceDto
        {
            Id = attendance.Id,
            BusinessId = attendance.BusinessId,
            StoreId = attendance.StoreId,
            ServiceId = attendance.ServiceId,
            ServiceName = attendance.Service?.Name ?? string.Empty,
            AttendanceDate = attendance.AttendanceDate,
            AttendanceTime = attendance.AttendanceTime,
            ServiceClientId = attendance.ServiceClientId,
            ClientName = attendance.DisplayName,
            ClientServicePlanId = attendance.ClientServicePlanId,
            PlanName = attendance.ClientServicePlan?.ServicePlan?.Name,
            PlanBillingPeriodId = attendance.PlanBillingPeriodId,
            ServiceSaleId = attendance.ServiceSaleId,
            AttendanceType = attendance.AttendanceType,
            AttendanceTypeDisplay = attendance.AttendanceType.ToString(),
            Status = attendance.Status,
            StatusDisplay = attendance.Status.ToString(),
            Notes = attendance.Notes,
            RegisteredByUserId = attendance.RegisteredByUserId,
            RegisteredByUserName = attendance.RegisteredByUser?.Name,
            CreatedAt = attendance.CreatedAt,
            UpdatedAt = attendance.UpdatedAt,
            AmountPaid = attendance.ServiceSale?.TotalAmount,
            IsPlanUsage = attendance.IsPlanUsage
        };
    }
}
