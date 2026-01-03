using GPInventory.Application.Interfaces;
using GPInventory.Domain.Entities;
using GPInventory.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace GPInventory.Infrastructure.Repositories;

public class SupplyRepository : ISupplyRepository
{
    private readonly ApplicationDbContext _context;

    public SupplyRepository(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Supply?> GetByIdAsync(int id)
    {
        Supply? supply = null;

        await _context.Database.OpenConnectionAsync();
        
        try
        {
            using var command = _context.Database.GetDbConnection().CreateCommand();
            command.CommandText = @"
                SELECT 
                    s.id,
                    s.name,
                    s.description,
                    s.business_id,
                    s.store_id,
                    s.unit_measure_id,
                    s.fixed_expense_id,
                    s.supply_category_id,
                    s.type,
                    s.active,
                    s.created_at,
                    s.updated_at
                FROM supplies s
                WHERE s.id = @supplyId";
            
            var supplyIdParam = command.CreateParameter();
            supplyIdParam.ParameterName = "@supplyId";
            supplyIdParam.Value = id;
            command.Parameters.Add(supplyIdParam);

            using var reader = await command.ExecuteReaderAsync();
            if (await reader.ReadAsync())
            {
                supply = new Supply(
                    name: reader.GetString(1),
                    businessId: reader.GetInt32(3),
                    storeId: reader.IsDBNull(4) ? 0 : reader.GetInt32(4),
                    unitMeasureId: reader.IsDBNull(5) ? 0 : reader.GetInt32(5),
                    description: reader.IsDBNull(2) ? null : reader.GetString(2),
                    fixedExpenseId: reader.IsDBNull(6) ? null : reader.GetInt32(6),
                    active: reader.GetBoolean(9)
                );

                // Set Id via reflection or use a property setter if available
                typeof(Supply).GetProperty("Id")?.SetValue(supply, reader.GetInt32(0));
                
                // Set additional properties
                supply.SupplyCategoryId = reader.IsDBNull(7) ? null : reader.GetInt32(7);
                supply.Type = reader.IsDBNull(8) ? Domain.Enums.SupplyType.Both : (Domain.Enums.SupplyType)reader.GetInt32(8);
                supply.CreatedAt = reader.IsDBNull(10) ? DateTime.UtcNow : reader.GetDateTime(10);
                supply.UpdatedAt = reader.IsDBNull(11) ? DateTime.UtcNow : reader.GetDateTime(11);
            }
        }
        finally
        {
            await _context.Database.CloseConnectionAsync();
        }

        return supply;
    }

    public async Task<Supply?> GetByIdWithDetailsAsync(int id)
    {
        // Get the basic supply data using raw SQL to avoid EF mapping issues
        Supply? supply = null;

        await _context.Database.OpenConnectionAsync();
        
        try
        {
            using var command = _context.Database.GetDbConnection().CreateCommand();
            command.CommandText = @"
                SELECT 
                    s.id,
                    s.name,
                    s.description,
                    s.business_id,
                    s.store_id,
                    s.unit_measure_id,
                    s.fixed_expense_id,
                    s.supply_category_id,
                    s.type,
                    s.active,
                    s.created_at,
                    s.updated_at,
                    um.id as um_id,
                    um.name as um_name,
                    um.symbol as um_symbol,
                    sc.id as sc_id,
                    sc.name as sc_name,
                    sc.description as sc_description
                FROM supplies s
                LEFT JOIN unit_measures um ON s.unit_measure_id = um.id
                LEFT JOIN supply_categories sc ON s.supply_category_id = sc.id
                WHERE s.id = @supplyId";
            
            var parameter = command.CreateParameter();
            parameter.ParameterName = "@supplyId";
            parameter.Value = id;
            command.Parameters.Add(parameter);

            using var reader = await command.ExecuteReaderAsync();
            
            if (await reader.ReadAsync())
            {
                supply = new Supply
                {
                    Id = reader.GetInt32(0),
                    Name = reader.GetString(1),
                    Description = reader.IsDBNull(2) ? null : reader.GetString(2),
                    BusinessId = reader.GetInt32(3),
                    StoreId = reader.GetInt32(4),
                    UnitMeasureId = reader.GetInt32(5),
                    FixedExpenseId = reader.IsDBNull(6) ? null : reader.GetInt32(6),
                    SupplyCategoryId = reader.IsDBNull(7) ? null : reader.GetInt32(7),
                    Type = reader.IsDBNull(8) ? Domain.Enums.SupplyType.Both : (Domain.Enums.SupplyType)reader.GetInt32(8),
                    Active = reader.GetBoolean(9),
                    CreatedAt = reader.IsDBNull(10) ? DateTime.UtcNow : reader.GetDateTime(10),
                    UpdatedAt = reader.IsDBNull(11) ? DateTime.UtcNow : reader.GetDateTime(11)
                };

                // Populate UnitMeasure
                if (!reader.IsDBNull(12))
                {
                    supply.UnitMeasure = new UnitMeasure
                    {
                        Id = reader.GetInt32(12),
                        Name = reader.GetString(13),
                        Symbol = reader.IsDBNull(14) ? null : reader.GetString(14)
                    };
                }

                // Populate SupplyCategory
                if (!reader.IsDBNull(15))
                {
                    supply.SupplyCategory = new SupplyCategory
                    {
                        Id = reader.GetInt32(15),
                        Name = reader.GetString(16),
                        Description = reader.IsDBNull(17) ? null : reader.GetString(17)
                    };
                }
            }
        }
        finally
        {
            await _context.Database.CloseConnectionAsync();
        }
        
        if (supply == null)
            return null;

        // Load Business, Store, FixedExpense separately
        supply.Business = await _context.Businesses
            .FirstOrDefaultAsync(b => b.Id == supply.BusinessId);

        supply.Store = await _context.Stores
            .FirstOrDefaultAsync(s => s.Id == supply.StoreId);

        if (supply.FixedExpenseId.HasValue)
        {
            supply.FixedExpense = await _context.FixedExpenses
                .FirstOrDefaultAsync(fe => fe.Id == supply.FixedExpenseId.Value);
        }

        // Load SupplyEntries using simple SQL to avoid EF conflicts
        var supplyEntries = new List<SupplyEntry>();
        var totalStock = 0;
        
        try
        {
            await _context.Database.OpenConnectionAsync();
            using var command = _context.Database.GetDbConnection().CreateCommand();
            command.CommandText = @"
                SELECT se.id as Id, se.unit_cost as UnitCost, se.amount as Amount, se.provider_id as ProviderId, se.supply_id as SupplyId, se.supply_entry_id as SupplyEntryId,
                       se.process_done_id as ProcessDoneId, se.created_at as CreatedAt, se.updated_at as UpdatedAt, se.active, sep.Id, sep.active as padre_active
                FROM supply_entry se
                LEFT JOIN process_done pd ON se.process_done_id = pd.id
                LEFT JOIN supply_entry sep ON se.supply_entry_id = sep.Id
                WHERE se.supply_id = @supplyId
                  AND (
                    (se.active = 1 AND se.amount > 0) OR  
                    (se.amount < 0 AND se.supply_entry_id IS NOT NULL AND sep.active = 1)
                  )";;

            var parameter = command.CreateParameter();
            parameter.ParameterName = "@supplyId";
            parameter.Value = id;
            command.Parameters.Add(parameter);
            
            using var reader = await command.ExecuteReaderAsync();
            
            while (await reader.ReadAsync())
            {
                var amount = Convert.ToInt32(reader.GetValue(2)); // se.Amount
                
                // Solo sumar al stock las entradas positivas (stock disponible)
                // Las entradas negativas son consumos y no se suman al stock total
                if (amount > 0)
                {
                    totalStock += amount;
                }
                
                var supplyEntry = new SupplyEntry
                {
                    Id = reader.GetInt32(0), // se.Id
                    UnitCost = Convert.ToDecimal(reader.GetValue(1)), // se.UnitCost - safe conversion
                    Amount = amount, // se.Amount - safe conversion
                    ProviderId = reader.GetInt32(3), // se.ProviderId
                    SupplyId = reader.GetInt32(4), // se.SupplyId
                    ReferenceToSupplyEntry = reader.IsDBNull(5) ? null : reader.GetInt32(5), // se.supply_entry_id
                    ProcessDoneId = reader.IsDBNull(6) ? null : reader.GetInt32(6), // se.ProcessDoneId
                    CreatedAt = reader.IsDBNull(7) ? DateTime.UtcNow : reader.GetDateTime(7), // se.CreatedAt
                    UpdatedAt = reader.IsDBNull(8) ? DateTime.UtcNow : reader.GetDateTime(8) // se.UpdatedAt
                };

                supplyEntries.Add(supplyEntry);
            }

            await _context.Database.CloseConnectionAsync();
        }
        catch (Exception ex)
        {
            // If SupplyEntries query fails, just log and continue without them
            Console.WriteLine($"Warning: Could not load SupplyEntries for Supply {id}: {ex.Message}");
            await _context.Database.CloseConnectionAsync();
        }

        supply.SupplyEntries = supplyEntries;
        
        // Store the calculated stock somewhere accessible
        // Since Supply entity doesn't have a Stock property, we'll add it to a custom property or use a service method
        // For now, let's add the stock information to the SupplyDto in the service layer
        
        return supply;
    }

    public async Task<IEnumerable<Supply>> GetAllAsync()
    {
        return await _context.Supplies
            .ToListAsync();
    }

    public async Task<IEnumerable<Supply>> GetByBusinessIdAsync(int businessId)
    {
        var supplies = new List<Supply>();

        await _context.Database.OpenConnectionAsync();
        using var command = _context.Database.GetDbConnection().CreateCommand();
        
        command.CommandText = @"
            SELECT 
                s.id,
                s.name,
                s.description,
                s.business_id,
                s.store_id,
                s.unit_measure_id,
                s.fixed_expense_id,
                s.supply_category_id,
                s.type,
                s.active,
                s.created_at,
                s.updated_at,
                um.id as um_id,
                um.name as um_name,
                um.symbol as um_symbol,
                sc.id as sc_id,
                sc.name as sc_name,
                COALESCE(comp_count.component_usage, 0) as component_usage,
                COALESCE(proc_count.process_usage, 0) as process_usage
            FROM supplies s
            LEFT JOIN unit_measures um ON s.unit_measure_id = um.id
            LEFT JOIN supply_categories sc ON s.supply_category_id = sc.id
            LEFT JOIN (
                SELECT supply_id, COUNT(DISTINCT component_id) as component_usage
                FROM component_supplies
                GROUP BY supply_id
            ) comp_count ON s.id = comp_count.supply_id
            LEFT JOIN (
                SELECT supply_id, COUNT(DISTINCT process_id) as process_usage
                FROM process_supplies
                GROUP BY supply_id
            ) proc_count ON s.id = proc_count.supply_id
            WHERE s.business_id = @BusinessId
            ORDER BY s.name";
        
        var businessIdParam = command.CreateParameter();
        businessIdParam.ParameterName = "@BusinessId";
        businessIdParam.Value = businessId;
        command.Parameters.Add(businessIdParam);

        using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            var supply = new Supply
            {
                Id = reader.GetInt32(0),
                Name = reader.GetString(1),
                Description = reader.IsDBNull(2) ? null : reader.GetString(2),
                BusinessId = reader.GetInt32(3),
                StoreId = reader.GetInt32(4),
                UnitMeasureId = reader.GetInt32(5),
                FixedExpenseId = reader.IsDBNull(6) ? null : reader.GetInt32(6),
                SupplyCategoryId = reader.IsDBNull(7) ? null : reader.GetInt32(7),
                Type = reader.IsDBNull(8) ? Domain.Enums.SupplyType.Both : (Domain.Enums.SupplyType)reader.GetInt32(8),
                Active = reader.GetBoolean(9),
                CreatedAt = reader.IsDBNull(10) ? DateTime.UtcNow : reader.GetDateTime(10),
                UpdatedAt = reader.IsDBNull(11) ? DateTime.UtcNow : reader.GetDateTime(11),
                ComponentUsageCount = reader.GetInt32(17),
                ProcessUsageCount = reader.GetInt32(18)
            };

            // Populate UnitMeasure navigation property
            if (!reader.IsDBNull(12))
            {
                supply.UnitMeasure = new UnitMeasure
                {
                    Id = reader.GetInt32(12),
                    Name = reader.GetString(13),
                    Symbol = reader.IsDBNull(14) ? null : reader.GetString(14)
                };
            }

            // Populate SupplyCategory navigation property
            if (!reader.IsDBNull(15))
            {
                supply.SupplyCategory = new SupplyCategory
                {
                    Id = reader.GetInt32(15),
                    Name = reader.GetString(16)
                };
            }

            supplies.Add(supply);
        }

        await _context.Database.CloseConnectionAsync();
        return supplies;
    }

