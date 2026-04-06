using System.ComponentModel.DataAnnotations;
using GPInventory.Domain.Enums;

namespace GPInventory.Application.DTOs.Services;

// ===== SERVICE CATEGORY DTOs =====

public class CreateServiceCategoryDto
{
    [Required]
    [StringLength(255)]
    public string Name { get; set; } = string.Empty;

    [StringLength(500)]
    public string? Description { get; set; }

    [Required]
    public int BusinessId { get; set; }
}

public class UpdateServiceCategoryDto
{
    [Required]
    [StringLength(255)]
    public string Name { get; set; } = string.Empty;

    [StringLength(500)]
    public string? Description { get; set; }

    public bool Active { get; set; } = true;
}

public class ServiceCategoryDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public int BusinessId { get; set; }
    public bool Active { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

// ===== SERVICE DTOs =====

public class CreateServiceDto
{
    [Required]
    [StringLength(255)]
    public string Name { get; set; } = string.Empty;

    [Required]
    public int CategoryId { get; set; }

    [Required]
    public int BusinessId { get; set; }

    public int? StoreId { get; set; }

    [Required]
    [Range(0, double.MaxValue)]
    public decimal BasePrice { get; set; }

    public int? DurationMinutes { get; set; }

    [StringLength(1000)]
    public string? Description { get; set; }

    [Required]
    public PricingType PricingType { get; set; } = PricingType.Fixed;

    [Required]
    public bool IsTaxable { get; set; } = true;

    public bool Active { get; set; } = true;
}

public class UpdateServiceDto
{
    [Required]
    [StringLength(255)]
    public string Name { get; set; } = string.Empty;

    [Required]
    public int CategoryId { get; set; }

    public int? StoreId { get; set; }

    [Required]
    [Range(0, double.MaxValue)]
    public decimal BasePrice { get; set; }

    public int? DurationMinutes { get; set; }

    [StringLength(1000)]
    public string? Description { get; set; }

    [Required]
    public PricingType PricingType { get; set; }

    [Required]
    public bool IsTaxable { get; set; }

    public bool Active { get; set; }
}

public class ServiceDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public int CategoryId { get; set; }
    public int BusinessId { get; set; }
    public int? StoreId { get; set; }
    public decimal BasePrice { get; set; }
    public int? DurationMinutes { get; set; }
    public string? Description { get; set; }
    public PricingType PricingType { get; set; }
    public bool IsTaxable { get; set; }
    public bool Active { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    // Navigation
    public ServiceCategoryDto? Category { get; set; }
}

// ===== SERVICE CLIENT DTOs =====

public class CreateServiceClientDto
{
    [Required]
    [StringLength(255)]
    public string Name { get; set; } = string.Empty;

    [StringLength(20)]
    public string? Rut { get; set; }

    [EmailAddress]
    [StringLength(255)]
    public string? Email { get; set; }

    [Phone]
    [StringLength(50)]
    public string? Phone { get; set; }

    [StringLength(500)]
    public string? Address { get; set; }

    [StringLength(100)]
    public string? City { get; set; }

    [StringLength(255)]
    public string? ContactPerson { get; set; }

    [Required]
    public ClientType ClientType { get; set; } = ClientType.Individual;

    [StringLength(50)]
    public string? Segment { get; set; }

    public string? Tags { get; set; }

    public string? Notes { get; set; }

    [Required]
    public int BusinessId { get; set; }

    public int? StoreId { get; set; }

    public int? CreatedByUserId { get; set; }

    /// <summary>Si se está registrando como sub-cliente, ID del cliente raíz (pagador)</summary>
    public int? ParentClientId { get; set; }

    /// <summary>ID del tipo de relación con el cliente raíz</summary>
    public int? RelationshipTypeId { get; set; }

    /// <summary>Fecha de nacimiento del sub-cliente (opcional)</summary>
    public DateTime? BirthDate { get; set; }
}

public class UpdateServiceClientDto
{
    [Required]
    [StringLength(255)]
    public string Name { get; set; } = string.Empty;

    [StringLength(20)]
    public string? Rut { get; set; }

    [EmailAddress]
    [StringLength(255)]
    public string? Email { get; set; }

    [Phone]
    [StringLength(50)]
    public string? Phone { get; set; }

    [StringLength(500)]
    public string? Address { get; set; }

    [StringLength(100)]
    public string? City { get; set; }

    [StringLength(255)]
    public string? ContactPerson { get; set; }

    [Required]
    public ClientType ClientType { get; set; }

    [StringLength(50)]
    public string? Segment { get; set; }

    public string? Tags { get; set; }

    public string? Notes { get; set; }

    public bool Active { get; set; } = true;

    public int? RelationshipTypeId { get; set; }

    public DateTime? BirthDate { get; set; }
}

public class ServiceClientDto
{
    public int Id { get; set; }
    public int BusinessId { get; set; }
    public int? StoreId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Rut { get; set; }
    public string? Email { get; set; }
    public string? Phone { get; set; }
    public string? Address { get; set; }
    public string? City { get; set; }
    public string? ContactPerson { get; set; }
    public ClientType ClientType { get; set; }
    public string? Segment { get; set; }
    public string? Tags { get; set; }
    public string? Notes { get; set; }
    public bool Active { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public int? CreatedByUserId { get; set; }

    // Additional info
    public int TotalPurchases { get; set; } = 0;
    public decimal TotalRevenue { get; set; } = 0;
    public DateTime? LastPurchaseDate { get; set; }

    // Sub-clientes
    /// <summary>ID del cliente raíz (null si es cliente principal)</summary>
    public int? ParentClientId { get; set; }
    /// <summary>Nombre del cliente raíz (null si es cliente principal)</summary>
    public string? ParentClientName { get; set; }
    /// <summary>ID del tipo de relación con el cliente raíz</summary>
    public int? RelationshipTypeId { get; set; }
    /// <summary>Etiqueta del tipo de relación (e.g. "Hijo/a", "Pareja")</summary>
    public string? RelationshipLabel { get; set; }
    /// <summary>Fecha de nacimiento</summary>
    public DateTime? BirthDate { get; set; }
    /// <summary>True cuando tiene padre (es un sub-cliente/beneficiario)</summary>
    public bool IsSubClient => ParentClientId.HasValue;
    /// <summary>Lista de sub-clientes directos de este cliente raíz</summary>
    public List<SubClientSummaryDto> SubClients { get; set; } = new();
}

/// <summary>
/// Vista compacta de un sub-cliente, incluida dentro de ServiceClientDto
/// </summary>
public class SubClientSummaryDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public int? RelationshipTypeId { get; set; }
    public string? RelationshipLabel { get; set; }
    public DateTime? BirthDate { get; set; }
    public bool Active { get; set; }
    public string? Rut { get; set; }
    public string? Phone { get; set; }
    public string? Notes { get; set; }
}

/// <summary>
/// Tipo de relación entre cliente raíz y sub-cliente
/// </summary>
public class RelationshipTypeDto
{
    public int Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
    public string? Description { get; set; }
    public int SortOrder { get; set; }
}

// ===== SERVICE SALE DTOs =====

public class CreateServiceSaleDto
{
    [Required]
    public int BusinessId { get; set; }

    public int? StoreId { get; set; }

    [Required]
    public int UserId { get; set; }

    // Cliente registrado o ad-hoc
    public int? ServiceClientId { get; set; }

    [StringLength(255)]
    public string? ClientName { get; set; }

    [StringLength(20)]
    public string? ClientRut { get; set; }

    [EmailAddress]
    [StringLength(255)]
    public string? ClientEmail { get; set; }

    [Phone]
    [StringLength(50)]
    public string? ClientPhone { get; set; }

    [Required]
    public DateTime Date { get; set; }

    public DateTime? ScheduledDate { get; set; }

    [Required]
    public DocumentType DocumentType { get; set; } = DocumentType.None;

    [Required]
    public PaymentType PaymentType { get; set; } = PaymentType.Cash;

    public int? InstallmentsCount { get; set; }

    public DateTime? PaymentStartDate { get; set; }

    // Items de la venta
    public List<CreateServiceSaleItemDto> Items { get; set; } = new();
}

public class CreateServiceSaleItemDto
{
    [Required]
    public int ServiceId { get; set; }

    public decimal? Price { get; set; }

    public string? Notes { get; set; }
}

public class CompleteServiceSaleDto
{
    /// <summary>
    /// Cantidad de inscripciones (requerido solo para servicios PerEnrollment)
    /// </summary>
    public int? EnrollmentCount { get; set; }

    public DateTime? CompletedDate { get; set; }

    // Supplies consumidos durante la ejecución
    public List<ServiceSaleSupplyDto> Supplies { get; set; } = new();
}

public class ServiceSaleSupplyDto
{
    [Required]
    public int SupplyId { get; set; }

    [Required]
    [Range(0.01, double.MaxValue)]
    public decimal Quantity { get; set; }

    public string? Notes { get; set; }
}

public class ServiceSaleDto
{
    public int Id { get; set; }
    public int BusinessId { get; set; }
    public int? StoreId { get; set; }
    public int UserId { get; set; }
    public int? ServiceClientId { get; set; }
    public string? ClientName { get; set; }
    public string? ClientRut { get; set; }
    public string? ClientEmail { get; set; }
    public string? ClientPhone { get; set; }
    public int? EnrollmentCount { get; set; }
    public decimal? AmountNet { get; set; }
    public decimal? AmountIva { get; set; }
    public decimal? TotalAmount { get; set; }
    public ServiceSaleStatus Status { get; set; }
    public DateTime Date { get; set; }
    public DateTime? ScheduledDate { get; set; }
    public DateTime? CompletedDate { get; set; }
    public DocumentType DocumentType { get; set; }
    public PaymentType PaymentType { get; set; }
    public int? InstallmentsCount { get; set; }
    public DateTime? PaymentStartDate { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    // Navigation
    public ServiceClientDto? ServiceClient { get; set; }
    public List<ServiceSaleItemDto> Items { get; set; } = new();
    public List<ServiceSaleSupplyDto> Supplies { get; set; } = new();
}

public class ServiceSaleItemDto
{
    public int Id { get; set; }
    public int ServiceSaleId { get; set; }
    public int ServiceId { get; set; }
    public decimal? Price { get; set; }
    public bool IsCompleted { get; set; }
    public string? Notes { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    // Navigation
    public ServiceDto? Service { get; set; }
}
