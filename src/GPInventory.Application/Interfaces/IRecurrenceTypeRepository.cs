using GPInventory.Domain.Entities;

namespace GPInventory.Application.Interfaces;

public interface IRecurrenceTypeRepository
{
    Task<RecurrenceType?> GetByIdAsync(int id);
    Task<IEnumerable<RecurrenceType>> GetAllAsync();
    Task<RecurrenceType> AddAsync(RecurrenceType entity);
    Task UpdateAsync(RecurrenceType entity);
    Task DeleteAsync(int id);
    Task<IEnumerable<RecurrenceType>> GetAllActiveAsync();
    Task<RecurrenceType?> GetByNameAsync(string name);
    Task<bool> ExistsByNameAsync(string name, int? excludeId = null);
}
