using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using GPInventory.Domain.Enums;

namespace GPInventory.Domain.Entities;

/// <summary>
/// Servicio ofrecido por la empresa
/// </summary>
[Table("service")]
public class Service
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Required]
    [StringLength(255)]
    [Column("name")]
    public string Name { get; set; } = string.Empty;

    [Required]
    [ForeignKey(nameof(Category))]
    [Column("category_id")]
    public int CategoryId { get; set; }

    [Required]
    [ForeignKey(nameof(Business))]
    [Column("business_id")]
    public int BusinessId { get; set; }

    [ForeignKey(nameof(Store))]
    [Column("store_id")]
    public int? StoreId { get; set; }

    [Required]
    [Column("base_price", TypeName = "decimal(12,2)")]
    public decimal BasePrice { get; set; }

    [Column("duration_minutes")]
    public int? DurationMinutes { get; set; }

    [StringLength(1000)]
    [Column("description")]
    public string? Description { get; set; }

    /// <summary>
    /// Modelo de pricing: Fixed=precio total fijo, PerEnrollment=precio por inscripción
    /// </summary>
    [Required]
    [Column("pricing_type")]
    public PricingType PricingType { get; set; } = PricingType.Fixed;

    /// <summary>
    /// Si el servicio está afecto a IVA (19%). true=sí, false=exento
    /// </summary>
    [Required]
    [Column("is_taxable")]
    public bool IsTaxable { get; set; } = true;

    [Column("active")]
    public bool Active { get; set; } = true;

    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [Column("updated_at")]
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    // Navigation properties
    public virtual ServiceCategory? Category { get; set; }
    public virtual Business? Business { get; set; }
    public virtual Store? Store { get; set; }
    public virtual ICollection<ServiceCostItem>? CostItems { get; set; }
    public virtual ICollection<ServiceSubService>? SubServices { get; set; }
    public virtual ICollection<ServiceSale>? Sales { get; set; }
}
