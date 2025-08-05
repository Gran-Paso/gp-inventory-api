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

    public async Task<List<(int UserId, string UserName, string RoleName)>> GetBusinessUsersWithRolesAsync(int businessId, string[] targetRoles)
    {
        var result = await _context.UserHasBusinesses
            .Include(ub => ub.User)
            .Include(ub => ub.Role)
            .Where(ub => ub.BusinessId == businessId && targetRoles.Contains(ub.Role.Name))
            .Select(ub => new { 
                UserId = ub.UserId, 
                UserName = ub.User.Name ?? "Usuario", 
                RoleName = ub.Role.Name 
            })
            .ToListAsync();

        return result.Select(x => (x.UserId, x.UserName, x.RoleName)).ToList();
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
            .Include(s => s.FlowType)
            .OrderByDescending(s => s.Date)
            .ToListAsync();
    }

    public async Task<int> GetCurrentStockAsync(int productId)
    {
        var stocks = await _dbSet
            .Where(s => s.ProductId == productId)
            .Include(s => s.FlowType)
            .ToListAsync();

        int currentStock = 0;
        foreach (var stock in stocks)
        {
            // Usar comparación case-insensitive para mayor robustez
            var flowTypeName = stock.FlowType.Name.ToLowerInvariant();
            if (flowTypeName == "entrada" || flowTypeName == "compra")
            {
                currentStock += stock.Amount;
            }
            else if (flowTypeName == "salida" || flowTypeName == "venta")
            {
                currentStock -= stock.Amount;
            }
        }

        return currentStock;
    }

    /// <summary>
    /// Versión optimizada que calcula el stock directamente en la base de datos
    /// </summary>
    public async Task<int> GetCurrentStockOptimizedAsync(int productId)
    {
        return await _dbSet
            .Where(s => s.ProductId == productId)
            .Include(s => s.FlowType)
            .SumAsync(s => 
                s.FlowType.Name.ToLower() == "entrada" || s.FlowType.Name.ToLower() == "compra" 
                    ? s.Amount 
                    : s.FlowType.Name.ToLower() == "salida" || s.FlowType.Name.ToLower() == "venta"
                        ? -s.Amount 
                        : 0);
    }
}
