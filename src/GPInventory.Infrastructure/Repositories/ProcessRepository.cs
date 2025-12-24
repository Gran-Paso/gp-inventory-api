using GPInventory.Application.Interfaces;
using GPInventory.Domain.Entities;
using GPInventory.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace GPInventory.Infrastructure.Repositories;

public class ProcessRepository : IProcessRepository
{
    private readonly ApplicationDbContext _context;

    public ProcessRepository(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Process?> GetByIdAsync(int id)
    {
        return await _context.Processes
            .FirstOrDefaultAsync(p => p.Id == id);
    }

    public async Task<Process?> GetByIdWithDetailsAsync(int id)
    {
        return await _context.Processes
            .Include(p => p.Product)
            .Include(p => p.TimeUnit)
            .Include(p => p.Store)
            .Include(p => p.ProcessSupplies)
            .Include(p => p.ProcessComponents)
            .FirstOrDefaultAsync(p => p.Id == id);
    }

    public async Task<IEnumerable<Process>> GetAllAsync()
    {
        return await _context.Processes
            .Include(p => p.Product)
            .Include(p => p.TimeUnit)
            .Include(p => p.Store)
            .ToListAsync();
    }

    public async Task<IEnumerable<Process>> GetByStoreIdAsync(int storeId)
    {
        return await _context.Processes
            .Include(p => p.Product)
            .Include(p => p.TimeUnit)
            .Include(p => p.Store)
            .Where(p => p.StoreId == storeId)
            .ToListAsync();
    }

    public async Task<IEnumerable<Process>> GetByProductIdAsync(int productId)
    {
        return await _context.Processes
            .Include(p => p.Product)
            .Include(p => p.TimeUnit)
            .Include(p => p.Store)
            .Where(p => p.ProductId == productId)
            .ToListAsync();
    }

    public async Task<Process?> GetByNameAsync(string name, int storeId)
    {
        return await _context.Processes
            .FirstOrDefaultAsync(p => p.Name == name && p.StoreId == storeId);
    }

    public async Task<Process> CreateAsync(Process process)
    {
        _context.Processes.Add(process);
        await _context.SaveChangesAsync();
        return process;
    }

    public async Task<Process> UpdateAsync(Process process)
    {
        _context.Processes.Update(process);
        await _context.SaveChangesAsync();
        return process;
    }

    public async Task DeleteAsync(int id)
    {
        var process = await GetByIdAsync(id);
        if (process != null)
        {
            _context.Processes.Remove(process);
            await _context.SaveChangesAsync();
        }
    }

    public async Task<bool> ExistsAsync(int id)
    {
        return await _context.Processes.AnyAsync(p => p.Id == id);
    }

    public async Task<IEnumerable<Process>> GetProcessesWithDetailsAsync(int[]? storeIds = null, int? businessId = null)
    {
        var query = _context.Processes
            .Include(p => p.Product)
            .Include(p => p.TimeUnit)
            .Include(p => p.Store)
            .Include(p => p.ProcessSupplies)
                .ThenInclude(ps => ps.Supply)
            .Include(p => p.ProcessComponents)
                .ThenInclude(pc => pc.Component)
            .AsQueryable();

        if (storeIds != null && storeIds.Length > 0)
        {
            query = query.Where(p => storeIds.Contains(p.StoreId));
        }

        if (businessId.HasValue)
        {
            query = query.Where(p => p.Store.BusinessId == businessId.Value);
        }

        return await query.ToListAsync();
    }
}
