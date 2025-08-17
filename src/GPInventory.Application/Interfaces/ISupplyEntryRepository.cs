using GPInventory.Domain.Entities;

namespace GPInventory.Application.Interfaces;

public interface ISupplyEntryRepository
{
    Task<IEnumerable<SupplyEntry>> GetAllAsync();
    Task<IEnumerable<SupplyEntry>> GetAllWithDetailsAsync();
    Task<SupplyEntry?> GetByIdAsync(int id);
    Task<IEnumerable<SupplyEntry>> GetBySupplyIdAsync(int supplyId);
    Task<IEnumerable<SupplyEntry>> GetByProcessDoneIdAsync(int processDoneId);
    Task<decimal> GetCurrentStockAsync(int supplyId);
    Task<SupplyEntry> CreateAsync(SupplyEntry supplyEntry);
    Task<SupplyEntry> UpdateAsync(SupplyEntry supplyEntry);
    Task DeleteAsync(int id);
    Task<IEnumerable<SupplyEntry>> GetSupplyHistoryAsync(int supplyId);
}
