using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace GPInventory.Domain.Entities;

/// <summary>
/// Sub-servicio componente de un servicio principal (composición de servicios)
/// </summary>
[Table("service_sub_service")]
public class ServiceSubService
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Required]
    [ForeignKey("ParentService")]
    [Column("parent_service_id")]
    public int ParentServiceId { get; set; }

    [Required]
    [ForeignKey("ChildService")]
    [Column("child_service_id")]
    public int ChildServiceId { get; set; }

    [Column("quantity")]
    public int Quantity { get; set; } = 1;

    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Navigation properties
    [ForeignKey(nameof(ParentServiceId))]
    public virtual Service? ParentService { get; set; }

    [ForeignKey(nameof(ChildServiceId))]
    public virtual Service? ChildService { get; set; }
}
