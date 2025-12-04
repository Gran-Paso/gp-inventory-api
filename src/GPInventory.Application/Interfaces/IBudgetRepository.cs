using GPInventory.Application.DTOs.Budgets;

namespace GPInventory.Application.Interfaces;

public interface IBudgetRepository
{
    Task<List<BudgetDto>> GetBudgetsAsync(int? storeId, int? businessId, int? year, string? status);
    Task<BudgetDto?> GetBudgetByIdAsync(int id);
    Task<int> CreateBudgetAsync(CreateBudgetDto createDto);
    Task<bool> UpdateBudgetAsync(int id, UpdateBudgetDto updateDto);
    Task<bool> DeleteBudgetAsync(int id);
    Task<BudgetSummaryDto?> GetBudgetSummaryAsync(int id);
    Task<List<BudgetAllocationDto>> GetBudgetAllocationsAsync(int budgetId);
    Task<List<MonthlyDistributionDto>> GetMonthlyDistributionAsync(int budgetId);
    Task<bool> CreateBudgetAllocationAsync(int budgetId, CreateBudgetAllocationDto allocationDto);
    Task<bool> CreateMonthlyDistributionAsync(int budgetId, CreateMonthlyDistributionDto distributionDto);
    Task<bool> DeleteBudgetAllocationsAsync(int budgetId);
    Task<bool> DeleteMonthlyDistributionsAsync(int budgetId);
}
