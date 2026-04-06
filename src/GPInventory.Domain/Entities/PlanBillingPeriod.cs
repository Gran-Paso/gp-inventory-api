using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace GPInventory.Domain.Entities;

/// <summary>
/// Período de facturación mensual de un plan de cliente.
/// Representa un mes concreto dentro de la vida del plan:
/// cuántas sesiones le corresponden, cuántas asistió y si pagó o no.
/// </summary>
[Table("plan_billing_period")]
public class PlanBillingPeriod
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
    [ForeignKey(nameof(ClientServicePlan))]
    [Column("client_service_plan_id")]
    public int ClientServicePlanId { get; set; }

    /// <summary>Desnormalizado para queries rápidos sin JOIN</summary>
    [Required]
    [ForeignKey(nameof(ServiceClient))]
    [Column("service_client_id")]
    public int ServiceClientId { get; set; }

    /// <summary>Año del período, ej: 2026</summary>
    [Required]
    [Column("billing_year")]
    public int BillingYear { get; set; }

    /// <summary>Mes del período 1-12</summary>
    [Required]
    [Column("billing_month")]
    public int BillingMonth { get; set; }

    [Required]
    [Column("period_start_date", TypeName = "date")]
    public DateTime PeriodStartDate { get; set; }

    [Required]
    [Column("period_end_date", TypeName = "date")]
    public DateTime PeriodEndDate { get; set; }

    /// <summary>Cupos incluidos en el plan para este mes</summary>
    [Required]
    [Column("sessions_allowed")]
    public int SessionsAllowed { get; set; }

    /// <summary>Sesiones confirmadas/asistidas en este período</summary>
    [Required]
    [Column("sessions_attended")]
    public int SessionsAttended { get; set; } = 0;

    /// <summary>Sesiones agendadas (no confirmadas aún) en este período</summary>
    [Required]
    [Column("sessions_reserved")]
    public int SessionsReserved { get; set; } = 0;

    /// <summary>pending | paid | overdue | waived</summary>
    [Required]
    [Column("status")]
    public string Status { get; set; } = "pending";

    [Required]
    [Column("amount_due", TypeName = "decimal(12,2)")]
    public decimal AmountDue { get; set; }

    /// <summary>Transacción que pagó este período (NULL si aún no está pagado)</summary>
    [ForeignKey(nameof(PlanTransaction))]
    [Column("plan_transaction_id")]
    public int? PlanTransactionId { get; set; }

    [Column("paid_at")]
    public DateTime? PaidAt { get; set; }

    [Required]
    [Column("due_date", TypeName = "date")]
    public DateTime DueDate { get; set; }

    [Column("notes", TypeName = "text")]
    public string? Notes { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [Column("updated_at")]
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    // ── Computed (not mapped) ─────────────────────────────────────────────────

    /// <summary>Sesiones comprometidas (asistidas + agendadas) respecto al total</summary>
    [NotMapped]
    public double CommittedPercent =>
        SessionsAllowed > 0
            ? (double)(SessionsAttended + SessionsReserved) / SessionsAllowed * 100
            : 0;

    // ── Navigation properties ─────────────────────────────────────────────────

    public virtual Business? Business { get; set; }
    public virtual Store? Store { get; set; }
    public virtual ClientServicePlan? ClientServicePlan { get; set; }
    public virtual ServiceClient? ServiceClient { get; set; }
    public virtual PlanTransaction? PlanTransaction { get; set; }
    public virtual ICollection<ServiceAttendance> Attendances { get; set; } = new List<ServiceAttendance>();
}
