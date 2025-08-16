namespace GPInventory.Application.DTOs.Production;

public class SupplyEntryDto
{
    public int Id { get; set; }
    public decimal UnitCost { get; set; }
    public decimal Amount { get; set; }
    public int ProviderId { get; set; }
    public int SupplyId { get; set; }
    public int? ProcessDoneId { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    // Navigation properties
    public ProviderDto? Provider { get; set; }
    public SupplyDto? Supply { get; set; }
    public ProcessDoneDto? ProcessDone { get; set; }
}

public class CreateSupplyEntryDto
{
    public decimal UnitCost { get; set; }
    public decimal Amount { get; set; }
    public int ProviderId { get; set; }
    public int SupplyId { get; set; }
    public int? ProcessDoneId { get; set; }
}

public class UpdateSupplyEntryDto
{
    public decimal UnitCost { get; set; }
    public decimal Amount { get; set; }
    public int ProviderId { get; set; }
}

public class SupplyStockDto
{
    public int SupplyId { get; set; }
    public string SupplyName { get; set; } = string.Empty;
    public decimal CurrentStock { get; set; }
    public string UnitMeasureName { get; set; } = string.Empty;
    public string? UnitMeasureSymbol { get; set; }
    public decimal TotalIncoming { get; set; }
    public decimal TotalOutgoing { get; set; }
}

public class ProviderDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public int? StoreId { get; set; }
}
