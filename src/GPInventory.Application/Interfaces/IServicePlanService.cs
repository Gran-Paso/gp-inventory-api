using GPInventory.Application.DTOs.Services;

namespace GPInventory.Application.Interfaces;

/// <summary>
/// Servicio para gestión de planes de servicios
/// </summary>
public interface IServicePlanService
{
    Task<ServicePlanDto> GetByIdAsync(int id);
    Task<IEnumerable<ServicePlanDto>> GetAllAsync(int businessId);
    Task<IEnumerable<ServicePlanDto>> GetActiveAsync(int businessId);
    Task<IEnumerable<ServicePlanDto>> GetByServiceAsync(int serviceId);
    Task<IEnumerable<ServicePlanDto>> GetByCategoryAsync(int categoryId);
    Task<ServicePlanDto> CreateAsync(CreateServicePlanDto dto);
    Task<ServicePlanDto> UpdateAsync(int id, UpdateServicePlanDto dto);
    Task DeleteAsync(int id);
    Task<ServicePlanDto> ToggleActiveAsync(int id);
}
