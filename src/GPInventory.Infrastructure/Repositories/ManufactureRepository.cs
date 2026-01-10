using GPInventory.Application.Interfaces;
using GPInventory.Domain.Entities;
using GPInventory.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace GPInventory.Infrastructure.Repositories;

public class ManufactureRepository : IManufactureRepository
{
    private readonly ApplicationDbContext _context;

    public ManufactureRepository(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Manufacture?> GetByIdAsync(int id)
    {
        return await _context.Manufactures
            .Include(m => m.Product)
            .Include(m => m.ProcessDone)
                .ThenInclude(pd => pd.Process)
            .Include(m => m.Store)
            .Include(m => m.Business)
            .FirstOrDefaultAsync(m => m.Id == id);
    }

    public async Task<IEnumerable<Manufacture>> GetAllAsync()
    {
        return await _context.Manufactures
            .Include(m => m.Product)
            .Include(m => m.ProcessDone)
                .ThenInclude(pd => pd.Process)
            .Include(m => m.Store)
            .OrderByDescending(m => m.CreatedAt)
            .ToListAsync();
    }

    public async Task<IEnumerable<Manufacture>> GetByBusinessIdAsync(int businessId)
    {
        return await _context.Manufactures
            .Include(m => m.Product)
            .Include(m => m.ProcessDone)
                .ThenInclude(pd => pd.Process)
            .Include(m => m.Store)
            .Where(m => m.BusinessId == businessId)
            .OrderByDescending(m => m.CreatedAt)
            .ToListAsync();
    }

    public async Task<IEnumerable<Manufacture>> GetByProcessDoneIdAsync(int processDoneId)
    {
        return await _context.Manufactures
            .Include(m => m.Product)
            .Include(m => m.ProcessDone)
            .Include(m => m.Store)
            .Where(m => m.ProcessDoneId == processDoneId)
            .OrderByDescending(m => m.CreatedAt)
            .ToListAsync();
    }

    public async Task<IEnumerable<Manufacture>> GetByProductIdAsync(int productId)
    {
        return await _context.Manufactures
            .Include(m => m.Product)
            .Include(m => m.ProcessDone)
                .ThenInclude(pd => pd.Process)
            .Include(m => m.Store)
            .Where(m => m.ProductId == productId)
            .OrderByDescending(m => m.CreatedAt)
            .ToListAsync();
    }

    public async Task<IEnumerable<Manufacture>> GetByStatusAsync(string status, int? businessId = null)
    {
        var query = _context.Manufactures
            .Include(m => m.Product)
            .Include(m => m.ProcessDone)
                .ThenInclude(pd => pd.Process)
            .Include(m => m.Store)
            .Where(m => m.Status == status);

        if (businessId.HasValue)
        {
            query = query.Where(m => m.BusinessId == businessId.Value);
        }

        return await query
            .OrderByDescending(m => m.CreatedAt)
            .ToListAsync();
    }

    public async Task<IEnumerable<Manufacture>> GetPendingAsync(int businessId)
    {
        return await GetByStatusAsync("pending", businessId);
    }

    public async Task<Manufacture> AddAsync(Manufacture manufacture)
    {
        _context.Manufactures.Add(manufacture);
        await _context.SaveChangesAsync();
        return manufacture;
    }

    public async Task<Manufacture> UpdateAsync(Manufacture manufacture)
    {
        manufacture.UpdatedAt = DateTime.UtcNow;
        _context.Manufactures.Update(manufacture);
        await _context.SaveChangesAsync();
        return manufacture;
    }

    public async Task DeleteAsync(int id)
    {
        var manufacture = await _context.Manufactures.FindAsync(id);
        if (manufacture != null)
        {
            manufacture.IsActive = false;
            manufacture.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();
        }
    }

    public async Task<bool> ExistsAsync(int id)
    {
        return await _context.Manufactures.AnyAsync(m => m.Id == id);
    }

    public async Task<System.Data.Common.DbConnection> GetDbConnectionAsync()
    {
        return await Task.FromResult(_context.Database.GetDbConnection());
    }
}