    public async Task<IEnumerable<Supply>> GetByStoreIdAsync(int storeId)
    {
        var supplies = await _context.Supplies
            .Where(s => s.StoreId == storeId)
            .ToListAsync();

        // Manually populate the UnitMeasure navigation property
        foreach (var supply in supplies)
        {
            supply.UnitMeasure = await _context.UnitMeasures
                .FirstOrDefaultAsync(um => um.Id == supply.UnitMeasureId) ?? new UnitMeasure();
        }

        return supplies;
    }

    public async Task<IEnumerable<Supply>> GetActiveSuppliesAsync(int businessId)
    {
        var supplies = await _context.Supplies
            .Where(s => s.BusinessId == businessId && s.Active)
            .ToListAsync();

        // Manually populate the UnitMeasure navigation property
        foreach (var supply in supplies)
        {
            supply.UnitMeasure = await _context.UnitMeasures
                .FirstOrDefaultAsync(um => um.Id == supply.UnitMeasureId) ?? new UnitMeasure();
        }

        return supplies;
    }

    public async Task<Supply> AddAsync(Supply entity)
    {
        entity.CreatedAt = DateTime.UtcNow;
        entity.UpdatedAt = DateTime.UtcNow;
        
        _context.Supplies.Add(entity);
        await _context.SaveChangesAsync();
        return entity;
    }

