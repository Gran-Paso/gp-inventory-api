using GPInventory.Domain.Entities;

namespace GPInventory.Application.Interfaces;

public interface IProcessRepository
{
    Task<Process?> GetByIdAsync(int id);
    Task<Process?> GetByIdWithDetailsAsync(int id);
    Task<IEnumerable<Process>> GetAllAsync();
    Task<IEnumerable<Process>> GetByStoreIdAsync(int storeId);
    Task<IEnumerable<Process>> GetByProductIdAsync(int productId);
    Task<Process?> GetByNameAsync(string name, int storeId);
    Task<Process> CreateAsync(Process process);
    Task<Process> UpdateAsync(Process process);
    Task DeleteAsync(int id);
    Task<bool> ExistsAsync(int id);
    Task<IEnumerable<Process>> GetProcessesWithDetailsAsync(int[]? storeIds = null, int? businessId = null);
}
