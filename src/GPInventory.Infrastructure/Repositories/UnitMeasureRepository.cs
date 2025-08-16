using GPInventory.Application.Interfaces;
using GPInventory.Domain.Entities;
using GPInventory.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace GPInventory.Infrastructure.Repositories;

public class UnitMeasureRepository : IUnitMeasureRepository
{
    private readonly ApplicationDbContext _context;

    public UnitMeasureRepository(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<IEnumerable<UnitMeasure>> GetAllAsync()
    {
        return await _context.UnitMeasures
            .OrderBy(u => u.Name)
            .ToListAsync();
    }

    public async Task<UnitMeasure?> GetByIdAsync(int id)
    {
        return await _context.UnitMeasures
            .FirstOrDefaultAsync(u => u.Id == id);
    }

    public async Task<UnitMeasure> CreateAsync(UnitMeasure unitMeasure)
    {
        _context.UnitMeasures.Add(unitMeasure);
        await _context.SaveChangesAsync();
        return unitMeasure;
    }

    public async Task<UnitMeasure> UpdateAsync(UnitMeasure unitMeasure)
    {
        _context.UnitMeasures.Update(unitMeasure);
        await _context.SaveChangesAsync();
        return unitMeasure;
    }

    public async Task DeleteAsync(int id)
    {
        var unitMeasure = await GetByIdAsync(id);
        if (unitMeasure != null)
        {
            _context.UnitMeasures.Remove(unitMeasure);
            await _context.SaveChangesAsync();
        }
    }
}
