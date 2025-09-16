namespace GPInventory.Application.DTOs.Production;

public class ProcessDto
{
    public int Id { get; set; }
    public int ProductId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public int ProductionTime { get; set; }
    public int TimeUnitId { get; set; }
    public int StoreId { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public bool IsActive { get; set; }
    
    // Navigation properties
    public ProductDto? Product { get; set; }
    public TimeUnitDto? TimeUnit { get; set; }
    public StoreDto? Store { get; set; }
    
    // Collection properties
    public List<ProcessSupplyDto> ProcessSupplies { get; set; } = new();
}

public class CreateProcessDto
{
    public int ProductId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public int ProductionTime { get; set; }
    public int TimeUnitId { get; set; }
    public int StoreId { get; set; }
    public List<CreateProcessSupplyDto> ProcessSupplies { get; set; } = new();
}

public class UpdateProcessDto
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public int ProductionTime { get; set; }
    public int TimeUnitId { get; set; }
    public List<CreateProcessSupplyDto> ProcessSupplies { get; set; } = new();
}

public class ProcessSupplyDto
{
    public int Id { get; set; }
    public int ProcessId { get; set; }
    public int SupplyId { get; set; }
    public int Order { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public bool IsActive { get; set; }
    
    // Navigation properties
    public SupplyDto? Supply { get; set; }
}

public class CreateProcessSupplyDto
{
    public int SupplyId { get; set; }
    public int Order { get; set; }
}

public class TimeUnitDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public bool IsActive { get; set; }
}

public class ProductDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public decimal Price { get; set; }
    // Otras propiedades según la entidad Product existente
}

// ProcessDone DTOs
public class ProcessDoneDto
{
    public int Id { get; set; }
    public int ProcessId { get; set; }
    public int Stage { get; set; }
    public DateTime? StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public int? StockId { get; set; }
    public int Amount { get; set; }
    public decimal Cost { get; set; }
    public DateTime CompletedAt { get; set; }
    public string? Notes { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public bool IsActive { get; set; }
    
    // Navigation properties
    public ProcessDto? Process { get; set; }
    public List<SupplyUsageDto> SupplyUsages { get; set; } = new();
}

public class CreateProcessDoneDto
{
    public int ProcessId { get; set; }
    public int Amount { get; set; }
    public int Stage { get; set; } = 0;
    public DateTime? StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public string? Notes { get; set; }
    public List<CreateSupplyUsageDto> SupplyUsages { get; set; } = new();
}

public class SupplyUsageDto
{
    public int Id { get; set; }
    public int SupplyId { get; set; }
    public decimal QuantityUsed { get; set; }
    public decimal UnitCost { get; set; }
    public decimal TotalCost { get; set; }
    public string? SupplyName { get; set; }
}

public class CreateSupplyUsageDto
{
    public int SupplyId { get; set; }
    public decimal QuantityUsed { get; set; }
    public decimal UnitCost { get; set; } = 0;
}
