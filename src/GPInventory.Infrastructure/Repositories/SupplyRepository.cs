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
                    s.sku,
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
                    s.minimum_stock,
                    s.preferred_provider_id
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
                    businessId: reader.GetInt32(4),
                    storeId: reader.IsDBNull(5) ? 0 : reader.GetInt32(5),
                    unitMeasureId: reader.IsDBNull(6) ? 0 : reader.GetInt32(6),
                    description: reader.IsDBNull(3) ? null : reader.GetString(3),
                    fixedExpenseId: reader.IsDBNull(7) ? null : reader.GetInt32(7),
                    active: reader.GetBoolean(10)
                );

                // Set Id via reflection or use a property setter if available
                typeof(Supply).GetProperty("Id")?.SetValue(supply, reader.GetInt32(0));

                // Set additional properties
                supply.Sku = reader.IsDBNull(2) ? null : reader.GetString(2);
                supply.SupplyCategoryId = reader.IsDBNull(8) ? null : reader.GetInt32(8);
                supply.Type = reader.IsDBNull(9) ? Domain.Enums.SupplyType.Both : (Domain.Enums.SupplyType)reader.GetInt32(9);
                supply.CreatedAt = reader.IsDBNull(11) ? DateTime.UtcNow : reader.GetDateTime(11);
                supply.UpdatedAt = reader.IsDBNull(12) ? DateTime.UtcNow : reader.GetDateTime(12);
                supply.MinimumStock = reader.IsDBNull(13) ? 0 : reader.GetInt32(13);
                supply.PreferredProviderId = reader.IsDBNull(14) ? null : reader.GetInt32(14);
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
                    s.sku,
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
                    s.minimum_stock,
                    s.preferred_provider_id,
                    um.id as um_id,
                    um.name as um_name,
                    um.symbol as um_symbol,
                    sc.id as sc_id,
                    sc.name as sc_name,
                    sc.description as sc_description,
                    p.id as p_id,
                    p.name as p_name,
                    p.id_business as p_business_id,
                    p.id_store as p_store_id,
                    p.contact as p_contact,
                    p.address as p_address,
                    p.mail as p_mail,
                    p.prefix as p_prefix,
                    p.active as p_active,
                    p.created_at as p_created_at,
                    p.updated_at as p_updated_at
                FROM supplies s
                LEFT JOIN unit_measures um ON s.unit_measure_id = um.id
                LEFT JOIN supply_categories sc ON s.supply_category_id = sc.id
                LEFT JOIN provider p ON s.preferred_provider_id = p.id
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
                    Sku = reader.IsDBNull(2) ? null : reader.GetString(2),
                    Description = reader.IsDBNull(3) ? null : reader.GetString(3),
                    BusinessId = reader.GetInt32(4),
                    StoreId = reader.GetInt32(5),
                    UnitMeasureId = reader.GetInt32(6),
                    FixedExpenseId = reader.IsDBNull(7) ? null : reader.GetInt32(7),
                    SupplyCategoryId = reader.IsDBNull(8) ? null : reader.GetInt32(8),
                    Type = reader.IsDBNull(9) ? Domain.Enums.SupplyType.Both : (Domain.Enums.SupplyType)reader.GetInt32(9),
                    Active = reader.GetBoolean(10),
                    CreatedAt = reader.IsDBNull(11) ? DateTime.UtcNow : reader.GetDateTime(11),
                    UpdatedAt = reader.IsDBNull(12) ? DateTime.UtcNow : reader.GetDateTime(12),
                    MinimumStock = reader.IsDBNull(13) ? 0 : reader.GetInt32(13),
                    PreferredProviderId = reader.IsDBNull(14) ? null : reader.GetInt32(14)
                };

                // Populate UnitMeasure
                if (!reader.IsDBNull(15))
                {
                    supply.UnitMeasure = new UnitMeasure
                    {
                        Id = reader.GetInt32(15),
                        Name = reader.GetString(16),
                        Symbol = reader.IsDBNull(17) ? null : reader.GetString(17)
                    };
                }

                // Populate SupplyCategory
                if (!reader.IsDBNull(18))
                {
                    supply.SupplyCategory = new SupplyCategory
                    {
                        Id = reader.GetInt32(18),
                        Name = reader.GetString(19),
                        Description = reader.IsDBNull(20) ? null : reader.GetString(20)
                    };
                }

                // Populate PreferredProvider
                if (!reader.IsDBNull(21))
                {
                    supply.PreferredProvider = new Provider(
                        name: reader.GetString(22),
                        businessId: reader.GetInt32(23),
                        storeId: reader.IsDBNull(24) ? null : reader.GetInt32(24)
                    )
                    {
                        Contact = reader.IsDBNull(25) ? null : reader.GetInt32(25),
                        Address = reader.IsDBNull(26) ? null : reader.GetString(26),
                        Mail = reader.IsDBNull(27) ? null : reader.GetString(27),
                        Prefix = reader.IsDBNull(28) ? null : reader.GetString(28),
                        Active = reader.GetBoolean(29),
                        CreatedAt = reader.GetDateTime(30),
                        UpdatedAt = reader.GetDateTime(31)
                    };
                    
                    typeof(Provider).GetProperty("Id")?.SetValue(supply.PreferredProvider, reader.GetInt32(21));
                }
            }
        }
        catch (Exception ex) {
            Console.WriteLine($"Error in GetByIdWithDetailsAsync: {ex.Message}");
            throw;
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
                SELECT se.id as Id, se.unit_cost as UnitCost, se.amount as Amount, se.tag as Tag, se.provider_id as ProviderId, se.supply_id as SupplyId, se.supply_entry_id as SupplyEntryId,
                       se.process_done_id as ProcessDoneId, se.created_at as CreatedAt, se.updated_at as UpdatedAt, se.active, sep.Id, sep.active as padre_active
                FROM supply_entry se
                LEFT JOIN process_done pd ON se.process_done_id = pd.id
                LEFT JOIN supply_entry sep ON se.supply_entry_id = sep.Id
                WHERE se.supply_id = @supplyId
                  AND (
                    (se.active = 1 AND se.amount > 0) OR  
                    (se.amount < 0 AND se.supply_entry_id IS NOT NULL) OR
                    (se.amount > 0 AND EXISTS (SELECT 1 FROM supply_entry child WHERE child.supply_entry_id = se.id AND child.active = 1))
                  )"; ;

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
                    Tag = reader.IsDBNull(3) ? null : reader.GetString(3), // se.Tag
                    ProviderId = reader.GetInt32(4), // se.ProviderId
                    SupplyId = reader.GetInt32(5), // se.SupplyId
                    ReferenceToSupplyEntry = reader.IsDBNull(6) ? null : reader.GetInt32(6), // se.supply_entry_id
                    ProcessDoneId = reader.IsDBNull(7) ? null : reader.GetInt32(7), // se.ProcessDoneId
                    CreatedAt = reader.IsDBNull(8) ? DateTime.UtcNow : reader.GetDateTime(8), // se.CreatedAt
                    UpdatedAt = reader.IsDBNull(9) ? DateTime.UtcNow : reader.GetDateTime(9) // se.UpdatedAt
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
                s.sku,
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
                s.minimum_stock,
                s.preferred_provider_id,
                um.id as um_id,
                um.name as um_name,
                um.symbol as um_symbol,
                sc.id as sc_id,
                sc.name as sc_name,
                COALESCE(comp_count.component_usage, 0) as component_usage,
                COALESCE(proc_count.process_usage, 0) as process_usage,
                p.id as p_id,
                p.name as p_name,
                p.contact as p_contact
            FROM supplies s
            LEFT JOIN unit_measures um ON s.unit_measure_id = um.id
            LEFT JOIN supply_categories sc ON s.supply_category_id = sc.id
            LEFT JOIN provider p ON s.preferred_provider_id = p.id
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
                Sku = reader.IsDBNull(2) ? null : reader.GetString(2),
                Description = reader.IsDBNull(3) ? null : reader.GetString(3),
                BusinessId = reader.GetInt32(4),
                StoreId = reader.GetInt32(5),
                UnitMeasureId = reader.GetInt32(6),
                FixedExpenseId = reader.IsDBNull(7) ? null : reader.GetInt32(7),
                SupplyCategoryId = reader.IsDBNull(8) ? null : reader.GetInt32(8),
                Type = reader.IsDBNull(9) ? Domain.Enums.SupplyType.Both : (Domain.Enums.SupplyType)reader.GetInt32(9),
                Active = reader.GetBoolean(10),
                CreatedAt = reader.IsDBNull(11) ? DateTime.UtcNow : reader.GetDateTime(11),
                UpdatedAt = reader.IsDBNull(12) ? DateTime.UtcNow : reader.GetDateTime(12),
                MinimumStock = reader.IsDBNull(13) ? 0 : reader.GetInt32(13),
                PreferredProviderId = reader.IsDBNull(14) ? null : reader.GetInt32(14),
                ComponentUsageCount = reader.GetInt32(20),
                ProcessUsageCount = reader.GetInt32(21)
            };

            // Populate UnitMeasure navigation property
            if (!reader.IsDBNull(15))
            {
                supply.UnitMeasure = new UnitMeasure
                {
                    Id = reader.GetInt32(15),
                    Name = reader.GetString(16),
                    Symbol = reader.IsDBNull(17) ? null : reader.GetString(17)
                };
            }

            // Populate SupplyCategory navigation property
            if (!reader.IsDBNull(18))
            {
                supply.SupplyCategory = new SupplyCategory
                {
                    Id = reader.GetInt32(18),
                    Name = reader.GetString(19)
                };
            }

            // Populate PreferredProvider navigation property
            if (!reader.IsDBNull(22))
            {
                var providerId = reader.GetInt32(22);
                var providerName = reader.GetString(23);
                var providerContact = reader.IsDBNull(24) ? null : (int?)reader.GetInt32(24);
                
                supply.PreferredProvider = new Provider(
                    name: providerName,
                    businessId: supply.BusinessId,
                    storeId: supply.StoreId > 0 ? supply.StoreId : null
                )
                {
                    Contact = providerContact
                };
                
                typeof(Provider).GetProperty("Id")?.SetValue(supply.PreferredProvider, providerId);
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
                    sku = @Sku,
                    description = @Description,
                    unit_measure_id = @UnitMeasureId,
                    fixed_expense_id = @FixedExpenseId,
                    supply_category_id = @SupplyCategoryId,
                    type = @Type,
                    active = @Active,
                    store_id = @StoreId,
                    minimum_stock = @MinimumStock,
                    preferred_provider_id = @PreferredProviderId,
                    updated_at = @UpdatedAt
                WHERE id = @Id";

            var parameters = new[]
            {
                CreateParameter(command, "@Id", entity.Id),
                CreateParameter(command, "@Name", entity.Name),
                CreateParameter(command, "@Sku", (object?)entity.Sku ?? DBNull.Value),
                CreateParameter(command, "@Description", (object?)entity.Description ?? DBNull.Value),
                CreateParameter(command, "@UnitMeasureId", entity.UnitMeasureId),
                CreateParameter(command, "@FixedExpenseId", (object?)entity.FixedExpenseId ?? DBNull.Value),
                CreateParameter(command, "@SupplyCategoryId", (object?)entity.SupplyCategoryId ?? DBNull.Value),
                CreateParameter(command, "@Type", (int)entity.Type),
                CreateParameter(command, "@Active", entity.Active),
                CreateParameter(command, "@StoreId", entity.StoreId),
                CreateParameter(command, "@MinimumStock", entity.MinimumStock),
                CreateParameter(command, "@PreferredProviderId", (object?)entity.PreferredProviderId ?? DBNull.Value),
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
                    s.sku,
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
                    s.minimum_stock,
                    s.preferred_provider_id
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
                    businessId: reader.GetInt32(4),
                    storeId: reader.IsDBNull(5) ? 0 : reader.GetInt32(5),
                    unitMeasureId: reader.IsDBNull(6) ? 0 : reader.GetInt32(6),
                    description: reader.IsDBNull(3) ? null : reader.GetString(3),
                    fixedExpenseId: reader.IsDBNull(7) ? null : reader.GetInt32(7),
                    active: reader.GetBoolean(10)
                );

                typeof(Supply).GetProperty("Id")?.SetValue(supply, reader.GetInt32(0));

                supply.Sku = reader.IsDBNull(2) ? null : reader.GetString(2);
                supply.SupplyCategoryId = reader.IsDBNull(8) ? null : reader.GetInt32(8);
                supply.Type = reader.IsDBNull(9) ? Domain.Enums.SupplyType.Both : (Domain.Enums.SupplyType)reader.GetInt32(9);
                supply.CreatedAt = reader.IsDBNull(11) ? DateTime.UtcNow : reader.GetDateTime(11);
                supply.UpdatedAt = reader.IsDBNull(12) ? DateTime.UtcNow : reader.GetDateTime(12);
                supply.MinimumStock = reader.IsDBNull(13) ? 0 : reader.GetInt32(13);
                supply.PreferredProviderId = reader.IsDBNull(14) ? null : reader.GetInt32(14);
            }
        }
        finally
        {
            await _context.Database.CloseConnectionAsync();
        }

        return supply;
    }

    public async Task<IEnumerable<Application.DTOs.Production.SupplyStockDto>> GetAllSupplyStocksAsync(int? businessId = null)
    {
        var stockList = new List<Application.DTOs.Production.SupplyStockDto>();

        try
        {
            await _context.Database.OpenConnectionAsync();

            try
            {
                using var command = _context.Database.GetDbConnection().CreateCommand();

                // Query optimizado que calcula todo en una sola consulta SQL
                command.CommandText = @"
                SELECT 
                    s.id as supply_id,
                    s.name as supply_name,
                    s.sku as supply_sku,
                    um.name as unit_measure_name,
                    um.symbol as unit_measure_symbol,
                    s.minimum_stock,
                    COALESCE(SUM(CASE WHEN se.amount > 0 AND se.active = 1 THEN se.amount ELSE 0 END), 0) as total_incoming,
                    COALESCE(SUM(CASE 
                        WHEN se.amount < 0 AND se.active = 1 THEN 
                            CASE 
                                WHEN se.supply_entry_id IS NULL THEN se.amount
                                WHEN EXISTS (SELECT 1 FROM supply_entry parent WHERE parent.id = se.supply_entry_id AND parent.active = 1) THEN se.amount
                                ELSE 0
                            END
                        ELSE 0 
                    END), 0) as total_outgoing
                FROM supplies s
                LEFT JOIN supply_entry se ON s.id = se.supply_id
                LEFT JOIN unit_measures um ON s.unit_measure_id = um.id
                WHERE (@businessId IS NULL OR s.business_id = @businessId)
                GROUP BY s.id, s.name, s.sku, um.name, um.symbol, s.minimum_stock
                ORDER BY s.name";

                var businessIdParam = command.CreateParameter();
                businessIdParam.ParameterName = "@businessId";
                businessIdParam.Value = businessId.HasValue ? (object)businessId.Value : DBNull.Value;
                command.Parameters.Add(businessIdParam);

                using var reader = await command.ExecuteReaderAsync();

                while (await reader.ReadAsync())
                {
                    try
                    {
                        var supplyId = reader.GetInt32(reader.GetOrdinal("supply_id"));
                        var supplyName = reader.GetString(reader.GetOrdinal("supply_name"));
                        var unitMeasureName = reader.IsDBNull(reader.GetOrdinal("unit_measure_name"))
                            ? "Unknown"
                            : reader.GetString(reader.GetOrdinal("unit_measure_name"));
                        var unitMeasureSymbol = reader.IsDBNull(reader.GetOrdinal("unit_measure_symbol"))
                            ? null
                            : reader.GetString(reader.GetOrdinal("unit_measure_symbol"));
                        var minimumStock = reader.IsDBNull(reader.GetOrdinal("minimum_stock"))
                            ? 0
                            : reader.GetInt32(reader.GetOrdinal("minimum_stock"));
                        var totalIncoming = reader.GetDecimal(reader.GetOrdinal("total_incoming"));
                        var totalOutgoing = reader.GetDecimal(reader.GetOrdinal("total_outgoing"));
                        var currentStock = totalIncoming + totalOutgoing; // totalOutgoing ya incluye valores negativos

                        stockList.Add(new Application.DTOs.Production.SupplyStockDto
                        {
                            SupplyId = supplyId,
                            SupplyName = supplyName,
                            CurrentStock = currentStock,
                            UnitMeasureName = unitMeasureName,
                            UnitMeasureSymbol = unitMeasureSymbol,
                            TotalIncoming = totalIncoming,
                            TotalOutgoing = Math.Abs(totalOutgoing), // Mostrar valor absoluto para la UI
                            MinimumStock = minimumStock,
                            StockStatus = Application.Helpers.StockHelper.CalculateStockStatus(currentStock, minimumStock)
                        });
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Error processing supply stock row: {ex.Message}");
                        // Continue processing other rows
                    }
                }
            }
            finally
            {
                await _context.Database.CloseConnectionAsync();
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error retrieving supply stocks: {ex.Message}");
            Console.WriteLine($"Stack trace: {ex.StackTrace}");
            throw new Exception($"Failed to retrieve supply stocks: {ex.Message}", ex);
        }

        return stockList;
    }
}