    public async Task UpdateAsync(Supply entity)
    {
        entity.UpdatedAt = DateTime.UtcNow;
        
        await _context.Database.OpenConnectionAsync();
        
        try
        {
            using var command = _context.Database.GetDbConnection().CreateCommand();
            command.CommandText = @"
                UPDATE supplies 
                SET 
                    name = @Name,
                    description = @Description,
                    unit_measure_id = @UnitMeasureId,
                    fixed_expense_id = @FixedExpenseId,
                    supply_category_id = @SupplyCategoryId,
                    type = @Type,
                    active = @Active,
                    store_id = @StoreId,
                    updated_at = @UpdatedAt
                WHERE id = @Id";

            var parameters = new[]
            {
                CreateParameter(command, "@Id", entity.Id),
                CreateParameter(command, "@Name", entity.Name),
                CreateParameter(command, "@Description", (object?)entity.Description ?? DBNull.Value),
                CreateParameter(command, "@UnitMeasureId", entity.UnitMeasureId),
                CreateParameter(command, "@FixedExpenseId", (object?)entity.FixedExpenseId ?? DBNull.Value),
                CreateParameter(command, "@SupplyCategoryId", (object?)entity.SupplyCategoryId ?? DBNull.Value),
                CreateParameter(command, "@Type", (int)entity.Type),
                CreateParameter(command, "@Active", entity.Active),
                CreateParameter(command, "@StoreId", entity.StoreId),
                CreateParameter(command, "@UpdatedAt", entity.UpdatedAt)
            };

            foreach (var param in parameters)
            {
                command.Parameters.Add(param);
            }

            await command.ExecuteNonQueryAsync();
        }
        finally
        {
            await _context.Database.CloseConnectionAsync();
        }
    }

