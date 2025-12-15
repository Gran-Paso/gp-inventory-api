using GPInventory.Domain.Entities;

namespace GPInventory.Application.Interfaces;

public interface IComponentRepository
{
    Task<Component?> GetByIdAsync(int id);
    Task<Component?> GetByIdWithSuppliesAsync(int id);
    Task<IEnumerable<Component>> GetAllAsync(int businessId, bool? activeOnly = true);
    Task<Component> CreateAsync(Component component);
    Task<Component> UpdateAsync(Component component);
    Task<bool> DeleteAsync(int id);
    Task<bool> ExistsAsync(int id);
    Task<bool> HasCircularReferenceAsync(int componentId, int subComponentId);
    Task<decimal> CalculateTotalCostAsync(int componentId);
    
    // Component Supplies
    Task<IEnumerable<ComponentSupply>> GetSuppliesByComponentIdAsync(int componentId);
    Task<ComponentSupply> CreateSupplyAsync(ComponentSupply supply);
    Task<bool> DeleteSupplyAsync(int supplyId);
    Task<bool> DeleteAllSuppliesByComponentIdAsync(int componentId);
    
    // Component Production
    Task<IEnumerable<ComponentProduction>> GetProductionsByComponentIdAsync(int componentId);
    Task<IEnumerable<ComponentProduction>> GetActiveProductionsAsync(int businessId);
    Task<IEnumerable<ComponentProduction>> GetExpiringProductionsAsync(int businessId, DateTime beforeDate);
    Task<ComponentProduction?> GetProductionByIdAsync(int id);
    Task<ComponentProduction> CreateProductionAsync(ComponentProduction production);
    Task<ComponentProduction> UpdateProductionAsync(ComponentProduction production);
    Task<bool> ConsumeProductionAsync(int productionId, decimal amountConsumed);
}
