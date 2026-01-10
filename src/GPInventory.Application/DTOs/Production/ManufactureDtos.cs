using System.ComponentModel.DataAnnotations;

namespace GPInventory.Application.DTOs.Production;

public class ManufactureDto
{
    public int Id { get; set; }
    public int ProductId { get; set; }
    public DateTime Date { get; set; }
    public int Amount { get; set; }
    public int? Cost { get; set; }
    public string? Notes { get; set; }
    public int? StoreId { get; set; }
    public int? StockId { get; set; }
    public DateTime? ExpirationDate { get; set; }
    public int ProcessDoneId { get; set; }
    public int BusinessId { get; set; }
    public string Status { get; set; } = "pending";
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    
    // Navigation properties
    public ProductDto? Product { get; set; }
    public ProcessDoneDto? ProcessDone { get; set; }
    public StoreDto? Store { get; set; }
}

public class CreateManufactureDto
{
    [Required]
    public int ProductId { get; set; }
    
    [Required]
    public int ProcessDoneId { get; set; }
    
    [Required]
    public int BusinessId { get; set; }
    
    [Required]
    [Range(1, int.MaxValue, ErrorMessage = "La cantidad debe ser mayor a 0")]
    public int Amount { get; set; }
    
    public int? Cost { get; set; }
    
    [StringLength(500)]
    public string? Notes { get; set; }
    
    public DateTime? ExpirationDate { get; set; }
}

public class UpdateManufactureDto
{
    [Range(1, int.MaxValue, ErrorMessage = "La cantidad debe ser mayor a 0")]
    public int? Amount { get; set; }
    
    public int? Cost { get; set; }
    
    [StringLength(500)]
    public string? Notes { get; set; }
    
    public DateTime? ExpirationDate { get; set; }
    
    [StringLength(50)]
    public string? Status { get; set; }
    
    public int? StoreId { get; set; }
}

public class ProcessDoneSummaryDto
{
    public int ProcessDoneId { get; set; }
    public int ProcessId { get; set; }
    public string? ProcessName { get; set; }
    public DateTime CompletedAt { get; set; }
    public string? Notes { get; set; }
    public int TotalBatches { get; set; }
    public int TotalAmount { get; set; }
    public List<ManufactureDto> Batches { get; set; } = new List<ManufactureDto>();
}
