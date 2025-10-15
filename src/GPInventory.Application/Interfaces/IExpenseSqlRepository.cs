using GPInventory.Application.DTOs.Expenses;

namespace GPInventory.Application.Interfaces;

public interface IExpenseSqlRepository
{
    Task<List<ExpenseWithDetailsDto>> GetExpensesByTypeAsync(
        int businessId, 
        int? expenseTypeId = null,
        int? categoryId = null,
        int? subcategoryId = null,
        DateTime? startDate = null,
        DateTime? endDate = null,
        int page = 1,
        int pageSize = 50);

    Task<ExpenseSummaryDto> GetExpenseSummaryByTypeAsync(
        int businessId,
        int? expenseTypeId = null,
        DateTime? startDate = null,
        DateTime? endDate = null);
}