using GPInventory.Domain.Entities;

namespace GPInventory.Application.Interfaces;

public interface IProviderRepository
{
    Task<Provider?> GetByIdAsync(int id);
    Task<IEnumerable<Provider>> GetAllAsync();
    Task<IEnumerable<Provider>> GetByBusinessIdAsync(int businessId);
    Task<IEnumerable<Provider>> GetByStoreIdAsync(int storeId);
    Task<Provider> AddAsync(Provider entity);
    Task UpdateAsync(Provider entity);
    Task DeleteAsync(int id);
    Task<bool> ExistsAsync(int id);
    Task<Provider?> GetByNameAsync(string name, int businessId);
}
