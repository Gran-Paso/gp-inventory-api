using GPInventory.Domain.Entities;
using System.Data.Common;

namespace GPInventory.Application.Interfaces;

public interface IManufactureRepository
{
    Task<Manufacture?> GetByIdAsync(int id);
    Task<IEnumerable<Manufacture>> GetAllAsync();
    Task<IEnumerable<Manufacture>> GetByBusinessIdAsync(int businessId);
    Task<IEnumerable<Manufacture>> GetByProcessDoneIdAsync(int processDoneId);
    Task<IEnumerable<Manufacture>> GetByProductIdAsync(int productId);
    Task<IEnumerable<Manufacture>> GetByStatusAsync(string status, int? businessId = null);
    Task<IEnumerable<Manufacture>> GetPendingAsync(int businessId);
    Task<Manufacture> AddAsync(Manufacture manufacture);
    Task<Manufacture> UpdateAsync(Manufacture manufacture);
    Task DeleteAsync(int id);
    Task<bool> ExistsAsync(int id);
    Task<DbConnection> GetDbConnectionAsync();
}
