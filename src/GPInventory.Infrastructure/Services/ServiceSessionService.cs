using GPInventory.Application.DTOs.Services;
using GPInventory.Application.Interfaces;
using GPInventory.Domain.Entities;
using GPInventory.Domain.Enums;
using GPInventory.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace GPInventory.Infrastructure.Services;

public class ServiceSessionService : IServiceSessionService
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<ServiceSessionService> _logger;
    private readonly IServiceSessionExpenseService _sessionExpenseService;

    public ServiceSessionService(ApplicationDbContext context, ILogger<ServiceSessionService> logger, IServiceSessionExpenseService sessionExpenseService)
    {
        _context = context;
        _logger = logger;
        _sessionExpenseService = sessionExpenseService;
    }

    // ── Queries ───────────────────────────────────────────────────────────────

    public async Task<ServiceSessionDto> GetByIdAsync(int id)
    {
        var session = await _context.ServiceSessions
            .Include(s => s.Service)
            .Include(s => s.ServicePlan)
            .Include(s => s.Attendances!)
                .ThenInclude(a => a.ServiceClient)
                    .ThenInclude(sc => sc!.ParentClient)
            .Include(s => s.Attendances!)
                .ThenInclude(a => a.ClientServicePlan)
            .FirstOrDefaultAsync(s => s.Id == id)
            ?? throw new InvalidOperationException($"Sesión {id} no encontrada");

        return MapToDto(session);
    }

    public async Task<IEnumerable<ServiceSessionSummaryDto>> GetByBusinessAsync(int businessId, DateTime? from = null, DateTime? to = null)
    {
        var query = _context.ServiceSessions
            .Include(s => s.Service)
            .Include(s => s.ServicePlan)
            .Include(s => s.Attendances)
            .AsSplitQuery()
            .Where(s => s.BusinessId == businessId);

        if (from.HasValue) query = query.Where(s => s.SessionDate >= from.Value.Date);
        if (to.HasValue)   query = query.Where(s => s.SessionDate <= to.Value.Date);

        var sessions = await query.OrderBy(s => s.SessionDate).ThenBy(s => s.StartTime).ToListAsync();
        return sessions.Select(MapToSummaryDto);
    }

    public async Task<IEnumerable<ServiceSessionSummaryDto>> GetByServiceAsync(int serviceId, DateTime? from = null, DateTime? to = null)
    {
        var query = _context.ServiceSessions
            .Include(s => s.Service)
            .Include(s => s.ServicePlan)
            .Include(s => s.Attendances)
            .AsSplitQuery()
            .Where(s => s.ServiceId == serviceId);

        if (from.HasValue) query = query.Where(s => s.SessionDate >= from.Value.Date);
        if (to.HasValue)   query = query.Where(s => s.SessionDate <= to.Value.Date);

        var sessions = await query.OrderBy(s => s.SessionDate).ThenBy(s => s.StartTime).ToListAsync();
        return sessions.Select(MapToSummaryDto);
    }

    public async Task<IEnumerable<ServiceSessionSummaryDto>> GetUpcomingByPlanAsync(int servicePlanId, int days = 30)
    {
        var until = DateTime.UtcNow.Date.AddDays(days);
        var sessions = await _context.ServiceSessions
            .Include(s => s.Service)
            .Include(s => s.ServicePlan)
            .Include(s => s.Attendances)
            .AsSplitQuery()
            .Where(s => s.ServicePlanId == servicePlanId
                     && s.SessionDate >= DateTime.UtcNow.Date
                     && s.SessionDate <= until
                     && s.Status != ServiceSessionStatus.Cancelled)
            .OrderBy(s => s.SessionDate)
            .ThenBy(s => s.StartTime)
            .ToListAsync();

        return sessions.Select(MapToSummaryDto);
    }

    // ── Create ────────────────────────────────────────────────────────────────

    public async Task<ServiceSessionDto> CreateAsync(CreateServiceSessionDto dto, int userId)
    {
        var session = new ServiceSession
        {
            BusinessId       = dto.BusinessId,
            StoreId          = dto.StoreId,
            ServiceId        = dto.ServiceId,
            ServicePlanId    = dto.ServicePlanId,
            SessionDate      = dto.SessionDate.Date,
            StartTime        = ParseTime(dto.StartTime),
            EndTime          = ParseTime(dto.EndTime),
            Capacity         = dto.Capacity,
            InstructorName   = dto.InstructorName,
            InstructorUserId = dto.InstructorUserId,
            Location         = dto.Location,
            Notes            = dto.Notes,
            AllowWalkIns     = dto.AllowWalkIns,
            Status           = ServiceSessionStatus.Scheduled,
            CreatedByUserId  = userId,
            CreatedAt        = DateTime.UtcNow,
            UpdatedAt        = DateTime.UtcNow
        };

        _context.ServiceSessions.Add(session);
        await _context.SaveChangesAsync();

        // Auto-generar gastos pendientes desde los ítems de costo del servicio
        await _sessionExpenseService.GenerateFromSessionAsync(session.Id);

        if ((dto.PreRegisterClientPlanIds?.Count > 0) || (dto.PreRegisterWalkIns?.Count > 0))
            await PreRegisterAttendeesAsync(session.Id, dto.PreRegisterClientPlanIds, dto.PreRegisterWalkIns, userId);

        _logger.LogInformation("Sesión {SessionId} creada para servicio {ServiceId} el {Date}", session.Id, dto.ServiceId, dto.SessionDate);
        return await GetByIdAsync(session.Id);
    }

    public async Task<IEnumerable<ServiceSessionSummaryDto>> CreateBulkAsync(CreateBulkServiceSessionsDto dto, int userId)
    {
        if (dto == null) throw new ArgumentNullException(nameof(dto));
        if (dto.DaysOfWeek == null || dto.DaysOfWeek.Count == 0)
            throw new InvalidOperationException("Debe especificar al menos un día de la semana");

        if (dto.RangeEnd < dto.RangeStart)
            throw new InvalidOperationException("La fecha fin debe ser posterior a la fecha inicio");

        var startTime = ParseTime(dto.StartTime);
        var endTime   = ParseTime(dto.EndTime);

        var sessions = new List<ServiceSession>();
        var current  = dto.RangeStart.Date;
        var until    = dto.RangeEnd.Date;

        while (current <= until)
        {
            if (dto.DaysOfWeek.Contains((int)current.DayOfWeek))
            {
                sessions.Add(new ServiceSession
                {
                    BusinessId       = dto.BusinessId,
                    StoreId          = dto.StoreId,
                    ServiceId        = dto.ServiceId,
                    ServicePlanId    = dto.ServicePlanId,
                    SessionDate      = current,
                    StartTime        = startTime,
                    EndTime          = endTime,
                    Capacity         = dto.Capacity,
                    InstructorName   = dto.InstructorName,
                    InstructorUserId = dto.InstructorUserId,
                    Location         = dto.Location,
                    Notes            = dto.Notes,
                    AllowWalkIns     = dto.AllowWalkIns,
                    Status           = ServiceSessionStatus.Scheduled,
                    CreatedByUserId  = userId,
                    CreatedAt        = DateTime.UtcNow,
                    UpdatedAt        = DateTime.UtcNow
                });
            }
            current = current.AddDays(1);
        }

        if (sessions.Count == 0)
            throw new InvalidOperationException("No se generaron sesiones para el patrón y rango indicados");

        _context.ServiceSessions.AddRange(sessions);
        await _context.SaveChangesAsync();

        // Auto-generar gastos pendientes por sesión creada en bulk
        foreach (var session in sessions)
            await _sessionExpenseService.GenerateFromSessionAsync(session.Id);

        if ((dto.PreRegisterClientPlanIds?.Count > 0) || (dto.PreRegisterWalkIns?.Count > 0))
        {
            foreach (var session in sessions)
                await PreRegisterAttendeesAsync(session.Id, dto.PreRegisterClientPlanIds, dto.PreRegisterWalkIns, userId);
        }

        _logger.LogInformation("{Count} sesiones creadas en bulk para servicio {ServiceId}", sessions.Count, dto.ServiceId);
        return sessions.Select(MapToSummaryDto);
    }

    // ── State changes ─────────────────────────────────────────────────────────

    public async Task<ServiceSessionDto> UpdateAsync(int id, UpdateServiceSessionDto dto)
    {
        var session = await _context.ServiceSessions.FindAsync(id)
            ?? throw new InvalidOperationException($"Sesión {id} no encontrada");

        if (session.Status == ServiceSessionStatus.Completed || session.Status == ServiceSessionStatus.Cancelled)
            throw new InvalidOperationException("No se puede modificar una sesión completada o cancelada");

        if (dto.SessionDate.HasValue)      session.SessionDate  = dto.SessionDate.Value.Date;
        if (dto.StartTime != null)         session.StartTime    = ParseTime(dto.StartTime);
        if (dto.EndTime != null)           session.EndTime      = ParseTime(dto.EndTime);
        if (dto.Capacity.HasValue)         session.Capacity     = dto.Capacity;
        if (dto.InstructorName != null)    session.InstructorName = dto.InstructorName;
        if (dto.InstructorUserId.HasValue) session.InstructorUserId = dto.InstructorUserId;
        if (dto.Location != null)          session.Location     = dto.Location;
        if (dto.Notes != null)             session.Notes        = dto.Notes;
        if (dto.AllowWalkIns.HasValue)     session.AllowWalkIns  = dto.AllowWalkIns.Value;

        session.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();

        return await GetByIdAsync(id);
    }

    public async Task<ServiceSessionDto> StartSessionAsync(int id)
    {
        var session = await _context.ServiceSessions.FindAsync(id)
            ?? throw new InvalidOperationException($"Sesión {id} no encontrada");

        if (session.Status != ServiceSessionStatus.Scheduled)
            throw new InvalidOperationException("Solo se puede iniciar una sesión en estado Scheduled");

        session.Status    = ServiceSessionStatus.InProgress;
        session.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();

        return await GetByIdAsync(id);
    }

    public async Task<ServiceSessionDto> CompleteSessionAsync(int id)
    {
        var session = await _context.ServiceSessions
            .Include(s => s.Attendances!)
                .ThenInclude(a => a.ClientServicePlan)
            .FirstOrDefaultAsync(s => s.Id == id)
            ?? throw new InvalidOperationException($"Sesión {id} no encontrada");

        if (session.Status != ServiceSessionStatus.InProgress && session.Status != ServiceSessionStatus.Scheduled)
            throw new InvalidOperationException("La sesión debe estar en curso o planificada para completarse");

        using var tx = await _context.Database.BeginTransactionAsync();
        try
        {
            session.Status    = ServiceSessionStatus.Completed;
            session.UpdatedAt = DateTime.UtcNow;

            // Clase ya fue descontada en el momento de registro.
            // Aquí solo reconocemos revenue y marcamos estado final.
            var planAttendances = session.Attendances?
                .Where(a => a.ClientServicePlanId.HasValue
                         && (a.Status == AttendanceStatus.Attended || a.Status == AttendanceStatus.Confirmed))
                .ToList() ?? new();

            foreach (var att in planAttendances)
            {
                var plan = att.ClientServicePlan;
                if (plan != null)
                {
                    // Solo reconocer revenue (ClassesUsed ya descontado al registrar)
                    plan.RevenueRecognized  += plan.RevenuePerClass;
                    plan.UpdatedAt = DateTime.UtcNow;

                    att.Status    = AttendanceStatus.Attended;
                    att.UpdatedAt = DateTime.UtcNow;
                }
            }

            // Marcar como Absent los que siguen en Scheduled/Confirmed y no asistieron
            // (la clase ya fue consumida al registrarlos — no se devuelve)
            var absent = session.Attendances?
                .Where(a => a.Status == AttendanceStatus.Scheduled || a.Status == AttendanceStatus.Confirmed)
                .ToList() ?? new();

            foreach (var att in absent)
            {
                att.Status    = AttendanceStatus.Absent;
                att.UpdatedAt = DateTime.UtcNow;
            }

            await _context.SaveChangesAsync();
            await tx.CommitAsync();

            _logger.LogInformation("Sesión {SessionId} completada. {PlanCount} clases de plan reconocidas", id, planAttendances.Count);
            return await GetByIdAsync(id);
        }
        catch
        {
            await tx.RollbackAsync();
            throw;
        }
    }

    public async Task<ServiceSessionDto> CancelSessionAsync(int id, string? reason = null)
    {
        var session = await _context.ServiceSessions
            .Include(s => s.Attendances!)
                .ThenInclude(a => a.ClientServicePlan)
            .FirstOrDefaultAsync(s => s.Id == id)
            ?? throw new InvalidOperationException($"Sesión {id} no encontrada");

        if (session.Status == ServiceSessionStatus.Completed)
            throw new InvalidOperationException("No se puede cancelar una sesión ya completada");

        using var tx = await _context.Database.BeginTransactionAsync();
        try
        {
            session.Status    = ServiceSessionStatus.Cancelled;
            session.Notes     = reason != null ? $"[Cancelada] {reason}" : session.Notes;
            session.UpdatedAt = DateTime.UtcNow;

            // Cancelar asistencias y DEVOLVER la clase a los planes
            // (la sesión se cancela por causas externas, no por decisión del cliente)
            var attendances = session.Attendances?.ToList() ?? new();
            var refundCount  = 0;
            foreach (var att in attendances)
            {
                // Devolver clase solo si aún no está marcada como Absent
                // (Absent = el cliente ya consumió su turno intencionalmente)
                if (att.ClientServicePlanId.HasValue && att.Status != AttendanceStatus.Absent)
                {
                    var plan = att.ClientServicePlan;
                    if (plan != null)
                    {
                        plan.ClassesUsed  = Math.Max(plan.ClassesUsed - 1, 0);
                        plan.UpdatedAt    = DateTime.UtcNow;
                        refundCount++;
                    }
                }

                att.Status    = AttendanceStatus.Cancelled;
                att.UpdatedAt = DateTime.UtcNow;
            }

            await _context.SaveChangesAsync();
            await tx.CommitAsync();

            _logger.LogInformation("Sesión {SessionId} cancelada. {Count} asistencias canceladas, {Refund} clases devueltas", id, attendances.Count, refundCount);
            return await GetByIdAsync(id);
        }
        catch
        {
            await tx.RollbackAsync();
            throw;
        }
    }

    // ── Attendance ────────────────────────────────────────────────────────────

    public async Task<SessionAttendeeDto> RegisterAttendanceAsync(RegisterSessionAttendanceDto dto, int userId)
    {
        var session = await _context.ServiceSessions.FindAsync(dto.SessionId)
            ?? throw new InvalidOperationException($"Sesión {dto.SessionId} no encontrada");

        if (session.Status == ServiceSessionStatus.Cancelled)
            throw new InvalidOperationException("No se puede registrar asistencia en una sesión cancelada");

        bool isWalkIn = !dto.ClientServicePlanId.HasValue;

        if (isWalkIn)
        {
            if (!session.AllowWalkIns)
                throw new InvalidOperationException("Esta sesión no permite asistentes sin plan (walk-in desactivado).");
            if (string.IsNullOrWhiteSpace(dto.WalkInName))
                throw new InvalidOperationException("Debe indicar un nombre para el asistente walk-in");
        }
        else
        {
            // Evitar duplicado
            var existing = await _context.ServiceAttendances
                .FirstOrDefaultAsync(a => a.ServiceSessionId == dto.SessionId
                                       && a.ClientServicePlanId == dto.ClientServicePlanId);
            if (existing != null)
                throw new InvalidOperationException("Este cliente ya está registrado en la sesión");
        }

        ClientServicePlan? plan = null;
        if (dto.ClientServicePlanId.HasValue)
        {
            plan = await _context.ClientServicePlans
                .Include(p => p.ServiceClient)
                .FirstOrDefaultAsync(p => p.Id == dto.ClientServicePlanId.Value)
                ?? throw new InvalidOperationException("Plan de cliente no encontrado");

            if (plan.Status != ClientServicePlanStatus.Active)
                throw new InvalidOperationException("El plan del cliente no está activo");

            if (plan.ClassesUsed >= plan.TotalClasses)
                throw new InvalidOperationException("El plan no tiene clases disponibles");
        }

        // Descontar clase del plan en el momento del registro
        if (plan != null)
        {
            plan.ClassesUsed  = Math.Min(plan.ClassesUsed + 1, plan.TotalClasses);
            plan.UpdatedAt    = DateTime.UtcNow;
        }

        var attendance = new ServiceAttendance
        {
            BusinessId          = session.BusinessId,
            StoreId             = session.StoreId,
            ServiceId           = session.ServiceId,
            ServiceSessionId    = session.Id,
            ServiceClientId     = plan?.ServiceClientId,
            ClientName          = dto.WalkInName ?? plan?.ServiceClient?.Name,
            ClientServicePlanId = dto.ClientServicePlanId,
            AttendanceDate      = session.SessionDate,
            AttendanceTime      = session.StartTime,
            AttendanceType      = isWalkIn ? AttendanceType.Free : dto.AttendanceType,
            Status              = AttendanceStatus.Confirmed,
            Notes               = dto.Notes,
            RegisteredByUserId  = userId,
            CreatedAt           = DateTime.UtcNow,
            UpdatedAt           = DateTime.UtcNow
        };

        _context.ServiceAttendances.Add(attendance);
        await _context.SaveChangesAsync();

        return MapToAttendeeDto(attendance, plan);
    }

    public async Task<SessionAttendeeDto> MarkAttendedAsync(int attendanceId)
    {
        var att = await _context.ServiceAttendances
            .Include(a => a.ClientServicePlan)
            .Include(a => a.ServiceClient)
            .FirstOrDefaultAsync(a => a.Id == attendanceId)
            ?? throw new InvalidOperationException("Registro de asistencia no encontrado");

        att.Status    = AttendanceStatus.Attended;
        att.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();

        return MapToAttendeeDto(att, att.ClientServicePlan);
    }

    public async Task<SessionAttendeeDto> MarkAbsentAsync(int attendanceId)
    {
        var att = await _context.ServiceAttendances
            .Include(a => a.ClientServicePlan)
            .Include(a => a.ServiceClient)
            .FirstOrDefaultAsync(a => a.Id == attendanceId)
            ?? throw new InvalidOperationException("Registro de asistencia no encontrado");

        att.Status    = AttendanceStatus.Absent;
        att.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();

        return MapToAttendeeDto(att, att.ClientServicePlan);
    }

    // ── Private helpers ───────────────────────────────────────────────────────

    /// <summary>
    /// Pre-registra asistentes explícitamente seleccionados (plan clients + walk-ins).
    /// </summary>
    private async Task PreRegisterAttendeesAsync(
        int sessionId,
        List<int>? clientPlanIds,
        List<string>? walkInNames,
        int userId)
    {
        var session = await _context.ServiceSessions.FindAsync(sessionId);
        if (session == null) return;

        var toAdd = new List<ServiceAttendance>();

        // ── Plan clients ─────────────────────────────────────────────────────
        if (clientPlanIds != null && clientPlanIds.Count > 0)
        {
            var plans = await _context.ClientServicePlans
                .Include(p => p.ServiceClient)
                .Where(p => clientPlanIds.Contains(p.Id)
                         && p.Status == ClientServicePlanStatus.Active)
                .ToListAsync();

            var existing = await _context.ServiceAttendances
                .Where(a => a.ServiceSessionId == sessionId)
                .Select(a => a.ClientServicePlanId)
                .ToListAsync();

            foreach (var plan in plans.Where(p => !existing.Contains(p.Id)))
            {
                // Verificar cupo disponible y descontar clase al pre-registrar
                if (plan.ClassesUsed >= plan.TotalClasses) continue; // sin cupos, omitir

                plan.ClassesUsed = Math.Min(plan.ClassesUsed + 1, plan.TotalClasses);
                plan.UpdatedAt   = DateTime.UtcNow;

                toAdd.Add(new ServiceAttendance
                {
                    BusinessId          = session.BusinessId,
                    StoreId             = session.StoreId,
                    ServiceId           = session.ServiceId,
                    ServiceSessionId    = sessionId,
                    ServiceClientId     = plan.ServiceClientId,
                    ClientName          = plan.ServiceClient?.Name,
                    ClientServicePlanId = plan.Id,
                    AttendanceDate      = session.SessionDate,
                    AttendanceTime      = session.StartTime,
                    AttendanceType      = AttendanceType.Plan,
                    Status              = AttendanceStatus.Confirmed,
                    RegisteredByUserId  = userId,
                    CreatedAt           = DateTime.UtcNow,
                    UpdatedAt           = DateTime.UtcNow
                });
            }
        }

        // ── Walk-ins / particulares ───────────────────────────────────────────
        if (walkInNames != null)
        {
            foreach (var name in walkInNames.Where(n => !string.IsNullOrWhiteSpace(n)))
            {
                toAdd.Add(new ServiceAttendance
                {
                    BusinessId         = session.BusinessId,
                    StoreId            = session.StoreId,
                    ServiceId          = session.ServiceId,
                    ServiceSessionId   = sessionId,
                    ClientName         = name.Trim(),
                    AttendanceDate     = session.SessionDate,
                    AttendanceTime     = session.StartTime,
                    AttendanceType     = AttendanceType.Free,
                    Status             = AttendanceStatus.Confirmed,
                    RegisteredByUserId = userId,
                    CreatedAt          = DateTime.UtcNow,
                    UpdatedAt          = DateTime.UtcNow
                });
            }
        }

        if (toAdd.Any())
        {
            _context.ServiceAttendances.AddRange(toAdd);
            await _context.SaveChangesAsync();
            _logger.LogInformation("{Count} asistentes pre-registrados en sesión {SessionId}", toAdd.Count, sessionId);
        }
    }

    private ServiceSessionDto MapToDto(ServiceSession s) => new()
    {
        Id             = s.Id,
        BusinessId     = s.BusinessId,
        StoreId        = s.StoreId,
        ServiceId      = s.ServiceId,
        ServiceName    = s.Service?.Name ?? string.Empty,
        ServicePlanId  = s.ServicePlanId,
        PlanName       = s.ServicePlan?.Name,
        SessionDate    = s.SessionDate,
        StartTime      = s.StartTime,
        EndTime        = s.EndTime,
        Capacity       = s.Capacity,
        AttendeeCount  = s.ConfirmedCount,
        AvailableSpots = s.AvailableSpots,
        InstructorName = s.InstructorName,
        Location       = s.Location,
        Status         = s.Status,
        StatusDisplay  = GetStatusDisplay(s.Status),
        IsFull         = s.IsFull,
        AllowWalkIns   = s.AllowWalkIns,
        Notes          = s.Notes,
        CreatedAt      = s.CreatedAt,
        Attendees      = s.Attendances?.Select(a => MapToAttendeeDto(a, a.ClientServicePlan)).ToList() ?? new()
    };

    private ServiceSessionSummaryDto MapToSummaryDto(ServiceSession s) => new()
    {
        Id             = s.Id,
        BusinessId     = s.BusinessId,
        StoreId        = s.StoreId,
        ServiceId      = s.ServiceId,
        ServiceName    = s.Service?.Name ?? string.Empty,
        ServicePlanId  = s.ServicePlanId,
        PlanName       = s.ServicePlan?.Name,
        SessionDate    = s.SessionDate,
        StartTime      = s.StartTime,
        EndTime        = s.EndTime,
        Capacity       = s.Capacity,
        AttendeeCount  = s.ConfirmedCount,
        AvailableSpots = s.AvailableSpots,
        InstructorName = s.InstructorName,
        Location       = s.Location,
        Status         = s.Status,
        StatusDisplay  = GetStatusDisplay(s.Status),
        IsFull         = s.IsFull,
        AllowWalkIns   = s.AllowWalkIns
    };

    private static SessionAttendeeDto MapToAttendeeDto(ServiceAttendance a, ClientServicePlan? plan) => new()
    {
        AttendanceId        = a.Id,
        ServiceClientId     = a.ServiceClientId,
        DisplayName         = a.ClientName ?? a.ServiceClient?.Name ?? "Walk-in",
        ClientEmail         = a.ServiceClient?.Email,
        LinkedUserId        = a.ServiceClient?.LinkedUserId,
        ClientServicePlanId = a.ClientServicePlanId,
        AttendanceType      = a.AttendanceType,
        Status              = a.Status,
        StatusDisplay       = GetAttendanceStatusDisplay(a.Status),
        Notes               = a.Notes,
        ParentClientName    = a.ServiceClient?.ParentClient?.Name,
    };

    private static string GetStatusDisplay(ServiceSessionStatus s) => s switch
    {
        ServiceSessionStatus.Scheduled  => "Programada",
        ServiceSessionStatus.InProgress => "En curso",
        ServiceSessionStatus.Completed  => "Completada",
        ServiceSessionStatus.Cancelled  => "Cancelada",
        _                               => s.ToString()
    };

    private static string GetAttendanceStatusDisplay(AttendanceStatus s) => s switch
    {
        AttendanceStatus.Scheduled  => "Programado",
        AttendanceStatus.Confirmed  => "Confirmado",
        AttendanceStatus.Attended   => "Asistió",
        AttendanceStatus.Absent     => "No asistió",
        AttendanceStatus.Cancelled  => "Cancelado",
        _                           => s.ToString()
    };

    /// <summary>Parsea "HH:mm" o "HH:mm:ss" a TimeSpan. Retorna null si está vacío o inválido.</summary>
    private static TimeSpan? ParseTime(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        // Normalizar a HH:mm:ss
        if (value.Length == 5) value += ":00";
        return TimeSpan.TryParse(value, out var ts) ? ts : null;
    }

}

