using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using GPInventory.Domain.Enums;

namespace GPInventory.Domain.Entities;

/// <summary>
/// Cliente registrado en el sistema de servicios (CRM opcional)
/// </summary>
[Table("service_client")]
public class ServiceClient
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

    [StringLength(20)]
    [Column("rut")]
    public string? Rut { get; set; }

    [StringLength(255)]
    [Column("email")]
    public string? Email { get; set; }

    [StringLength(50)]
    [Column("phone")]
    public string? Phone { get; set; }

    [StringLength(500)]
    [Column("address")]
    public string? Address { get; set; }

    [StringLength(100)]
    [Column("city")]
    public string? City { get; set; }

    [StringLength(255)]
    [Column("contact_person")]
    public string? ContactPerson { get; set; }

    [Required]
    [Column("client_type")]
    public ClientType ClientType { get; set; } = ClientType.Individual;

    [StringLength(50)]
    [Column("segment")]
    public string? Segment { get; set; }

    [Column("tags", TypeName = "longtext")]
    public string? Tags { get; set; }

    [Column("notes", TypeName = "text")]
    public string? Notes { get; set; }

    [Column("active")]
    public bool Active { get; set; } = true;

    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [Column("updated_at")]
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    [ForeignKey(nameof(CreatedByUser))]
    [Column("created_by_user_id")]
    public int? CreatedByUserId { get; set; }

    /// <summary>
    /// Usuario del sistema vinculado a este cliente.
    /// Permite auto check-in cuando el usuario está conectado a la página de la sesión.
    /// </summary>
    [Column("linked_user_id")]
    public int? LinkedUserId { get; set; }

    /// <summary>
    /// Si es sub-cliente, apunta al cliente raíz (pagador / tutor).
    /// NULL = cliente raíz independiente.
    /// </summary>
    [ForeignKey(nameof(ParentClient))]
    [Column("parent_client_id")]
    public int? ParentClientId { get; set; }

    /// <summary>
    /// Tipo de relación con el cliente raíz (FK a tabla de catálogo)
    /// </summary>
    [ForeignKey(nameof(RelationshipType))]
    [Column("relationship_type_id")]
    public int? RelationshipTypeId { get; set; }

    /// <summary>
    /// Fecha de nacimiento del sub-cliente (opcional)
    /// </summary>
    [Column("birth_date", TypeName = "date")]
    public DateTime? BirthDate { get; set; }

    // Navigation properties
    public virtual Business? Business { get; set; }
    public virtual Store? Store { get; set; }
    public virtual User? CreatedByUser { get; set; }
    public virtual ICollection<ServiceSale>? ServiceSales { get; set; }

    /// <summary>Cliente raíz al que pertenece este sub-cliente (null si es raíz)</summary>
    public virtual ServiceClient? ParentClient { get; set; }

    /// <summary>Sub-clientes de este cliente raíz</summary>
    public virtual ICollection<ServiceClient> SubClients { get; set; } = [];

    /// <summary>Tipo de relación con el cliente raíz</summary>
    public virtual ServiceClientRelationshipType? RelationshipType { get; set; }
}
