using GPInventory.Domain.Entities;

namespace GPInventory.Application.Interfaces;

public interface IExpenseTagRepository
{
    // Tag CRUD
    Task<IEnumerable<ExpenseTag>> GetByBusinessAsync(int businessId);
    Task<ExpenseTag?> GetByIdAsync(int id);
    Task<ExpenseTag> AddAsync(ExpenseTag tag);
    Task UpdateAsync(ExpenseTag tag);
    Task DeleteAsync(int id);
    Task<bool> ExistsByNameAsync(int businessId, string name, int? excludeId = null);

    // Tag assignments
    Task<IEnumerable<ExpenseTag>> GetTagsByExpenseIdAsync(int expenseId);
    Task SetTagsForExpenseAsync(int expenseId, IEnumerable<int> tagIds);
    Task<Dictionary<int, List<ExpenseTag>>> GetTagsByExpenseIdsAsync(IEnumerable<int> expenseIds);
}
