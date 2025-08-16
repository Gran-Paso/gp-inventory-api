using GPInventory.Application.DTOs.Production;
using GPInventory.Application.Interfaces;
using GPInventory.Domain.Entities;

namespace GPInventory.Application.Services;

public class UnitMeasureService : IUnitMeasureService
{
    private readonly IUnitMeasureRepository _repository;

    public UnitMeasureService(IUnitMeasureRepository repository)
    {
        _repository = repository;
    }

    public async Task<IEnumerable<UnitMeasureDto>> GetAllAsync()
    {
        var unitMeasures = await _repository.GetAllAsync();
        return unitMeasures.Select(MapToDto);
    }

    public async Task<UnitMeasureDto?> GetByIdAsync(int id)
    {
        var unitMeasure = await _repository.GetByIdAsync(id);
        return unitMeasure != null ? MapToDto(unitMeasure) : null;
    }

    public async Task<UnitMeasureDto> CreateAsync(CreateUnitMeasureDto createDto)
    {
        var unitMeasure = new UnitMeasure
        {
            Name = createDto.Name,
            Symbol = createDto.Symbol
        };

        var created = await _repository.CreateAsync(unitMeasure);
        return MapToDto(created);
    }

    public async Task<UnitMeasureDto> UpdateAsync(int id, UpdateUnitMeasureDto updateDto)
    {
        var unitMeasure = await _repository.GetByIdAsync(id);
        if (unitMeasure == null)
            throw new InvalidOperationException($"UnitMeasure with id {id} not found");

        unitMeasure.Name = updateDto.Name;
        unitMeasure.Symbol = updateDto.Symbol;

        var updated = await _repository.UpdateAsync(unitMeasure);
        return MapToDto(updated);
    }

    public async Task DeleteAsync(int id)
    {
        await _repository.DeleteAsync(id);
    }

    private static UnitMeasureDto MapToDto(UnitMeasure unitMeasure)
    {
        return new UnitMeasureDto
        {
            Id = unitMeasure.Id,
            Name = unitMeasure.Name,
            Symbol = unitMeasure.Symbol,
            Description = unitMeasure.Description,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            IsActive = true // Default value since column doesn't exist in DB
        };
    }
}
