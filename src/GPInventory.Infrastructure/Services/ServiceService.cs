using GPInventory.Application.DTOs.Services;
using GPInventory.Application.Interfaces;
using GPInventory.Domain.Entities;
using GPInventory.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace GPInventory.Application.Services;

public class ServiceService : IServiceService
{
    private readonly ApplicationDbContext _context;

    public ServiceService(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<ServiceDto> GetByIdAsync(int id)
    {
        var service = await _context.Services
            .Include(s => s.Category)
            .FirstOrDefaultAsync(s => s.Id == id);

        if (service == null)
            throw new InvalidOperationException($"Service with id {id} not found");

        return MapToDto(service);
    }

    public async Task<IEnumerable<ServiceDto>> GetAllAsync(int businessId)
    {
        var services = await _context.Services
            .Include(s => s.Category)
            .Where(s => s.BusinessId == businessId)
            .OrderBy(s => s.Name)
            .ToListAsync();

        return services.Select(MapToDto);
    }

    public async Task<IEnumerable<ServiceDto>> GetByStoreIdAsync(int storeId)
    {
        var services = await _context.Services
            .Include(s => s.Category)
            .Where(s => s.StoreId == storeId)
            .OrderBy(s => s.Name)
            .ToListAsync();

        return services.Select(MapToDto);
    }

    public async Task<IEnumerable<ServiceDto>> GetByCategoryIdAsync(int categoryId)
    {
        var services = await _context.Services
            .Include(s => s.Category)
            .Where(s => s.CategoryId == categoryId)
            .OrderBy(s => s.Name)
            .ToListAsync();

        return services.Select(MapToDto);
    }

    public async Task<IEnumerable<ServiceDto>> GetActiveServicesAsync(int businessId)
    {
        var services = await _context.Services
            .Include(s => s.Category)
            .Where(s => s.BusinessId == businessId && s.Active)
            .OrderBy(s => s.Name)
            .ToListAsync();

        return services.Select(MapToDto);
    }

    public async Task<ServiceDto> CreateAsync(CreateServiceDto dto)
    {
        var service = new Service
        {
            Name = dto.Name,
            CategoryId = dto.CategoryId,
            BusinessId = dto.BusinessId,
            StoreId = dto.StoreId,
            BasePrice = dto.BasePrice,
            DurationMinutes = dto.DurationMinutes,
            Description = dto.Description,
            PricingType = dto.PricingType,
            IsTaxable = dto.IsTaxable,
            Active = dto.Active
        };

        _context.Services.Add(service);
        await _context.SaveChangesAsync();

        return await GetByIdAsync(service.Id);
    }

    public async Task<ServiceDto> UpdateAsync(int id, UpdateServiceDto dto)
    {
        var service = await _context.Services.FindAsync(id);

        if (service == null)
            throw new InvalidOperationException($"Service with id {id} not found");

        service.Name = dto.Name;
        service.CategoryId = dto.CategoryId;
        service.StoreId = dto.StoreId;
        service.BasePrice = dto.BasePrice;
        service.DurationMinutes = dto.DurationMinutes;
        service.Description = dto.Description;
        service.PricingType = dto.PricingType;
        service.IsTaxable = dto.IsTaxable;
        service.Active = dto.Active;
        service.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        return await GetByIdAsync(id);
    }

    public async Task DeleteAsync(int id)
    {
        var service = await _context.Services.FindAsync(id);

        if (service == null)
            throw new InvalidOperationException($"Service with id {id} not found");

        _context.Services.Remove(service);
        await _context.SaveChangesAsync();
    }

    private static ServiceDto MapToDto(Service service)
    {
        return new ServiceDto
        {
            Id = service.Id,
            Name = service.Name,
            CategoryId = service.CategoryId,
            BusinessId = service.BusinessId,
            StoreId = service.StoreId,
            BasePrice = service.BasePrice,
            DurationMinutes = service.DurationMinutes,
            Description = service.Description,
            PricingType = service.PricingType,
            IsTaxable = service.IsTaxable,
            Active = service.Active,
            CreatedAt = service.CreatedAt,
            UpdatedAt = service.UpdatedAt,
            Category = service.Category != null ? new ServiceCategoryDto
            {
                Id = service.Category.Id,
                Name = service.Category.Name,
                Description = service.Category.Description,
                BusinessId = service.Category.BusinessId,
                Active = service.Category.Active,
                CreatedAt = service.Category.CreatedAt,
                UpdatedAt = service.Category.UpdatedAt
            } : null
        };
    }
}
