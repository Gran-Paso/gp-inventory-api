using GPInventory.Application.Interfaces;
using GPInventory.Domain.Entities;
using GPInventory.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace GPInventory.Infrastructure.Repositories;

public class UserRepository : Repository<User>, IUserRepository
{
    public UserRepository(ApplicationDbContext context) : base(context)
    {
    }

    public async Task<User?> GetByEmailAsync(string email)
    {
        return await _dbSet.FirstOrDefaultAsync(u => u.Mail == email);
    }

    public async Task<User?> GetByEmailWithRolesAsync(string email)
    {
        return await _dbSet
            .Include(u => u.UserBusinesses)
                .ThenInclude(ub => ub.Role)
            .Include(u => u.UserBusinesses)
                .ThenInclude(ub => ub.Business)
            .FirstOrDefaultAsync(u => u.Mail == email);
    }

    public async Task<bool> ExistsAsync(string email)
    {
        return await _dbSet.AnyAsync(u => u.Mail == email);
    }
}

public class BusinessRepository : Repository<Business>, IBusinessRepository
{
    public BusinessRepository(ApplicationDbContext context) : base(context)
    {
    }

    public async Task<IEnumerable<Business>> GetUserBusinessesAsync(int userId)
    {
        return await _context.UserHasBusinesses
            .Where(ub => ub.UserId == userId)
            .Select(ub => ub.Business)
            .ToListAsync();
    }
}

public class ProductRepository : Repository<Product>, IProductRepository
{
    public ProductRepository(ApplicationDbContext context) : base(context)
    {
    }

    public async Task<IEnumerable<Product>> GetByBusinessIdAsync(int businessId)
    {
        return await _dbSet
            .Where(p => p.BusinessId == businessId)
            .Include(p => p.ProductType)
            .ToListAsync();
    }

    public async Task<Product?> GetBySkuAsync(string sku)
    {
        return await _dbSet.FirstOrDefaultAsync(p => p.Sku == sku);
    }
}

public class StockRepository : Repository<Stock>, IStockRepository
{
    public StockRepository(ApplicationDbContext context) : base(context)
    {
    }

    public async Task<IEnumerable<Stock>> GetByProductIdAsync(int productId)
    {
        return await _dbSet
            .Where(s => s.ProductId == productId)
            .Include(s => s.Flow)
            .OrderByDescending(s => s.Date)
            .ToListAsync();
    }

    public async Task<int> GetCurrentStockAsync(int productId)
    {
        var stocks = await _dbSet
            .Where(s => s.ProductId == productId)
            .Include(s => s.Flow)
            .ToListAsync();

        int currentStock = 0;
        foreach (var stock in stocks)
        {
            if (stock.Flow.Type == "entrada")
            {
                currentStock += stock.Amount;
            }
            else if (stock.Flow.Type == "salida")
            {
                currentStock -= stock.Amount;
            }
        }

        return currentStock;
    }
}
