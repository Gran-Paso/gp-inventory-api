using GPInventory.Application.Interfaces;
using GPInventory.Domain.Entities;
using GPInventory.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace GPInventory.Infrastructure.Repositories;

public class SupplyCategoryRepository : ISupplyCategoryRepository
{
    private readonly ApplicationDbContext _context;

    public SupplyCategoryRepository(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<IEnumerable<SupplyCategory>> GetAllByBusinessIdAsync(int businessId, bool activeOnly = false)
    {
        var query = _context.SupplyCategories
            .Where(sc => sc.BusinessId == businessId);

        if (activeOnly)
        {
            query = query.Where(sc => sc.Active);
        }

        return await query
            .OrderBy(sc => sc.Name)
            .ToListAsync();
    }

    public async Task<SupplyCategory?> GetByIdAsync(int id)
    {
        return await _context.SupplyCategories
            .FirstOrDefaultAsync(sc => sc.Id == id);
    }

    public async Task<SupplyCategory> CreateAsync(SupplyCategory category)
    {
        _context.SupplyCategories.Add(category);
        await _context.SaveChangesAsync();
        return category;
    }

    public async Task<SupplyCategory> UpdateAsync(SupplyCategory category)
    {
        _context.SupplyCategories.Update(category);
        await _context.SaveChangesAsync();
        return category;
    }

    public async Task<bool> DeleteAsync(int id)
    {
        var category = await GetByIdAsync(id);
        if (category == null) return false;

        _context.SupplyCategories.Remove(category);
        await _context.SaveChangesAsync();
        return true;
    }

    public async Task<bool> ExistsAsync(int id)
    {
        return await _context.SupplyCategories.AnyAsync(sc => sc.Id == id);
    }

    public async Task<bool> NameExistsAsync(string name, int businessId, int? excludeId = null)
    {
        var query = _context.SupplyCategories
            .Where(sc => sc.BusinessId == businessId && sc.Name.ToLower() == name.ToLower());

        if (excludeId.HasValue)
        {
            query = query.Where(sc => sc.Id != excludeId.Value);
        }

        return await query.AnyAsync();
    }
}
