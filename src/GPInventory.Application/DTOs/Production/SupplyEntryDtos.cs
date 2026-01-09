using System.ComponentModel.DataAnnotations;
using GPInventory.Application.DTOs.Expenses;

namespace GPInventory.Application.DTOs.Production;

public class SupplyEntryDto
{
    public int Id { get; set; }
    public decimal UnitCost { get; set; }
    public decimal Amount { get; set; }
    public string? Tag { get; set; }
    public int ProviderId { get; set; }
    public int SupplyId { get; set; }
    public int? ProcessDoneId { get; set; }
    public int? ReferenceToSupplyEntry { get; set; }
    public bool IsActive { get; set; }
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
    public string? Tag { get; set; }
    public int ProviderId { get; set; }
    public int SupplyId { get; set; }
    public int? ProcessDoneId { get; set; }
    public int? ReferenceToSupplyEntry { get; set; }
    
    // Payment Plan fields (optional - for financing)
    public int? PaymentTypeId { get; set; }
    public int? InstallmentsCount { get; set; }
    public bool? ExpressedInUf { get; set; }
    public int? BankEntityId { get; set; }
    public DateTime? PaymentStartDate { get; set; }
}

public class UpdateSupplyEntryDto
{
    public decimal UnitCost { get; set; }
    public decimal Amount { get; set; }
    public string? Tag { get; set; }
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
    public int MinimumStock { get; set; }
    public Domain.Enums.StockStatus StockStatus { get; set; }
}

public class CreateProviderDto
{
    [Required]
    [StringLength(255)]
    public string Name { get; set; } = string.Empty;

    [Required]
    public int BusinessId { get; set; }

    public int? StoreId { get; set; }

    public int? Contact { get; set; }

    [StringLength(500)]
    public string? Address { get; set; }

    [StringLength(200)]
    [EmailAddress]
    public string? Mail { get; set; }

    [StringLength(20)]
    public string? Prefix { get; set; }

    public bool Active { get; set; } = true;
}

public class UpdateProviderDto
{
    [Required]
    [StringLength(255)]
    public string Name { get; set; } = string.Empty;

    public int? StoreId { get; set; }

    public int? Contact { get; set; }

    [StringLength(500)]
    public string? Address { get; set; }

    [StringLength(200)]
    [EmailAddress]
    public string? Mail { get; set; }

    [StringLength(20)]
    public string? Prefix { get; set; }

    public bool Active { get; set; } = true;
}

public class ProviderDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public int BusinessId { get; set; }
    public int? StoreId { get; set; }
    public int? Contact { get; set; }
    public string? Address { get; set; }
    public string? Mail { get; set; }
    public string? Prefix { get; set; }
    public bool Active { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    // Navigation properties (optional, for detailed responses)
    public BusinessDto? Business { get; set; }
    public StoreDto? Store { get; set; }
}
