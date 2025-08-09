using GPInventory.Application.Interfaces;
using GPInventory.Domain.Entities;
using GPInventory.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace GPInventory.Infrastructure.Repositories;

public class RecurrenceTypeRepository : IRecurrenceTypeRepository
{
    private readonly ApplicationDbContext _context;
    private readonly DbSet<RecurrenceType> _dbSet;

    public RecurrenceTypeRepository(ApplicationDbContext context)
    {
        _context = context;
        _dbSet = context.Set<RecurrenceType>();
    }

    public async Task<RecurrenceType?> GetByIdAsync(int id)
    {
        return await _dbSet.FindAsync(id);
    }

    public async Task<IEnumerable<RecurrenceType>> GetAllAsync()
    {
        return await _dbSet.ToListAsync();
    }

    public async Task<RecurrenceType> AddAsync(RecurrenceType entity)
    {
        _dbSet.Add(entity);
        await _context.SaveChangesAsync();
        return entity;
    }

    public async Task UpdateAsync(RecurrenceType entity)
    {
        _dbSet.Update(entity);
        await _context.SaveChangesAsync();
    }

    public async Task DeleteAsync(int id)
    {
        var entity = await GetByIdAsync(id);
        if (entity != null)
        {
            _dbSet.Remove(entity);
            await _context.SaveChangesAsync();
        }
    }

    public async Task<IEnumerable<RecurrenceType>> GetAllActiveAsync()
    {
        try
        {
            // Try simple EF query first (avoid SQL direct for now)
            var result = await _dbSet
                .Select(rt => new RecurrenceType
                {
                    Id = rt.Id,
                    Value = rt.Value,
                    Description = rt.Description
                })
                .OrderBy(r => r.Value)
                .ToListAsync();
                
            Console.WriteLine($"Successfully retrieved {result.Count()} recurrence types");
            return result;
        }
        catch (Exception ex)
        {
            // Log the error with more details
            Console.WriteLine($"EF Query failed: {ex.Message}");
            Console.WriteLine($"Stack Trace: {ex.StackTrace}");
            if (ex.InnerException != null)
            {
                Console.WriteLine($"Inner Exception: {ex.InnerException.Message}");
            }
            
            // Return empty list if query fails
            return new List<RecurrenceType>();
        }
    }

    public async Task<RecurrenceType?> GetByNameAsync(string name)
    {
        return await _dbSet
            .FirstOrDefaultAsync(r => r.Value.ToLower() == name.ToLower());
    }

    public async Task<bool> ExistsByNameAsync(string name, int? excludeId = null)
    {
        var query = _dbSet.Where(r => r.Value.ToLower() == name.ToLower());

        if (excludeId.HasValue)
        {
            query = query.Where(r => r.Id != excludeId.Value);
        }

        return await query.AnyAsync();
    }
}
