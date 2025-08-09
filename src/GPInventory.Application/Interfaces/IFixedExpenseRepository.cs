using GPInventory.Domain.Entities;

namespace GPInventory.Application.Interfaces;

public interface IFixedExpenseRepository
{
    Task<FixedExpense?> GetByIdAsync(int id);
    Task<IEnumerable<FixedExpense>> GetAllAsync();
    Task<FixedExpense> AddAsync(FixedExpense entity);
    Task UpdateAsync(FixedExpense entity);
    Task DeleteAsync(int id);
    Task<IEnumerable<FixedExpense>> GetFixedExpensesWithDetailsAsync(int[]? businessIds = null);

    Task<decimal> GetTotalFixedExpensesAmountAsync(int businessId);
    Task<IEnumerable<(int CategoryId, string CategoryName, decimal TotalAmount, int Count)>> GetFixedExpensesByCategoryAsync(int businessId);
    Task<IEnumerable<FixedExpense>> GetActiveFixedExpensesForGenerationAsync(DateTime currentDate);
}
