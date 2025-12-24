using GPInventory.Application.Interfaces;
using GPInventory.Domain.Entities;
using GPInventory.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace GPInventory.Infrastructure.Repositories;

public class ComponentProductionRepository : IComponentProductionRepository
{
    private readonly ApplicationDbContext _context;

    public ComponentProductionRepository(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<ComponentProduction> CreateAsync(ComponentProduction componentProduction)
    {
        _context.ComponentProductions.Add(componentProduction);
        await _context.SaveChangesAsync();
        return componentProduction;
    }

    public async Task<IEnumerable<ComponentProduction>> GetByComponentIdAsync(int componentId)
    {
        return await _context.ComponentProductions
            .Where(cp => cp.ComponentId == componentId && cp.IsActive)
            .OrderByDescending(cp => cp.CreatedAt)
            .ToListAsync();
    }

    public async Task<decimal> GetCurrentStockAsync(int componentId)
    {
        var productions = await _context.ComponentProductions
            .Where(cp => cp.ComponentId == componentId && cp.IsActive)
            .ToListAsync();

        return productions.Sum(cp => cp.ProducedAmount);
    }

    public async Task<IEnumerable<ComponentProduction>> GetByProcessDoneIdAsync(int processDoneId)
    {
        return await _context.ComponentProductions
            .Where(cp => cp.ProcessDoneId == processDoneId && cp.IsActive)
            .ToListAsync();
    }

    public async Task<IEnumerable<ComponentProduction>> GetAllAsync()
    {
        return await _context.ComponentProductions
            .Include(cp => cp.Component)
            .Where(cp => cp.IsActive)
            .OrderByDescending(cp => cp.CreatedAt)
            .ToListAsync();
    }
}
