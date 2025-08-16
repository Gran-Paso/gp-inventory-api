using GPInventory.Application.DTOs.Production;

namespace GPInventory.Application.Interfaces;

public interface ISupplyService
{
    Task<SupplyDto> GetSupplyByIdAsync(int id);
    Task<IEnumerable<SupplyDto>> GetAllSuppliesAsync();
    Task<IEnumerable<SupplyDto>> GetSuppliesByBusinessIdAsync(int businessId);
    Task<IEnumerable<SupplyDto>> GetSuppliesByStoreIdAsync(int storeId);
    Task<IEnumerable<SupplyDto>> GetActiveSuppliesAsync(int businessId);
    Task<SupplyDto> CreateSupplyAsync(CreateSupplyDto createSupplyDto);
    Task<SupplyDto> UpdateSupplyAsync(int id, UpdateSupplyDto updateSupplyDto);
    Task DeleteSupplyAsync(int id);
    Task<IEnumerable<SupplyDto>> GetSuppliesWithDetailsAsync(int[]? businessIds = null);
}
