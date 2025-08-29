using AutoMapper;
using GPInventory.Application.DTOs.Prospects;
using GPInventory.Application.Interfaces;
using GPInventory.Domain.Entities;

namespace GPInventory.Application.Services;

public class ProspectService : IProspectService
{
    private readonly IProspectRepository _prospectRepository;
    private readonly IMapper _mapper;

    public ProspectService(IProspectRepository prospectRepository, IMapper mapper)
    {
        _prospectRepository = prospectRepository;
        _mapper = mapper;
    }

    public async Task<ProspectDto> CreateProspectAsync(CreateProspectDto createProspectDto)
    {
        try
        {
            var prospect = _mapper.Map<Prospect>(createProspectDto);
            prospect.CreatedAt = DateTime.UtcNow;

            var createdProspect = await _prospectRepository.AddAsync(prospect);
            return _mapper.Map<ProspectDto>(createdProspect);
        }
        catch (Exception ex)
        {
            throw new Exception($"Error al crear el prospect: {ex.Message}", ex);
        }
    }

    public async Task<IEnumerable<ProspectDto>> GetAllProspectsAsync()
    {
        try
        {
            var prospects = await _prospectRepository.GetAllActiveAsync();
            return _mapper.Map<IEnumerable<ProspectDto>>(prospects);
        }
        catch (Exception ex)
        {
            throw new Exception($"Error al obtener los prospects: {ex.Message}", ex);
        }
    }

    public async Task<ProspectDto?> GetProspectByIdAsync(int id)
    {
        try
        {
            var prospect = await _prospectRepository.GetByIdAsync(id);
            return prospect != null ? _mapper.Map<ProspectDto>(prospect) : null;
        }
        catch (Exception ex)
        {
            throw new Exception($"Error al obtener el prospect: {ex.Message}", ex);
        }
    }
}
