using GPInventory.Application.Interfaces;
using GPInventory.Domain.Entities;
using GPInventory.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace GPInventory.Infrastructure.Repositories;

public class ExpenseRepository : IExpenseRepository
{
    private readonly ApplicationDbContext _context;
    private readonly DbSet<Expense> _dbSet;

    public ExpenseRepository(ApplicationDbContext context)
    {
        _context = context;
        _dbSet = context.Set<Expense>();
    }

    public async Task<Expense?> GetByIdAsync(int id)
    {
        return await _dbSet.FindAsync(id);
    }

    public async Task<IEnumerable<Expense>> GetAllAsync()
    {
        return await _dbSet.ToListAsync();
    }

    public async Task<Expense> AddAsync(Expense entity)
    {
        _dbSet.Add(entity);
        await _context.SaveChangesAsync();
        return entity;
    }

    public async Task UpdateAsync(Expense entity)
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

    public async Task<IEnumerable<Expense>> GetExpensesWithDetailsAsync(
        int? businessId = null,
        int[]? businessIds = null,
        int? storeId = null,
        int? categoryId = null,
        int? subcategoryId = null,
        DateTime? startDate = null,
        DateTime? endDate = null,
        int? minAmount = null,
        int? maxAmount = null,
        bool? isFixed = null,
        int page = 1,
        int pageSize = 10,
        string orderBy = "Date",
        bool orderDescending = true)
    {
        // Query simple sin navigation properties problemáticas
        var query = _dbSet
            .Include(e => e.ExpenseSubcategory)
                .ThenInclude(s => s.ExpenseCategory)
            .AsQueryable();

        // Aplicar filtros de business ID usando business_id
        if (businessIds != null && businessIds.Length > 0)
        {
            query = query.Where(e => businessIds.Contains(e.BusinessId));
        }
        else if (businessId.HasValue)
        {
            query = query.Where(e => e.BusinessId == businessId.Value);
        }
        else
        {
            // Si no se proporciona ningún business ID, retornar vacío
            return new List<Expense>();
        }

        // Filtro por store_id (opcional)
        if (storeId.HasValue)
            query = query.Where(e => e.StoreId == storeId.Value);

        // Filtro por categoría usando subcategory_id y la relación
        if (categoryId.HasValue)
            query = query.Where(e => e.ExpenseSubcategory.ExpenseCategoryId == categoryId.Value);

        // Filtro por subcategory_id
        if (subcategoryId.HasValue)
            query = query.Where(e => e.SubcategoryId == subcategoryId.Value);

        // Filtro por fecha (date)
        if (startDate.HasValue)
            query = query.Where(e => e.Date >= startDate.Value);

        if (endDate.HasValue)
            query = query.Where(e => e.Date <= endDate.Value);

        // Filtro por monto (amount)
        if (minAmount.HasValue)
            query = query.Where(e => e.Amount >= minAmount.Value);

        if (maxAmount.HasValue)
            query = query.Where(e => e.Amount <= maxAmount.Value);

        // Filtro por is_fixed
        if (isFixed.HasValue)
            query = query.Where(e => (e.IsFixed ?? false) == isFixed.Value);

        // Ordenamiento
        query = orderBy.ToLower() switch
        {
            "amount" => orderDescending ? query.OrderByDescending(e => e.Amount) : query.OrderBy(e => e.Amount),
            "description" => orderDescending ? query.OrderByDescending(e => e.Description) : query.OrderBy(e => e.Description),
            _ => orderDescending ? query.OrderByDescending(e => e.Date) : query.OrderBy(e => e.Date)
        };

        // Paginación
        query = query.Skip((page - 1) * pageSize).Take(pageSize);

        return await query.ToListAsync();
    }

    public async Task<decimal> GetTotalExpensesAmountAsync(int businessId, DateTime? startDate = null, DateTime? endDate = null)
    {
        var query = _dbSet.Where(e => e.BusinessId == businessId);

        if (startDate.HasValue)
            query = query.Where(e => e.Date >= startDate.Value);

        if (endDate.HasValue)
            query = query.Where(e => e.Date <= endDate.Value);

        return (decimal)await query.SumAsync(e => e.Amount);
    }

    public async Task<IEnumerable<(int CategoryId, string CategoryName, decimal TotalAmount, int Count)>> GetExpensesByCategoryAsync(
        int businessId, DateTime? startDate = null, DateTime? endDate = null)
    {
        var query = _dbSet
            .Include(e => e.ExpenseSubcategory)
                .ThenInclude(s => s.ExpenseCategory)
            .Where(e => e.BusinessId == businessId);

        if (startDate.HasValue)
            query = query.Where(e => e.Date >= startDate.Value);

        if (endDate.HasValue)
            query = query.Where(e => e.Date <= endDate.Value);

        return await query
            .GroupBy(e => new { e.ExpenseSubcategory.ExpenseCategory.Id, e.ExpenseSubcategory.ExpenseCategory.Name })
            .Select(g => new
            {
                CategoryId = g.Key.Id,
                CategoryName = g.Key.Name,
                TotalAmount = (decimal)g.Sum(e => e.Amount), // Cast to decimal
                Count = g.Count()
            })
            .Select(x => ValueTuple.Create(x.CategoryId, x.CategoryName, x.TotalAmount, x.Count))
            .ToListAsync();
    }

    public async Task<IEnumerable<(int Year, int Month, decimal TotalAmount, int Count)>> GetMonthlyExpensesAsync(
        int businessId, DateTime? startDate = null, DateTime? endDate = null)
    {
        var query = _dbSet.Where(e => e.BusinessId == businessId);

        if (startDate.HasValue)
            query = query.Where(e => e.Date >= startDate.Value);

        if (endDate.HasValue)
            query = query.Where(e => e.Date <= endDate.Value);

        return await query
            .GroupBy(e => new { e.Date.Year, e.Date.Month })
            .Select(g => new
            {
                Year = g.Key.Year,
                Month = g.Key.Month,
                TotalAmount = (decimal)g.Sum(e => e.Amount), // Cast to decimal
                Count = g.Count()
            })
            .OrderBy(x => x.Year)
            .ThenBy(x => x.Month)
            .Select(x => ValueTuple.Create(x.Year, x.Month, x.TotalAmount, x.Count))
            .ToListAsync();
    }
}
