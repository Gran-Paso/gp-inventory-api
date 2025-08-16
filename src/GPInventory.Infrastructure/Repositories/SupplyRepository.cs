using GPInventory.Application.Interfaces;
using GPInventory.Domain.Entities;
using GPInventory.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace GPInventory.Infrastructure.Repositories;

public class SupplyRepository : ISupplyRepository
{
    private readonly ApplicationDbContext _context;

    public SupplyRepository(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Supply?> GetByIdAsync(int id)
    {
        return await _context.Supplies
            .Include(s => s.FixedExpense)
            .Include(s => s.Business)
            .Include(s => s.Store)
            .FirstOrDefaultAsync(s => s.Id == id);
    }

    public async Task<IEnumerable<Supply>> GetAllAsync()
    {
        return await _context.Supplies
            .Include(s => s.FixedExpense)
            .Include(s => s.Business)
            .Include(s => s.Store)
            .ToListAsync();
    }

    public async Task<IEnumerable<Supply>> GetByBusinessIdAsync(int businessId)
    {
        return await _context.Supplies
            .Include(s => s.FixedExpense)
            .Include(s => s.Business)
            .Include(s => s.Store)
            .Where(s => s.BusinessId == businessId)
            .ToListAsync();
    }

    public async Task<IEnumerable<Supply>> GetByStoreIdAsync(int storeId)
    {
        return await _context.Supplies
            .Include(s => s.FixedExpense)
            .Include(s => s.Business)
            .Include(s => s.Store)
            .Where(s => s.StoreId == storeId)
            .ToListAsync();
    }

    public async Task<IEnumerable<Supply>> GetActiveSuppliesAsync(int businessId)
    {
        return await _context.Supplies
            .Include(s => s.FixedExpense)
            .Include(s => s.Business)
            .Include(s => s.Store)
            .Where(s => s.BusinessId == businessId && s.Active)
            .ToListAsync();
    }

    public async Task<Supply> AddAsync(Supply entity)
    {
        entity.CreatedAt = DateTime.UtcNow;
        entity.UpdatedAt = DateTime.UtcNow;
        
        _context.Supplies.Add(entity);
        await _context.SaveChangesAsync();
        return entity;
    }

    public async Task UpdateAsync(Supply entity)
    {
        entity.UpdatedAt = DateTime.UtcNow;
        _context.Supplies.Update(entity);
        await _context.SaveChangesAsync();
    }

    public async Task DeleteAsync(int id)
    {
        var supply = await _context.Supplies.FindAsync(id);
        if (supply != null)
        {
            _context.Supplies.Remove(supply);
            await _context.SaveChangesAsync();
        }
    }

    public async Task<IEnumerable<Supply>> GetSuppliesWithDetailsAsync(int[]? businessIds = null)
    {
        var query = _context.Supplies
            .Include(s => s.FixedExpense)
            .Include(s => s.Business)
            .Include(s => s.Store)
            .AsQueryable();

        if (businessIds != null && businessIds.Length > 0)
        {
            query = query.Where(s => businessIds.Contains(s.BusinessId));
        }

        return await query.ToListAsync();
    }

    public async Task<bool> ExistsAsync(int id)
    {
        return await _context.Supplies.AnyAsync(s => s.Id == id);
    }

    public async Task<Supply?> GetByNameAsync(string name, int businessId)
    {
        return await _context.Supplies
            .Include(s => s.FixedExpense)
            .Include(s => s.Business)
            .Include(s => s.Store)
            .FirstOrDefaultAsync(s => s.Name == name && s.BusinessId == businessId);
    }
}
