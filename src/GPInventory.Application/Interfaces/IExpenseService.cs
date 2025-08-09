using GPInventory.Application.DTOs.Expenses;

namespace GPInventory.Application.Interfaces;

public interface IExpenseService
{
    // Categorías y subcategorías
    Task<IEnumerable<ExpenseCategoryDto>> GetCategoriesAsync();
    Task<IEnumerable<ExpenseSubcategoryDto>> GetSubcategoriesAsync(int? categoryId = null);
    Task<IEnumerable<RecurrenceTypeDto>> GetRecurrenceTypesAsync();

    // Gastos
    Task<IEnumerable<ExpenseWithDetailsDto>> GetExpensesAsync(ExpenseFiltersDto filters);
    Task<ExpenseDto> GetExpenseByIdAsync(int id);
    Task<ExpenseDto> CreateExpenseAsync(CreateExpenseDto createExpenseDto);
    Task<ExpenseDto> UpdateExpenseAsync(int id, UpdateExpenseDto updateExpenseDto);
    Task DeleteExpenseAsync(int id);

    // Gastos fijos
    Task<IEnumerable<FixedExpenseWithDetailsDto>> GetFixedExpensesAsync(int[]? businessIds = null);
    Task<FixedExpenseDto> GetFixedExpenseByIdAsync(int id);
    Task<FixedExpenseDto> CreateFixedExpenseAsync(CreateFixedExpenseDto createFixedExpenseDto);
    Task<FixedExpenseDto> UpdateFixedExpenseAsync(int id, UpdateFixedExpenseDto updateFixedExpenseDto);
    Task DeleteFixedExpenseAsync(int id);

    // Resumen y reportes
    Task<ExpenseSummaryDto> GetExpenseSummaryAsync(int[] businessIds, ExpenseFiltersDto filters);
    Task<byte[]> ExportExpensesAsync(ExpenseFiltersDto filters);
}
