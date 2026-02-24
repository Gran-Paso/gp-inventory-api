using GPInventory.Domain.Entities;

namespace GPInventory.Application.Interfaces;

public interface IBankTransactionRepository
{
    Task<BankTransaction?> GetByIdAsync(int id);
    Task<IEnumerable<BankTransaction>> GetPendingByBusinessIdAsync(int businessId);
    Task<IEnumerable<BankTransaction>> GetByConnectionIdAsync(int connectionId);
    Task<bool> ExistsByFintocIdAsync(string fintocId);
    Task<BankTransaction> AddAsync(BankTransaction entity);
    Task AddRangeAsync(IEnumerable<BankTransaction> entities);
    Task UpdateAsync(BankTransaction entity);
    Task<int> SaveChangesAsync();
}
