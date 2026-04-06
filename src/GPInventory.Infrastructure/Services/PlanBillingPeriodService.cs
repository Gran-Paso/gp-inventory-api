using System.Globalization;
using GPInventory.Application.DTOs.Services;
using GPInventory.Application.Interfaces;
using GPInventory.Domain.Entities;
using GPInventory.Domain.Enums;
using GPInventory.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace GPInventory.Infrastructure.Services;

public class PlanBillingPeriodService : IPlanBillingPeriodService
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<PlanBillingPeriodService> _logger;

    public PlanBillingPeriodService(ApplicationDbContext context, ILogger<PlanBillingPeriodService> logger)
    {
        _context = context;
        _logger = logger;
    }

    // ── Queries ───────────────────────────────────────────────────────────────

    public async Task<PlanBillingPeriodDto> GetByIdAsync(int id)
    {
        var period = await _context.PlanBillingPeriods
            .Include(p => p.ClientServicePlan)
                .ThenInclude(csp => csp!.ServicePlan)
            .Include(p => p.ServiceClient)
            .FirstOrDefaultAsync(p => p.Id == id)
            ?? throw new InvalidOperationException($"Período de facturación {id} no encontrado.");

        return MapToDto(period);
    }

    public async Task<IEnumerable<PlanBillingPeriodDto>> GetByPlanAsync(int clientServicePlanId)
    {
        var periods = await _context.PlanBillingPeriods
            .Include(p => p.ClientServicePlan)
                .ThenInclude(csp => csp!.ServicePlan)
            .Include(p => p.ServiceClient)
            .Where(p => p.ClientServicePlanId == clientServicePlanId)
            .OrderByDescending(p => p.BillingYear)
            .ThenByDescending(p => p.BillingMonth)
            .ToListAsync();

        return periods.Select(MapToDto);
    }

    public async Task<IEnumerable<PlanBillingPeriodDto>> GetByClientAsync(int serviceClientId)
    {
        var periods = await _context.PlanBillingPeriods
            .Include(p => p.ClientServicePlan)
                .ThenInclude(csp => csp!.ServicePlan)
            .Include(p => p.ServiceClient)
            .Where(p => p.ServiceClientId == serviceClientId)
            .OrderByDescending(p => p.BillingYear)
            .ThenByDescending(p => p.BillingMonth)
            .ToListAsync();

        return periods.Select(MapToDto);
    }

    public async Task<IEnumerable<PendingBillingPeriodDto>> GetPendingByBusinessAsync(int businessId)
    {
        var today = DateTime.UtcNow.Date;

        var periods = await _context.PlanBillingPeriods
            .Include(p => p.ClientServicePlan)
                .ThenInclude(csp => csp!.ServicePlan)
            .Include(p => p.ServiceClient)
            .Where(p => p.BusinessId == businessId
                     && (p.Status == "pending" || p.Status == "overdue"))
            .OrderBy(p => p.DueDate)
            .ToListAsync();

        return periods.Select(p => new PendingBillingPeriodDto
        {
            PeriodId             = p.Id,
            ServiceClientId      = p.ServiceClientId,
            ClientName           = p.ServiceClient?.Name ?? string.Empty,
            ClientServicePlanId  = p.ClientServicePlanId,
            PlanName             = p.ClientServicePlan?.ServicePlan?.Name ?? string.Empty,
            BillingYear          = p.BillingYear,
            BillingMonth         = p.BillingMonth,
            PeriodLabel          = BuildPeriodLabel(p.BillingYear, p.BillingMonth),
            AmountDue            = p.AmountDue,
            DueDate              = p.DueDate,
            Status               = p.Status,
            IsOverdue            = p.Status == "overdue" || (p.Status == "pending" && p.DueDate.Date < today),
            SessionsAttended     = p.SessionsAttended,
            SessionsReserved     = p.SessionsReserved,
            SessionsAllowed      = p.SessionsAllowed,
        });
    }

    // ── Mutations ─────────────────────────────────────────────────────────────

    public async Task<PlanBillingPeriodDto> CreatePeriodAsync(CreateBillingPeriodDto dto, int userId)
    {
        _logger.LogInformation(
            "Creando período de facturación {Year}/{Month} para plan {PlanId}",
            dto.BillingYear, dto.BillingMonth, dto.ClientServicePlanId);

        // Si el período ya existe, devolver el existente sin duplicar
        var existing = await _context.PlanBillingPeriods
            .Include(p => p.ClientServicePlan).ThenInclude(csp => csp!.ServicePlan)
            .Include(p => p.ServiceClient)
            .FirstOrDefaultAsync(p => p.ClientServicePlanId == dto.ClientServicePlanId
                                   && p.BillingYear          == dto.BillingYear
                                   && p.BillingMonth         == dto.BillingMonth);

        if (existing != null)
        {
            _logger.LogWarning(
                "Período {Year}/{Month} ya existe para plan {PlanId} (id={PeriodId})",
                dto.BillingYear, dto.BillingMonth, dto.ClientServicePlanId, existing.Id);
            return MapToDto(existing);
        }

        var clientPlan = await _context.ClientServicePlans
            .Include(csp => csp.ServicePlan)
            .FirstOrDefaultAsync(csp => csp.Id == dto.ClientServicePlanId)
            ?? throw new InvalidOperationException($"Plan de cliente {dto.ClientServicePlanId} no encontrado.");

        var periodStart = new DateTime(dto.BillingYear, dto.BillingMonth, 1);

        // Calcular el fin del período y sesiones según la frecuencia de facturación del cliente
        var monthsInPeriod = clientPlan.BillingFrequency switch
        {
            BillingFrequency.Quarterly  => 3,
            BillingFrequency.Semiannual => 6,
            BillingFrequency.Annual     => 12,
            _                           => 1
        };

        var periodEnd = periodStart.AddMonths(monthsInPeriod).AddDays(-1);
        var dueDate   = dto.DueDate?.Date ?? periodEnd;

        // Monto según frecuencia del plan del cliente
        var sp = clientPlan.ServicePlan!;
        var amountDue = dto.AmountDue ?? clientPlan.BillingFrequency switch
        {
            BillingFrequency.Quarterly  => sp.PriceQuarterly   ?? sp.Price * 3,
            BillingFrequency.Semiannual => sp.PriceSemiannual  ?? sp.Price * 6,
            BillingFrequency.Annual     => sp.PriceAnnual      ?? sp.Price * 12,
            _                           => sp.Price
        };

        var sessionsAllowed = sp.ClassCount * monthsInPeriod;

        // Contar asistencias ya registradas para este período (puede que ya existan)
        var attended = await _context.ServiceAttendances
            .CountAsync(a => a.ClientServicePlanId == dto.ClientServicePlanId
                          && a.Status == AttendanceStatus.Attended
                          && a.AttendanceDate.Date >= periodStart.Date
                          && a.AttendanceDate.Date <= periodEnd.Date);

        var reserved = await _context.ServiceAttendances
            .CountAsync(a => a.ClientServicePlanId == dto.ClientServicePlanId
                          && a.Status == AttendanceStatus.Scheduled
                          && a.AttendanceDate.Date >= periodStart.Date
                          && a.AttendanceDate.Date <= periodEnd.Date);

        var period = new PlanBillingPeriod
        {
            BusinessId           = clientPlan.BusinessId,
            StoreId              = clientPlan.StoreId,
            ClientServicePlanId  = dto.ClientServicePlanId,
            ServiceClientId      = clientPlan.ServiceClientId,
            BillingYear          = dto.BillingYear,
            BillingMonth         = dto.BillingMonth,
            PeriodStartDate      = periodStart,
            PeriodEndDate        = periodEnd,
            SessionsAllowed      = sessionsAllowed,
            SessionsAttended     = attended,
            SessionsReserved     = reserved,
            Status               = "pending",
            AmountDue            = amountDue,
            DueDate              = dueDate,
            Notes                = dto.Notes,
            CreatedAt            = DateTime.UtcNow,
            UpdatedAt            = DateTime.UtcNow,
        };

        _context.PlanBillingPeriods.Add(period);
        await _context.SaveChangesAsync();

        // Backfill: vincular asistencias ya existentes en este período con la FK directa
        await _context.ServiceAttendances
            .Where(a => a.ClientServicePlanId == dto.ClientServicePlanId
                     && a.PlanBillingPeriodId == null
                     && a.AttendanceDate >= periodStart
                     && a.AttendanceDate <= periodEnd)
            .ExecuteUpdateAsync(s => s.SetProperty(a => a.PlanBillingPeriodId, period.Id));

        _logger.LogInformation(
            "Período {PeriodId} creado: {Year}/{Month} — plan {PlanId}",
            period.Id, dto.BillingYear, dto.BillingMonth, dto.ClientServicePlanId);

        return await GetByIdAsync(period.Id);
    }

    public async Task<PlanBillingPeriodDto> PayPeriodAsync(int periodId, PayBillingPeriodDto dto, int userId)
    {
        _logger.LogInformation("Registrando pago del período {PeriodId}", periodId);

        await using var tx = await _context.Database.BeginTransactionAsync();
        try
        {
            var period = await _context.PlanBillingPeriods
                .Include(p => p.ClientServicePlan)
                .FirstOrDefaultAsync(p => p.Id == periodId)
                ?? throw new InvalidOperationException($"Período {periodId} no encontrado.");

            if (period.Status == "paid")
                throw new InvalidOperationException($"El período {periodId} ya está pagado.");

            var clientPlan = period.ClientServicePlan!;
            var now = DateTime.UtcNow;
            var amountPaid = dto.AmountPaid ?? period.AmountDue;

            // Crear plan_transaction para el pago mensual
            var planTx = new PlanTransaction
            {
                BusinessId          = clientPlan.BusinessId,
                StoreId             = clientPlan.StoreId,
                ClientServicePlanId = clientPlan.Id,
                ServiceClientId     = clientPlan.ServiceClientId,
                ServicePlanId       = clientPlan.ServicePlanId,
                UserId              = userId,
                Amount              = amountPaid,
                PaymentMethodId     = dto.PaymentMethodId,
                DocumentType        = dto.DocumentType,
                TransactionDate     = dto.PaidAt ?? now,
                Notes               = dto.Notes ?? $"Pago período {BuildPeriodLabel(period.BillingYear, period.BillingMonth)}",
                CreatedAt           = now,
                UpdatedAt           = now,
            };

            _context.PlanTransactions.Add(planTx);
            await _context.SaveChangesAsync();

            // Vincular y marcar como pagado
            period.PlanTransactionId = planTx.Id;
            period.Status            = "paid";
            period.PaidAt            = dto.PaidAt ?? now;
            period.UpdatedAt         = now;

            await _context.SaveChangesAsync();
            await tx.CommitAsync();

            _logger.LogInformation(
                "Período {PeriodId} marcado como pagado. PlanTx={TxId}",
                periodId, planTx.Id);

            return await GetByIdAsync(periodId);
        }
        catch
        {
            await tx.RollbackAsync();
            throw;
        }
    }

    public async Task<PlanBillingPeriodDto> RecalculateAttendanceAsync(int periodId)
    {
        var period = await _context.PlanBillingPeriods
            .FirstOrDefaultAsync(p => p.Id == periodId)
            ?? throw new InvalidOperationException($"Período {periodId} no encontrado.");

        // 1. Vincular asistencias huérfanas por rango de fechas del período
        await _context.ServiceAttendances
            .Where(a => a.ClientServicePlanId == period.ClientServicePlanId
                     && a.PlanBillingPeriodId == null
                     && a.AttendanceDate >= period.PeriodStartDate
                     && a.AttendanceDate <= period.PeriodEndDate)
            .ExecuteUpdateAsync(s => s.SetProperty(a => a.PlanBillingPeriodId, period.Id));

        // 2. También vincular por mes calendario (cubre casos donde el plan empezó
        //    después de la asistencia pero el billing_month coincide)
        var calendarStart = new DateTime(period.BillingYear, period.BillingMonth, 1);
        var calendarEnd   = calendarStart.AddMonths(1).AddDays(-1);

        await _context.ServiceAttendances
            .Where(a => a.ClientServicePlanId == period.ClientServicePlanId
                     && a.PlanBillingPeriodId == null
                     && a.AttendanceDate >= calendarStart
                     && a.AttendanceDate <= calendarEnd)
            .ExecuteUpdateAsync(s => s.SetProperty(a => a.PlanBillingPeriodId, period.Id));

        // 3. Recontabilizar usando FK directa (ahora incluye las recién vinculadas)
        var attended = await _context.ServiceAttendances
            .CountAsync(a => a.PlanBillingPeriodId == periodId
                          && a.Status == AttendanceStatus.Attended);

        var reserved = await _context.ServiceAttendances
            .CountAsync(a => a.PlanBillingPeriodId == periodId
                          && a.Status == AttendanceStatus.Scheduled);

        period.SessionsAttended  = attended;
        period.SessionsReserved  = reserved;
        period.UpdatedAt         = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        return await GetByIdAsync(period.Id);
    }

    public async Task MarkOverdueAsync(int businessId)
    {
        var today = DateTime.UtcNow.Date;

        var overdue = await _context.PlanBillingPeriods
            .Where(p => p.BusinessId == businessId
                     && p.Status     == "pending"
                     && p.DueDate.Date < today)
            .ToListAsync();

        foreach (var p in overdue)
        {
            p.Status    = "overdue";
            p.UpdatedAt = DateTime.UtcNow;
        }

        if (overdue.Count > 0)
        {
            await _context.SaveChangesAsync();
            _logger.LogInformation(
                "Marcados {Count} períodos como overdue para business {BusinessId}",
                overdue.Count, businessId);
        }
    }

    public async Task<PlanBillingPeriodDto> WaivePeriodAsync(int periodId, string? reason)
    {
        var period = await _context.PlanBillingPeriods
            .FirstOrDefaultAsync(p => p.Id == periodId)
            ?? throw new InvalidOperationException($"Período {periodId} no encontrado.");

        period.Status    = "waived";
        period.Notes     = reason ?? period.Notes;
        period.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();
        return await GetByIdAsync(periodId);
    }

    // ── Mapping ───────────────────────────────────────────────────────────────

    private PlanBillingPeriodDto MapToDto(PlanBillingPeriod p)
    {
        var today         = DateTime.UtcNow.Date;
        var remaining     = Math.Max(0, p.SessionsAllowed - p.SessionsAttended - p.SessionsReserved);
        var committedPct  = p.SessionsAllowed > 0
            ? (double)(p.SessionsAttended + p.SessionsReserved) / p.SessionsAllowed * 100
            : 0;
        var isOverdue     = p.Status == "overdue"
            || (p.Status == "pending" && p.DueDate.Date < today);

        return new PlanBillingPeriodDto
        {
            Id                  = p.Id,
            BusinessId          = p.BusinessId,
            ClientServicePlanId = p.ClientServicePlanId,
            ServiceClientId     = p.ServiceClientId,
            ClientName          = p.ServiceClient?.Name ?? string.Empty,
            PlanName            = p.ClientServicePlan?.ServicePlan?.Name ?? string.Empty,
            BillingYear         = p.BillingYear,
            BillingMonth        = p.BillingMonth,
            PeriodLabel         = BuildPeriodLabel(p.BillingYear, p.BillingMonth),
            PeriodStartDate     = p.PeriodStartDate,
            PeriodEndDate       = p.PeriodEndDate,
            SessionsAllowed     = p.SessionsAllowed,
            SessionsAttended    = p.SessionsAttended,
            SessionsReserved    = p.SessionsReserved,
            SessionsRemaining   = remaining,
            CommittedPercent    = Math.Round(committedPct, 1),
            Status              = p.Status,
            StatusDisplay       = BuildStatusDisplay(p.Status, isOverdue),
            AmountDue           = p.AmountDue,
            PlanTransactionId   = p.PlanTransactionId,
            PaidAt              = p.PaidAt,
            DueDate             = p.DueDate,
            IsOverdue           = isOverdue,
            Notes               = p.Notes,
            CreatedAt           = p.CreatedAt,
        };
    }

    private static string BuildPeriodLabel(int year, int month)
    {
        var date = new DateTime(year, month, 1);
        var culture = new CultureInfo("es-CL");
        return $"{culture.DateTimeFormat.GetMonthName(month)} {year}";
    }

    private static string BuildStatusDisplay(string status, bool isOverdue) => status switch
    {
        "paid"    => "Pagado",
        "waived"  => "Condonado",
        "overdue" => "Vencido",
        "pending" => isOverdue ? "Vencido" : "Pendiente",
        _         => status,
    };
}
