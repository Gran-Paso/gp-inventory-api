using GPInventory.Application.DTOs.Bank;

namespace GPInventory.Application.Interfaces;

/// <summary>
/// External-service abstraction for the Fintoc REST API.
/// Implemented in Infrastructure so the Application layer stays clean.
/// </summary>
public interface IFintocService
{
    /// <summary>Fetch movements from Fintoc for the given link_token/account between two dates.</summary>
    Task<IEnumerable<FintocMovementDto>> GetMovementsAsync(
        string linkToken,
        string accountId,
        DateTime since,
        DateTime until);

    /// <summary>Fetch linked accounts for a given link_token. Used to auto-discover account_id on first sync.</summary>
    Task<IEnumerable<FintocAccountDto>> GetAccountsAsync(string linkToken);
}

// ─── Raw Fintoc API models ────────────────────────────────────────────────────

public class FintocMovementDto
{
    public string Id { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public string? Description { get; set; }
    public DateTime PostDate { get; set; }
    public string? Type { get; set; } // "debit" | "credit"
}

public class FintocAccountDto
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Type { get; set; } // e.g. "checking_account"
    public string? Number { get; set; }
}
