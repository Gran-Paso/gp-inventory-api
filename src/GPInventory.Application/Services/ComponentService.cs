using AutoMapper;
using GPInventory.Application.DTOs.Components;
using GPInventory.Application.Interfaces;
using GPInventory.Domain.Entities;

namespace GPInventory.Application.Services;

public class ComponentService : IComponentService
{
    private readonly IComponentRepository _repository;
    private readonly IMapper _mapper;

    public ComponentService(IComponentRepository repository, IMapper mapper)
    {
        _repository = repository;
        _mapper = mapper;
    }

    public async Task<ComponentDto?> GetByIdAsync(int id)
    {
        var component = await _repository.GetByIdAsync(id);
        return component == null ? null : _mapper.Map<ComponentDto>(component);
    }

    public async Task<ComponentWithSuppliesDto?> GetByIdWithSuppliesAsync(int id)
    {
        var component = await _repository.GetByIdWithSuppliesAsync(id);
        return component == null ? null : _mapper.Map<ComponentWithSuppliesDto>(component);
    }

    public async Task<IEnumerable<ComponentDto>> GetAllAsync(int businessId, bool? activeOnly = true)
    {
        var components = await _repository.GetAllAsync(businessId, activeOnly);
        return _mapper.Map<IEnumerable<ComponentDto>>(components);
    }

    public async Task<ComponentDto> CreateAsync(CreateComponentDto dto)
    {
        var component = _mapper.Map<Component>(dto);
        component.Active = true;
        component.CreatedAt = DateTime.UtcNow;

        var created = await _repository.CreateAsync(component);
        return _mapper.Map<ComponentDto>(created);
    }

    public async Task<ComponentDto> UpdateAsync(int id, UpdateComponentDto dto)
    {
        var existing = await _repository.GetByIdAsync(id);
        if (existing == null)
            throw new KeyNotFoundException($"Component with id {id} not found");

        _mapper.Map(dto, existing);
        existing.UpdatedAt = DateTime.UtcNow;

        var updated = await _repository.UpdateAsync(existing);
        return _mapper.Map<ComponentDto>(updated);
    }

    public async Task<bool> DeleteAsync(int id)
    {
        var exists = await _repository.ExistsAsync(id);
        if (!exists)
            throw new KeyNotFoundException($"Component with id {id} not found");

        return await _repository.DeleteAsync(id);
    }

    // Supply management
    public async Task<ComponentWithSuppliesDto> AddSuppliesAsync(int componentId, List<CreateComponentSupplyDto> supplies)
    {
        var component = await _repository.GetByIdAsync(componentId);
        if (component == null)
            throw new KeyNotFoundException($"Component with id {componentId} not found");

        // Validar referencias circulares
        var isValid = await ValidateSuppliesAsync(componentId, supplies);
        if (!isValid)
            throw new InvalidOperationException("Circular reference detected in component supplies");

        // Crear supplies
        foreach (var supplyDto in supplies)
        {
            var supply = _mapper.Map<ComponentSupply>(supplyDto);
            supply.ComponentId = componentId;
            await _repository.CreateSupplyAsync(supply);
        }

        // Retornar componente con supplies actualizados
        var updated = await _repository.GetByIdWithSuppliesAsync(componentId);
        return _mapper.Map<ComponentWithSuppliesDto>(updated);
    }

    public async Task<bool> RemoveSupplyAsync(int componentId, int supplyId)
    {
        var component = await _repository.GetByIdAsync(componentId);
        if (component == null)
            throw new KeyNotFoundException($"Component with id {componentId} not found");

        return await _repository.DeleteSupplyAsync(supplyId);
    }

    public async Task<ComponentWithSuppliesDto> UpdateSuppliesAsync(int componentId, List<CreateComponentSupplyDto> supplies)
    {
        var component = await _repository.GetByIdAsync(componentId);
        if (component == null)
            throw new KeyNotFoundException($"Component with id {componentId} not found");

        // Validar referencias circulares
        var isValid = await ValidateSuppliesAsync(componentId, supplies);
        if (!isValid)
            throw new InvalidOperationException("Circular reference detected in component supplies");

        // Eliminar todos los supplies existentes
        await _repository.DeleteAllSuppliesByComponentIdAsync(componentId);

        // Crear nuevos supplies
        foreach (var supplyDto in supplies)
        {
            var supply = _mapper.Map<ComponentSupply>(supplyDto);
            supply.ComponentId = componentId;
            await _repository.CreateSupplyAsync(supply);
        }

        // Retornar componente con supplies actualizados
        var updated = await _repository.GetByIdWithSuppliesAsync(componentId);
        return _mapper.Map<ComponentWithSuppliesDto>(updated);
    }

    // Production management
    public async Task<ComponentProductionDto> CreateProductionAsync(CreateComponentProductionDto dto)
    {
        var component = await _repository.GetByIdAsync(dto.ComponentId);
        if (component == null)
            throw new KeyNotFoundException($"Component with id {dto.ComponentId} not found");

        var production = _mapper.Map<ComponentProduction>(dto);
        production.IsActive = true;
        production.CreatedAt = DateTime.UtcNow;

        var created = await _repository.CreateProductionAsync(production);
        return _mapper.Map<ComponentProductionDto>(created);
    }

