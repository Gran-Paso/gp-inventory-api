using GPInventory.Application.DTOs.Production;

namespace GPInventory.Application.Interfaces;

public interface IUnitMeasureService
{
    Task<IEnumerable<UnitMeasureDto>> GetAllAsync();
    Task<UnitMeasureDto?> GetByIdAsync(int id);
    Task<UnitMeasureDto> CreateAsync(CreateUnitMeasureDto createDto);
    Task<UnitMeasureDto> UpdateAsync(int id, UpdateUnitMeasureDto updateDto);
    Task DeleteAsync(int id);
}
