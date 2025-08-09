using GPInventory.Application.Interfaces;
using GPInventory.Domain.Entities;
using GPInventory.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace GPInventory.Infrastructure.Repositories;

public class ExpenseCategoryRepository : IExpenseCategoryRepository
{
    private readonly ApplicationDbContext _context;
    private readonly DbSet<ExpenseCategory> _dbSet;

    public ExpenseCategoryRepository(ApplicationDbContext context)
    {
        _context = context;
        _dbSet = context.Set<ExpenseCategory>();
    }

    public async Task<ExpenseCategory?> GetByIdAsync(int id)
    {
        return await _dbSet.FindAsync(id);
    }

    public async Task<IEnumerable<ExpenseCategory>> GetAllAsync()
    {
        return await _dbSet.OrderBy(c => c.Name).ToListAsync();
    }

    public async Task<ExpenseCategory> AddAsync(ExpenseCategory entity)
    {
        _dbSet.Add(entity);
        await _context.SaveChangesAsync();
        return entity;
    }

    public async Task UpdateAsync(ExpenseCategory entity)
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

    public async Task<IEnumerable<ExpenseCategory>> GetCategoriesWithSubcategoriesAsync()
    {
        // Simplified - just return categories without includes for now
        return await _dbSet
            .OrderBy(c => c.Name)
            .ToListAsync();
    }

    public async Task<ExpenseCategory?> GetCategoryWithSubcategoriesAsync(int id)
    {
        // Simplified - just return category without includes for now
        return await _dbSet
            .FirstOrDefaultAsync(c => c.Id == id);
    }

    public async Task<bool> ExistsByNameAsync(string name, int? excludeId = null)
    {
        var query = _dbSet.Where(c => c.Name.ToLower() == name.ToLower());

        if (excludeId.HasValue)
        {
            query = query.Where(c => c.Id != excludeId.Value);
        }

        return await query.AnyAsync();
    }
}
