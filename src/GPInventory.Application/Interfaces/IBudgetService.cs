using GPInventory.Application.DTOs.Budgets;

namespace GPInventory.Application.Interfaces;

public interface IBudgetService
{
    Task<List<BudgetDto>> GetBudgetsAsync(int? storeId, int? businessId, int? year, string? status);
    Task<BudgetDto> GetBudgetByIdAsync(int id);
    Task<BudgetDto> CreateBudgetAsync(CreateBudgetDto createDto);
    Task<BudgetDto> UpdateBudgetAsync(int id, UpdateBudgetDto updateDto);
    Task DeleteBudgetAsync(int id);
    Task<BudgetSummaryDto> GetBudgetSummaryAsync(int id);
    Task<List<BudgetAllocationDto>> GetBudgetAllocationsAsync(int budgetId);
    Task<List<MonthlyDistributionDto>> GetMonthlyDistributionAsync(int budgetId);
}
