using GPInventory.Application.DTOs.Production;
using GPInventory.Application.Interfaces;
using GPInventory.Domain.Entities;

namespace GPInventory.Application.Services;

public class ProviderService : IProviderService
{
    private readonly IProviderRepository _providerRepository;

    public ProviderService(IProviderRepository providerRepository)
    {
        _providerRepository = providerRepository;
    }

    public async Task<ProviderDto> GetProviderByIdAsync(int id)
    {
        var provider = await _providerRepository.GetByIdAsync(id);
        if (provider == null)
            throw new ArgumentException($"Provider with ID {id} not found");

        return MapToDto(provider);
    }

    public async Task<IEnumerable<ProviderDto>> GetAllProvidersAsync()
    {
        var providers = await _providerRepository.GetAllAsync();
        return providers.Select(MapToDto);
    }

    public async Task<IEnumerable<ProviderDto>> GetProvidersByBusinessIdAsync(int businessId)
    {
        var providers = await _providerRepository.GetByBusinessIdAsync(businessId);
        return providers.Select(MapToDto);
    }

    public async Task<IEnumerable<ProviderDto>> GetProvidersByStoreIdAsync(int storeId)
    {
        var providers = await _providerRepository.GetByStoreIdAsync(storeId);
        return providers.Select(MapToDto);
    }

    public async Task<ProviderDto> CreateProviderAsync(CreateProviderDto createProviderDto)
    {
        // Verify that a provider with the same name doesn't exist in the business
        var existingProvider = await _providerRepository.GetByNameAsync(createProviderDto.Name, createProviderDto.BusinessId);
        if (existingProvider != null)
            throw new ArgumentException($"A provider with name '{createProviderDto.Name}' already exists in this business");

        var provider = new Provider(
            name: createProviderDto.Name,
            businessId: createProviderDto.BusinessId,
            storeId: createProviderDto.StoreId
        )
        {
            Contact = createProviderDto.Contact,
            Address = createProviderDto.Address,
            Mail = createProviderDto.Mail,
            Prefix = createProviderDto.Prefix,
            Active = createProviderDto.Active,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        var createdProvider = await _providerRepository.AddAsync(provider);
        return MapToDto(createdProvider);
    }

    public async Task<ProviderDto> UpdateProviderAsync(int id, UpdateProviderDto updateProviderDto)
    {
        var provider = await _providerRepository.GetByIdAsync(id);
        if (provider == null)
            throw new ArgumentException($"Provider with ID {id} not found");

        // Check if name changed and if it conflicts with another provider
        if (provider.Name != updateProviderDto.Name)
        {
            var existingProvider = await _providerRepository.GetByNameAsync(updateProviderDto.Name, provider.BusinessId);
            if (existingProvider != null && existingProvider.Id != id)
                throw new ArgumentException($"A provider with name '{updateProviderDto.Name}' already exists in this business");
        }

        // Update properties
        typeof(Provider).GetProperty("Name")?.SetValue(provider, updateProviderDto.Name);
        provider.StoreId = updateProviderDto.StoreId;
        provider.Contact = updateProviderDto.Contact;
        provider.Address = updateProviderDto.Address;
        provider.Mail = updateProviderDto.Mail;
        provider.Prefix = updateProviderDto.Prefix;
        provider.Active = updateProviderDto.Active;
        provider.UpdatedAt = DateTime.UtcNow;

        await _providerRepository.UpdateAsync(provider);
        return MapToDto(provider);
    }

    public async Task DeleteProviderAsync(int id)
    {
        var provider = await _providerRepository.GetByIdAsync(id);
        if (provider == null)
            throw new ArgumentException($"Provider with ID {id} not found");

        await _providerRepository.DeleteAsync(id);
    }

    private ProviderDto MapToDto(Provider provider)
    {
        return new ProviderDto
        {
            Id = provider.Id,
            Name = provider.Name,
            BusinessId = provider.BusinessId,
            StoreId = provider.StoreId,
            Contact = provider.Contact,
            Address = provider.Address,
            Mail = provider.Mail,
            Prefix = provider.Prefix,
            Active = provider.Active,
            CreatedAt = provider.CreatedAt,
            UpdatedAt = provider.UpdatedAt
        };
    }
}
