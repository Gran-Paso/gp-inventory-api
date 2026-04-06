using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using GPInventory.Domain.Enums;

namespace GPInventory.Domain.Entities;

/// <summary>
/// Registro financiero de la compra de un plan de servicio.
/// Tabla separada de service_sale: los planes no son ventas directas de servicio.
/// </summary>
[Table("plan_transaction")]
public class PlanTransaction
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

    /// <summary>
    /// Plan del cliente al que corresponde esta transacción
    /// </summary>
    [Required]
    [ForeignKey(nameof(ClientServicePlan))]
    [Column("client_service_plan_id")]
    public int ClientServicePlanId { get; set; }

    [Required]
    [ForeignKey(nameof(ServiceClient))]
    [Column("service_client_id")]
    public int ServiceClientId { get; set; }

    [Required]
    [ForeignKey(nameof(ServicePlan))]
    [Column("service_plan_id")]
    public int ServicePlanId { get; set; }

    /// <summary>
    /// Usuario que registró la transacción
    /// </summary>
    [Required]
    [Column("user_id")]
    public int UserId { get; set; }

    [Required]
    [Column("amount", TypeName = "decimal(12,2)")]
    public decimal Amount { get; set; }

    [Column("payment_method_id")]
    public int? PaymentMethodId { get; set; }

    [Required]
    [Column("document_type")]
    public DocumentType DocumentType { get; set; } = DocumentType.None;

    [Required]
    [Column("transaction_date")]
    public DateTime TransactionDate { get; set; }

    [Column("notes", TypeName = "text")]
    public string? Notes { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [Column("updated_at")]
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    // Navigation properties
    public virtual Business? Business { get; set; }
    public virtual Store? Store { get; set; }
    public virtual ClientServicePlan? ClientServicePlan { get; set; }
    public virtual ServiceClient? ServiceClient { get; set; }
    public virtual ServicePlan? ServicePlan { get; set; }
}
