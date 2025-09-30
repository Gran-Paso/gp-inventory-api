using System.Text.Json;
using GPInventory.Domain.Entities;
using GPInventory.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace GPInventory.Infrastructure.Services
{
    public interface IProductAuditService
    {
        Task LogProductActionAsync(int productId, string actionType, object? oldValues = null, object? newValues = null, string? changes = null, int? userId = null);
        Task<List<ProductLog>> GetProductLogsAsync(int productId, int limit = 50);
    }

    public class ProductAuditService : IProductAuditService
    {
        private readonly ApplicationDbContext _context;

        public ProductAuditService(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task LogProductActionAsync(int productId, string actionType, object? oldValues = null, object? newValues = null, string? changes = null, int? userId = null)
        {
            try
            {
                var productLog = new ProductLog
                {
                    ProductId = productId,
                    UserId = userId,
                    ActionType = actionType,
                    TableName = "Products",
                    Timestamp = DateTime.UtcNow,
                    Changes = changes,
                    OldValues = oldValues != null ? JsonSerializer.Serialize(oldValues, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase }) : null,
                    NewValues = newValues != null ? JsonSerializer.Serialize(newValues, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase }) : null
                };

                _context.ProductLogs.Add(productLog);
                await _context.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                // Log the error but don't throw to avoid breaking the main operation
                Console.WriteLine($"Error logging product action: {ex.Message}");
            }
        }

        public async Task<List<ProductLog>> GetProductLogsAsync(int productId, int limit = 50)
        {
            return await _context.ProductLogs
                .Where(pl => pl.ProductId == productId)
                .Include(pl => pl.User)
                .Include(pl => pl.Product)
                .OrderByDescending(pl => pl.Timestamp)
                .Take(limit)
                .ToListAsync();
        }
    }
}
