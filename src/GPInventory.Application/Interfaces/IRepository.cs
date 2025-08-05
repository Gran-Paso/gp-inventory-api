using GPInventory.Domain.Entities;
using System.Linq.Expressions;

namespace GPInventory.Application.Interfaces;

public interface IRepository<T> where T : BaseEntity
{
    Task<T?> GetByIdAsync(int id);
    Task<IEnumerable<T>> GetAllAsync();
    Task<IEnumerable<T>> FindAsync(Expression<Func<T, bool>> predicate);
    Task<T?> FirstOrDefaultAsync(Expression<Func<T, bool>> predicate);
    Task<T> AddAsync(T entity);
    Task UpdateAsync(T entity);
    Task DeleteAsync(int id);
    Task<int> SaveChangesAsync();
}

public interface IUserRepository : IRepository<User>
{
    Task<User?> GetByEmailAsync(string email);
    Task<User?> GetByEmailWithRolesAsync(string email);
    Task<bool> ExistsAsync(string email);
    Task<List<(int UserId, string UserName, string RoleName)>> GetBusinessUsersWithRolesAsync(int businessId, string[] targetRoles);
}

public interface IBusinessRepository : IRepository<Business>
{
    Task<IEnumerable<Business>> GetUserBusinessesAsync(int userId);
}

public interface IProductRepository : IRepository<Product>
{
    Task<IEnumerable<Product>> GetByBusinessIdAsync(int businessId);
    Task<Product?> GetBySkuAsync(string sku);
}

public interface IStockRepository : IRepository<Stock>
{
    Task<IEnumerable<Stock>> GetByProductIdAsync(int productId);
    Task<int> GetCurrentStockAsync(int productId);
}
