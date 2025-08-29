using GPInventory.Domain.Entities;

namespace GPInventory.Application.Interfaces;

public interface IProspectRepository
{
    Task<Prospect?> GetByIdAsync(int id);
    Task<IEnumerable<Prospect>> GetAllAsync();
    Task<IEnumerable<Prospect>> GetAllActiveAsync();
    Task<Prospect?> GetByEmailAsync(string email);
    Task<Prospect> AddAsync(Prospect prospect);
    Task UpdateAsync(Prospect prospect);
    Task DeleteAsync(int id);
}
