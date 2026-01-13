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
        var connection = _context.Database.GetDbConnection();
        var shouldCloseConnection = connection.State == System.Data.ConnectionState.Closed;
        
        if (shouldCloseConnection)
            await connection.OpenAsync();
        
        try
        {
            using var command = connection.CreateCommand();
            command.CommandText = @"
                SELECT 
                    m.id, m.product_id, m.process_done_id, m.business_id, m.store_id,
                    m.amount, m.cost, m.date, m.expiration_date, m.stock_id, m.notes,
                    m.status, m.is_active, m.created_at, m.updated_at, m.created_by_user_id,
                    pr.id as prod_id, pr.name as product_name, pr.price, pr.cost as product_cost,
                    pd.id as pd_id, pd.process_id as pd_process_id, pd.completed_at, pd.notes as pd_notes,
                    p.id as proc_id, p.name as process_name,
                    s.id as store_id, s.name as store_name,
                    b.id as business_id, b.company_name as business_name
                FROM manufacture m
                LEFT JOIN product pr ON m.product_id = pr.id
                LEFT JOIN process_done pd ON m.process_done_id = pd.id
                LEFT JOIN processes p ON pd.process_id = p.id
                LEFT JOIN store s ON m.store_id = s.id
                LEFT JOIN business b ON m.business_id = b.id
                WHERE m.id = @id
                LIMIT 1";
            
            var param = command.CreateParameter();
            param.ParameterName = "@id";
            param.Value = id;
            command.Parameters.Add(param);
            
            using var reader = await command.ExecuteReaderAsync();
            
            if (await reader.ReadAsync())
            {
                var manufacture = new Manufacture(
                    productId: reader.GetInt32(1),
                    processDoneId: reader.GetInt32(2),
                    businessId: reader.GetInt32(3),
                    amount: reader.GetInt32(5),
                    cost: reader.IsDBNull(6) ? null : reader.GetDecimal(6),
                    notes: reader.IsDBNull(10) ? null : reader.GetString(10),
                    expirationDate: reader.IsDBNull(8) ? null : reader.GetDateTime(8)
                );
                
                typeof(Manufacture).GetProperty("Id")?.SetValue(manufacture, reader.GetInt32(0));
                
                manufacture.StoreId = reader.IsDBNull(4) ? null : reader.GetInt32(4);
                manufacture.Date = reader.GetDateTime(7);
                manufacture.StockId = reader.IsDBNull(9) ? null : reader.GetInt32(9);
                manufacture.Status = reader.GetString(11);
                manufacture.IsActive = reader.GetBoolean(12);
                manufacture.CreatedAt = reader.GetDateTime(13);
                manufacture.UpdatedAt = reader.GetDateTime(14);
                manufacture.CreatedByUserId = reader.IsDBNull(15) ? null : reader.GetInt32(15);
                
                // Set Product navigation
                if (!reader.IsDBNull(16))
                {
                    manufacture.Product = new Product
                    {
                        Id = reader.GetInt32(16),
                        Name = reader.GetString(17),
                        Price = reader.IsDBNull(18) ? 0 : reader.GetInt32(18),
                        Cost = reader.IsDBNull(19) ? 0 : reader.GetInt32(19)
                    };
                }
                
                // Set ProcessDone navigation
                if (!reader.IsDBNull(20))
                {
                    manufacture.ProcessDone = new ProcessDone
                    {
                        Id = reader.GetInt32(20),
                        ProcessId = reader.GetInt32(21),
                        CompletedAt = reader.GetDateTime(22),
                        Notes = reader.IsDBNull(23) ? null : reader.GetString(23)
                    };
                    
                    // Set Process navigation inside ProcessDone
                    if (!reader.IsDBNull(24))
                    {
                        manufacture.ProcessDone.Process = new Process
                        {
                            Id = reader.GetInt32(24),
                            Name = reader.GetString(25)
                        };
                    }
                }
                
                // Set Store navigation
                if (!reader.IsDBNull(26))
                {
                    manufacture.Store = new Store
                    {
                        Id = reader.GetInt32(26),
                        Name = reader.GetString(27)
                    };
                }
                
                // Set Business navigation
                if (!reader.IsDBNull(28))
                {
                    manufacture.Business = new Business
                    {
                        Id = reader.GetInt32(28),
                        CompanyName = reader.GetString(29)
                    };
                }
                
                return manufacture;
            }
            
            return null;
        }
        finally
        {
            if (shouldCloseConnection)
                await connection.CloseAsync();
        }
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
        var manufactures = new List<Manufacture>();
        var connection = _context.Database.GetDbConnection();
        var shouldCloseConnection = connection.State == System.Data.ConnectionState.Closed;
        
        if (shouldCloseConnection)
            await connection.OpenAsync();
        
        try
        {
            using var command = connection.CreateCommand();
            command.CommandText = @"
                SELECT 
                    m.id, m.product_id, m.process_done_id, m.business_id, m.store_id,
                    m.amount, m.cost, m.date, m.expiration_date, m.stock_id, m.notes,
                    m.status, m.is_active, m.created_at, m.updated_at, m.created_by_user_id,
                    pr.id as prod_id, pr.name as product_name,
                    pd.id as pd_id, pd.process_id as pd_process_id, pd.completed_at, pd.notes as pd_notes,
                    p.id as proc_id, p.name as process_name
                FROM manufacture m
                LEFT JOIN product pr ON m.product_id = pr.id
                LEFT JOIN process_done pd ON m.process_done_id = pd.id
                LEFT JOIN processes p ON pd.process_id = p.id
                WHERE m.business_id = @businessId
                ORDER BY m.created_at DESC";
            
            var param = command.CreateParameter();
            param.ParameterName = "@businessId";
            param.Value = businessId;
            command.Parameters.Add(param);
            
            using var reader = await command.ExecuteReaderAsync();
            
            while (await reader.ReadAsync())
            {
                var manufacture = new Manufacture(
                    productId: reader.GetInt32(1),
                    processDoneId: reader.GetInt32(2),
                    businessId: reader.GetInt32(3),
                    amount: reader.GetInt32(5),
                    cost: reader.IsDBNull(6) ? null : reader.GetDecimal(6),
                    notes: reader.IsDBNull(10) ? null : reader.GetString(10),
                    expirationDate: reader.IsDBNull(8) ? null : reader.GetDateTime(8)
                );
                
                // Set Id
                typeof(Manufacture).GetProperty("Id")?.SetValue(manufacture, reader.GetInt32(0));
                
                manufacture.StoreId = reader.IsDBNull(4) ? null : reader.GetInt32(4);
                manufacture.Date = reader.GetDateTime(7);
                manufacture.StockId = reader.IsDBNull(9) ? null : reader.GetInt32(9);
                manufacture.Status = reader.GetString(11);
                manufacture.IsActive = reader.GetBoolean(12);
                manufacture.CreatedAt = reader.GetDateTime(13);
                manufacture.UpdatedAt = reader.GetDateTime(14);
                manufacture.CreatedByUserId = reader.IsDBNull(15) ? null : reader.GetInt32(15);
                
                // Set Product navigation
                if (!reader.IsDBNull(16))
                {
                    manufacture.Product = new Product
                    {
                        Id = reader.GetInt32(16),
                        Name = reader.GetString(17)
                    };
                }
                
                // Set ProcessDone navigation
                if (!reader.IsDBNull(18))
                {
                    manufacture.ProcessDone = new ProcessDone
                    {
                        Id = reader.GetInt32(18),
                        ProcessId = reader.GetInt32(19),
                        CompletedAt = reader.GetDateTime(20),
                        Notes = reader.IsDBNull(21) ? null : reader.GetString(21)
                    };
                    
                    // Set Process navigation inside ProcessDone
                    if (!reader.IsDBNull(22))
                    {
                        var processId = reader.GetInt32(22);
                        var processName = reader.IsDBNull(23) ? null : reader.GetString(23);
                        
                        Console.WriteLine($"🔍 Process data - ID: {processId}, Name: {processName ?? "NULL"}");
                        
                        manufacture.ProcessDone.Process = new Process
                        {
                            Id = processId,
                            Name = processName ?? "Unknown"
                        };
                    }
                }
                
                manufactures.Add(manufacture);
            }
        }
        finally
        {
            if (shouldCloseConnection)
                await connection.CloseAsync();
        }
        
        return manufactures;
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
        var sql = @"
            INSERT INTO manufacture (
                product_id, 
                process_done_id, 
                business_id, 
                store_id, 
                amount, 
                cost, 
                date, 
                expiration_date, 
                stock_id, 
                notes, 
                status, 
                created_by_user_id,
                is_active, 
                created_at, 
                updated_at
            ) VALUES (
                @p0, @p1, @p2, @p3, @p4, @p5, @p6, @p7, @p8, @p9, @p10, @p11, @p12, @p13, @p14
            );
            SELECT LAST_INSERT_ID();";

        var connection = _context.Database.GetDbConnection();
        var shouldCloseConnection = connection.State == System.Data.ConnectionState.Closed;
        
        if (shouldCloseConnection)
            await connection.OpenAsync();
        
        try
        {
            using var command = connection.CreateCommand();
            command.CommandText = sql;
            
            var p0 = command.CreateParameter();
            p0.ParameterName = "@p0";
            p0.Value = manufacture.ProductId;
            command.Parameters.Add(p0);
            
            var p1 = command.CreateParameter();
            p1.ParameterName = "@p1";
            p1.Value = manufacture.ProcessDoneId;
            command.Parameters.Add(p1);
            
            var p2 = command.CreateParameter();
            p2.ParameterName = "@p2";
            p2.Value = manufacture.BusinessId;
            command.Parameters.Add(p2);
            
            var p3 = command.CreateParameter();
            p3.ParameterName = "@p3";
            p3.Value = (object?)manufacture.StoreId ?? DBNull.Value;
            command.Parameters.Add(p3);
            
            var p4 = command.CreateParameter();
            p4.ParameterName = "@p4";
            p4.Value = manufacture.Amount;
            command.Parameters.Add(p4);
            
            var p5 = command.CreateParameter();
            p5.ParameterName = "@p5";
            p5.Value = (object?)manufacture.Cost ?? DBNull.Value;
            command.Parameters.Add(p5);
            
            var p6 = command.CreateParameter();
            p6.ParameterName = "@p6";
            p6.Value = manufacture.Date;
            command.Parameters.Add(p6);
            
            var p7 = command.CreateParameter();
            p7.ParameterName = "@p7";
            p7.Value = (object?)manufacture.ExpirationDate ?? DBNull.Value;
            command.Parameters.Add(p7);
            
            var p8 = command.CreateParameter();
            p8.ParameterName = "@p8";
            p8.Value = (object?)manufacture.StockId ?? DBNull.Value;
            command.Parameters.Add(p8);
            
            var p9 = command.CreateParameter();
            p9.ParameterName = "@p9";
            p9.Value = (object?)manufacture.Notes ?? DBNull.Value;
            command.Parameters.Add(p9);
            
            var p10 = command.CreateParameter();
            p10.ParameterName = "@p10";
            p10.Value = manufacture.Status;
            command.Parameters.Add(p10);
            
            var p11 = command.CreateParameter();
            p11.ParameterName = "@p11";
            p11.Value = (object?)manufacture.CreatedByUserId ?? DBNull.Value;
            command.Parameters.Add(p11);
            
            var p12 = command.CreateParameter();
            p12.ParameterName = "@p12";
            p12.Value = manufacture.IsActive;
            command.Parameters.Add(p12);
            
            var p13 = command.CreateParameter();
            p13.ParameterName = "@p13";
            p13.Value = manufacture.CreatedAt;
            command.Parameters.Add(p13);
            
            var p14 = command.CreateParameter();
            p14.ParameterName = "@p14";
            p14.Value = manufacture.UpdatedAt;
            command.Parameters.Add(p14);
            
            var result = await command.ExecuteScalarAsync();
            manufacture.Id = Convert.ToInt32(result);
            
            return manufacture;
        }
        finally
        {
            if (shouldCloseConnection)
                await connection.CloseAsync();
        }
    }

    public async Task<Manufacture> UpdateAsync(Manufacture manufacture)
    {
        manufacture.UpdatedAt = DateTime.UtcNow;
        
        var sql = @"
            UPDATE manufacture 
            SET product_id = @p0,
                process_done_id = @p1,
                business_id = @p2,
                store_id = @p3,
                amount = @p4,
                cost = @p5,
                date = @p6,
                expiration_date = @p7,
                stock_id = @p8,
                notes = @p9,
                status = @p10,
                is_active = @p11,
                updated_at = @p12
            WHERE id = @p13";
        
        var connection = _context.Database.GetDbConnection();
        var shouldCloseConnection = connection.State == System.Data.ConnectionState.Closed;
        
        if (shouldCloseConnection)
            await connection.OpenAsync();
        
        try
        {
            using var command = connection.CreateCommand();
            command.CommandText = sql;
            
            var p0 = command.CreateParameter();
            p0.ParameterName = "@p0";
            p0.Value = manufacture.ProductId;
            command.Parameters.Add(p0);
            
            var p1 = command.CreateParameter();
            p1.ParameterName = "@p1";
            p1.Value = manufacture.ProcessDoneId;
            command.Parameters.Add(p1);
            
            var p2 = command.CreateParameter();
            p2.ParameterName = "@p2";
            p2.Value = manufacture.BusinessId;
            command.Parameters.Add(p2);
            
            var p3 = command.CreateParameter();
            p3.ParameterName = "@p3";
            p3.Value = (object?)manufacture.StoreId ?? DBNull.Value;
            command.Parameters.Add(p3);
            
            var p4 = command.CreateParameter();
            p4.ParameterName = "@p4";
            p4.Value = manufacture.Amount;
            command.Parameters.Add(p4);
            
            var p5 = command.CreateParameter();
            p5.ParameterName = "@p5";
            p5.Value = (object?)manufacture.Cost ?? DBNull.Value;
            command.Parameters.Add(p5);
            
            var p6 = command.CreateParameter();
            p6.ParameterName = "@p6";
            p6.Value = manufacture.Date;
            command.Parameters.Add(p6);
            
            var p7 = command.CreateParameter();
            p7.ParameterName = "@p7";
            p7.Value = (object?)manufacture.ExpirationDate ?? DBNull.Value;
            command.Parameters.Add(p7);
            
            var p8 = command.CreateParameter();
            p8.ParameterName = "@p8";
            p8.Value = (object?)manufacture.StockId ?? DBNull.Value;
            command.Parameters.Add(p8);
            
            var p9 = command.CreateParameter();
            p9.ParameterName = "@p9";
            p9.Value = (object?)manufacture.Notes ?? DBNull.Value;
            command.Parameters.Add(p9);
            
            var p10 = command.CreateParameter();
            p10.ParameterName = "@p10";
            p10.Value = manufacture.Status;
            command.Parameters.Add(p10);
            
            var p11 = command.CreateParameter();
            p11.ParameterName = "@p11";
            p11.Value = manufacture.IsActive;
            command.Parameters.Add(p11);
            
            var p12 = command.CreateParameter();
            p12.ParameterName = "@p12";
            p12.Value = manufacture.UpdatedAt;
            command.Parameters.Add(p12);
            
            var p13 = command.CreateParameter();
            p13.ParameterName = "@p13";
            p13.Value = manufacture.Id;
            command.Parameters.Add(p13);
            
            await command.ExecuteNonQueryAsync();
            
            return manufacture;
        }
        finally
        {
            if (shouldCloseConnection)
                await connection.CloseAsync();
        }
    }

    public async Task DeleteAsync(int id)
    {
        var sql = @"
            UPDATE manufacture 
            SET is_active = 0,
                updated_at = @p0
            WHERE id = @p1";
        
        var connection = _context.Database.GetDbConnection();
        var shouldCloseConnection = connection.State == System.Data.ConnectionState.Closed;
        
        if (shouldCloseConnection)
            await connection.OpenAsync();
        
        try
        {
            using var command = connection.CreateCommand();
            command.CommandText = sql;
            
            var p0 = command.CreateParameter();
            p0.ParameterName = "@p0";
            p0.Value = DateTime.UtcNow;
            command.Parameters.Add(p0);
            
            var p1 = command.CreateParameter();
            p1.ParameterName = "@p1";
            p1.Value = id;
            command.Parameters.Add(p1);
            
            await command.ExecuteNonQueryAsync();
        }
        finally
        {
            if (shouldCloseConnection)
                await connection.CloseAsync();
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
