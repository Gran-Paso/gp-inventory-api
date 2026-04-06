using GPInventory.Application.DTOs.Services;
using GPInventory.Application.Interfaces;
using GPInventory.Domain.Entities;
using GPInventory.Domain.Enums;
using GPInventory.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace GPInventory.Infrastructure.Services;

public class ServicePlanService : IServicePlanService
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<ServicePlanService> _logger;

    public ServicePlanService(ApplicationDbContext context, ILogger<ServicePlanService> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<ServicePlanDto> GetByIdAsync(int id)
    {
        _logger.LogInformation("Obteniendo plan de servicio {PlanId}", id);

        var plan = await _context.ServicePlans
            .Include(p => p.Service)
            .Include(p => p.ServiceCategory)
            .FirstOrDefaultAsync(p => p.Id == id);

        if (plan == null)
        {
            throw new InvalidOperationException($"Plan con ID {id} no encontrado");
        }

        return await MapToDtoAsync(plan);
    }

    public async Task<IEnumerable<ServicePlanDto>> GetAllAsync(int businessId)
    {
        _logger.LogInformation("Obteniendo todos los planes de servicio para business {BusinessId}", businessId);

        var plans = await _context.ServicePlans
            .Include(p => p.Service)
            .Include(p => p.ServiceCategory)
            .Where(p => p.BusinessId == businessId)
            .OrderByDescending(p => p.CreatedAt)
            .ToListAsync();

        var dtos = new List<ServicePlanDto>();
        foreach (var plan in plans)
        {
            dtos.Add(await MapToDtoAsync(plan));
        }

        return dtos;
    }

    public async Task<IEnumerable<ServicePlanDto>> GetActiveAsync(int businessId)
    {
        _logger.LogInformation("Obteniendo planes activos para business {BusinessId}", businessId);

        var plans = await _context.ServicePlans
            .Include(p => p.Service)
            .Include(p => p.ServiceCategory)
            .Where(p => p.BusinessId == businessId && p.Active)
            .OrderBy(p => p.Price)
            .ToListAsync();

        var dtos = new List<ServicePlanDto>();
        foreach (var plan in plans)
        {
            dtos.Add(await MapToDtoAsync(plan));
        }

        return dtos;
    }

    public async Task<IEnumerable<ServicePlanDto>> GetByServiceAsync(int serviceId)
    {
        _logger.LogInformation("Obteniendo planes para servicio {ServiceId}", serviceId);

        var plans = await _context.ServicePlans
            .Include(p => p.Service)
            .Include(p => p.ServiceCategory)
            .Where(p => p.ServiceId == serviceId && p.Active)
            .OrderBy(p => p.Price)
            .ToListAsync();

        var dtos = new List<ServicePlanDto>();
        foreach (var plan in plans)
        {
            dtos.Add(await MapToDtoAsync(plan));
        }

        return dtos;
    }

    public async Task<IEnumerable<ServicePlanDto>> GetByCategoryAsync(int categoryId)
    {
        _logger.LogInformation("Obteniendo planes para categoría {CategoryId}", categoryId);

        var plans = await _context.ServicePlans
            .Include(p => p.Service)
            .Include(p => p.ServiceCategory)
            .Where(p => p.ServiceCategoryId == categoryId && p.Active)
            .OrderBy(p => p.Price)
            .ToListAsync();

        var dtos = new List<ServicePlanDto>();
        foreach (var plan in plans)
        {
            dtos.Add(await MapToDtoAsync(plan));
        }

        return dtos;
    }

    public async Task<ServicePlanDto> CreateAsync(CreateServicePlanDto dto)
    {
        _logger.LogInformation("Creando nuevo plan de servicio: {PlanName}", dto.Name);

        var plan = new ServicePlan
        {
            BusinessId = dto.BusinessId,
            StoreId = dto.StoreId,
            Name = dto.Name,
            Description = dto.Description,
            ServiceId = dto.ServiceId,
            ServiceCategoryId = dto.ServiceCategoryId,
            ClassCount = dto.ClassCount,
            Price = dto.Price,
            PriceQuarterly = dto.PriceQuarterly,
            PriceSemiannual = dto.PriceSemiannual,
            PriceAnnual = dto.PriceAnnual,
            PaymentTiming = dto.PaymentTiming,
            DefaultPaymentMethodId = dto.DefaultPaymentMethodId,
            Active = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _context.ServicePlans.Add(plan);
        await _context.SaveChangesAsync();

        _logger.LogInformation("Plan de servicio creado con ID {PlanId}", plan.Id);

        return await GetByIdAsync(plan.Id);
    }

    public async Task<ServicePlanDto> UpdateAsync(int id, UpdateServicePlanDto dto)
    {
        _logger.LogInformation("Actualizando plan de servicio {PlanId}", id);

        var plan = await _context.ServicePlans.FindAsync(id);
        if (plan == null)
        {
            throw new InvalidOperationException($"Plan con ID {id} no encontrado");
        }

        plan.Name = dto.Name;
        plan.Description = dto.Description;
        plan.ServiceId = dto.ServiceId;
        plan.ServiceCategoryId = dto.ServiceCategoryId;
        plan.ClassCount = dto.ClassCount;
        plan.Price = dto.Price;
        plan.PriceQuarterly = dto.PriceQuarterly;
        plan.PriceSemiannual = dto.PriceSemiannual;
        plan.PriceAnnual = dto.PriceAnnual;
        plan.PaymentTiming = dto.PaymentTiming;
        plan.DefaultPaymentMethodId = dto.DefaultPaymentMethodId;
        plan.Active = dto.Active;
        plan.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        _logger.LogInformation("Plan de servicio {PlanId} actualizado", id);

        return await GetByIdAsync(id);
    }

    public async Task DeleteAsync(int id)
    {
        _logger.LogInformation("Eliminando plan de servicio {PlanId}", id);

        var plan = await _context.ServicePlans.FindAsync(id);
        if (plan == null)
        {
            throw new InvalidOperationException($"Plan con ID {id} no encontrado");
        }

        // Verificar si hay clientes con planes activos
        var hasActivePlans = await _context.ClientServicePlans
            .AnyAsync(cp => cp.ServicePlanId == id && cp.Status == Domain.Enums.ClientServicePlanStatus.Active);

        if (hasActivePlans)
        {
            throw new InvalidOperationException("No se puede eliminar un plan con clientes activos. Desactívelo en su lugar.");
        }

        _context.ServicePlans.Remove(plan);
        await _context.SaveChangesAsync();

        _logger.LogInformation("Plan de servicio {PlanId} eliminado", id);
    }

    public async Task<ServicePlanDto> ToggleActiveAsync(int id)
    {
        _logger.LogInformation("Cambiando estado de plan {PlanId}", id);

        var plan = await _context.ServicePlans.FindAsync(id);
        if (plan == null)
        {
            throw new InvalidOperationException($"Plan con ID {id} no encontrado");
        }

        plan.Active = !plan.Active;
        plan.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        _logger.LogInformation("Plan {PlanId} ahora está {Status}", id, plan.Active ? "activo" : "inactivo");

        return await GetByIdAsync(id);
    }

    private async Task<ServicePlanDto> MapToDtoAsync(ServicePlan plan)
    {
        // Obtener estadísticas
        var activeClients = await _context.ClientServicePlans
            .Where(cp => cp.ServicePlanId == plan.Id && cp.Status == Domain.Enums.ClientServicePlanStatus.Active)
            .CountAsync();

        var totalPurchases = await _context.ClientServicePlans
            .Where(cp => cp.ServicePlanId == plan.Id)
            .CountAsync();

        return new ServicePlanDto
        {
            Id = plan.Id,
            BusinessId = plan.BusinessId,
            StoreId = plan.StoreId,
            Name = plan.Name,
            Description = plan.Description,
            ServiceId = plan.ServiceId,
            ServiceName = plan.Service?.Name,
            ServiceCategoryId = plan.ServiceCategoryId,
            ServiceCategoryName = plan.ServiceCategory?.Name,
            ClassCount = plan.ClassCount,
            Price = plan.Price,
            PriceQuarterly = plan.PriceQuarterly,
            PriceSemiannual = plan.PriceSemiannual,
            PriceAnnual = plan.PriceAnnual,
            PaymentTiming = plan.PaymentTiming,
            PaymentTimingDisplay = plan.PaymentTiming == PlanPaymentTiming.PrePay ? "Pre-pago"
                : plan.PaymentTiming == PlanPaymentTiming.Both ? "Cualquiera" : "Diferido",
            DefaultPaymentMethodId = plan.DefaultPaymentMethodId,
            PricePerClass = plan.ClassCount > 0 ? plan.Price / plan.ClassCount : 0,
            Active = plan.Active,
            CreatedAt = plan.CreatedAt,
            UpdatedAt = plan.UpdatedAt,
            ActiveClientsCount = activeClients,
            TotalPurchasesCount = totalPurchases
        };
    }
}
