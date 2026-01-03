using GPInventory.Domain.Entities;

namespace GPInventory.Application.Interfaces;

public interface ISupplyCategoryRepository
{
    Task<IEnumerable<SupplyCategory>> GetAllByBusinessIdAsync(int businessId, bool activeOnly = false);
    Task<SupplyCategory?> GetByIdAsync(int id);
    Task<SupplyCategory> CreateAsync(SupplyCategory category);
    Task<SupplyCategory> UpdateAsync(SupplyCategory category);
    Task<bool> DeleteAsync(int id);
    Task<bool> ExistsAsync(int id);
    Task<bool> NameExistsAsync(string name, int businessId, int? excludeId = null);
}
