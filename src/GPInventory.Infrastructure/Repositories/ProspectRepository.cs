using GPInventory.Application.Interfaces;
using GPInventory.Domain.Entities;
using GPInventory.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace GPInventory.Infrastructure.Repositories;

public class ProspectRepository : IProspectRepository
{
    private readonly ApplicationDbContext _context;
    private readonly DbSet<Prospect> _dbSet;

    public ProspectRepository(ApplicationDbContext context)
    {
        _context = context;
        _dbSet = context.Set<Prospect>();
    }

    public async Task<Prospect?> GetByIdAsync(int id)
    {
        return await _dbSet.FindAsync(id);
    }

    public async Task<IEnumerable<Prospect>> GetAllAsync()
    {
        return await _dbSet.OrderByDescending(p => p.CreatedAt).ToListAsync();
    }

    public async Task<IEnumerable<Prospect>> GetAllActiveAsync()
    {
        return await _dbSet
            .OrderByDescending(p => p.CreatedAt)
            .ToListAsync();
    }

    public async Task<Prospect?> GetByEmailAsync(string email)
    {
        return await _dbSet
            .FirstOrDefaultAsync(p => p.Mail.ToLower() == email.ToLower());
    }

    public async Task<Prospect> AddAsync(Prospect prospect)
    {
        prospect.CreatedAt = DateTime.UtcNow;
        _dbSet.Add(prospect);
        await _context.SaveChangesAsync();
        return prospect;
    }

    public async Task UpdateAsync(Prospect prospect)
    {
        _dbSet.Update(prospect);
        await _context.SaveChangesAsync();
    }

    public async Task DeleteAsync(int id)
    {
        var prospect = await GetByIdAsync(id);
        if (prospect != null)
        {
            _dbSet.Remove(prospect);
            await _context.SaveChangesAsync();
        }
    }
}
