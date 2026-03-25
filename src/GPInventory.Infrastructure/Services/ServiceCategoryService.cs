using GPInventory.Application.DTOs.Services;
using GPInventory.Application.Interfaces;
using GPInventory.Domain.Entities;
using GPInventory.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace GPInventory.Application.Services;

public class ServiceCategoryService : IServiceCategoryService
{
    private readonly ApplicationDbContext _context;

    public ServiceCategoryService(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<ServiceCategoryDto> GetByIdAsync(int id)
    {
        var category = await _context.ServiceCategories.FindAsync(id);

        if (category == null)
            throw new InvalidOperationException($"ServiceCategory with id {id} not found");

        return MapToDto(category);
    }

    public async Task<IEnumerable<ServiceCategoryDto>> GetAllAsync(int businessId)
    {
        var categories = await _context.ServiceCategories
            .Where(c => c.BusinessId == businessId)
            .OrderBy(c => c.Name)
            .ToListAsync();

        return categories.Select(MapToDto);
    }

    public async Task<ServiceCategoryDto> CreateAsync(CreateServiceCategoryDto dto)
    {
        var category = new ServiceCategory
        {
            Name = dto.Name,
            Description = dto.Description,
            BusinessId = dto.BusinessId
        };

        _context.ServiceCategories.Add(category);
        await _context.SaveChangesAsync();

        return MapToDto(category);
    }

    public async Task<ServiceCategoryDto> UpdateAsync(int id, UpdateServiceCategoryDto dto)
    {
        var category = await _context.ServiceCategories.FindAsync(id);

        if (category == null)
            throw new InvalidOperationException($"ServiceCategory with id {id} not found");

        category.Name = dto.Name;
        category.Description = dto.Description;
        category.Active = dto.Active;
        category.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        return MapToDto(category);
    }

    public async Task DeleteAsync(int id)
    {
        var category = await _context.ServiceCategories.FindAsync(id);

        if (category == null)
            throw new InvalidOperationException($"ServiceCategory with id {id} not found");

        _context.ServiceCategories.Remove(category);
        await _context.SaveChangesAsync();
    }

    private static ServiceCategoryDto MapToDto(ServiceCategory category)
    {
        return new ServiceCategoryDto
        {
            Id = category.Id,
            Name = category.Name,
            Description = category.Description,
            BusinessId = category.BusinessId,
            Active = category.Active,
            CreatedAt = category.CreatedAt,
            UpdatedAt = category.UpdatedAt
        };
    }
}
