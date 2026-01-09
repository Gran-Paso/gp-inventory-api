using GPInventory.Domain.Entities;

namespace GPInventory.Application.Interfaces;

public interface IComponentProductionRepository
{
    Task<ComponentProduction> CreateAsync(ComponentProduction componentProduction);
    Task<IEnumerable<ComponentProduction>> GetByComponentIdAsync(int componentId);
    Task<decimal> GetCurrentStockAsync(int componentId);
    Task<IEnumerable<ComponentProduction>> GetByProcessDoneIdAsync(int processDoneId);
    Task<IEnumerable<ComponentProduction>> GetAllAsync();
    Task<IEnumerable<ComponentProduction>> GetAvailableProductionsByComponentIdAsync(int componentId);
    Task<ComponentProduction?> GetByIdAsync(int id);
    Task UpdateAsync(ComponentProduction componentProduction);
}
