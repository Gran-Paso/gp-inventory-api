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
        return await _context.Processes
            .Include(p => p.Product)
            .Include(p => p.TimeUnit)
            .Include(p => p.Store)
            .Include(p => p.ProcessSupplies)
            .Include(p => p.ProcessComponents)
            .FirstOrDefaultAsync(p => p.Id == id);
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
        _context.Processes.Update(process);
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
                            UpdatedAt = reader.IsDBNull(8) ? DateTime.UtcNow : reader.GetDateTime(8)
                        };

                        // Populate Product
                        if (!reader.IsDBNull(9))
                        {
                            process.Product = new Product
                            {
                                Id = reader.GetInt32(9),
                                Name = reader.GetString(10),
                                Sku = reader.IsDBNull(11) ? null : reader.GetString(11)
                            };
                        }

                        // Populate TimeUnit
                        if (!reader.IsDBNull(12))
                        {
                            process.TimeUnit = new TimeUnit
                            {
                                Id = reader.GetInt32(12),
                                Name = reader.GetString(13)
                            };
                        }

                        // Populate Store
                        if (!reader.IsDBNull(14))
                        {
                            process.Store = new Store
                            {
                                Id = reader.GetInt32(14),
                                Name = reader.GetString(15),
                                Location = reader.IsDBNull(16) ? null : reader.GetString(16),
                                BusinessId = reader.GetInt32(17)
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
                    UpdatedAt = reader.IsDBNull(5) ? DateTime.UtcNow : reader.GetDateTime(5)
                };

                if (!reader.IsDBNull(6))
                {
                    processSupply.Supply = new Supply
                    {
                        Id = reader.GetInt32(6),
                        Name = reader.GetString(7),
                        Description = reader.IsDBNull(8) ? null : reader.GetString(8),
                        UnitMeasureId = reader.GetInt32(9)
                    };

                    if (!reader.IsDBNull(10))
                    {
                        processSupply.Supply.UnitMeasure = new UnitMeasure
                        {
                            Name = reader.GetString(10),
                            Symbol = reader.IsDBNull(11) ? null : reader.GetString(11)
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
                    UpdatedAt = reader.IsDBNull(5) ? DateTime.UtcNow : reader.GetDateTime(5)
                };

                if (!reader.IsDBNull(6))
                {
                    processComponent.Component = new Component
                    {
                        Id = reader.GetInt32(6),
                        Name = reader.GetString(7),
                        Description = reader.IsDBNull(8) ? null : reader.GetString(8),
                        YieldAmount = reader.GetDecimal(9),
                        UnitMeasureId = reader.GetInt32(10)
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
}
