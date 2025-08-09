using GPInventory.Application.Interfaces;
using GPInventory.Domain.Entities;
using GPInventory.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace GPInventory.Infrastructure.Repositories;

public class ExpenseSubcategoryRepository : IExpenseSubcategoryRepository
{
    private readonly ApplicationDbContext _context;
    private readonly DbSet<ExpenseSubcategory> _dbSet;

    public ExpenseSubcategoryRepository(ApplicationDbContext context)
    {
        _context = context;
        _dbSet = context.Set<ExpenseSubcategory>();
    }

    public async Task<IEnumerable<ExpenseSubcategory>> GetAllAsync()
    {
        try
        {
            return await _dbSet
                .Include(s => s.ExpenseCategory)
                .OrderBy(s => s.ExpenseCategory.Name)
                .ThenBy(s => s.Name)
                .ToListAsync();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"ExpenseSubcategoryRepository.GetAllAsync Error: {ex.Message}");
            
            // Fallback query sin Include si hay problemas con las relaciones
            try
            {
                var subcategoriesRaw = await _context.Database.SqlQueryRaw<ExpenseSubcategory>(
                    "SELECT id as Id, name as Name, expense_category_id as ExpenseCategoryId FROM expense_subcategory ORDER BY name"
                ).ToListAsync();

                // Cargar las categorías manualmente
                var categoryIds = subcategoriesRaw.Select(s => s.ExpenseCategoryId).Distinct().ToList();
                var categories = await _context.Set<ExpenseCategory>()
                    .Where(c => categoryIds.Contains(c.Id))
                    .ToListAsync();

                foreach (var subcategory in subcategoriesRaw)
                {
                    subcategory.ExpenseCategory = categories.FirstOrDefault(c => c.Id == subcategory.ExpenseCategoryId)!;
                }

                return subcategoriesRaw.OrderBy(s => s.ExpenseCategory?.Name).ThenBy(s => s.Name);
            }
            catch (Exception fallbackEx)
            {
                Console.WriteLine($"ExpenseSubcategoryRepository.GetAllAsync Fallback Error: {fallbackEx.Message}");
                throw new ApplicationException($"Error al obtener subcategorías: {ex.Message}", ex);
            }
        }
    }

    public async Task<ExpenseSubcategory?> GetByIdAsync(int id)
    {
        try
        {
            return await _dbSet
                .Include(s => s.ExpenseCategory)
                .FirstOrDefaultAsync(s => s.Id == id);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"ExpenseSubcategoryRepository.GetByIdAsync Error: {ex.Message}");
            
            // Fallback query
            try
            {
                var subcategory = await _context.Database.SqlQueryRaw<ExpenseSubcategory>(
                    "SELECT id as Id, name as Name, expense_category_id as ExpenseCategoryId FROM expense_subcategory WHERE id = {0}",
                    id
                ).FirstOrDefaultAsync();

                if (subcategory != null)
                {
                    subcategory.ExpenseCategory = await _context.Set<ExpenseCategory>()
                        .FirstOrDefaultAsync(c => c.Id == subcategory.ExpenseCategoryId) ?? new ExpenseCategory();
                }

                return subcategory;
            }
            catch (Exception fallbackEx)
            {
                Console.WriteLine($"ExpenseSubcategoryRepository.GetByIdAsync Fallback Error: {fallbackEx.Message}");
                throw new ApplicationException($"Error al obtener subcategoría con ID {id}: {ex.Message}", ex);
            }
        }
    }

    public async Task<ExpenseSubcategory> AddAsync(ExpenseSubcategory entity)
    {
        try
        {
            _dbSet.Add(entity);
            await _context.SaveChangesAsync();
            return entity;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"ExpenseSubcategoryRepository.AddAsync Error: {ex.Message}");
            throw new ApplicationException($"Error al crear subcategoría: {ex.Message}", ex);
        }
    }

    public async Task UpdateAsync(ExpenseSubcategory entity)
    {
        try
        {
            _dbSet.Update(entity);
            await _context.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"ExpenseSubcategoryRepository.UpdateAsync Error: {ex.Message}");
            throw new ApplicationException($"Error al actualizar subcategoría: {ex.Message}", ex);
        }
    }

    public async Task DeleteAsync(int id)
    {
        try
        {
            var entity = await GetByIdAsync(id);
            if (entity != null)
            {
                _dbSet.Remove(entity);
                await _context.SaveChangesAsync();
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"ExpenseSubcategoryRepository.DeleteAsync Error: {ex.Message}");
            throw new ApplicationException($"Error al eliminar subcategoría: {ex.Message}", ex);
        }
    }

    public async Task<IEnumerable<ExpenseSubcategory>> GetSubcategoriesByCategoryAsync(int categoryId)
    {
        try
        {
            return await _dbSet
                .Include(s => s.ExpenseCategory)
                .Where(s => s.ExpenseCategoryId == categoryId)
                .OrderBy(s => s.Name)
                .ToListAsync();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"ExpenseSubcategoryRepository.GetSubcategoriesByCategoryAsync Error: {ex.Message}");
            
            // Fallback query
            try
            {
                var subcategoriesRaw = await _context.Database.SqlQueryRaw<ExpenseSubcategory>(
                    "SELECT id as Id, name as Name, expense_category_id as ExpenseCategoryId FROM expense_subcategory WHERE expense_category_id = {0} ORDER BY name",
                    categoryId
                ).ToListAsync();

                // Cargar la categoría
                var category = await _context.Set<ExpenseCategory>().FirstOrDefaultAsync(c => c.Id == categoryId);
                foreach (var subcategory in subcategoriesRaw)
                {
                    subcategory.ExpenseCategory = category!;
                }

                return subcategoriesRaw;
            }
            catch (Exception fallbackEx)
            {
                Console.WriteLine($"ExpenseSubcategoryRepository.GetSubcategoriesByCategoryAsync Fallback Error: {fallbackEx.Message}");
                throw new ApplicationException($"Error al obtener subcategorías por categoría: {ex.Message}", ex);
            }
        }
    }
}
