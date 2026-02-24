using System.Text.Json.Serialization;

namespace GPInventory.Application.DTOs.Bank;

// ─── BankConnection DTOs ──────────────────────────────────────────────────────

public class BankConnectionDto
{
    [JsonPropertyName("id")]
    public int Id { get; set; }

    [JsonPropertyName("business_id")]
    public int BusinessId { get; set; }

    [JsonPropertyName("link_token")]
    public string LinkToken { get; set; } = string.Empty;

    [JsonPropertyName("account_id")]
    public string? AccountId { get; set; }

    [JsonPropertyName("bank_entity_id")]
    public int? BankEntityId { get; set; }

    [JsonPropertyName("bank_entity_name")]
    public string? BankEntityName { get; set; }

    [JsonPropertyName("label")]
    public string? Label { get; set; }

    [JsonPropertyName("last_sync_at")]
    public DateTime? LastSyncAt { get; set; }

    [JsonPropertyName("is_active")]
    public bool IsActive { get; set; }

    [JsonPropertyName("created_at")]
    public DateTime CreatedAt { get; set; }
}

public class CreateBankConnectionDto
{
    [JsonPropertyName("business_id")]
    public int BusinessId { get; set; }

    /// <summary>Fintoc link_token obtained from the frontend widget.</summary>
    [JsonPropertyName("link_token")]
    public string LinkToken { get; set; } = string.Empty;

    /// <summary>Id from the bank_entities table.</summary>
    [JsonPropertyName("bank_entity_id")]
    public int? BankEntityId { get; set; }

    [JsonPropertyName("label")]
    public string? Label { get; set; }
}

// ─── BankTransaction DTOs ─────────────────────────────────────────────────────

public class BankTransactionDto
{
    [JsonPropertyName("id")]
    public int Id { get; set; }

    [JsonPropertyName("bank_connection_id")]
    public int BankConnectionId { get; set; }

    [JsonPropertyName("business_id")]
    public int BusinessId { get; set; }

    [JsonPropertyName("fintoc_id")]
    public string FintocId { get; set; } = string.Empty;

    [JsonPropertyName("amount")]
    public decimal Amount { get; set; }

    [JsonPropertyName("description")]
    public string? Description { get; set; }

    [JsonPropertyName("transaction_date")]
    public DateTime TransactionDate { get; set; }

    [JsonPropertyName("transaction_type")]
    public string? TransactionType { get; set; }

    [JsonPropertyName("status")]
    public string Status { get; set; } = "pending";

    [JsonPropertyName("expense_id")]
    public int? ExpenseId { get; set; }

    [JsonPropertyName("suggested_subcategory_id")]
    public int? SuggestedSubcategoryId { get; set; }

    [JsonPropertyName("suggested_subcategory_name")]
    public string? SuggestedSubcategoryName { get; set; }

    [JsonPropertyName("created_at")]
    public DateTime CreatedAt { get; set; }
}

public class ConfirmBankTransactionDto
{
    /// <summary>Expense subcategory to assign when creating the Expense.</summary>
    [JsonPropertyName("subcategory_id")]
    public int SubcategoryId { get; set; }

    [JsonPropertyName("description")]
    public string? Description { get; set; }

    [JsonPropertyName("receipt_type_id")]
    public int? ReceiptTypeId { get; set; }

    [JsonPropertyName("expense_type_id")]
    public int? ExpenseTypeId { get; set; }

    [JsonPropertyName("notes")]
    public string? Notes { get; set; }
}

// ─── Sync request/response ────────────────────────────────────────────────────

public class SyncBankConnectionDto
{
    [JsonPropertyName("connection_id")]
    public int ConnectionId { get; set; }

    /// <summary>How many days back to fetch (default 30).</summary>
    [JsonPropertyName("days_back")]
    public int DaysBack { get; set; } = 30;
}

public class SyncResultDto
{
    [JsonPropertyName("imported")]
    public int Imported { get; set; }

    [JsonPropertyName("duplicates_skipped")]
    public int DuplicatesSkipped { get; set; }

    [JsonPropertyName("message")]
    public string Message { get; set; } = string.Empty;
}
