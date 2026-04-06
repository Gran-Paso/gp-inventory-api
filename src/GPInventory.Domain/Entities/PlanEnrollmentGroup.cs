using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using GPInventory.Domain.Enums;

namespace GPInventory.Domain.Entities;

/// <summary>
/// Matrícula grupal (combo): agrupa N planes de distintos beneficiarios
/// bajo un único contrato con precio negociado.
/// El pagador (payer_client_id) es el responsable del cobro.
/// </summary>
[Table("plan_enrollment_group")]
public class PlanEnrollmentGroup
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

    /// <summary>Cliente raíz que firma el contrato y paga el combo</summary>
    [Required]
    [ForeignKey(nameof(PayerClient))]
    [Column("payer_client_id")]
    public int PayerClientId { get; set; }

    [Required]
    [Column("billing_frequency")]
    public BillingFrequency BillingFrequency { get; set; } = BillingFrequency.Monthly;

    [Required]
    [Column("payment_timing")]
    public PlanPaymentTiming PaymentTiming { get; set; } = PlanPaymentTiming.Deferred;

    [ForeignKey(nameof(PaymentMethod))]
    [Column("payment_method_id")]
    public int? PaymentMethodId { get; set; }

    [Required]
    [Column("contract_months")]
    public int ContractMonths { get; set; } = 12;

    [Required]
    [Column("start_date", TypeName = "date")]
    public DateTime StartDate { get; set; }

    [Required]
    [Column("end_date", TypeName = "date")]
    public DateTime EndDate { get; set; }

    /// <summary>
    /// Precio combo acordado por período de facturación.
    /// Puede ser menor que la suma de los planes individuales.
    /// </summary>
    [Required]
    [Column("total_amount_per_period", TypeName = "decimal(12,2)")]
    public decimal TotalAmountPerPeriod { get; set; }

    [Required]
    [StringLength(20)]
    [Column("status")]
    public string Status { get; set; } = "active";

    [Column("notes", TypeName = "text")]
    public string? Notes { get; set; }

    [ForeignKey(nameof(CreatedByUser))]
    [Column("created_by_user_id")]
    public int? CreatedByUserId { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [Column("updated_at")]
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    // Navigation
    public virtual Business? Business { get; set; }
    public virtual Store? Store { get; set; }
    public virtual ServiceClient? PayerClient { get; set; }
    public virtual PaymentMethod? PaymentMethod { get; set; }
    public virtual User? CreatedByUser { get; set; }

    /// <summary>Planes individuales que forman parte de este combo</summary>
    public virtual ICollection<ClientServicePlan> MemberPlans { get; set; } = [];
}
