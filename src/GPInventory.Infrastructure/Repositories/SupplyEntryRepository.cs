using GPInventory.Application.Interfaces;
using GPInventory.Domain.Entities;
using GPInventory.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace GPInventory.Infrastructure.Repositories;

public class SupplyEntryRepository : ISupplyEntryRepository
{
    private readonly ApplicationDbContext _context;

    public SupplyEntryRepository(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<IEnumerable<SupplyEntry>> GetAllAsync()
    {
        return await _context.SupplyEntries
            .Include(se => se.Provider)
            .Include(se => se.Supply)
            .Include(se => se.ProcessDone)
            .OrderByDescending(se => se.CreatedAt)
            .ToListAsync();
    }

    public async Task<SupplyEntry?> GetByIdAsync(int id)
    {
        return await _context.SupplyEntries
            .Include(se => se.Provider)
            .Include(se => se.Supply)
            .Include(se => se.ProcessDone)
            .FirstOrDefaultAsync(se => se.Id == id);
    }

    public async Task<IEnumerable<SupplyEntry>> GetBySupplyIdAsync(int supplyId)
    {
        return await _context.SupplyEntries
            .Include(se => se.Provider)
            .Include(se => se.ProcessDone)
            .Where(se => se.SupplyId == supplyId)
            .OrderByDescending(se => se.CreatedAt)
            .ToListAsync();
    }

    public async Task<IEnumerable<SupplyEntry>> GetByProcessDoneIdAsync(int processDoneId)
    {
        return await _context.SupplyEntries
            .Include(se => se.Provider)
            .Include(se => se.Supply)
            .Where(se => se.ProcessDoneId == processDoneId)
            .OrderByDescending(se => se.CreatedAt)
            .ToListAsync();
    }

    public async Task<decimal> GetCurrentStockAsync(int supplyId)
    {
        var totalStock = await _context.SupplyEntries
            .Where(se => se.SupplyId == supplyId)
            .SumAsync(se => se.Amount);
        
        return totalStock;
    }

    public async Task<SupplyEntry> CreateAsync(SupplyEntry supplyEntry)
    {
        // Reset all navigation properties to null to avoid auto-detection
        supplyEntry.Supply = null!;
        supplyEntry.Provider = null!;
        supplyEntry.ProcessDone = null;
        
        supplyEntry.CreatedAt = DateTime.UtcNow;
        supplyEntry.UpdatedAt = DateTime.UtcNow;
        
        // Add the entity
        _context.SupplyEntries.Add(supplyEntry);
        await _context.SaveChangesAsync();
        
        return supplyEntry;
    }

    public async Task<SupplyEntry> UpdateAsync(SupplyEntry supplyEntry)
    {
        supplyEntry.UpdatedAt = DateTime.UtcNow;
        
        _context.SupplyEntries.Update(supplyEntry);
        await _context.SaveChangesAsync();
        
        return await GetByIdAsync(supplyEntry.Id) ?? supplyEntry;
    }

    public async Task DeleteAsync(int id)
    {
        var supplyEntry = await _context.SupplyEntries.FindAsync(id);
        if (supplyEntry != null)
        {
            _context.SupplyEntries.Remove(supplyEntry);
            await _context.SaveChangesAsync();
        }
    }

    public async Task<IEnumerable<SupplyEntry>> GetSupplyHistoryAsync(int supplyId)
    {
        return await _context.SupplyEntries
            .Include(se => se.Provider)
            .Include(se => se.ProcessDone)
                .ThenInclude(pd => pd!.Process)
            .Where(se => se.SupplyId == supplyId)
            .OrderByDescending(se => se.CreatedAt)
            .ToListAsync();
    }
}
