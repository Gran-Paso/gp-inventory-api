using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace GPInventory.Domain.Entities;

/// <summary>
/// A raw bank movement imported from Fintoc. The user reviews each transaction
/// and decides whether to confirm it as an Expense or dismiss it.
/// </summary>
public class BankTransaction : BaseEntity
{
    [Required]
    [ForeignKey(nameof(BankConnection))]
    public int BankConnectionId { get; set; }

    [Required]
    [ForeignKey(nameof(Business))]
    public int BusinessId { get; set; }

    /// <summary>Unique id returned by Fintoc ("mov_xxx..."). Used to avoid duplicates.</summary>
    [Required]
    [StringLength(200)]
    public string FintocId { get; set; } = string.Empty;

    /// <summary>Transaction amount in CLP (always positive; use TransactionType to distinguish debit/credit).</summary>
    [Required]
    [Column(TypeName = "decimal(18,2)")]
    public decimal Amount { get; set; }

    /// <summary>Raw description/narration from the bank.</summary>
    [StringLength(500)]
    public string? Description { get; set; }

    /// <summary>Date the transaction was posted by the bank.</summary>
    [Required]
    public DateTime TransactionDate { get; set; }

    /// <summary>"debit" or "credit" as returned by Fintoc.</summary>
    [StringLength(50)]
    public string? TransactionType { get; set; }

    /// <summary>
    /// Workflow status:
    ///   pending   – imported, not yet reviewed
    ///   confirmed – user confirmed as an Expense
    ///   dismissed – user ignored it
    /// </summary>
    [Required]
    [StringLength(50)]
    public string Status { get; set; } = "pending";

    /// <summary>Set when the user confirms this transaction and an Expense is created.</summary>
    [ForeignKey(nameof(Expense))]
    public int? ExpenseId { get; set; }

    /// <summary>Auto-suggested subcategory (optional, set during sync).</summary>
    [ForeignKey(nameof(ExpenseSubcategory))]
    public int? SuggestedSubcategoryId { get; set; }

    // Navigation
    public BankConnection? BankConnection { get; set; }
    public Business? Business { get; set; }
    public Expense? Expense { get; set; }
    public ExpenseSubcategory? ExpenseSubcategory { get; set; }
}
