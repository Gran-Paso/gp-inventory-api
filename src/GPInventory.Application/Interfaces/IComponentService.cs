using GPInventory.Application.DTOs.Components;

namespace GPInventory.Application.Interfaces;

public interface IComponentService
{
    Task<ComponentDto?> GetByIdAsync(int id);
    Task<ComponentWithSuppliesDto?> GetByIdWithSuppliesAsync(int id);
    Task<IEnumerable<ComponentDto>> GetAllAsync(int businessId, bool? activeOnly = true);
    Task<ComponentDto> CreateAsync(CreateComponentDto dto);
    Task<ComponentDto> UpdateAsync(int id, UpdateComponentDto dto);
    Task<bool> DeleteAsync(int id);
    
    // Supply management
    Task<ComponentWithSuppliesDto> AddSuppliesAsync(int componentId, List<CreateComponentSupplyDto> supplies);
    Task<bool> RemoveSupplyAsync(int componentId, int supplyId);
    Task<ComponentWithSuppliesDto> UpdateSuppliesAsync(int componentId, List<CreateComponentSupplyDto> supplies);
    
    // Production management
    Task<ComponentProductionDto> CreateProductionAsync(CreateComponentProductionDto dto);
    Task<ComponentProductionDto> UpdateProductionAsync(int id, UpdateComponentProductionDto dto);
    Task<IEnumerable<ComponentProductionDto>> GetProductionsByComponentAsync(int componentId);
    Task<IEnumerable<ComponentProductionDto>> GetActiveProductionsAsync(int businessId);
    Task<IEnumerable<ComponentProductionDto>> GetExpiringProductionsAsync(int businessId, int daysAhead = 3);
    Task<bool> ConsumeProductionAsync(int productionId, decimal amountConsumed);
    
    // BOM and cost calculation
    Task<BOMTreeNodeDto> GetBOMTreeAsync(int componentId);
    Task<decimal> CalculateTotalCostAsync(int componentId);
    
    // Validation
    Task<bool> ValidateSuppliesAsync(int componentId, List<CreateComponentSupplyDto> supplies);
}
