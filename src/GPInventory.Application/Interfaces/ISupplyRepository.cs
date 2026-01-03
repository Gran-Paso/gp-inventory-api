using GPInventory.Domain.Entities;
using GPInventory.Application.DTOs.Production;

namespace GPInventory.Application.Interfaces;

public interface ISupplyRepository
{
    Task<Supply?> GetByIdAsync(int id);
    Task<Supply?> GetByIdWithDetailsAsync(int id);
    Task<IEnumerable<Supply>> GetAllAsync();
    Task<IEnumerable<Supply>> GetByBusinessIdAsync(int businessId);
    Task<IEnumerable<Supply>> GetByStoreIdAsync(int storeId);
    Task<IEnumerable<Supply>> GetActiveSuppliesAsync(int businessId);
    Task<Supply> AddAsync(Supply entity);
    Task UpdateAsync(Supply entity);
    Task DeleteAsync(int id);
    Task<IEnumerable<Supply>> GetSuppliesWithDetailsAsync(int[]? businessIds = null);
    Task<bool> ExistsAsync(int id);
    Task<Supply?> GetByNameAsync(string name, int businessId);
    Task<IEnumerable<SupplyStockDto>> GetAllSupplyStocksAsync(int? businessId = null);
}
