using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using GPInventory.Domain.Enums;

namespace GPInventory.Domain.Entities;

/// <summary>
/// Plan activo de un cliente con control de clases consumidas
/// e ingresos diferidos (deferred revenue)
/// </summary>
[Table("client_service_plan")]
public class ClientServicePlan
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
    [ForeignKey(nameof(ServiceClient))]
    [Column("service_client_id")]
    public int ServiceClientId { get; set; }

    [Required]
    [ForeignKey(nameof(ServicePlan))]
    [Column("service_plan_id")]
    public int ServicePlanId { get; set; }

    /// <summary>
    /// Venta directa asociada (legacy / servicios directos).
    /// Para compras de plan la transacción está en PlanTransaction.
    /// </summary>
    [ForeignKey(nameof(ServiceSale))]
    [Column("service_sale_id")]
    public int? ServiceSaleId { get; set; }

    /// <summary>
    /// Matrícula grupal (combo). Null = plan individual sin grupo.
    /// </summary>
    [ForeignKey(nameof(EnrollmentGroup))]
    [Column("enrollment_group_id")]
    public int? EnrollmentGroupId { get; set; }

    /// <summary>
    /// Precio pactado por período para este miembro (solo en combos descontados).
    /// Null = usar el precio estándar del plan vinculado.
    /// </summary>
    [Column("custom_amount_per_period", TypeName = "decimal(12,2)")]
    public decimal? CustomAmountPerPeriod { get; set; }

    [Required]
    [Column("start_date", TypeName = "date")]
    public DateTime StartDate { get; set; }

    [Required]
    [Column("end_date", TypeName = "date")]
    public DateTime EndDate { get; set; }

    /// <summary>
    /// Total de clases incluidas en el plan
    /// </summary>
    [Required]
    [Column("total_classes")]
    public int TotalClasses { get; set; }

    /// <summary>
    /// Clases ya consumidas/usadas
    /// </summary>
    [Required]
    [Column("classes_used")]
    public int ClassesUsed { get; set; } = 0;

    /// <summary>
    /// Cupos comprometidos en sesiones futuras agendadas (no asistidos aún)
    /// </summary>
    [Required]
    [Column("classes_reserved")]
    public int ClassesReserved { get; set; } = 0;

    /// <summary>
    /// Clases restantes = total - used - reserved (columna generada en DB)
    /// </summary>
    [Column("classes_remaining")]
    [DatabaseGenerated(DatabaseGeneratedOption.Computed)]
    public int ClassesRemaining { get; private set; }

    [Required]
    [Column("status")]
    public ClientServicePlanStatus Status { get; set; } = ClientServicePlanStatus.Active;

    // ── Suscripción ────────────────────────────────────────────────────────

    /// <summary>Frecuencia de facturación: mensual, trimestral, semestral, anual.</summary>
    [Column("billing_frequency")]
    public BillingFrequency BillingFrequency { get; set; } = BillingFrequency.Monthly;

    /// <summary>Si el cliente pre-paga el período o paga al cierre.</summary>
    [Column("payment_timing")]
    public PlanPaymentTiming PaymentTiming { get; set; } = PlanPaymentTiming.Deferred;

    /// <summary>Método de pago asignado para este cliente/plan.</summary>
    [Column("payment_method_id")]
    public int? PaymentMethodId { get; set; }

    /// <summary>Duración total del contrato en meses (ej: 12 = un año completo).</summary>
    [Column("contract_months")]
    public int ContractMonths { get; set; } = 12;

    [Column("cancelled_at")]
    public DateTime? CancelledAt { get; set; }

    [Column("cancellation_reason", TypeName = "text")]
    public string? CancellationReason { get; set; }

    /// <summary>
    /// Monto total pagado por el plan
    /// </summary>
    [Required]
    [Column("total_paid", TypeName = "decimal(12,2)")]
    public decimal TotalPaid { get; set; }

    /// <summary>
    /// Ingreso ya reconocido contablemente (proporcional a clases usadas)
    /// </summary>
    [Required]
    [Column("revenue_recognized", TypeName = "decimal(12,2)")]
    public decimal RevenueRecognized { get; set; } = 0;

    /// <summary>
    /// Ingreso diferido/pendiente (calculado en DB como columna generada)
    /// </summary>
    [Column("deferred_revenue", TypeName = "decimal(12,2)")]
    [DatabaseGenerated(DatabaseGeneratedOption.Computed)]
    public decimal DeferredRevenue { get; private set; }

    /// <summary>
    /// Fecha hasta la cual el plan está congelado (opcional)
    /// </summary>
    [Column("frozen_until", TypeName = "date")]
    public DateTime? FrozenUntil { get; set; }

    [Column("notes", TypeName = "text")]
    public string? Notes { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [Column("updated_at")]
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    // Navigation properties
    public virtual Business? Business { get; set; }
    public virtual Store? Store { get; set; }
    public virtual ServiceClient? ServiceClient { get; set; }
    public virtual ServicePlan? ServicePlan { get; set; }
    public virtual ServiceSale? ServiceSale { get; set; }
    public virtual PlanEnrollmentGroup? EnrollmentGroup { get; set; }
    public virtual ICollection<ServiceAttendance>? Attendances { get; set; }

    // Computed properties
    [NotMapped]
    public decimal CostPerClass => TotalClasses > 0 ? TotalPaid / TotalClasses : 0;

    /// <summary>
    /// Ingreso a reconocer por clase asistida usando la tarifa del período (mensual, trimestral…).
    /// Más preciso que CostPerClass cuando TotalPaid sólo refleja un pago parcial del contrato.
    /// Requiere que la navegación ServicePlan esté cargada; si no, usa TotalPaid / TotalClasses.
    /// </summary>
    [NotMapped]
    public decimal RevenuePerClass
    {
        get
        {
            var sp = ServicePlan;
            if (sp != null && sp.ClassCount > 0)
            {
                var monthsInPeriod = BillingFrequency switch
                {
                    BillingFrequency.Quarterly  => 3,
                    BillingFrequency.Semiannual => 6,
                    BillingFrequency.Annual     => 12,
                    _                           => 1
                };
                var amountPerPeriod = BillingFrequency switch
                {
                    BillingFrequency.Quarterly  => sp.PriceQuarterly  ?? sp.Price * 3,
                    BillingFrequency.Semiannual => sp.PriceSemiannual ?? sp.Price * 6,
                    BillingFrequency.Annual     => sp.PriceAnnual     ?? sp.Price * 12,
                    _                           => sp.Price
                };
                var classesInPeriod = sp.ClassCount * monthsInPeriod;
                return classesInPeriod > 0 ? Math.Round(amountPerPeriod / classesInPeriod, 2) : 0;
            }
            // Fallback: planes de pago único (precio total / total de clases)
            return TotalClasses > 0 ? Math.Round(TotalPaid / TotalClasses, 2) : 0;
        }
    }

    [NotMapped]
    public int DaysRemaining => (EndDate - DateTime.Today).Days;

    [NotMapped]
    public double UtilizationPercent => TotalClasses > 0 ? (double)ClassesUsed / TotalClasses * 100 : 0;

    /// <summary>
    /// % de cupos comprometidos (usados + reservados)
    /// </summary>
    [NotMapped]
    public double CommittedPercent => TotalClasses > 0 ? (double)(ClassesUsed + ClassesReserved) / TotalClasses * 100 : 0;

    [NotMapped]
    public bool IsExpiringSoon => DaysRemaining <= 7 && DaysRemaining > 0 && Status == ClientServicePlanStatus.Active;

    [NotMapped]
    public bool IsHighRisk => IsExpiringSoon && ClassesRemaining > (TotalClasses * 0.5);
}
