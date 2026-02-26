using GPInventory.Application.Interfaces;
using GPInventory.Domain.Entities;
using GPInventory.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace GPInventory.Infrastructure.Repositories;

public class BankTransactionRepository : IBankTransactionRepository
{
    private readonly ApplicationDbContext _context;

    public BankTransactionRepository(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<BankTransaction?> GetByIdAsync(int id)
        => await _context.BankTransactions
            .FirstOrDefaultAsync(t => t.Id == id);

    public async Task<IEnumerable<BankTransaction>> GetPendingByBusinessIdAsync(int businessId)
        => await _context.BankTransactions
            .Where(t => t.BusinessId == businessId && t.Status == "pending")
            .OrderByDescending(t => t.TransactionDate)
            .ToListAsync();

    public async Task<IEnumerable<BankTransaction>> GetByConnectionIdAsync(int connectionId)
        => await _context.BankTransactions
            .Where(t => t.BankConnectionId == connectionId)
            .OrderByDescending(t => t.TransactionDate)
            .ToListAsync();

    public async Task<bool> ExistsByFintocIdAsync(string fintocId)
        => await _context.BankTransactions.AnyAsync(t => t.FintocId == fintocId);

    public async Task<BankTransaction> AddAsync(BankTransaction entity)
    {
        entity.CreatedAt = DateTime.UtcNow;
        entity.UpdatedAt = DateTime.UtcNow;
        entity.IsActive = true;
        _context.BankTransactions.Add(entity);
        await _context.SaveChangesAsync();
        return entity;
    }

    public async Task AddRangeAsync(IEnumerable<BankTransaction> entities)
    {
        var now = DateTime.UtcNow;
        foreach (var e in entities)
        {
            e.CreatedAt = now;
            e.UpdatedAt = now;
            e.IsActive = true;
        }
        _context.BankTransactions.AddRange(entities);
        await _context.SaveChangesAsync();
    }

    public async Task UpdateAsync(BankTransaction entity)
    {
        entity.UpdatedAt = DateTime.UtcNow;
        _context.BankTransactions.Update(entity);
        await _context.SaveChangesAsync();
    }

    public async Task<int> SaveChangesAsync()
        => await _context.SaveChangesAsync();
}
