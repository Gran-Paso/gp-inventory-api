using GPInventory.Application.Interfaces;
using GPInventory.Domain.Entities;
using GPInventory.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace GPInventory.Infrastructure.Repositories;

public class ExpenseTagRepository : IExpenseTagRepository
{
    private readonly ApplicationDbContext _context;

    public ExpenseTagRepository(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<IEnumerable<ExpenseTag>> GetByBusinessAsync(int businessId)
    {
        return await _context.ExpenseTags
            .Where(t => t.BusinessId == businessId)
            .OrderBy(t => t.Name)
            .ToListAsync();
    }

    public async Task<ExpenseTag?> GetByIdAsync(int id)
    {
        return await _context.ExpenseTags.FindAsync(id);
    }

    public async Task<ExpenseTag> AddAsync(ExpenseTag tag)
    {
        _context.ExpenseTags.Add(tag);
        await _context.SaveChangesAsync();
        return tag;
    }

    public async Task UpdateAsync(ExpenseTag tag)
    {
        _context.ExpenseTags.Update(tag);
        await _context.SaveChangesAsync();
    }

    public async Task DeleteAsync(int id)
    {
        var tag = await GetByIdAsync(id);
        if (tag != null)
        {
            _context.ExpenseTags.Remove(tag);
            await _context.SaveChangesAsync();
        }
    }

    public async Task<bool> ExistsByNameAsync(int businessId, string name, int? excludeId = null)
    {
        var query = _context.ExpenseTags
            .Where(t => t.BusinessId == businessId && t.Name.ToLower() == name.ToLower());

        if (excludeId.HasValue)
            query = query.Where(t => t.Id != excludeId.Value);

        return await query.AnyAsync();
    }

    public async Task<IEnumerable<ExpenseTag>> GetTagsByExpenseIdAsync(int expenseId)
    {
        return await _context.ExpenseTagAssignments
            .Where(a => a.ExpenseId == expenseId)
            .Include(a => a.Tag)
            .Select(a => a.Tag!)
            .ToListAsync();
    }

    public async Task SetTagsForExpenseAsync(int expenseId, IEnumerable<int> tagIds)
    {
        // Remove existing assignments
        var existing = await _context.ExpenseTagAssignments
            .Where(a => a.ExpenseId == expenseId)
            .ToListAsync();

        _context.ExpenseTagAssignments.RemoveRange(existing);

        // Add new
        var newAssignments = tagIds.Select(tagId => new ExpenseTagAssignment
        {
            ExpenseId = expenseId,
            TagId = tagId
        });

        await _context.ExpenseTagAssignments.AddRangeAsync(newAssignments);
        await _context.SaveChangesAsync();
    }

    public async Task<Dictionary<int, List<ExpenseTag>>> GetTagsByExpenseIdsAsync(IEnumerable<int> expenseIds)
    {
        var assignments = await _context.ExpenseTagAssignments
            .Where(a => expenseIds.Contains(a.ExpenseId))
            .Include(a => a.Tag)
            .ToListAsync();

        return assignments
            .GroupBy(a => a.ExpenseId)
            .ToDictionary(
                g => g.Key,
                g => g.Select(a => a.Tag!).OrderBy(t => t.Name).ToList()
            );
    }
}
