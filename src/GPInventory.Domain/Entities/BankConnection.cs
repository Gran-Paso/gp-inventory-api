using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace GPInventory.Domain.Entities;

/// <summary>
/// Stores a Fintoc link token for a business's bank account connection.
/// One business can have multiple connections (e.g. BCI checking + Banco Estado savings).
/// </summary>
public class BankConnection : BaseEntity
{
    [Required]
    [ForeignKey(nameof(Business))]
    public int BusinessId { get; set; }

    /// <summary>Fintoc link_token returned after the widget OAuth flow.</summary>
    [Required]
    [StringLength(500)]
    public string LinkToken { get; set; } = string.Empty;

    /// <summary>Fintoc account id linked to this connection.</summary>
    [StringLength(200)]
    public string? AccountId { get; set; }

    /// <summary>FK to bank_entities table (id of the selected bank).</summary>
    [ForeignKey(nameof(BankEntity))]
    public int? BankEntityId { get; set; }

    /// <summary>Human-readable label (e.g. "BCI Cuenta Corriente").</summary>
    [StringLength(200)]
    public string? Label { get; set; }

    /// <summary>Last time transactions were synced from Fintoc.</summary>
    public DateTime? LastSyncAt { get; set; }

    // Navigation
    public Business? Business { get; set; }
    public BankEntity? BankEntity { get; set; }
    public ICollection<BankTransaction> Transactions { get; set; } = new List<BankTransaction>();
}
