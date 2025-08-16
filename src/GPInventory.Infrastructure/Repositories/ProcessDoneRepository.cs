using GPInventory.Application.Interfaces;
using GPInventory.Domain.Entities;
using GPInventory.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace GPInventory.Infrastructure.Repositories;

public class ProcessDoneRepository : IProcessDoneRepository
{
    private readonly ApplicationDbContext _context;

    public ProcessDoneRepository(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<ProcessDone?> GetByIdAsync(int id)
    {
        return await _context.ProcessDones
            .FirstOrDefaultAsync(pd => pd.Id == id);
    }

    public async Task<ProcessDone?> GetByIdWithDetailsAsync(int id)
    {
        return await _context.ProcessDones
            .Include(pd => pd.Process)
                .ThenInclude(p => p.Product)
            .Include(pd => pd.Process)
                .ThenInclude(p => p.Store)
            .Include(pd => pd.SupplyEntries)
                .ThenInclude(se => se.Supply)
            .FirstOrDefaultAsync(pd => pd.Id == id);
    }

    public async Task<IEnumerable<ProcessDone>> GetAllAsync()
    {
        return await _context.ProcessDones
            .Include(pd => pd.Process)
                .ThenInclude(p => p.Product)
            .Include(pd => pd.Process)
                .ThenInclude(p => p.Store)
            .Include(pd => pd.SupplyEntries)
                .ThenInclude(se => se.Supply)
            .OrderByDescending(pd => pd.CompletedAt)
            .ToListAsync();
    }

    public async Task<IEnumerable<ProcessDone>> GetByProcessIdAsync(int processId)
    {
        return await _context.ProcessDones
            .Include(pd => pd.Process)
            .Include(pd => pd.SupplyEntries)
                .ThenInclude(se => se.Supply)
            .Where(pd => pd.ProcessId == processId)
            .OrderByDescending(pd => pd.CompletedAt)
            .ToListAsync();
    }

    public async Task<ProcessDone> CreateAsync(ProcessDone processDone)
    {
        _context.ProcessDones.Add(processDone);
        await _context.SaveChangesAsync();
        return processDone;
    }

    public async Task<ProcessDone> UpdateAsync(ProcessDone processDone)
    {
        _context.ProcessDones.Update(processDone);
        await _context.SaveChangesAsync();
        return processDone;
    }

    public async Task DeleteAsync(int id)
    {
        var processDone = await GetByIdAsync(id);
        if (processDone != null)
        {
            _context.ProcessDones.Remove(processDone);
            await _context.SaveChangesAsync();
        }
    }
}
