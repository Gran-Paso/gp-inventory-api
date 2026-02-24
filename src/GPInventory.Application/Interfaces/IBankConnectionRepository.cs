using GPInventory.Domain.Entities;

namespace GPInventory.Application.Interfaces;

public interface IBankConnectionRepository
{
    Task<BankConnection?> GetByIdAsync(int id);
    Task<IEnumerable<BankConnection>> GetByBusinessIdAsync(int businessId);
    Task<BankConnection> AddAsync(BankConnection entity);
    Task UpdateAsync(BankConnection entity);
    Task DeleteAsync(int id);
    Task<int> SaveChangesAsync();
}
