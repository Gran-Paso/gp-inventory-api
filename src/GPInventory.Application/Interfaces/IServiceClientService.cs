using GPInventory.Application.DTOs.Services;

namespace GPInventory.Application.Interfaces;

public interface IServiceClientService
{
    Task<ServiceClientDto> GetByIdAsync(int id);
    Task<IEnumerable<ServiceClientDto>> GetAllAsync(int businessId);
    Task<IEnumerable<ServiceClientDto>> GetByStoreIdAsync(int storeId);
    Task<IEnumerable<ServiceClientDto>> GetActiveClientsAsync(int businessId);
    Task<ServiceClientDto> CreateAsync(CreateServiceClientDto dto);
    Task<ServiceClientDto> UpdateAsync(int id, UpdateServiceClientDto dto);
    Task DeleteAsync(int id);
    Task<IEnumerable<ServiceSaleDto>> GetClientHistoryAsync(int clientId);
    Task<IEnumerable<ServiceClientDto>> GetSubClientsAsync(int parentClientId);
    Task<IEnumerable<RelationshipTypeDto>> GetRelationshipTypesAsync();
}
