using GPInventory.Application.Interfaces;
using GPInventory.Domain.Entities;
using GPInventory.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using MySqlConnector;

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

    public async Task<Dictionary<int, string>> GetUserNamesByIdsAsync(IEnumerable<int> userIds)
    {
        var result = new Dictionary<int, string>();
        
        var idsList = userIds.ToList();
        if (!idsList.Any())
            return result;

        try
        {
            var idList = string.Join(",", idsList);
            var connectionString = _context.Database.GetConnectionString();
            
            using var connection = new MySqlConnection(connectionString);
            await connection.OpenAsync();

            using var command = connection.CreateCommand();
            command.CommandText = $@"
                SELECT id, name, lastname 
                FROM user
                WHERE id IN ({idList})";

            using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                var id = reader.GetInt32(0);
                var name = reader.GetString(1);
                var lastName = reader.GetString(2);
                result[id] = $"{name} {lastName}".Trim();
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"⚠️ Error loading user names: {ex.Message}");
            // Return empty dictionary on error, don't fail the entire operation
        }

        return result;
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

    /// <summary>
    /// Obtiene la primera entrada de stock disponible para un producto (FIFO)
    /// Similar a GetFirstEntryBySupplyIdAsync pero para stock de productos
    /// </summary>
    public async Task<Stock?> GetFirstAvailableStockAsync(int productId, int storeId)
    {
        using var connection = _context.Database.GetDbConnection();
        await connection.OpenAsync();

        using var command = connection.CreateCommand();
        command.CommandText = @"
            SELECT s.id, s.product, s.amount, s.date, s.flow, s.auction_price, s.cost, s.provider, s.notes, s.id_store, s.sale_id, s.stock_id, s.active, s.created_at, s.updated_at
            FROM stock s 
            INNER JOIN flow_type ft ON s.flow = ft.id
            WHERE s.product = @productId 
              AND s.id_store = @storeId
              AND s.stock_id IS NULL 
              AND s.amount > 0 
              AND s.active = 1
              AND (ft.name = 'entrada' OR ft.name = 'compra')
            ORDER BY s.date ASC, s.created_at ASC
            LIMIT 1";

        var productParam = command.CreateParameter();
        productParam.ParameterName = "@productId";
        productParam.Value = productId;
        command.Parameters.Add(productParam);

        var storeParam = command.CreateParameter();
        storeParam.ParameterName = "@storeId";
        storeParam.Value = storeId;
        command.Parameters.Add(storeParam);

        using var reader = await command.ExecuteReaderAsync();
        if (await reader.ReadAsync())
        {
            var stock = new Stock
            {
                Id = reader.GetInt32(0),
                ProductId = reader.GetInt32(1),
                Amount = reader.GetInt32(2),
                Date = reader.GetDateTime(3),
                FlowTypeId = reader.GetInt32(4),
                AuctionPrice = reader.IsDBNull(5) ? null : reader.GetInt32(5),
                Cost = reader.IsDBNull(6) ? null : reader.GetInt32(6),
                ProviderId = reader.IsDBNull(7) ? null : reader.GetInt32(7),
                Notes = reader.IsDBNull(8) ? null : reader.GetString(8),
                StoreId = reader.GetInt32(9),
                SaleId = reader.IsDBNull(10) ? null : reader.GetInt32(10),
                StockId = reader.IsDBNull(11) ? null : reader.GetInt32(11),
                IsActive = reader.GetBoolean(12),
                CreatedAt = reader.GetDateTime(13),
                UpdatedAt = reader.GetDateTime(14)
            };

            // Calcular el stock disponible real considerando las salidas
            await connection.CloseAsync();
            var stockHistory = await GetStockHistoryAsync(productId, storeId, stock.Id);
            
            // Restar las salidas del stock original
            var remainingAmount = stock.Amount;
            foreach (var historyEntry in stockHistory.Where(s => s.Id != stock.Id && s.Amount < 0))
            {
                var consumedAmount = Math.Abs(historyEntry.Amount);
                remainingAmount -= consumedAmount;
            }
            
            // Actualizar el amount con lo que queda disponible
            stock.Amount = Math.Max(0, remainingAmount);
            
            return stock.Amount > 0 ? stock : null;
        }

        return null;
    }

    /// <summary>
    /// Obtiene todas las entradas de stock disponibles para un producto en una tienda (FIFO)
    /// </summary>
    public async Task<IEnumerable<Stock>> GetAvailableStockEntriesAsync(int productId, int storeId)
    {
        var rawSql = @"
            SELECT s.id, s.product, s.amount, s.date, s.flow, s.auction_price, s.cost, s.provider, s.notes, s.id_store, s.sale_id, s.stock_id, s.active, s.created_at, s.updated_at
            FROM stock s 
            INNER JOIN flow_type ft ON s.flow = ft.id
            WHERE s.product = {0}
              AND s.id_store = {1}
              AND s.stock_id IS NULL 
              AND s.amount > 0 
              AND s.active = 1
              AND (ft.name = 'entrada' OR ft.name = 'compra')
            ORDER BY s.date ASC, s.created_at ASC";

        try
        {
            var stockEntries = await _context.Database
                .SqlQueryRaw<StockRawData>(rawSql, productId, storeId)
                .ToListAsync();

            var availableStocks = stockEntries.Select(se => new Stock
            {
                Id = se.id,
                ProductId = se.product,
                Amount = se.amount,
                Date = se.date,
                FlowTypeId = se.flow,
                AuctionPrice = se.auction_price,
                Cost = se.cost,
                ProviderId = se.provider,
                Notes = se.notes,
                StoreId = se.id_store,
                SaleId = se.sale_id,
                StockId = se.stock_id,
                IsActive = se.active == 1,
                CreatedAt = se.created_at,
                UpdatedAt = se.updated_at
            }).ToList();

            return availableStocks;
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Error getting available stock entries for product {productId} in store {storeId}: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Obtiene el historial de movimientos de stock para un producto en una tienda
    /// </summary>
    public async Task<IEnumerable<Stock>> GetStockHistoryAsync(int productId, int storeId, int? specificStockId = null)
    {
        var stockMovements = new List<Stock>();

        using var connection = _context.Database.GetDbConnection();
        await connection.OpenAsync();

        using var command = connection.CreateCommand();

        if (specificStockId == null)
        {
            command.CommandText = @"
                SELECT s.id, s.product, s.amount, s.date, s.flow, s.auction_price, s.cost, s.provider, s.notes, s.id_store, s.sale_id, s.stock_id, s.active, s.created_at, s.updated_at,
                       ft.name as flow_type_name
                FROM stock s 
                INNER JOIN flow_type ft ON s.flow = ft.id
                WHERE s.product = @productId AND s.id_store = @storeId
                ORDER BY s.date ASC, s.created_at ASC";
        }
        else
        {
            command.CommandText = @"
                SELECT s.id, s.product, s.amount, s.date, s.flow, s.auction_price, s.cost, s.provider, s.notes, s.id_store, s.sale_id, s.stock_id, s.active, s.created_at, s.updated_at,
                       ft.name as flow_type_name
                FROM stock s 
                INNER JOIN flow_type ft ON s.flow = ft.id
                WHERE s.product = @productId AND s.id_store = @storeId
                ORDER BY s.date ASC, s.created_at ASC";
        }

        var productParam = command.CreateParameter();
        productParam.ParameterName = "@productId";
        productParam.Value = productId;
        command.Parameters.Add(productParam);

        var storeParam = command.CreateParameter();
        storeParam.ParameterName = "@storeId";
        storeParam.Value = storeId;
        command.Parameters.Add(storeParam);

        using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            var stock = new Stock
            {
                Id = reader.GetInt32(0),
                ProductId = reader.GetInt32(1),
                Amount = reader.GetInt32(2),
                Date = reader.GetDateTime(3),
                FlowTypeId = reader.GetInt32(4),
                AuctionPrice = reader.IsDBNull(5) ? null : reader.GetInt32(5),
                Cost = reader.IsDBNull(6) ? null : reader.GetInt32(6),
                ProviderId = reader.IsDBNull(7) ? null : reader.GetInt32(7),
                Notes = reader.IsDBNull(8) ? null : reader.GetString(8),
                StoreId = reader.GetInt32(9),
                SaleId = reader.IsDBNull(10) ? null : reader.GetInt32(10),
                StockId = reader.IsDBNull(11) ? null : reader.GetInt32(11),
                IsActive = reader.GetBoolean(12),
                CreatedAt = reader.GetDateTime(13),
                UpdatedAt = reader.GetDateTime(14)
            };

            // Crear el FlowType para compatibilidad
            if (!reader.IsDBNull(15))
            {
                stock.FlowType = new FlowType
                {
                    Id = stock.FlowTypeId,
                    Name = reader.GetString(15)
                };
            }

            stockMovements.Add(stock);
        }

        return stockMovements;
    }

    /// <summary>
    /// Calcula el stock disponible con lógica FIFO para un producto en una tienda específica
    /// </summary>
    public async Task<int> GetCurrentStockFIFOAsync(int productId, int storeId)
    {
        using var connection = _context.Database.GetDbConnection();
        await connection.OpenAsync();

        using var command = connection.CreateCommand();
        command.CommandText = @"
            SELECT 
                COALESCE(SUM(CASE 
                    WHEN ft.name IN ('entrada', 'compra') AND s.stock_id IS NULL AND s.active = 1 THEN s.amount 
                    ELSE 0 
                END), 0) as total_incoming,
                COALESCE(SUM(CASE 
                    WHEN ft.name IN ('salida', 'venta') AND s.active = 1 THEN s.amount 
                    ELSE 0 
                END), 0) as total_outgoing
            FROM stock s
            INNER JOIN flow_type ft ON s.flow = ft.id
            WHERE s.product = @productId AND s.id_store = @storeId AND s.active = 1";

        var productParam = command.CreateParameter();
        productParam.ParameterName = "@productId";
        productParam.Value = productId;
        command.Parameters.Add(productParam);

        var storeParam = command.CreateParameter();
        storeParam.ParameterName = "@storeId";
        storeParam.Value = storeId;
        command.Parameters.Add(storeParam);

        using var reader = await command.ExecuteReaderAsync();
        if (await reader.ReadAsync())
        {
            var totalIncoming = reader.GetInt32(0);
            var totalOutgoing = reader.GetInt32(1);
            return Math.Max(0, totalIncoming - totalOutgoing);
        }

        return 0;
    }

    // Clase helper para mapear resultados SQL crudos del stock
    private class StockRawData
    {
        public int id { get; set; }
        public int product { get; set; }
        public int amount { get; set; }
        public DateTime date { get; set; }
        public int flow { get; set; }
        public int? auction_price { get; set; }
        public int? cost { get; set; }
        public int? provider { get; set; }
        public string? notes { get; set; }
        public int id_store { get; set; }
        public int? sale_id { get; set; }
        public int? stock_id { get; set; }
        public int active { get; set; }
        public DateTime created_at { get; set; }
        public DateTime updated_at { get; set; }
    }
}