    private static System.Data.Common.DbParameter CreateParameter(System.Data.Common.DbCommand command, string name, object value)
    {
        var parameter = command.CreateParameter();
        parameter.ParameterName = name;
        parameter.Value = value;
        return parameter;
    }

    public async Task DeleteAsync(int id)
    {
        var supply = await _context.Supplies.FindAsync(id);
        if (supply != null)
        {
            _context.Supplies.Remove(supply);
            await _context.SaveChangesAsync();
        }
    }

    public async Task<IEnumerable<Supply>> GetSuppliesWithDetailsAsync(int[]? businessIds = null)
    {
        var query = _context.Supplies
            .Include(s => s.FixedExpense)
            .Include(s => s.Business)
            .Include(s => s.Store)
            .AsQueryable();

        if (businessIds != null && businessIds.Length > 0)
        {
            query = query.Where(s => businessIds.Contains(s.BusinessId));
        }

        var supplies = await query.ToListAsync();

        // Manually populate the UnitMeasure navigation property
        foreach (var supply in supplies)
        {
            supply.UnitMeasure = await _context.UnitMeasures
                .FirstOrDefaultAsync(um => um.Id == supply.UnitMeasureId) ?? new UnitMeasure();
        }

        return supplies;
    }

    public async Task<bool> ExistsAsync(int id)
    {
        return await _context.Supplies.AnyAsync(s => s.Id == id);
    }

