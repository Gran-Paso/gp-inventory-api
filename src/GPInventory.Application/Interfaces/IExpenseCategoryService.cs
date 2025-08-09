using GPInventory.Application.DTOs.Expenses;

namespace GPInventory.Application.Interfaces;

public interface IExpenseCategoryService
{
    Task<IEnumerable<ExpenseCategoryDto>> GetAllAsync();
    Task<ExpenseCategoryDto?> GetByIdAsync(int id);
    Task<ExpenseCategoryDto> CreateAsync(CreateExpenseCategoryDto createDto);
    Task<ExpenseCategoryDto> UpdateAsync(int id, UpdateExpenseCategoryDto updateDto);
    Task<bool> DeleteAsync(int id);
    Task<IEnumerable<ExpenseCategoryDto>> GetCategoriesWithSubcategoriesAsync();
    Task<IEnumerable<ExpenseSubcategoryDto>> GetSubcategoriesByCategoryAsync(int categoryId);
}
