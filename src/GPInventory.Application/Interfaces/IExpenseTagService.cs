using GPInventory.Application.DTOs.Expenses;

namespace GPInventory.Application.Interfaces;

public interface IExpenseTagService
{
    Task<IEnumerable<ExpenseTagDto>> GetByBusinessAsync(int businessId);
    Task<ExpenseTagDto> CreateAsync(CreateExpenseTagDto dto);
    Task<ExpenseTagDto> UpdateAsync(int id, UpdateExpenseTagDto dto);
    Task DeleteAsync(int id);

    Task<IEnumerable<ExpenseTagDto>> GetTagsByExpenseAsync(int expenseId);
    Task SetTagsForExpenseAsync(int expenseId, List<int> tagIds);
}
