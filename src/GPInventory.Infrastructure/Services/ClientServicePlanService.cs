using GPInventory.Application.DTOs.Services;
using GPInventory.Application.Interfaces;
using GPInventory.Domain.Entities;
using GPInventory.Domain.Enums;
using GPInventory.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace GPInventory.Infrastructure.Services;

public class ClientServicePlanService : IClientServicePlanService
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<ClientServicePlanService> _logger;

    public ClientServicePlanService(ApplicationDbContext context, ILogger<ClientServicePlanService> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<ClientServicePlanDto> GetByIdAsync(int id)
    {
        _logger.LogInformation("Obteniendo plan de cliente {PlanId}", id);

        var clientPlan = await _context.ClientServicePlans
            .Include(cp => cp.ServicePlan)
            .ThenInclude(p => p!.Service)
            .Include(cp => cp.ServicePlan)
            .ThenInclude(p => p!.ServiceCategory)
            .Include(cp => cp.ServiceClient)
                .ThenInclude(sc => sc!.ParentClient)
            .Include(cp => cp.ServiceSale)
            .FirstOrDefaultAsync(cp => cp.Id == id);

        if (clientPlan == null)
        {
            throw new InvalidOperationException($"Plan de cliente con ID {id} no encontrado");
        }

        return MapToDto(clientPlan);
    }

    public async Task<IEnumerable<ClientServicePlanDto>> GetByClientAsync(int clientId)
    {
        _logger.LogInformation("Obteniendo planes del cliente {ClientId}", clientId);

        var clientPlans = await _context.ClientServicePlans
            .Include(cp => cp.ServicePlan)
            .ThenInclude(p => p!.Service)
            .Include(cp => cp.ServicePlan)
            .ThenInclude(p => p!.ServiceCategory)
            .Include(cp => cp.ServiceClient)
                .ThenInclude(sc => sc!.ParentClient)
            .Include(cp => cp.ServiceSale)
            .Where(cp => cp.ServiceClientId == clientId)
            .OrderByDescending(cp => cp.StartDate)
            .ToListAsync();

        return clientPlans.Select(cp => MapToDto(cp)).ToList();
    }

    public async Task<IEnumerable<ClientServicePlanDto>> GetActiveByClientAsync(int clientId)
    {
        _logger.LogInformation("Obteniendo planes activos del cliente {ClientId}", clientId);

        var now = DateTime.UtcNow;
        var clientPlans = await _context.ClientServicePlans
            .Include(cp => cp.ServicePlan)
            .ThenInclude(p => p!.Service)
            .Include(cp => cp.ServicePlan)
            .ThenInclude(p => p!.ServiceCategory)
            .Include(cp => cp.ServiceClient)
                .ThenInclude(sc => sc!.ParentClient)
            .Include(cp => cp.ServiceSale)
            .Where(cp => cp.ServiceClientId == clientId 
                && cp.Status == ClientServicePlanStatus.Active
                && cp.EndDate > now)
            .OrderBy(cp => cp.EndDate)
            .ToListAsync();

        var planIds = clientPlans.Select(p => p.Id).ToList();
        var currentPeriods = await _context.PlanBillingPeriods
            .Where(p => planIds.Contains(p.ClientServicePlanId)
                     && p.BillingYear == now.Year
                     && p.BillingMonth == now.Month)
            .ToListAsync();
        var periodDict = currentPeriods.ToDictionary(p => p.ClientServicePlanId);

        return clientPlans.Select(cp => MapToDto(cp, periodDict.GetValueOrDefault(cp.Id))).ToList();
    }

    public async Task<IEnumerable<ClientServicePlanDto>> GetActiveByBusinessAsync(int businessId)
    {
        _logger.LogInformation("Obteniendo planes activos para business {BusinessId}", businessId);

        var now = DateTime.UtcNow;
        var clientPlans = await _context.ClientServicePlans
            .Include(cp => cp.ServicePlan)
            .ThenInclude(p => p!.Service)
            .Include(cp => cp.ServicePlan)
            .ThenInclude(p => p!.ServiceCategory)
            .Include(cp => cp.ServiceClient)
                .ThenInclude(sc => sc!.ParentClient)
            .Include(cp => cp.ServiceSale)
            .Where(cp => cp.ServicePlan != null && cp.ServicePlan.BusinessId == businessId
                && cp.Status == ClientServicePlanStatus.Active
                && cp.EndDate > now)
            .OrderBy(cp => cp.EndDate)
            .ToListAsync();

        var planIds = clientPlans.Select(p => p.Id).ToList();
        var currentPeriods = await _context.PlanBillingPeriods
            .Where(p => planIds.Contains(p.ClientServicePlanId)
                     && p.BillingYear == now.Year
                     && p.BillingMonth == now.Month)
            .ToListAsync();
        var periodDict = currentPeriods.ToDictionary(p => p.ClientServicePlanId);

        return clientPlans.Select(cp => MapToDto(cp, periodDict.GetValueOrDefault(cp.Id))).ToList();
    }

    public async Task<IEnumerable<ClientServicePlanDto>> GetExpiringPlansAsync(int businessId, int daysAhead)
    {
        _logger.LogInformation("Obteniendo planes que expiran en {Days} días para business {BusinessId}", daysAhead, businessId);

        var now = DateTime.UtcNow;
        var expirationThreshold = now.AddDays(daysAhead);

        var clientPlans = await _context.ClientServicePlans
            .Include(cp => cp.ServicePlan)
            .ThenInclude(p => p!.Service)
            .Include(cp => cp.ServicePlan)
            .ThenInclude(p => p!.ServiceCategory)
            .Include(cp => cp.ServiceClient)
                .ThenInclude(sc => sc!.ParentClient)
            .Include(cp => cp.ServiceSale)
            .Where(cp => cp.ServicePlan != null && cp.ServicePlan.BusinessId == businessId
                && cp.Status == ClientServicePlanStatus.Active
                && cp.EndDate > now
                && cp.EndDate <= expirationThreshold)
            .OrderBy(cp => cp.EndDate)
            .ToListAsync();

        return clientPlans.Select(cp => MapToDto(cp)).ToList();
    }

    public async Task<IEnumerable<ClientServicePlanDto>> GetHighRiskPlansAsync(int businessId)
    {
        _logger.LogInformation("Obteniendo planes de alto riesgo para business {BusinessId}", businessId);

        var now = DateTime.UtcNow;
        var clientPlans = await _context.ClientServicePlans
            .Include(cp => cp.ServicePlan)
            .ThenInclude(p => p!.Service)
            .Include(cp => cp.ServicePlan)
            .ThenInclude(p => p!.ServiceCategory)
            .Include(cp => cp.ServiceClient)
                .ThenInclude(sc => sc!.ParentClient)
            .Include(cp => cp.ServiceSale)
            .Where(cp => cp.ServicePlan != null && cp.ServicePlan.BusinessId == businessId
                && cp.Status == ClientServicePlanStatus.Active
                && cp.EndDate > now)
            .ToListAsync();

        // Filtrar por IsHighRisk (propiedad computada)
        var highRiskPlans = clientPlans
            .Where(cp => cp.IsHighRisk)
            .OrderBy(cp => cp.DaysRemaining)
            .ToList();

        return highRiskPlans.Select(cp => MapToDto(cp)).ToList();
    }

    public async Task<ClientServicePlanDto> PurchasePlanAsync(PurchaseServicePlanDto dto, int userId)
    {
        _logger.LogInformation("Cliente {ClientId} comprando plan {PlanId}", dto.ClientId, dto.PlanId);

        using var transaction = await _context.Database.BeginTransactionAsync();
        try
        {
            // Validar plan existe y está activo
            var servicePlan = await _context.ServicePlans.FindAsync(dto.PlanId);
            if (servicePlan == null || !servicePlan.Active)
                throw new InvalidOperationException("Plan de servicio no disponible");

            // Validar cliente existe
            var client = await _context.ServiceClients.FindAsync(dto.ClientId);
            if (client == null)
                throw new InvalidOperationException("Cliente no encontrado");

            var now = DateTime.UtcNow;
            var startDate = (dto.StartDate ?? now).Date;
            var contractMonths = dto.ContractMonths > 0 ? dto.ContractMonths : 12;
            var endDate = startDate.AddMonths(contractMonths);

            // Total de sesiones en todo el contrato
            var totalClasses = servicePlan.ClassCount * contractMonths;

            // Monto del PRIMER período según frecuencia de facturación
            var billingFrequency = dto.BillingFrequency;
            var amountFirstPeriod = billingFrequency switch
            {
                BillingFrequency.Quarterly  => servicePlan.PriceQuarterly   ?? servicePlan.Price * 3,
                BillingFrequency.Semiannual => servicePlan.PriceSemiannual  ?? servicePlan.Price * 6,
                BillingFrequency.Annual     => servicePlan.PriceAnnual      ?? servicePlan.Price * 12,
                _                           => servicePlan.Price
            };

            // Efectivo cobrado al inicio: solo si viene con método de pago (pre-pago inicial)
            var paymentMethodId = dto.PaymentMethodId ?? servicePlan.DefaultPaymentMethodId;
            var initialPayment  = paymentMethodId.HasValue ? amountFirstPeriod : 0m;

            // Crear el plan del cliente (suscripción)
            var clientPlan = new ClientServicePlan
            {
                BusinessId       = servicePlan.BusinessId,
                StoreId          = servicePlan.StoreId,
                ServiceClientId  = dto.ClientId,
                ServicePlanId    = dto.PlanId,
                ServiceSaleId    = null,
                StartDate        = startDate,
                EndDate          = endDate,
                TotalClasses     = totalClasses,
                ClassesUsed      = 0,
                ClassesReserved  = 0,
                TotalPaid        = initialPayment,
                RevenueRecognized = 0,
                Status           = ClientServicePlanStatus.Active,
                BillingFrequency = billingFrequency,
                PaymentTiming    = dto.PaymentTiming,
                PaymentMethodId  = paymentMethodId,
                ContractMonths   = contractMonths,
                Notes            = dto.Notes,
                CreatedAt        = now,
                UpdatedAt        = now
            };

            _context.ClientServicePlans.Add(clientPlan);
            await _context.SaveChangesAsync();

            // Crear transacción financiera del primer período (solo si hay pago inicial)
            int? firstPeriodTxId = null;
            if (paymentMethodId.HasValue && initialPayment > 0)
            {
                var planTx = new PlanTransaction
                {
                    BusinessId          = servicePlan.BusinessId,
                    StoreId             = servicePlan.StoreId,
                    ClientServicePlanId = clientPlan.Id,
                    ServiceClientId     = dto.ClientId,
                    ServicePlanId       = dto.PlanId,
                    UserId              = userId,
                    Amount              = initialPayment,
                    PaymentMethodId     = paymentMethodId,
                    DocumentType        = dto.DocumentType,
                    TransactionDate     = now,
                    Notes               = dto.Notes,
                    CreatedAt           = now,
                    UpdatedAt           = now
                };

                _context.PlanTransactions.Add(planTx);
                await _context.SaveChangesAsync();
                firstPeriodTxId = planTx.Id;
            }

            // ── Generar todos los períodos del contrato según frecuencia ──
            var monthsInPeriod = billingFrequency switch
            {
                BillingFrequency.Quarterly  => 3,
                BillingFrequency.Semiannual => 6,
                BillingFrequency.Annual     => 12,
                _                           => 1
            };
            var totalPeriods = contractMonths / monthsInPeriod;

            for (var i = 0; i < totalPeriods; i++)
            {
                var periodStart = startDate.AddMonths(i * monthsInPeriod);
                var periodEnd   = periodStart.AddMonths(monthsInPeriod).AddDays(-1);
                var dueDate     = dto.PaymentTiming == PlanPaymentTiming.PrePay
                    ? periodStart
                    : periodEnd;
                var isFirstPaid = i == 0 && firstPeriodTxId.HasValue;

                _context.PlanBillingPeriods.Add(new PlanBillingPeriod
                {
                    BusinessId          = servicePlan.BusinessId,
                    StoreId             = servicePlan.StoreId,
                    ClientServicePlanId = clientPlan.Id,
                    ServiceClientId     = dto.ClientId,
                    BillingYear         = periodStart.Year,
                    BillingMonth        = periodStart.Month,
                    PeriodStartDate     = periodStart,
                    PeriodEndDate       = periodEnd,
                    SessionsAllowed     = servicePlan.ClassCount * monthsInPeriod,
                    SessionsAttended    = 0,
                    SessionsReserved    = 0,
                    Status              = isFirstPaid ? "paid" : "pending",
                    AmountDue           = amountFirstPeriod,
                    DueDate             = dueDate,
                    PlanTransactionId   = isFirstPaid ? firstPeriodTxId : null,
                    PaidAt              = isFirstPaid ? now : null,
                    CreatedAt           = now,
                    UpdatedAt           = now,
                });
            }
            await _context.SaveChangesAsync();

            await transaction.CommitAsync();

            _logger.LogInformation(
                "Plan {PlanId} asignado exitosamente. ClientPlan={ClientPlanId}, Frecuencia={Freq}, Meses={Months}, Períodos={Periods}",
                dto.PlanId, clientPlan.Id, dto.BillingFrequency, contractMonths, totalPeriods);

            return await GetByIdAsync(clientPlan.Id);
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync();
            _logger.LogError(ex, "Error al comprar plan {PlanId} para cliente {ClientId}", dto.PlanId, dto.ClientId);
            throw;
        }
    }

    public async Task<ClientServicePlanDto> FreezePlanAsync(int id, FreezePlanDto dto)
    {
        _logger.LogInformation("Congelando plan {PlanId} hasta {FrozenUntil}", id, dto.FrozenUntil);

        var clientPlan = await _context.ClientServicePlans.FindAsync(id);
        if (clientPlan == null)
        {
            throw new InvalidOperationException("Plan no encontrado");
        }

        if (clientPlan.Status != ClientServicePlanStatus.Active)
        {
            throw new InvalidOperationException("Solo se pueden congelar planes activos");
        }

        if (dto.FrozenUntil <= DateTime.UtcNow)
        {
            throw new InvalidOperationException("La fecha de congelación debe ser futura");
        }

        // Calcular cuántos días se congela
        var freezeDays = (int)(dto.FrozenUntil - DateTime.UtcNow).TotalDays;

        // Extender la expiración por los días de congelación
        clientPlan.FrozenUntil = dto.FrozenUntil;
        clientPlan.EndDate = clientPlan.EndDate.AddDays(freezeDays);
        clientPlan.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        _logger.LogInformation("Plan {PlanId} congelado. Nueva fecha de expiración: {NewExpiration}", 
            id, clientPlan.EndDate);

        return await GetByIdAsync(id);
    }

    public async Task<ClientServicePlanDto> UnfreezePlanAsync(int id)
    {
        _logger.LogInformation("Descongelando plan {PlanId}", id);

        var clientPlan = await _context.ClientServicePlans.FindAsync(id);
        if (clientPlan == null)
        {
            throw new InvalidOperationException("Plan no encontrado");
        }

        clientPlan.FrozenUntil = null;
        clientPlan.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        _logger.LogInformation("Plan {PlanId} descongelado", id);

        return await GetByIdAsync(id);
    }

    public async Task<ClientServicePlanDto> CancelPlanAsync(int id, CancelPlanDto dto)
    {
        _logger.LogInformation("Cancelando plan {PlanId}", id);

        using var transaction = await _context.Database.BeginTransactionAsync();
        try
        {
            var clientPlan = await _context.ClientServicePlans
                .Include(cp => cp.ServicePlan)
                .FirstOrDefaultAsync(cp => cp.Id == id);
                
            if (clientPlan == null)
            {
                throw new InvalidOperationException("Plan no encontrado");
            }

            // Marcar como cancelado
            clientPlan.Status = ClientServicePlanStatus.Cancelled;
            clientPlan.CancelledAt = DateTime.UtcNow;
            clientPlan.CancellationReason = dto.Reason;
            clientPlan.UpdatedAt = DateTime.UtcNow;

            // Si se debe reembolsar clases no usadas
            if (dto.RefundUnusedClasses && clientPlan.ClassesRemaining > 0)
            {
                var refundAmount = clientPlan.CostPerClass * clientPlan.ClassesRemaining;
                _logger.LogInformation("Reembolsando ${Amount} por {Classes} clases no usadas", 
                    refundAmount, clientPlan.ClassesRemaining);
                
                // Crear una venta negativa para el reembolso
                if (clientPlan.ServicePlan != null)
                {
                    var refundSale = new ServiceSale
                    {
                        BusinessId = clientPlan.ServicePlan.BusinessId,
                        StoreId = clientPlan.ServicePlan.StoreId,
                        UserId = 1, // TODO: Get from context
                        ServiceClientId = clientPlan.ServiceClientId,
                        Date = DateTime.UtcNow,
                        TotalAmount = -refundAmount,
                        SaleType = "refund",
                        Status = ServiceSaleStatus.Completed,
                        CreatedAt = DateTime.UtcNow,
                        UpdatedAt = DateTime.UtcNow
                    };

                    _context.ServiceSales.Add(refundSale);
                }
            }
            else
            {
                // Si no hay reembolso, reconocer todo el ingreso diferido restante
                clientPlan.RevenueRecognized = clientPlan.TotalPaid;
            }

            await _context.SaveChangesAsync();
            await transaction.CommitAsync();

            _logger.LogInformation("Plan {PlanId} cancelado", id);

            return await GetByIdAsync(id);
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync();
            _logger.LogError(ex, "Error al cancelar plan {PlanId}", id);
            throw;
        }
    }

    public async Task ExpireOldPlansAsync(int businessId)
    {
        _logger.LogInformation("Expirando planes vencidos para business {BusinessId}", businessId);

        var now = DateTime.UtcNow;
        var expiredPlans = await _context.ClientServicePlans
            .Include(cp => cp.ServicePlan)
            .Where(cp => cp.ServicePlan != null && cp.ServicePlan.BusinessId == businessId
                && cp.Status == ClientServicePlanStatus.Active
                && cp.EndDate <= now)
            .ToListAsync();

        foreach (var plan in expiredPlans)
        {
            plan.Status = ClientServicePlanStatus.Expired;
            
            // Reconocer cualquier ingreso diferido restante
            if (plan.DeferredRevenue > 0)
            {
                plan.RevenueRecognized = plan.TotalPaid;
                _logger.LogInformation("Plan {PlanId} expirado. Reconociendo ingreso diferido de ${Amount}",
                    plan.Id, plan.DeferredRevenue);
            }
            
            plan.UpdatedAt = DateTime.UtcNow;
        }

        await _context.SaveChangesAsync();

        _logger.LogInformation("Se expiraron {Count} planes", expiredPlans.Count);
    }

    public async Task<ClientDashboardDto> GetClientDashboardAsync(int clientId)
    {
        _logger.LogInformation("Obteniendo dashboard para cliente {ClientId}", clientId);

        var client = await _context.ServiceClients.FindAsync(clientId);
        if (client == null)
        {
            throw new InvalidOperationException("Cliente no encontrado");
        }

        var activePlans = await GetActiveByClientAsync(clientId);
        
        // Asistencias recientes (últimos 30 días)
        var thirtyDaysAgo = DateTime.UtcNow.AddDays(-30);
        var totalAttendances = await _context.ServiceAttendances
            .Where(a => a.ServiceClientId == clientId 
                && a.AttendanceDate >= thirtyDaysAgo
                && a.Status == AttendanceStatus.Attended)
            .CountAsync();

        // Gasto total
        var totalSpent = await _context.ServiceSales
            .Where(s => s.ServiceClientId == clientId && s.TotalAmount.HasValue)
            .SumAsync(s => s.TotalAmount ?? 0);

        return new ClientDashboardDto
        {
            ClientId = clientId,
            ClientName = client.Name,
            ActivePlans = activePlans.ToList(),
            TotalClassesAttended = totalAttendances,
            TotalClassesAvailable = activePlans.Sum(p => p.ClassesRemaining),
            TotalSpent = totalSpent
        };
    }

    public async Task<DeferredRevenueReportDto> GetDeferredRevenueReportAsync(int businessId)
    {
        _logger.LogInformation("Generando reporte de ingresos diferidos para business {BusinessId}", businessId);

        var business = await _context.Businesses.FindAsync(businessId);
        
        var activePlans = await _context.ClientServicePlans
            .Include(cp => cp.ServicePlan)
            .Include(cp => cp.ServiceClient)
                .ThenInclude(sc => sc!.ParentClient)
            .Where(cp => cp.ServicePlan != null && cp.ServicePlan.BusinessId == businessId
                && cp.Status == ClientServicePlanStatus.Active)
            .ToListAsync();

        var planDtos = activePlans.Select(cp => MapToDto(cp)).ToList();
        
        var totalPaid = planDtos.Sum(p => p.TotalPaid);
        var totalRecognized = planDtos.Sum(p => p.RevenueRecognized);
        var totalDeferred = planDtos.Sum(p => p.DeferredRevenue);

        return new DeferredRevenueReportDto
        {
            BusinessId = businessId,
            BusinessName = business?.CompanyName ?? string.Empty,
            ActivePlansCount = activePlans.Count,
            TotalPaid = totalPaid,
            TotalRevenueRecognized = totalRecognized,
            TotalDeferredRevenue = totalDeferred,
            RecognitionPercent = totalPaid > 0 ? (double)(totalRecognized / totalPaid) * 100 : 0,
            Plans = planDtos
        };
    }

    private ClientServicePlanDto MapToDto(ClientServicePlan clientPlan, PlanBillingPeriod? currentPeriod = null)
    {
        var sp = clientPlan.ServicePlan;
        var monthsInPeriod = clientPlan.BillingFrequency switch
        {
            BillingFrequency.Quarterly  => 3,
            BillingFrequency.Semiannual => 6,
            BillingFrequency.Annual     => 12,
            _                           => 1
        };
        var amountPerPeriod = clientPlan.CustomAmountPerPeriod ?? (sp == null ? 0m : clientPlan.BillingFrequency switch
        {
            BillingFrequency.Quarterly  => sp.PriceQuarterly   ?? sp.Price * 3,
            BillingFrequency.Semiannual => sp.PriceSemiannual  ?? sp.Price * 6,
            BillingFrequency.Annual     => sp.PriceAnnual      ?? sp.Price * 12,
            _                           => sp.Price
        });
        // Costo por clase = precio del período / clases que incluye ese período
        // Ej: mensual 30.000 / 4 clases = 7.500. NO dividir TotalPaid (solo lo pagado hasta hoy) / TotalClasses (todo el año).
        var classesPerPeriod = (sp?.ClassCount ?? 0) * monthsInPeriod;
        var costPerClass = classesPerPeriod > 0 ? amountPerPeriod / classesPerPeriod : 0m;

        return new ClientServicePlanDto
        {
            Id = clientPlan.Id,
            BusinessId = clientPlan.BusinessId,
            StoreId = clientPlan.StoreId,
            ServiceClientId = clientPlan.ServiceClientId,
            ClientName = clientPlan.ServiceClient?.Name ?? string.Empty,
            ClientEmail = clientPlan.ServiceClient?.Email,
            ParentClientName = clientPlan.ServiceClient?.ParentClient?.Name,
            ServicePlanId = clientPlan.ServicePlanId,
            PlanName = clientPlan.ServicePlan?.Name ?? string.Empty,
            ServiceSaleId = clientPlan.ServiceSaleId,
            StartDate = clientPlan.StartDate,
            EndDate = clientPlan.EndDate,
            TotalClasses = clientPlan.TotalClasses,
            ClassesUsed = clientPlan.ClassesUsed,
            ClassesReserved = clientPlan.ClassesReserved,
            ClassesRemaining = clientPlan.ClassesRemaining,
            Status = clientPlan.Status,
            StatusDisplay = clientPlan.Status.ToString(),
            BillingFrequency = clientPlan.BillingFrequency,
            BillingFrequencyDisplay = clientPlan.BillingFrequency switch
            {
                BillingFrequency.Quarterly  => "Trimestral",
                BillingFrequency.Semiannual => "Semestral",
                BillingFrequency.Annual     => "Anual",
                _                           => "Mensual"
            },
            PaymentTiming = clientPlan.PaymentTiming,
            PaymentTimingDisplay = clientPlan.PaymentTiming == PlanPaymentTiming.PrePay ? "Pre-pago" : "Diferido",
            PaymentMethodId = clientPlan.PaymentMethodId,
            ContractMonths = clientPlan.ContractMonths,
            AmountPerPeriod = amountPerPeriod,
            CancelledAt = clientPlan.CancelledAt,
            CancellationReason = clientPlan.CancellationReason,
            TotalPaid = clientPlan.TotalPaid,
            RevenueRecognized = clientPlan.RevenueRecognized,
            DeferredRevenue = clientPlan.DeferredRevenue,
            FrozenUntil = clientPlan.FrozenUntil,
            Notes = clientPlan.Notes,
            CreatedAt = clientPlan.CreatedAt,
            UpdatedAt = clientPlan.UpdatedAt,
            CostPerClass = costPerClass,
            DaysRemaining = clientPlan.DaysRemaining,
            UtilizationPercent = clientPlan.UtilizationPercent,
            CommittedPercent = clientPlan.CommittedPercent,
            IsExpiringSoon = clientPlan.IsExpiringSoon,
            IsHighRisk = clientPlan.IsHighRisk,
            AlertLevel = clientPlan.IsHighRisk ? "HIGH" : clientPlan.IsExpiringSoon ? "MEDIUM" : "OK",
            CurrentPeriodSessionsAllowed  = currentPeriod?.SessionsAllowed  ?? 0,
            CurrentPeriodSessionsAttended = currentPeriod?.SessionsAttended ?? 0,
            CurrentPeriodSessionsReserved = currentPeriod?.SessionsReserved ?? 0,
            CurrentPeriodIsFull = currentPeriod != null
                && currentPeriod.SessionsAllowed > 0
                && (currentPeriod.SessionsAttended + currentPeriod.SessionsReserved) >= currentPeriod.SessionsAllowed,
            EnrollmentGroupId = clientPlan.EnrollmentGroupId,
        };
    }

    // ─────────────────────────────────────────────────────────────────────────────
    // Matrículas grupales (combos)
    // ─────────────────────────────────────────────────────────────────────────────

    public async Task<PlanEnrollmentGroupDto> PurchaseGroupEnrollmentAsync(PurchaseGroupEnrollmentDto dto, int userId)
    {
        _logger.LogInformation("Creando matrícula grupal para cliente pagador {PayerClientId} con {MemberCount} miembro(s)",
            dto.PayerClientId, dto.Members.Count);

        using var transaction = await _context.Database.BeginTransactionAsync();
        try
        {
            // 1. Validar cliente pagador
            var payer = await _context.ServiceClients.FindAsync(dto.PayerClientId)
                ?? throw new InvalidOperationException("Cliente pagador no encontrado");

            // 2. Validar miembros + planes
            var memberCount = dto.Members.Count;
            var memberData = new List<(ServiceClient Client, ServicePlan Plan, decimal MemberAmount)>();
            foreach (var member in dto.Members)
            {
                var client = await _context.ServiceClients.FindAsync(member.ClientId)
                    ?? throw new InvalidOperationException($"Cliente miembro {member.ClientId} no encontrado");
                var plan = await _context.ServicePlans.FindAsync(member.PlanId)
                    ?? throw new InvalidOperationException($"Plan {member.PlanId} no encontrado");
                if (!plan.Active)
                    throw new InvalidOperationException($"El plan {member.PlanId} no está activo");

                var memberAmount = member.AmountPerPeriodOverride
                    ?? Math.Round(dto.TotalAmountPerPeriod / memberCount, 2);
                memberData.Add((client, plan, memberAmount));
            }

            var now        = DateTime.UtcNow;
            var startDate  = (dto.StartDate ?? now).Date;
            var endDate    = startDate.AddMonths(dto.ContractMonths);
            var businessId = memberData[0].Plan.BusinessId;
            var storeId    = memberData[0].Plan.StoreId;

            // 3. Crear el grupo
            var group = new PlanEnrollmentGroup
            {
                BusinessId           = businessId,
                StoreId              = storeId,
                PayerClientId        = dto.PayerClientId,
                BillingFrequency     = dto.BillingFrequency,
                PaymentTiming        = dto.PaymentTiming,
                PaymentMethodId      = dto.PaymentMethodId,
                ContractMonths       = dto.ContractMonths,
                StartDate            = startDate,
                EndDate              = endDate,
                TotalAmountPerPeriod = dto.TotalAmountPerPeriod,
                Status               = "active",
                Notes                = dto.Notes,
                CreatedByUserId      = userId,
                CreatedAt            = now,
                UpdatedAt            = now,
            };
            _context.PlanEnrollmentGroups.Add(group);
            await _context.SaveChangesAsync();

            // 4. Transacción financiera: se crea después del primer ClientServicePlan (ver loop)
            int? sharedTxId = null;

            // 5. Períodos: calcular cuántos meses por período
            var monthsInPeriod = dto.BillingFrequency switch
            {
                BillingFrequency.Quarterly  => 3,
                BillingFrequency.Semiannual => 6,
                BillingFrequency.Annual     => 12,
                _                           => 1
            };
            var totalPeriods = dto.ContractMonths / monthsInPeriod;

            // 6. Crear un ClientServicePlan por miembro
            bool firstPlanLinkedToTx = false;
            foreach (var (client, plan, memberAmount) in memberData)
            {
                var totalClasses   = plan.ClassCount * dto.ContractMonths;
                var initialPayment = dto.PaymentMethodId.HasValue ? memberAmount : 0m;

                var clientPlan = new ClientServicePlan
                {
                    BusinessId            = businessId,
                    StoreId               = storeId,
                    ServiceClientId       = client.Id,
                    ServicePlanId         = plan.Id,
                    EnrollmentGroupId     = group.Id,
                    CustomAmountPerPeriod = memberAmount,
                    StartDate             = startDate,
                    EndDate               = endDate,
                    TotalClasses          = totalClasses,
                    ClassesUsed           = 0,
                    ClassesReserved       = 0,
                    TotalPaid             = initialPayment,
                    RevenueRecognized     = 0,
                    Status                = ClientServicePlanStatus.Active,
                    BillingFrequency      = dto.BillingFrequency,
                    PaymentTiming         = dto.PaymentTiming,
                    PaymentMethodId       = dto.PaymentMethodId,
                    ContractMonths        = dto.ContractMonths,
                    Notes                 = dto.Notes,
                    CreatedAt             = now,
                    UpdatedAt             = now,
                };
                _context.ClientServicePlans.Add(clientPlan);
                await _context.SaveChangesAsync();  // clientPlan.Id es válido ahora

                // Crear la transacción financiera única al guardar el PRIMER plan
                if (dto.PaymentMethodId.HasValue && !firstPlanLinkedToTx)
                {
                    var planTx = new PlanTransaction
                    {
                        BusinessId          = businessId,
                        StoreId             = storeId,
                        ClientServicePlanId = clientPlan.Id,   // ID real
                        ServiceClientId     = dto.PayerClientId,
                        ServicePlanId       = plan.Id,
                        UserId              = userId,
                        Amount              = dto.TotalAmountPerPeriod,
                        PaymentMethodId     = dto.PaymentMethodId,
                        DocumentType        = dto.DocumentType,
                        TransactionDate     = now,
                        Notes               = dto.Notes,
                        CreatedAt           = now,
                        UpdatedAt           = now,
                    };
                    _context.PlanTransactions.Add(planTx);
                    await _context.SaveChangesAsync();
                    sharedTxId          = planTx.Id;
                    firstPlanLinkedToTx = true;
                }

                // Generar períodos para este miembro
                for (var i = 0; i < totalPeriods; i++)
                {
                    var periodStart = startDate.AddMonths(i * monthsInPeriod);
                    var periodEnd   = periodStart.AddMonths(monthsInPeriod).AddDays(-1);
                    var dueDate     = dto.PaymentTiming == PlanPaymentTiming.PrePay ? periodStart : periodEnd;
                    var isFirstPaid = i == 0 && sharedTxId.HasValue;

                    _context.PlanBillingPeriods.Add(new PlanBillingPeriod
                    {
                        BusinessId          = businessId,
                        StoreId             = storeId,
                        ClientServicePlanId = clientPlan.Id,
                        ServiceClientId     = client.Id,
                        BillingYear         = periodStart.Year,
                        BillingMonth        = periodStart.Month,
                        PeriodStartDate     = periodStart,
                        PeriodEndDate       = periodEnd,
                        SessionsAllowed     = plan.ClassCount * monthsInPeriod,
                        SessionsAttended    = 0,
                        SessionsReserved    = 0,
                        Status              = isFirstPaid ? "paid" : "pending",
                        AmountDue           = memberAmount,
                        DueDate             = dueDate,
                        PlanTransactionId   = isFirstPaid ? sharedTxId : null,
                        PaidAt              = isFirstPaid ? now : null,
                        CreatedAt           = now,
                        UpdatedAt           = now,
                    });
                }
                await _context.SaveChangesAsync();
            }

            await transaction.CommitAsync();

            _logger.LogInformation(
                "Matrícula grupal {GroupId} creada. Miembros={Members}, Meses={Months}, Total={Total}",
                group.Id, memberCount, dto.ContractMonths, dto.TotalAmountPerPeriod);

            return await GetGroupByIdAsync(group.Id);
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync();
            _logger.LogError(ex, "Error al crear matrícula grupal para pagador {PayerClientId}", dto.PayerClientId);
            throw;
        }
    }

    public async Task<PlanEnrollmentGroupDto> GetGroupByIdAsync(int groupId)
    {
        _logger.LogInformation("Obteniendo matrícula grupal {GroupId}", groupId);

        var group = await _context.PlanEnrollmentGroups
            .Include(g => g.PayerClient)
            .Include(g => g.MemberPlans)
                .ThenInclude(cp => cp.ServicePlan)
                    .ThenInclude(p => p!.Service)
            .Include(g => g.MemberPlans)
                .ThenInclude(cp => cp.ServicePlan)
                    .ThenInclude(p => p!.ServiceCategory)
            .Include(g => g.MemberPlans)
                .ThenInclude(cp => cp.ServiceClient)
                    .ThenInclude(sc => sc!.ParentClient)
            .FirstOrDefaultAsync(g => g.Id == groupId)
            ?? throw new InvalidOperationException($"Matrícula grupal {groupId} no encontrada");

        return MapGroupToDto(group);
    }

    public async Task<IEnumerable<PlanEnrollmentGroupDto>> GetGroupsByPayerAsync(int payerClientId)
    {
        _logger.LogInformation("Obteniendo matrículas grupales del pagador {PayerClientId}", payerClientId);

        var groups = await _context.PlanEnrollmentGroups
            .Include(g => g.PayerClient)
            .Include(g => g.MemberPlans)
                .ThenInclude(cp => cp.ServicePlan)
                    .ThenInclude(p => p!.Service)
            .Include(g => g.MemberPlans)
                .ThenInclude(cp => cp.ServicePlan)
                    .ThenInclude(p => p!.ServiceCategory)
            .Include(g => g.MemberPlans)
                .ThenInclude(cp => cp.ServiceClient)
                    .ThenInclude(sc => sc!.ParentClient)
            .Where(g => g.PayerClientId == payerClientId)
            .OrderByDescending(g => g.CreatedAt)
            .ToListAsync();

        return groups.Select(MapGroupToDto).ToList();
    }

    private PlanEnrollmentGroupDto MapGroupToDto(PlanEnrollmentGroup group) => new()
    {
        Id                   = group.Id,
        BusinessId           = group.BusinessId,
        PayerClientId        = group.PayerClientId,
        PayerClientName      = group.PayerClient?.Name ?? string.Empty,
        BillingFrequency     = group.BillingFrequency,
        BillingFrequencyDisplay = group.BillingFrequency switch
        {
            BillingFrequency.Monthly    => "Mensual",
            BillingFrequency.Quarterly  => "Trimestral",
            BillingFrequency.Semiannual => "Semestral",
            BillingFrequency.Annual     => "Anual",
            _                           => group.BillingFrequency.ToString()
        },
        PaymentTiming        = group.PaymentTiming,
        ContractMonths       = group.ContractMonths,
        StartDate            = group.StartDate,
        EndDate              = group.EndDate,
        TotalAmountPerPeriod = group.TotalAmountPerPeriod,
        Status               = group.Status,
        Notes                = group.Notes,
        CreatedAt            = group.CreatedAt,
        MemberPlans          = group.MemberPlans.Select(cp => MapToDto(cp)).ToList(),
    };
}
