using GPInventory.Application.Interfaces;
using GPInventory.Domain.Entities;
using GPInventory.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace GPInventory.Infrastructure.Repositories;

public class BankConnectionRepository : IBankConnectionRepository
{
    private readonly ApplicationDbContext _context;

    public BankConnectionRepository(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<BankConnection?> GetByIdAsync(int id)
        => await _context.BankConnections
            .Include(c => c.BankEntity)
            .FirstOrDefaultAsync(c => c.Id == id && c.IsActive);

    public async Task<IEnumerable<BankConnection>> GetByBusinessIdAsync(int businessId)
        => await _context.BankConnections
            .Include(c => c.BankEntity)
            .Where(c => c.BusinessId == businessId && c.IsActive)
            .OrderByDescending(c => c.CreatedAt)
            .ToListAsync();

    public async Task<BankConnection> AddAsync(BankConnection entity)
    {
        entity.CreatedAt = DateTime.UtcNow;
        entity.UpdatedAt = DateTime.UtcNow;
        entity.IsActive = true;
        _context.BankConnections.Add(entity);
        await _context.SaveChangesAsync();
        return entity;
    }

    public async Task UpdateAsync(BankConnection entity)
    {
        entity.UpdatedAt = DateTime.UtcNow;
        _context.BankConnections.Update(entity);
        await _context.SaveChangesAsync();
    }

    public async Task DeleteAsync(int id)
    {
        var entity = await _context.BankConnections.FindAsync(id);
        if (entity != null)
        {
            entity.IsActive = false;
            entity.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();
        }
    }

    public async Task<int> SaveChangesAsync()
        => await _context.SaveChangesAsync();
}
