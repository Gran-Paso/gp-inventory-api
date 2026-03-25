using GPInventory.Application.DTOs.Services;

namespace GPInventory.Application.Interfaces;

public interface IServiceSaleService
{
    Task<ServiceSaleDto> GetByIdAsync(int id);
    Task<IEnumerable<ServiceSaleDto>> GetAllAsync(int businessId);
    Task<IEnumerable<ServiceSaleDto>> GetByStoreIdAsync(int storeId);
    Task<IEnumerable<ServiceSaleDto>> GetPendingSalesAsync(int businessId);
    Task<IEnumerable<ServiceSaleDto>> GetSalesByClientIdAsync(int clientId);
    Task<ServiceSaleDto> CreateAsync(CreateServiceSaleDto dto);
    Task<ServiceSaleDto> CompleteAsync(int id, CompleteServiceSaleDto dto);
    Task<ServiceSaleDto> CancelAsync(int id);
    Task<ServiceSaleDto> StartAsync(int id);
    Task DeleteAsync(int id);
}
