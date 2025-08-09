using GPInventory.Domain.Entities;

namespace GPInventory.Application.Interfaces;

public interface IExpenseSubcategoryRepository
{
    Task<IEnumerable<ExpenseSubcategory>> GetAllAsync();
    Task<ExpenseSubcategory?> GetByIdAsync(int id);
    Task<ExpenseSubcategory> AddAsync(ExpenseSubcategory entity);
    Task UpdateAsync(ExpenseSubcategory entity);
    Task DeleteAsync(int id);
    Task<IEnumerable<ExpenseSubcategory>> GetSubcategoriesByCategoryAsync(int categoryId);
}
