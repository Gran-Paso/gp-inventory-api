using GPInventory.Application.DTOs.Bank;
using GPInventory.Application.Interfaces;
using GPInventory.Domain.Entities;

namespace GPInventory.Application.Services;

public class BankService : IBankService
{
    private readonly IBankConnectionRepository _connectionRepo;
    private readonly IBankTransactionRepository _transactionRepo;
    private readonly IExpenseRepository _expenseRepo;
    private readonly IFintocService _fintoc;

    public BankService(
        IBankConnectionRepository connectionRepo,
        IBankTransactionRepository transactionRepo,
        IExpenseRepository expenseRepo,
        IFintocService fintoc)
    {
        _connectionRepo = connectionRepo;
        _transactionRepo = transactionRepo;
        _expenseRepo = expenseRepo;
        _fintoc = fintoc;
    }

    // ─── Connections ──────────────────────────────────────────────────────────

    public async Task<BankConnectionDto> CreateConnectionAsync(CreateBankConnectionDto dto)
    {
        var connection = new BankConnection
        {
            BusinessId   = dto.BusinessId,
            LinkToken    = dto.LinkToken,
            BankEntityId = dto.BankEntityId,
            Label        = dto.Label
        };

        var saved = await _connectionRepo.AddAsync(connection);
        return MapConnectionToDto(saved);
    }

    public async Task<IEnumerable<BankConnectionDto>> GetConnectionsAsync(int businessId)
    {
        var connections = await _connectionRepo.GetByBusinessIdAsync(businessId);
        return connections.Select(MapConnectionToDto);
    }

    public async Task DeleteConnectionAsync(int connectionId)
        => await _connectionRepo.DeleteAsync(connectionId);

    // ─── Sync ─────────────────────────────────────────────────────────────────

    public async Task<SyncResultDto> SyncTransactionsAsync(int connectionId, int daysBack = 30)
    {
        var connection = await _connectionRepo.GetByIdAsync(connectionId)
            ?? throw new KeyNotFoundException($"Conexión bancaria {connectionId} no encontrada.");

        // Auto-discover account_id the first time (no need for user to specify it)
        if (string.IsNullOrEmpty(connection.AccountId))
        {
            var accounts = (await _fintoc.GetAccountsAsync(connection.LinkToken)).ToList();
            if (accounts.Count == 0)
                throw new InvalidOperationException("No se encontraron cuentas para esta conexión. Verifique el link_token.");
            connection.AccountId = accounts[0].Id;
            await _connectionRepo.UpdateAsync(connection);
        }

        var until = DateTime.UtcNow.Date;
        var since = until.AddDays(-daysBack);

        var movements = await _fintoc.GetMovementsAsync(
            connection.LinkToken,
            connection.AccountId,
            since,
            until);

        int imported = 0;
        int skipped  = 0;
        var toInsert = new List<BankTransaction>();

        foreach (var mov in movements)
        {
            // Only import debits (egresos = gastos del negocio)
            if (mov.Type != "debit") continue;

            if (await _transactionRepo.ExistsByFintocIdAsync(mov.Id))
            {
                skipped++;
                continue;
            }

            toInsert.Add(new BankTransaction
            {
                BankConnectionId = connection.Id,
                BusinessId       = connection.BusinessId,
                FintocId         = mov.Id,
                Amount           = mov.Amount,
                Description      = mov.Description,
                TransactionDate  = mov.PostDate,
                TransactionType  = mov.Type,
                Status           = "pending"
            });
            imported++;
        }

        if (toInsert.Count > 0)
            await _transactionRepo.AddRangeAsync(toInsert);

        // Update last sync timestamp
        connection.LastSyncAt = DateTime.UtcNow;
        await _connectionRepo.UpdateAsync(connection);

        Console.WriteLine($"BankService: Sync complete for connection {connectionId}: {imported} imported, {skipped} duplicates skipped.");

        return new SyncResultDto
        {
            Imported          = imported,
            DuplicatesSkipped = skipped,
            Message           = $"Sincronización completada: {imported} nuevos movimientos importados."
        };
    }

    // ─── Transaction review ───────────────────────────────────────────────────

    public async Task<IEnumerable<BankTransactionDto>> GetPendingTransactionsAsync(int businessId)
    {
        var txns = await _transactionRepo.GetPendingByBusinessIdAsync(businessId);
        return txns.Select(MapTransactionToDto);
    }

    public async Task<BankTransactionDto> ConfirmTransactionAsync(
        int transactionId,
        ConfirmBankTransactionDto dto)
    {
        var txn = await _transactionRepo.GetByIdAsync(transactionId)
            ?? throw new KeyNotFoundException($"Transacción {transactionId} no encontrada.");

        if (txn.Status != "pending")
            throw new InvalidOperationException($"La transacción ya fue procesada (estado: {txn.Status}).");

        // Create the Expense
        var expense = new Expense
        {
            BusinessId    = txn.BusinessId,
            Date          = txn.TransactionDate,
            SubcategoryId = dto.SubcategoryId,
            Amount        = txn.Amount,
            AmountNet     = txn.Amount,
            AmountIva     = 0,
            AmountTotal   = txn.Amount,
            ReceiptTypeId = dto.ReceiptTypeId,
            ExpenseTypeId = dto.ExpenseTypeId,
            Description   = dto.Description ?? txn.Description ?? "Movimiento bancario",
            Notes         = dto.Notes ?? string.Empty,
            IsFixed       = false
        };

        var savedExpense = await _expenseRepo.AddAsync(expense);

        txn.Status    = "confirmed";
        txn.ExpenseId = savedExpense.Id;
        await _transactionRepo.UpdateAsync(txn);

        return MapTransactionToDto(txn);
    }

    public async Task<BankTransactionDto> DismissTransactionAsync(int transactionId)
    {
        var txn = await _transactionRepo.GetByIdAsync(transactionId)
            ?? throw new KeyNotFoundException($"Transacción {transactionId} no encontrada.");

        if (txn.Status != "pending")
            throw new InvalidOperationException($"La transacción ya fue procesada (estado: {txn.Status}).");

        txn.Status = "dismissed";
        await _transactionRepo.UpdateAsync(txn);

        return MapTransactionToDto(txn);
    }

    // ─── Mapping helpers ──────────────────────────────────────────────────────

    private static BankConnectionDto MapConnectionToDto(BankConnection c) => new()
    {
        Id             = c.Id,
        BusinessId     = c.BusinessId,
        LinkToken      = c.LinkToken,
        AccountId      = c.AccountId,
        BankEntityId   = c.BankEntityId,
        BankEntityName = c.BankEntity?.Name,
        Label          = c.Label,
        LastSyncAt     = c.LastSyncAt,
        IsActive       = c.IsActive,
        CreatedAt      = c.CreatedAt
    };

    private static BankTransactionDto MapTransactionToDto(BankTransaction t) => new()
    {
        Id                      = t.Id,
        BankConnectionId        = t.BankConnectionId,
        BusinessId              = t.BusinessId,
        FintocId                = t.FintocId,
        Amount                  = t.Amount,
        Description             = t.Description,
        TransactionDate         = t.TransactionDate,
        TransactionType         = t.TransactionType,
        Status                  = t.Status,
        ExpenseId               = t.ExpenseId,
        SuggestedSubcategoryId  = t.SuggestedSubcategoryId,
        CreatedAt               = t.CreatedAt
    };
}
