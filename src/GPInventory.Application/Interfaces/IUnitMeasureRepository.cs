using GPInventory.Domain.Entities;

namespace GPInventory.Application.Interfaces;

public interface IUnitMeasureRepository
{
    Task<IEnumerable<UnitMeasure>> GetAllAsync();
    Task<UnitMeasure?> GetByIdAsync(int id);
    Task<UnitMeasure> CreateAsync(UnitMeasure unitMeasure);
    Task<UnitMeasure> UpdateAsync(UnitMeasure unitMeasure);
    Task DeleteAsync(int id);
}
