using GPInventory.Application.DTOs.Production;

namespace GPInventory.Application.DTOs.Components;

public class ComponentDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public int BusinessId { get; set; }
    public int? StoreId { get; set; }
    public int UnitMeasureId { get; set; }
    public string? UnitMeasureName { get; set; }
    public string? UnitMeasureSymbol { get; set; }
    public int? PreparationTime { get; set; }
    public int? TimeUnitId { get; set; }
    public string? TimeUnitName { get; set; }
    public decimal YieldAmount { get; set; }
    public bool Active { get; set; }
    public int? SupplyCategoryId { get; set; }
    public SupplyCategoryDto? SupplyCategory { get; set; }
    public int ComponentUsageCount { get; set; } = 0;
    public int ProcessUsageCount { get; set; } = 0;
    public int UsageCount => ComponentUsageCount + ProcessUsageCount;
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}

public class ComponentSupplyDto
{
    public int Id { get; set; }
    public int ComponentId { get; set; }
    public int? SupplyId { get; set; }
    public string? SupplyName { get; set; }
    public string? SupplyUnitSymbol { get; set; }
    public int? SubComponentId { get; set; }
    public string? SubComponentName { get; set; }
    public string? SubComponentUnitSymbol { get; set; }
    public decimal Quantity { get; set; }
    public int Order { get; set; }
    public string ItemType { get; set; } = "supply";
    public bool IsOptional { get; set; }
}

public class ComponentWithSuppliesDto : ComponentDto
{
    public List<ComponentSupplyDto> Supplies { get; set; } = new();
    public decimal? TotalCost { get; set; }
}

public class CreateComponentDto
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public int BusinessId { get; set; }
    public int? StoreId { get; set; }
    public int UnitMeasureId { get; set; }
    public int? PreparationTime { get; set; }
    public int? TimeUnitId { get; set; }
    public decimal YieldAmount { get; set; }
    public int? SupplyCategoryId { get; set; }
    public List<CreateComponentSupplyDto> Supplies { get; set; } = new();
}

public class CreateComponentSupplyDto
{
    public int? SupplyId { get; set; }
    public int? SubComponentId { get; set; }
    public decimal Quantity { get; set; }
    public int Order { get; set; }
    public string ItemType { get; set; } = "supply";
    public bool IsOptional { get; set; }
}

public class UpdateComponentDto
{
    public string? Name { get; set; }
    public string? Description { get; set; }
    public int? StoreId { get; set; }
    public int? UnitMeasureId { get; set; }
    public int? PreparationTime { get; set; }
    public int? TimeUnitId { get; set; }
    public decimal? YieldAmount { get; set; }
    public int? SupplyCategoryId { get; set; }
    public bool? Active { get; set; }
    public List<CreateComponentSupplyDto>? Supplies { get; set; }
}

public class ComponentProductionDto
{
    public int Id { get; set; }
    public int ComponentId { get; set; }
    public string? ComponentName { get; set; }
    public decimal ProducedAmount { get; set; }
    public DateTime ProductionDate { get; set; }
    public DateTime? ExpirationDate { get; set; }
    public string? BatchNumber { get; set; }
    public decimal? Cost { get; set; }
    public string? Notes { get; set; }
    public bool Active { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class CreateComponentProductionDto
{
    public int ComponentId { get; set; }
    public decimal ProducedAmount { get; set; }
    public DateTime ProductionDate { get; set; }
    public DateTime? ExpirationDate { get; set; }
    public string? BatchNumber { get; set; }
    public decimal? Cost { get; set; }
    public string? Notes { get; set; }
}

public class UpdateComponentProductionDto
{
    public decimal? ProducedAmount { get; set; }
    public DateTime? ExpirationDate { get; set; }
    public decimal? Cost { get; set; }
    public string? Notes { get; set; }
    public bool? Active { get; set; }
}

public class BOMTreeNodeDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Type { get; set; } = "supply"; // 'supply' | 'component'
    public decimal Quantity { get; set; }
    public int Level { get; set; }
    public decimal? Cost { get; set; }
    public List<BOMTreeNodeDto> Children { get; set; } = new();
}
