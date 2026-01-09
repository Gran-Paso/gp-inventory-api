using GPInventory.Application.DTOs.Expenses;

namespace GPInventory.Application.Interfaces;

public interface IExpenseService
{
    // Categorías y subcategorías
    Task<IEnumerable<ExpenseCategoryDto>> GetCategoriesAsync();
    Task<IEnumerable<ExpenseSubcategoryDto>> GetSubcategoriesAsync(int? categoryId = null);
    Task<IEnumerable<RecurrenceTypeDto>> GetRecurrenceTypesAsync();

    // Gastos
    Task<IEnumerable<ExpenseListItemDto>> GetExpensesListAsync(ExpenseFiltersDto filters); // Optimizado: lista ligera
    Task<ExpenseWithDetailsDto> GetExpenseWithDetailsAsync(int id); // Optimizado: detalles completos
    Task<IEnumerable<ExpenseWithDetailsDto>> GetExpensesAsync(ExpenseFiltersDto filters); // Legacy: mantener compatibilidad
    Task<ExpenseDto> GetExpenseByIdAsync(int id);
    Task<ExpenseDto> CreateExpenseAsync(CreateExpenseDto createExpenseDto);
    Task<ExpenseDto> UpdateExpenseAsync(int id, UpdateExpenseDto updateExpenseDto);
    Task DeleteExpenseAsync(int id);

    // Gastos fijos
    Task<IEnumerable<FixedExpenseListItemDto>> GetFixedExpensesListAsync(int[]? businessIds = null, int? expenseTypeId = null); // Optimizado: lista ligera
    Task<FixedExpenseWithDetailsDto> GetFixedExpenseWithDetailsAsync(int id); // Optimizado: detalles completos
    Task<IEnumerable<FixedExpenseWithDetailsDto>> GetFixedExpensesAsync(int[]? businessIds = null, int? expenseTypeId = null); // Legacy
    Task<FixedExpenseDto> GetFixedExpenseByIdAsync(int id);
    Task<FixedExpenseDto> CreateFixedExpenseAsync(CreateFixedExpenseDto createFixedExpenseDto);
    Task<FixedExpenseDto> UpdateFixedExpenseAsync(int id, UpdateFixedExpenseDto updateFixedExpenseDto);
    Task DeleteFixedExpenseAsync(int id);

    // Resumen y reportes
    Task<ExpenseSummaryDto> GetExpenseSummaryAsync(int[] businessIds, ExpenseFiltersDto filters);
    Task<object> GetMonthlyKPIsAsync(int businessId);
    Task<object> GetExpenseTypeKPIsAsync(int businessId, int expenseTypeId);
    Task<byte[]> ExportExpensesAsync(ExpenseFiltersDto filters);
    
    // Visualizaciones estratégicas
    Task<ExpenseTypeChartsDto> GetExpenseTypeChartsAsync(int[] businessIds, int expenseTypeId, ExpenseFiltersDto filters);
}
