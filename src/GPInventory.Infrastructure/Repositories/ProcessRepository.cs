using GPInventory.Application.Interfaces;
using GPInventory.Domain.Entities;
using GPInventory.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace GPInventory.Infrastructure.Repositories;

public class ProcessRepository : IProcessRepository
{
    private readonly ApplicationDbContext _context;

    public ProcessRepository(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Process?> GetByIdAsync(int id)
    {
        return await _context.Processes
            .FirstOrDefaultAsync(p => p.Id == id);
    }

    public async Task<Process?> GetByIdWithDetailsAsync(int id)
    {
        var process = await _context.Processes
            .Include(p => p.Product)
            .Include(p => p.TimeUnit)
            .Include(p => p.Store)
            .FirstOrDefaultAsync(p => p.Id == id);

        if (process != null)
        {
            await LoadProcessSupplies(process);
            await LoadProcessComponents(process);
        }

        return process;
    }

    public async Task<IEnumerable<Process>> GetAllAsync()
    {
        return await _context.Processes
            .Include(p => p.Product)
            .Include(p => p.TimeUnit)
            .Include(p => p.Store)
            .ToListAsync();
    }

    public async Task<IEnumerable<Process>> GetByStoreIdAsync(int storeId)
    {
        return await _context.Processes
            .Include(p => p.Product)
            .Include(p => p.TimeUnit)
            .Include(p => p.Store)
            .Where(p => p.StoreId == storeId)
            .ToListAsync();
    }

    public async Task<IEnumerable<Process>> GetByProductIdAsync(int productId)
    {
        return await _context.Processes
            .Include(p => p.Product)
            .Include(p => p.TimeUnit)
            .Include(p => p.Store)
            .Where(p => p.ProductId == productId)
            .ToListAsync();
    }

    public async Task<Process?> GetByNameAsync(string name, int storeId)
    {
        return await _context.Processes
            .FirstOrDefaultAsync(p => p.Name == name && p.StoreId == storeId);
    }

    public async Task<Process> CreateAsync(Process process)
    {
        _context.Processes.Add(process);
        await _context.SaveChangesAsync();
        return process;
    }

    public async Task<Process> UpdateAsync(Process process)
    {
        _context.Entry(process).State = EntityState.Modified;
        await _context.SaveChangesAsync();
        return process;
    }

    public async Task DeleteAsync(int id)
    {
        var process = await GetByIdAsync(id);
        if (process != null)
        {
            _context.Processes.Remove(process);
            await _context.SaveChangesAsync();
        }
    }

    public async Task<bool> ExistsAsync(int id)
    {
        return await _context.Processes.AnyAsync(p => p.Id == id);
    }

    public async Task<IEnumerable<Process>> GetProcessesWithDetailsAsync(int[]? storeIds = null, int? businessId = null)
    {
        var processes = new List<Process>();

        try
        {
            await _context.Database.OpenConnectionAsync();

            try
            {
                using var command = _context.Database.GetDbConnection().CreateCommand();

                var whereConditions = new List<string>();
                if (storeIds != null && storeIds.Length > 0)
                {
                    var storeIdsList = string.Join(",", storeIds);
                    whereConditions.Add($"p.store_id IN ({storeIdsList})");
                }
                if (businessId.HasValue)
                {
                    whereConditions.Add("s.id_business = @businessId");
                }

                var whereClause = whereConditions.Count > 0 ? "WHERE " + string.Join(" AND ", whereConditions) : "";

                command.CommandText = $@"
                    SELECT 
                        p.id,
                        p.name,
                        p.description,
                        p.product_id,
                        p.production_time,
                        p.time_unit_id,
                        p.store_id,
                        p.created_at,
                        p.updated_at,
                        p.active,
                        prod.id as prod_id,
                        prod.name as prod_name,
                        prod.sku as prod_sku,
                        tu.id as tu_id,
                        tu.name as tu_name,
                        s.id as store_id,
                        s.name as store_name,
                        s.location as store_location,
                        s.id_business as store_business_id
                    FROM processes p
                    LEFT JOIN product prod ON p.product_id = prod.id
                    LEFT JOIN time_units tu ON p.time_unit_id = tu.id
                    LEFT JOIN store s ON p.store_id = s.id
                    {whereClause}
                    ORDER BY p.name";

                if (businessId.HasValue)
                {
                    var businessIdParam = command.CreateParameter();
                    businessIdParam.ParameterName = "@businessId";
                    businessIdParam.Value = businessId.Value;
                    command.Parameters.Add(businessIdParam);
                }

                using var reader = await command.ExecuteReaderAsync();

                while (await reader.ReadAsync())
                {
                    try
                    {
                        var process = new Process
                        {
                            Id = reader.GetInt32(0),
                            Name = reader.GetString(1),
                            Description = reader.IsDBNull(2) ? null : reader.GetString(2),
                            ProductId = reader.GetInt32(3),
                            ProductionTime = reader.GetInt32(4),
                            TimeUnitId = reader.GetInt32(5),
                            StoreId = reader.GetInt32(6),
                            CreatedAt = reader.IsDBNull(7) ? DateTime.UtcNow : reader.GetDateTime(7),
                            UpdatedAt = reader.IsDBNull(8) ? DateTime.UtcNow : reader.GetDateTime(8),
                            IsActive = reader.IsDBNull(9) ? true : reader.GetBoolean(9)
                        };

                        // Populate Product
                        if (!reader.IsDBNull(10))
                        {
                            process.Product = new Product
                            {
                                Id = reader.GetInt32(10),
                                Name = reader.GetString(11),
                                Sku = reader.IsDBNull(12) ? null : reader.GetString(12)
                            };
                        }

                        // Populate TimeUnit
                        if (!reader.IsDBNull(13))
                        {
                            process.TimeUnit = new TimeUnit
                            {
                                Id = reader.GetInt32(13),
                                Name = reader.GetString(14)
                            };
                        }

                        // Populate Store
                        if (!reader.IsDBNull(15))
                        {
                            process.Store = new Store
                            {
                                Id = reader.GetInt32(15),
                                Name = reader.GetString(16),
                                Location = reader.IsDBNull(17) ? null : reader.GetString(17),
                                BusinessId = reader.GetInt32(18)
                            };
                        }

                        processes.Add(process);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Error processing process row: {ex.Message}");
                    }
                }
            }
            finally
            {
                await _context.Database.CloseConnectionAsync();
            }

            // Load ProcessSupplies and ProcessComponents separately for each process
            foreach (var process in processes)
            {
                try
                {
                    await LoadProcessSupplies(process);
                    await LoadProcessComponents(process);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error loading process details for process {process.Id}: {ex.Message}");
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error in GetProcessesWithDetailsAsync: {ex.Message}");
            Console.WriteLine($"Stack trace: {ex.StackTrace}");
            throw new Exception($"Failed to retrieve processes: {ex.Message}", ex);
        }

        return processes;
    }

    private async Task LoadProcessSupplies(Process process)
    {
        var processSupplies = new List<ProcessSupply>();

        await _context.Database.OpenConnectionAsync();

        try
        {
            using var command = _context.Database.GetDbConnection().CreateCommand();
            command.CommandText = @"
                SELECT 
                    ps.id,
                    ps.process_id,
                    ps.supply_id,
                    ps.order,
                    ps.created_at,
                    ps.updated_at,
                    ps.active,
                    s.id as supply_id,
                    s.name as supply_name,
                    s.description as supply_description,
                    s.unit_measure_id,
                    um.name as um_name,
                    um.symbol as um_symbol
                FROM process_supplies ps
                LEFT JOIN supplies s ON ps.supply_id = s.id
                LEFT JOIN unit_measures um ON s.unit_measure_id = um.id
                WHERE ps.process_id = @processId";

            var processIdParam = command.CreateParameter();
            processIdParam.ParameterName = "@processId";
            processIdParam.Value = process.Id;
            command.Parameters.Add(processIdParam);

            using var reader = await command.ExecuteReaderAsync();

            while (await reader.ReadAsync())
            {
                var processSupply = new ProcessSupply
                {
                    Id = reader.GetInt32(0),
                    ProcessId = reader.GetInt32(1),
                    SupplyId = reader.GetInt32(2),
                    Order = reader.GetInt32(3),
                    CreatedAt = reader.IsDBNull(4) ? DateTime.UtcNow : reader.GetDateTime(4),
                    UpdatedAt = reader.IsDBNull(5) ? DateTime.UtcNow : reader.GetDateTime(5),
                    IsActive = reader.IsDBNull(6) ? true : reader.GetBoolean(6)
                };

                if (!reader.IsDBNull(7))
                {
                    processSupply.Supply = new Supply
                    {
                        Id = reader.GetInt32(7),
                        Name = reader.GetString(8),
                        Description = reader.IsDBNull(9) ? null : reader.GetString(9),
                        UnitMeasureId = reader.GetInt32(10)
                    };

                    if (!reader.IsDBNull(11))
                    {
                        processSupply.Supply.UnitMeasure = new UnitMeasure
                        {
                            Name = reader.GetString(11),
                            Symbol = reader.IsDBNull(12) ? null : reader.GetString(12)
                        };
                    }
                }

                processSupplies.Add(processSupply);
            }
        }
        finally
        {
            await _context.Database.CloseConnectionAsync();
        }

        process.ProcessSupplies = processSupplies;
    }

    private async Task LoadProcessComponents(Process process)
    {
        var processComponents = new List<ProcessComponent>();

        await _context.Database.OpenConnectionAsync();

        try
        {
            using var command = _context.Database.GetDbConnection().CreateCommand();
            command.CommandText = @"
                SELECT 
                    pc.id,
                    pc.process_id,
                    pc.component_id,
                    pc.order,
                    pc.created_at,
                    pc.updated_at,
                    pc.is_active,
                    c.id as component_id,
                    c.name as component_name,
                    c.description as component_description,
                    c.yield_amount,
                    c.unit_measure_id
                FROM process_components pc
                LEFT JOIN components c ON pc.component_id = c.id
                WHERE pc.process_id = @processId";

            var processIdParam = command.CreateParameter();
            processIdParam.ParameterName = "@processId";
            processIdParam.Value = process.Id;
            command.Parameters.Add(processIdParam);

            using var reader = await command.ExecuteReaderAsync();

            while (await reader.ReadAsync())
            {
                var processComponent = new ProcessComponent
                {
                    Id = reader.GetInt32(0),
                    ProcessId = reader.GetInt32(1),
                    ComponentId = reader.GetInt32(2),
                    Order = reader.GetInt32(3),
                    CreatedAt = reader.IsDBNull(4) ? DateTime.UtcNow : reader.GetDateTime(4),
                    UpdatedAt = reader.IsDBNull(5) ? DateTime.UtcNow : reader.GetDateTime(5),
                    IsActive = reader.IsDBNull(6) ? true : reader.GetBoolean(6)
                };

                if (!reader.IsDBNull(7))
                {
                    processComponent.Component = new Component
                    {
                        Id = reader.GetInt32(7),
                        Name = reader.GetString(8),
                        Description = reader.IsDBNull(9) ? null : reader.GetString(9),
                        YieldAmount = reader.GetDecimal(10),
                        UnitMeasureId = reader.GetInt32(11)
                    };
                }

                processComponents.Add(processComponent);
            }
        }
        finally
        {
            await _context.Database.CloseConnectionAsync();
        }

        process.ProcessComponents = processComponents;
    }

    public async Task<decimal?> GetAverageCostAsync(int processId)
    {
        try
        {
            await _context.Database.OpenConnectionAsync();

            try
            {
                using var command = _context.Database.GetDbConnection().CreateCommand();

                command.CommandText = @"
                    SELECT AVG(cost) 
                    FROM process_done 
                    WHERE process_id = @processId 
                    AND cost IS NOT NULL";

                var processIdParam = command.CreateParameter();
                processIdParam.ParameterName = "@processId";
                processIdParam.Value = processId;
                command.Parameters.Add(processIdParam);

                var result = await command.ExecuteScalarAsync();
                
                if (result == null || result == DBNull.Value)
                    return null;

                return Convert.ToDecimal(result);
            }
            finally
            {
                await _context.Database.CloseConnectionAsync();
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error in GetAverageCostAsync for process {processId}: {ex.Message}");
            return null;
        }
    }

    public async Task<(DateTime? date, string? userName, decimal? amount)?> GetLastExecutionAsync(int processId)
    {
        try
        {
            await _context.Database.OpenConnectionAsync();

            try
            {
                using var command = _context.Database.GetDbConnection().CreateCommand();

                command.CommandText = @"
                    SELECT 
                        pd.completed_at,
                        u.name,
                        pd.quantity_produced
                    FROM process_done pd
                    LEFT JOIN users u ON pd.created_by_user_id = u.id
                    WHERE pd.process_id = @processId
                    ORDER BY pd.completed_at DESC
                    LIMIT 1";

                var processIdParam = command.CreateParameter();
                processIdParam.ParameterName = "@processId";
                processIdParam.Value = processId;
                command.Parameters.Add(processIdParam);

                using var reader = await command.ExecuteReaderAsync();

                if (await reader.ReadAsync())
                {
                    var date = reader.IsDBNull(0) ? (DateTime?)null : reader.GetDateTime(0);
                    var userName = reader.IsDBNull(1) ? null : reader.GetString(1);
                    var amount = reader.IsDBNull(2) ? (decimal?)null : reader.GetDecimal(2);

                    return (date, userName, amount);
                }

                return null;
            }
            finally
            {
                await _context.Database.CloseConnectionAsync();
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error in GetLastExecutionAsync for process {processId}: {ex.Message}");
            return null;
        }
    }

    public async Task<int> GetProcessSuppliesStockStatusAsync(int processId)
    {
        try
        {
            await _context.Database.OpenConnectionAsync();

            try
            {
                using var command = _context.Database.GetDbConnection().CreateCommand();

                // Query que verifica el stock de todos los supplies del proceso
                // Retorna: 0 = OK (todos tienen stock suficiente)
                //          1 = Warning (alguno tiene stock bajo pero > 0)
                //          2 = Critical (alguno tiene stock = 0)
                command.CommandText = @"
                    SELECT 
                        CASE 
                            WHEN MIN(current_stock) <= 0 THEN 2
                            WHEN MIN(current_stock) < MIN(minimum_stock) THEN 1
                            ELSE 0
                        END as stock_status
                    FROM (
                        SELECT 
                            ps.supply_id,
                            s.minimum_stock,
                            COALESCE(SUM(CASE WHEN se.amount > 0 AND se.active = 1 THEN se.amount ELSE 0 END), 0) +
                            COALESCE(SUM(CASE 
                                WHEN se.amount < 0 AND se.active = 1 THEN 
                                    CASE 
                                        WHEN se.supply_entry_id IS NULL THEN se.amount
                                        WHEN EXISTS (SELECT 1 FROM supply_entry parent WHERE parent.id = se.supply_entry_id AND parent.active = 1) THEN se.amount
                                        ELSE 0
                                    END
                                ELSE 0 
                            END), 0) as current_stock
                        FROM process_supply ps
                        INNER JOIN supplies s ON ps.supply_id = s.id
                        LEFT JOIN supply_entry se ON s.id = se.supply_id
                        WHERE ps.process_id = @processId
                        GROUP BY ps.supply_id, s.minimum_stock
                    ) as supply_stocks";

                var processIdParam = command.CreateParameter();
                processIdParam.ParameterName = "@processId";
                processIdParam.Value = processId;
                command.Parameters.Add(processIdParam);

                var result = await command.ExecuteScalarAsync();
                
                if (result == null || result == DBNull.Value)
                    return 0; // OK por defecto si no hay supplies

                return Convert.ToInt32(result);
            }
            finally
            {
                await _context.Database.CloseConnectionAsync();
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error in GetProcessSuppliesStockStatusAsync for process {processId}: {ex.Message}");
            return 0; // OK por defecto en caso de error
        }
    }

    public async Task<int> GetExecutionCountAsync(int processId)
    {
        try
        {
            await _context.Database.OpenConnectionAsync();

            try
            {
                using var command = _context.Database.GetDbConnection().CreateCommand();

                command.CommandText = @"
                    SELECT COUNT(*) 
                    FROM process_done 
                    WHERE process_id = @processId";

                var processIdParam = command.CreateParameter();
                processIdParam.ParameterName = "@processId";
                processIdParam.Value = processId;
                command.Parameters.Add(processIdParam);

                var result = await command.ExecuteScalarAsync();
                
                if (result == null || result == DBNull.Value)
                    return 0;

                return Convert.ToInt32(result);
            }
            finally
            {
                await _context.Database.CloseConnectionAsync();
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error in GetExecutionCountAsync for process {processId}: {ex.Message}");
            return 0;
        }
    }
}
