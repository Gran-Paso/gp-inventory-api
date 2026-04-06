using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using GPInventory.Domain.Enums;

namespace GPInventory.Domain.Entities;

/// <summary>
/// Definición de un plan/membresía de servicios
/// Ej: "Plan Mensual 12 Clases", "Plan Trimestral Ilimitado"
/// </summary>
[Table("service_plan")]
public class ServicePlan
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
    [StringLength(255)]
    [Column("name")]
    public string Name { get; set; } = string.Empty;

    [Column("description", TypeName = "text")]
    public string? Description { get; set; }

    /// <summary>
    /// Si es específico para un servicio. NULL = aplica a todos
    /// </summary>
    [ForeignKey(nameof(Service))]
    [Column("service_id")]
    public int? ServiceId { get; set; }

    /// <summary>
    /// Si aplica a una categoría de servicios
    /// </summary>
    [ForeignKey(nameof(ServiceCategory))]
    [Column("service_category_id")]
    public int? ServiceCategoryId { get; set; }

    /// <summary>
    /// Número de sesiones incluidas en el plan por período de facturación
    /// </summary>
    [Required]
    [Column("class_count")]
    public int ClassCount { get; set; }

    /// <summary>
    /// DEPRECATED — reemplazado por contract_months en client_service_plan.
    /// Se mantiene por compatibilidad con datos existentes.
    /// </summary>
    [Column("validity_days")]
    public int ValidityDays { get; set; } = 30;

    // ── Precios por frecuencia de facturación ─────────────────────────────

    /// <summary>Precio base mensual (mapeado a columna price_monthly)</summary>
    [Required]
    [Column("price_monthly", TypeName = "decimal(12,2)")]
    public decimal Price { get; set; }

    /// <summary>Precio por trimestre (3 meses). Null = sin descuento trimestral.</summary>
    [Column("price_quarterly", TypeName = "decimal(12,2)")]
    public decimal? PriceQuarterly { get; set; }

    /// <summary>Precio por semestre (6 meses). Null = sin descuento semestral.</summary>
    [Column("price_semiannual", TypeName = "decimal(12,2)")]
    public decimal? PriceSemiannual { get; set; }

    /// <summary>Precio por año (12 meses). Null = sin descuento anual.</summary>
    [Column("price_annual", TypeName = "decimal(12,2)")]
    public decimal? PriceAnnual { get; set; }

    // ── Configuración de cobro ─────────────────────────────────────────────

    /// <summary>
    /// Define si el cliente paga antes del período (pre_pay), al cierre (deferred) o ambos.
    /// </summary>
    [Column("payment_timing")]
    public PlanPaymentTiming PaymentTiming { get; set; } = PlanPaymentTiming.Deferred;

    /// <summary>Método de pago por defecto sugerido al asignar el plan a un cliente.</summary>
    [Column("default_payment_method_id")]
    public int? DefaultPaymentMethodId { get; set; }

    [Column("active")]
    public bool Active { get; set; } = true;

    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [Column("updated_at")]
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    // Navigation properties
    public virtual Business? Business { get; set; }
    public virtual Store? Store { get; set; }
    public virtual Service? Service { get; set; }
    public virtual ServiceCategory? ServiceCategory { get; set; }
    public virtual ICollection<ClientServicePlan>? ClientServicePlans { get; set; }
}