    public async Task<ComponentProductionDto> UpdateProductionAsync(int id, UpdateComponentProductionDto dto)
    {
        var existing = await _repository.GetProductionByIdAsync(id);
        if (existing == null)
            throw new KeyNotFoundException($"Production with id {id} not found");

        _mapper.Map(dto, existing);
        var updated = await _repository.UpdateProductionAsync(existing);
        return _mapper.Map<ComponentProductionDto>(updated);
    }

    public async Task<IEnumerable<ComponentProductionDto>> GetProductionsByComponentAsync(int componentId)
    {
        var productions = await _repository.GetProductionsByComponentIdAsync(componentId);
        return _mapper.Map<IEnumerable<ComponentProductionDto>>(productions);
    }

    public async Task<IEnumerable<ComponentProductionDto>> GetActiveProductionsAsync(int businessId)
    {
        var productions = await _repository.GetActiveProductionsAsync(businessId);
        return _mapper.Map<IEnumerable<ComponentProductionDto>>(productions);
    }

    public async Task<IEnumerable<ComponentProductionDto>> GetExpiringProductionsAsync(int businessId, int daysAhead = 3)
    {
        var beforeDate = DateTime.UtcNow.AddDays(daysAhead);
        var productions = await _repository.GetExpiringProductionsAsync(businessId, beforeDate);
        return _mapper.Map<IEnumerable<ComponentProductionDto>>(productions);
    }

    public async Task<bool> ConsumeProductionAsync(int productionId, decimal amountConsumed)
    {
        var production = await _repository.GetProductionByIdAsync(productionId);
        if (production == null)
            throw new KeyNotFoundException($"Production with id {productionId} not found");

        if (production.ProducedAmount < amountConsumed)
            throw new InvalidOperationException($"Insufficient amount. Available: {production.ProducedAmount}, Requested: {amountConsumed}");

        return await _repository.ConsumeProductionAsync(productionId, amountConsumed);
    }

    // BOM and cost calculation
    public async Task<BOMTreeNodeDto> GetBOMTreeAsync(int componentId)
    {
        var component = await _repository.GetByIdWithSuppliesAsync(componentId);
        if (component == null)
            throw new KeyNotFoundException($"Component with id {componentId} not found");

        return await BuildBOMTreeRecursive(component);
    }

    private async Task<BOMTreeNodeDto> BuildBOMTreeRecursive(Component component, int level = 0)
    {
        var node = new BOMTreeNodeDto
        {
            Id = component.Id,
            Name = component.Name,
            Type = "component",
            Quantity = component.YieldAmount,
            Level = level,
            Children = new List<BOMTreeNodeDto>()
        };

        foreach (var supply in component.Supplies)
        {
            if (supply.ItemType == "supply")
            {
                node.Children.Add(new BOMTreeNodeDto
                {
                    Id = supply.SupplyId!.Value,
                    Name = supply.Supply?.Name ?? "Unknown Supply",
                    Type = "supply",
                    Quantity = supply.Quantity,
                    Level = level + 1,
                    Children = new List<BOMTreeNodeDto>()
                });
            }
            else if (supply.ItemType == "component" && supply.SubComponentId.HasValue)
            {
                var subComponent = await _repository.GetByIdWithSuppliesAsync(supply.SubComponentId.Value);
                if (subComponent != null)
                {
                    var childNode = await BuildBOMTreeRecursive(subComponent, level + 1);
                    childNode.Quantity = supply.Quantity;
                    node.Children.Add(childNode);
                }
            }
        }

        return node;
    }

    public async Task<decimal> CalculateTotalCostAsync(int componentId)
    {
        var component = await _repository.GetByIdWithSuppliesAsync(componentId);
        if (component == null)
            return 0;

        return await CalculateCostRecursive(component);
    }

    private async Task<decimal> CalculateCostRecursive(Component component)
    {
        decimal totalCost = 0;

        foreach (var supply in component.Supplies)
        {
            if (supply.ItemType == "supply" && supply.Supply != null)
            {
                // TODO: Calcular costo promedio del supply desde supply_entries
                // Por ahora usamos 0 como placeholder
                decimal supplyCost = 0;
                totalCost += supplyCost * supply.Quantity;
            }
            else if (supply.ItemType == "component" && supply.SubComponentId.HasValue)
            {
                var subComponent = await _repository.GetByIdWithSuppliesAsync(supply.SubComponentId.Value);
                if (subComponent != null)
                {
                    var subCost = await CalculateCostRecursive(subComponent);
                    totalCost += subCost * supply.Quantity;
                }
            }
        }

        return totalCost;
    }

    // Validation
    public async Task<bool> ValidateSuppliesAsync(int componentId, List<CreateComponentSupplyDto> supplies)
    {
        foreach (var supply in supplies.Where(s => s.ItemType == "component" && s.SubComponentId.HasValue))
        {
            var hasCircular = await _repository.HasCircularReferenceAsync(componentId, supply.SubComponentId!.Value);
            if (hasCircular)
                return false;
        }

        return true;
    }
}