    public async Task<Supply?> GetByNameAsync(string name, int businessId)
    {
        Supply? supply = null;

        await _context.Database.OpenConnectionAsync();
        
        try
        {
            using var command = _context.Database.GetDbConnection().CreateCommand();
            command.CommandText = @"
                SELECT 
                    s.id,
                    s.name,
                    s.description,
                    s.business_id,
                    s.store_id,
                    s.unit_measure_id,
                    s.fixed_expense_id,
                    s.supply_category_id,
                    s.type,
                    s.active,
                    s.created_at,
                    s.updated_at
                FROM supplies s
                WHERE s.name = @Name AND s.business_id = @BusinessId";
            
            var nameParam = command.CreateParameter();
            nameParam.ParameterName = "@Name";
            nameParam.Value = name;
            command.Parameters.Add(nameParam);
            
            var businessIdParam = command.CreateParameter();
            businessIdParam.ParameterName = "@BusinessId";
            businessIdParam.Value = businessId;
            command.Parameters.Add(businessIdParam);

            using var reader = await command.ExecuteReaderAsync();
            if (await reader.ReadAsync())
            {
                supply = new Supply(
                    name: reader.GetString(1),
                    businessId: reader.GetInt32(3),
                    storeId: reader.IsDBNull(4) ? 0 : reader.GetInt32(4),
                    unitMeasureId: reader.IsDBNull(5) ? 0 : reader.GetInt32(5),
                    description: reader.IsDBNull(2) ? null : reader.GetString(2),
                    fixedExpenseId: reader.IsDBNull(6) ? null : reader.GetInt32(6),
                    active: reader.GetBoolean(9)
                );

                typeof(Supply).GetProperty("Id")?.SetValue(supply, reader.GetInt32(0));
                
                supply.SupplyCategoryId = reader.IsDBNull(7) ? null : reader.GetInt32(7);
                supply.Type = reader.IsDBNull(8) ? Domain.Enums.SupplyType.Both : (Domain.Enums.SupplyType)reader.GetInt32(8);
                supply.CreatedAt = reader.IsDBNull(10) ? DateTime.UtcNow : reader.GetDateTime(10);
                supply.UpdatedAt = reader.IsDBNull(11) ? DateTime.UtcNow : reader.GetDateTime(11);
            }
        }
        finally
        {
            await _context.Database.CloseConnectionAsync();
        }

        return supply;
    }
}
