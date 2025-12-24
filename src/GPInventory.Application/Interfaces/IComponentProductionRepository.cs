using GPInventory.Domain.Entities;

namespace GPInventory.Application.Interfaces;

public interface IComponentProductionRepository
{
    Task<ComponentProduction> CreateAsync(ComponentProduction componentProduction);
    Task<IEnumerable<ComponentProduction>> GetByComponentIdAsync(int componentId);
    Task<decimal> GetCurrentStockAsync(int componentId);
    Task<IEnumerable<ComponentProduction>> GetByProcessDoneIdAsync(int processDoneId);
    Task<IEnumerable<ComponentProduction>> GetAllAsync();
}
