using GPInventory.Application.DTOs.Bank;

namespace GPInventory.Application.Interfaces;

public interface IBankService
{
    // Connections
    Task<BankConnectionDto> CreateConnectionAsync(CreateBankConnectionDto dto);
    Task<IEnumerable<BankConnectionDto>> GetConnectionsAsync(int businessId);
    Task DeleteConnectionAsync(int connectionId);

    // Sync
    Task<SyncResultDto> SyncTransactionsAsync(int connectionId, int daysBack = 30);

    // Transactions review
    Task<IEnumerable<BankTransactionDto>> GetPendingTransactionsAsync(int businessId);
    Task<BankTransactionDto> ConfirmTransactionAsync(int transactionId, ConfirmBankTransactionDto dto);
    Task<BankTransactionDto> DismissTransactionAsync(int transactionId);
}
