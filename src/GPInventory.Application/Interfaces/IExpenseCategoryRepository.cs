using GPInventory.Domain.Entities;

namespace GPInventory.Application.Interfaces;

public interface IExpenseCategoryRepository
{
    Task<ExpenseCategory?> GetByIdAsync(int id);
    Task<IEnumerable<ExpenseCategory>> GetAllAsync();
    Task<ExpenseCategory> AddAsync(ExpenseCategory entity);
    Task UpdateAsync(ExpenseCategory entity);
    Task DeleteAsync(int id);
    Task<IEnumerable<ExpenseCategory>> GetCategoriesWithSubcategoriesAsync();
    Task<ExpenseCategory?> GetCategoryWithSubcategoriesAsync(int id);
    Task<bool> ExistsByNameAsync(string name, int? excludeId = null);
}
