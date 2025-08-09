using GPInventory.Domain.Entities;

namespace GPInventory.Application.Interfaces;

public interface IExpenseRepository
{
    Task<Expense?> GetByIdAsync(int id);
    Task<IEnumerable<Expense>> GetAllAsync();
    Task<Expense> AddAsync(Expense entity);
    Task UpdateAsync(Expense entity);
    Task DeleteAsync(int id);
    Task<IEnumerable<Expense>> GetExpensesWithDetailsAsync(
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
        bool orderDescending = true);

    Task<decimal> GetTotalExpensesAmountAsync(int businessId, DateTime? startDate = null, DateTime? endDate = null);
    Task<IEnumerable<(int CategoryId, string CategoryName, decimal TotalAmount, int Count)>> GetExpensesByCategoryAsync(int businessId, DateTime? startDate = null, DateTime? endDate = null);
    Task<IEnumerable<(int Year, int Month, decimal TotalAmount, int Count)>> GetMonthlyExpensesAsync(int businessId, DateTime? startDate = null, DateTime? endDate = null);
}
