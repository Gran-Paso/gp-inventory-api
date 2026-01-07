using GPInventory.Application.DTOs.Production;

namespace GPInventory.Application.Interfaces;

public interface IProviderService
{
    Task<ProviderDto> GetProviderByIdAsync(int id);
    Task<IEnumerable<ProviderDto>> GetAllProvidersAsync();
    Task<IEnumerable<ProviderDto>> GetProvidersByBusinessIdAsync(int businessId);
    Task<IEnumerable<ProviderDto>> GetProvidersByStoreIdAsync(int storeId);
    Task<ProviderDto> CreateProviderAsync(CreateProviderDto createProviderDto);
    Task<ProviderDto> UpdateProviderAsync(int id, UpdateProviderDto updateProviderDto);
    Task DeleteProviderAsync(int id);
}
