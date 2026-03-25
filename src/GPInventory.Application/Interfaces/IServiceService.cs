using GPInventory.Application.DTOs.Services;

namespace GPInventory.Application.Interfaces;

public interface IServiceService
{
    Task<ServiceDto> GetByIdAsync(int id);
    Task<IEnumerable<ServiceDto>> GetAllAsync(int businessId);
    Task<IEnumerable<ServiceDto>> GetByStoreIdAsync(int storeId);
    Task<IEnumerable<ServiceDto>> GetByCategoryIdAsync(int categoryId);
    Task<IEnumerable<ServiceDto>> GetActiveServicesAsync(int businessId);
    Task<ServiceDto> CreateAsync(CreateServiceDto dto);
    Task<ServiceDto> UpdateAsync(int id, UpdateServiceDto dto);
    Task DeleteAsync(int id);
}
