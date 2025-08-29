using GPInventory.Application.DTOs.Prospects;

namespace GPInventory.Application.Interfaces;

public interface IProspectService
{
    Task<ProspectDto> CreateProspectAsync(CreateProspectDto createProspectDto);
    Task<IEnumerable<ProspectDto>> GetAllProspectsAsync();
    Task<ProspectDto?> GetProspectByIdAsync(int id);
}
