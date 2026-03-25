using GPInventory.Application.DTOs.Services;

namespace GPInventory.Application.Interfaces;

public interface IServiceCategoryService
{
    Task<ServiceCategoryDto> GetByIdAsync(int id);
    Task<IEnumerable<ServiceCategoryDto>> GetAllAsync(int businessId);
    Task<ServiceCategoryDto> CreateAsync(CreateServiceCategoryDto dto);
    Task<ServiceCategoryDto> UpdateAsync(int id, UpdateServiceCategoryDto dto);
    Task DeleteAsync(int id);
}
