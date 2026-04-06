using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using GPInventory.Domain.Enums;

namespace GPInventory.Domain.Entities;

/// <summary>
/// Venta de servicio a un cliente
/// </summary>
[Table("service_sale")]
public class ServiceSale
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Required]
    [ForeignKey(nameof(Business))]
    [Column("business_id")]
    public int BusinessId { get; set; }

    [ForeignKey(nameof(Store))]
    [Column("store_id")]
    public int? StoreId { get; set; }

    [Required]
    [ForeignKey(nameof(User))]
    [Column("user_id")]
    public int UserId { get; set; }

    /// <summary>
    /// Cliente registrado (opcional). Si NULL, usar campos client_name, client_rut, etc.
    /// </summary>
    [ForeignKey(nameof(ServiceClient))]
    [Column("service_client_id")]
    public int? ServiceClientId { get; set; }

    /// <summary>
    /// Tipo de venta: servicio normal, plan/membresía, o mixto
    /// </summary>
    [Column("sale_type")]
    [StringLength(20)]
    public string SaleType { get; set; } = "service";

    /// <summary>
    /// Indica si esta venta generó un plan activo (membresía)
    /// </summary>
    [Column("is_plan_purchase")]
    public bool IsPlanPurchase { get; set; } = false;

    // Campos ad-hoc del cliente (se usan si ServiceClientId es NULL)
    [StringLength(255)]
    [Column("client_name")]
    public string? ClientName { get; set; }

    [StringLength(20)]
    [Column("client_rut")]
    public string? ClientRut { get; set; }

    [StringLength(255)]
    [Column("client_email")]
    public string? ClientEmail { get; set; }

    [StringLength(50)]
    [Column("client_phone")]
    public string? ClientPhone { get; set; }

    /// <summary>
    /// Cantidad de inscripciones. Requerido solo si el servicio tiene pricing_type='per_enrollment'
    /// </summary>
    [Column("enrollment_count")]
    public int? EnrollmentCount { get; set; }

    /// <summary>
    /// Monto neto sin IVA
    /// </summary>
    [Column("amount_net", TypeName = "decimal(12,2)")]
    public decimal? AmountNet { get; set; }

    /// <summary>
    /// Monto del IVA (19%)
    /// </summary>
    [Column("amount_iva", TypeName = "decimal(12,2)")]
    public decimal? AmountIva { get; set; }

    /// <summary>
    /// Monto total final (neto + IVA)
    /// </summary>
    [Column("total_amount", TypeName = "decimal(12,2)")]
    public decimal? TotalAmount { get; set; }

    [Required]
    [Column("status")]
    public ServiceSaleStatus Status { get; set; } = ServiceSaleStatus.Pending;

    [Required]
    [Column("date", TypeName = "date")]
    public DateTime Date { get; set; }

    [Column("scheduled_date")]
    public DateTime? ScheduledDate { get; set; }

    [Column("completed_date")]
    public DateTime? CompletedDate { get; set; }

    [Required]
    [Column("document_type")]
    public DocumentType DocumentType { get; set; } = DocumentType.None;

    [Required]
    [Column("payment_type")]
    public int PaymentType { get; set; } = 1; // 1=Cash, 2=Installments

    [Column("installments_count")]
    public int? InstallmentsCount { get; set; }

    [Column("payment_start_date", TypeName = "date")]
    public DateTime? PaymentStartDate { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [Column("updated_at")]
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    // Navigation properties
    public virtual Business? Business { get; set; }
    public virtual Store? Store { get; set; }
    public virtual User? User { get; set; }
    public virtual ServiceClient? ServiceClient { get; set; }
    public virtual ICollection<ServiceSaleItem>? Items { get; set; }
    public virtual ICollection<ServiceSaleSupply>? Supplies { get; set; }
}
