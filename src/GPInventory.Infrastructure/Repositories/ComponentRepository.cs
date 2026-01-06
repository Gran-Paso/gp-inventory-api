using GPInventory.Application.Interfaces;
using GPInventory.Domain.Entities;
using MySqlConnector;
using System.Data;

namespace GPInventory.Infrastructure.Repositories;

public class ComponentRepository : IComponentRepository
{
    private readonly string _connectionString;

    public ComponentRepository(string connectionString)
    {
        _connectionString = connectionString;
    }

    public async Task<Component?> GetByIdAsync(int id)
    {
        const string sql = @"
            SELECT c.*, um.name as unit_measure_name, um.symbol as unit_measure_symbol
            FROM components c
            LEFT JOIN unit_measures um ON c.unit_measure_id = um.id
            WHERE c.id = @Id";

        using var connection = new MySqlConnection(_connectionString);
        await connection.OpenAsync();

        using var command = new MySqlCommand(sql, connection);
        command.Parameters.AddWithValue("@Id", id);

        using var reader = await command.ExecuteReaderAsync();

        if (await reader.ReadAsync())
        {
            return MapComponent(reader);
        }

        return null;
    }

    public async Task<Component?> GetByIdWithSuppliesAsync(int id)
    {
        var component = await GetByIdAsync(id);
        if (component == null) return null;

        component.Supplies = (await GetSuppliesByComponentIdAsync(id)).ToList();
        return component;
    }

    public async Task<IEnumerable<Component>> GetAllAsync(int businessId, bool? activeOnly = true)
    {
        try
        {
            var sql = @"
            SELECT 
                c.*, 
                um.name as unit_measure_name, 
                um.symbol as unit_measure_symbol,
                sc.id as sc_id,
                sc.name as sc_name,
                COALESCE(comp_count.component_usage, 0) as component_usage,
                COALESCE(proc_count.process_usage, 0) as process_usage
            FROM components c
            LEFT JOIN unit_measures um ON c.unit_measure_id = um.id
            LEFT JOIN supply_categories sc ON c.supply_category_id = sc.id
            LEFT JOIN (
                SELECT sub_component_id as component_id, COUNT(DISTINCT component_id) as component_usage
                FROM component_supplies
                WHERE item_type = 'component' AND sub_component_id IS NOT NULL
                GROUP BY sub_component_id
            ) comp_count ON c.id = comp_count.component_id
            LEFT JOIN (
                SELECT component_id, COUNT(DISTINCT process_id) as process_usage
                FROM process_components
                GROUP BY component_id
            ) proc_count ON c.id = proc_count.component_id
            WHERE c.business_id = @BusinessId";

        if (activeOnly == true)
        {
            sql += " AND c.active = 1";
        }

        sql += " ORDER BY c.name";

        using var connection = new MySqlConnection(_connectionString);
        await connection.OpenAsync();

        using var command = new MySqlCommand(sql, connection);
        command.Parameters.AddWithValue("@BusinessId", businessId);

        using var reader = await command.ExecuteReaderAsync();
        var components = new List<Component>();

        while (await reader.ReadAsync())
        {
            var component = MapComponent(reader);
            
            // Add usage counts
            var componentUsageIdx = reader.GetOrdinal("component_usage");
            var processUsageIdx = reader.GetOrdinal("process_usage");
            component.ComponentUsageCount = reader.GetInt32(componentUsageIdx);
            component.ProcessUsageCount = reader.GetInt32(processUsageIdx);
            
            // Add SupplyCategory if present
            if (!reader.IsDBNull(reader.GetOrdinal("sc_id")))
            {
                component.SupplyCategory = new SupplyCategory
                {
                    Id = reader.GetInt32(reader.GetOrdinal("sc_id")),
                    Name = reader.GetString(reader.GetOrdinal("sc_name"))
                };
            }
            
            components.Add(component);
        }

        return components;
        } catch (Exception ex)
        {
            throw new Exception($"Error retrieving components for business {businessId}: {ex.Message}", ex);
        }
        
    }

    public async Task<Component> CreateAsync(Component component)
    {
        const string sql = @"
            INSERT INTO components 
            (name, description, business_id, store_id, unit_measure_id, 
             preparation_time, time_unit_id, yield_amount, supply_category_id, minimum_stock, active, created_at)
            VALUES 
            (@Name, @Description, @BusinessId, @StoreId, @UnitMeasureId,
             @PreparationTime, @TimeUnitId, @YieldAmount, @SupplyCategoryId, @MinimumStock, @Active, @CreatedAt);
            SELECT LAST_INSERT_ID();";

        using var connection = new MySqlConnection(_connectionString);
        await connection.OpenAsync();

        using var command = new MySqlCommand(sql, connection);
        command.Parameters.AddWithValue("@Name", component.Name);
        command.Parameters.AddWithValue("@Description", component.Description ?? (object)DBNull.Value);
        command.Parameters.AddWithValue("@BusinessId", component.BusinessId);
        command.Parameters.AddWithValue("@StoreId", component.StoreId ?? (object)DBNull.Value);
        command.Parameters.AddWithValue("@UnitMeasureId", component.UnitMeasureId);
        command.Parameters.AddWithValue("@PreparationTime", component.PreparationTime ?? (object)DBNull.Value);
        command.Parameters.AddWithValue("@TimeUnitId", component.TimeUnitId ?? (object)DBNull.Value);
        command.Parameters.AddWithValue("@YieldAmount", component.YieldAmount);
        command.Parameters.AddWithValue("@SupplyCategoryId", component.SupplyCategoryId ?? (object)DBNull.Value);
        command.Parameters.AddWithValue("@MinimumStock", component.MinimumStock);
        command.Parameters.AddWithValue("@Active", component.Active);
        command.Parameters.AddWithValue("@CreatedAt", DateTime.UtcNow);

        var id = Convert.ToInt32(await command.ExecuteScalarAsync());
        component.Id = id;

        // Reload component with unit measure info
        return await GetByIdAsync(id) ?? component;
    }

    public async Task<Component> UpdateAsync(Component component)
    {
        const string sql = @"
            UPDATE components 
            SET name = @Name,
                description = @Description,
                store_id = @StoreId,
                unit_measure_id = @UnitMeasureId,
                preparation_time = @PreparationTime,
                time_unit_id = @TimeUnitId,
                yield_amount = @YieldAmount,
                supply_category_id = @SupplyCategoryId,
                minimum_stock = @MinimumStock,
                active = @Active,
                updated_at = @UpdatedAt
            WHERE id = @Id";

        using var connection = new MySqlConnection(_connectionString);
        await connection.OpenAsync();

        using var command = new MySqlCommand(sql, connection);
        command.Parameters.AddWithValue("@Id", component.Id);
        command.Parameters.AddWithValue("@Name", component.Name);
        command.Parameters.AddWithValue("@Description", component.Description ?? (object)DBNull.Value);
        command.Parameters.AddWithValue("@StoreId", component.StoreId ?? (object)DBNull.Value);
        command.Parameters.AddWithValue("@UnitMeasureId", component.UnitMeasureId);
        command.Parameters.AddWithValue("@PreparationTime", component.PreparationTime ?? (object)DBNull.Value);
        command.Parameters.AddWithValue("@TimeUnitId", component.TimeUnitId ?? (object)DBNull.Value);
        command.Parameters.AddWithValue("@YieldAmount", component.YieldAmount);
        command.Parameters.AddWithValue("@SupplyCategoryId", component.SupplyCategoryId ?? (object)DBNull.Value);
        command.Parameters.AddWithValue("@MinimumStock", component.MinimumStock);
        command.Parameters.AddWithValue("@Active", component.Active);
        command.Parameters.AddWithValue("@UpdatedAt", DateTime.UtcNow);

        await command.ExecuteNonQueryAsync();

        // Reload component with unit measure info
        return await GetByIdAsync(component.Id) ?? component;
    }

    public async Task<bool> DeleteAsync(int id)
    {
        const string sql = "UPDATE components SET active = 0, updated_at = @UpdatedAt WHERE id = @Id";

        using var connection = new MySqlConnection(_connectionString);
        await connection.OpenAsync();

        using var command = new MySqlCommand(sql, connection);
        command.Parameters.AddWithValue("@Id", id);
        command.Parameters.AddWithValue("@UpdatedAt", DateTime.UtcNow);

        var rowsAffected = await command.ExecuteNonQueryAsync();
        return rowsAffected > 0;
    }

    public async Task<bool> ExistsAsync(int id)
    {
        const string sql = "SELECT COUNT(*) FROM components WHERE id = @Id";

        using var connection = new MySqlConnection(_connectionString);
        await connection.OpenAsync();

        using var command = new MySqlCommand(sql, connection);
        command.Parameters.AddWithValue("@Id", id);

        var count = Convert.ToInt32(await command.ExecuteScalarAsync());
        return count > 0;
    }

    public async Task<bool> HasCircularReferenceAsync(int componentId, int subComponentId)
    {
        // Recursive query para detectar referencias circulares
        const string sql = @"
            WITH RECURSIVE component_tree AS (
                SELECT sub_component_id, 1 as level
                FROM component_supplies
                WHERE component_id = @SubComponentId AND item_type = 'component'
                
                UNION ALL
                
                SELECT cs.sub_component_id, ct.level + 1
                FROM component_supplies cs
                INNER JOIN component_tree ct ON cs.component_id = ct.sub_component_id
                WHERE cs.item_type = 'component' AND ct.level < 10
            )
            SELECT COUNT(*) 
            FROM component_tree 
            WHERE sub_component_id = @ComponentId";

        using var connection = new MySqlConnection(_connectionString);
        await connection.OpenAsync();

        using var command = new MySqlCommand(sql, connection);
        command.Parameters.AddWithValue("@ComponentId", componentId);
        command.Parameters.AddWithValue("@SubComponentId", subComponentId);

        var count = Convert.ToInt32(await command.ExecuteScalarAsync());
        return count > 0;
    }

    public async Task<decimal> CalculateTotalCostAsync(int componentId)
    {
        // TODO: Implementar cálculo recursivo de costo
        // Por ahora retornamos 0, se implementará en el servicio
        return await Task.FromResult(0m);
    }

    // Component Supplies Methods
    public async Task<IEnumerable<ComponentSupply>> GetSuppliesByComponentIdAsync(int componentId)
    {
        try
        {
            const string sql = @"
                SELECT cs.*,
                       s.name as supply_name,
                       sum.symbol as supply_unit_symbol,
                       c.name as component_name,
                       cum.symbol as component_unit_symbol
                FROM component_supplies cs
                LEFT JOIN supplies s ON cs.supply_id = s.id
                LEFT JOIN unit_measures sum ON s.unit_measure_id = sum.id
                LEFT JOIN components c ON cs.sub_component_id = c.id
                LEFT JOIN unit_measures cum ON c.unit_measure_id = cum.id
                WHERE cs.component_id = @ComponentId
                ORDER BY cs.`order`";

            using var connection = new MySqlConnection(_connectionString);
            await connection.OpenAsync();

            using var command = new MySqlCommand(sql, connection);
            command.Parameters.AddWithValue("@ComponentId", componentId);

            using var reader = await command.ExecuteReaderAsync();
            var supplies = new List<ComponentSupply>();

            while (await reader.ReadAsync())
            {
                supplies.Add(MapComponentSupply(reader));
            }

            return supplies;
        }
        catch (Exception ex)
        {
            throw new Exception($"Error retrieving supplies for component {componentId}: {ex.Message}", ex);
        }
    }

    public async Task<ComponentSupply> CreateSupplyAsync(ComponentSupply supply)
    {
        const string sql = @"
            INSERT INTO component_supplies 
            (component_id, supply_id, sub_component_id, quantity, `order`, item_type, is_optional)
            VALUES 
            (@ComponentId, @SupplyId, @SubComponentId, @Quantity, @Order, @ItemType, @IsOptional);
            SELECT LAST_INSERT_ID();";

        using var connection = new MySqlConnection(_connectionString);
        await connection.OpenAsync();

        using var command = new MySqlCommand(sql, connection);
        command.Parameters.AddWithValue("@ComponentId", supply.ComponentId);
        command.Parameters.AddWithValue("@SupplyId", supply.SupplyId ?? (object)DBNull.Value);
        command.Parameters.AddWithValue("@SubComponentId", supply.SubComponentId ?? (object)DBNull.Value);
        command.Parameters.AddWithValue("@Quantity", supply.Quantity);
        command.Parameters.AddWithValue("@Order", supply.Order);
        command.Parameters.AddWithValue("@ItemType", supply.ItemType);
        command.Parameters.AddWithValue("@IsOptional", supply.IsOptional);

        var id = Convert.ToInt32(await command.ExecuteScalarAsync());
        supply.Id = id;

        return supply;
    }

    public async Task<bool> DeleteSupplyAsync(int supplyId)
    {
        const string sql = "DELETE FROM component_supplies WHERE id = @Id";

        using var connection = new MySqlConnection(_connectionString);
        await connection.OpenAsync();

        using var command = new MySqlCommand(sql, connection);
        command.Parameters.AddWithValue("@Id", supplyId);

        var rowsAffected = await command.ExecuteNonQueryAsync();
        return rowsAffected > 0;
    }

    public async Task<bool> DeleteAllSuppliesByComponentIdAsync(int componentId)
    {
        const string sql = "DELETE FROM component_supplies WHERE component_id = @ComponentId";

        using var connection = new MySqlConnection(_connectionString);
        await connection.OpenAsync();

        using var command = new MySqlCommand(sql, connection);
        command.Parameters.AddWithValue("@ComponentId", componentId);

        await command.ExecuteNonQueryAsync();
        return true;
    }

    // Component Production Methods
    public async Task<IEnumerable<ComponentProduction>> GetProductionsByComponentIdAsync(int componentId)
    {
        const string sql = @"
            SELECT cp.*, c.name as component_name
            FROM component_production cp
            INNER JOIN components c ON cp.component_id = c.id
            WHERE cp.component_id = @ComponentId AND cp.active = 1
            ORDER BY cp.production_date DESC";

        using var connection = new MySqlConnection(_connectionString);
        await connection.OpenAsync();

        using var command = new MySqlCommand(sql, connection);
        command.Parameters.AddWithValue("@ComponentId", componentId);

        using var reader = await command.ExecuteReaderAsync();
        var productions = new List<ComponentProduction>();

        while (await reader.ReadAsync())
        {
            productions.Add(MapComponentProduction(reader));
        }

        return productions;
    }

    public async Task<IEnumerable<ComponentProduction>> GetActiveProductionsAsync(int businessId)
    {
        const string sql = @"
            SELECT cp.*, c.name as component_name
            FROM component_production cp
            INNER JOIN components c ON cp.component_id = c.id
            WHERE c.business_id = @BusinessId 
              AND cp.active = 1 
              AND (cp.expiration_date IS NULL OR cp.expiration_date > NOW())
            ORDER BY cp.expiration_date ASC";

        using var connection = new MySqlConnection(_connectionString);
        await connection.OpenAsync();

        using var command = new MySqlCommand(sql, connection);
        command.Parameters.AddWithValue("@BusinessId", businessId);

        using var reader = await command.ExecuteReaderAsync();
        var productions = new List<ComponentProduction>();

        while (await reader.ReadAsync())
        {
            productions.Add(MapComponentProduction(reader));
        }

        return productions;
    }

    public async Task<IEnumerable<ComponentProduction>> GetExpiringProductionsAsync(int businessId, DateTime beforeDate)
    {
        const string sql = @"
            SELECT cp.*, c.name as component_name
            FROM component_production cp
            INNER JOIN components c ON cp.component_id = c.id
            WHERE c.business_id = @BusinessId 
              AND cp.active = 1 
              AND cp.expiration_date IS NOT NULL 
              AND cp.expiration_date <= @BeforeDate
              AND cp.expiration_date > NOW()
            ORDER BY cp.expiration_date ASC";

        using var connection = new MySqlConnection(_connectionString);
        await connection.OpenAsync();

        using var command = new MySqlCommand(sql, connection);
        command.Parameters.AddWithValue("@BusinessId", businessId);
        command.Parameters.AddWithValue("@BeforeDate", beforeDate);

        using var reader = await command.ExecuteReaderAsync();
        var productions = new List<ComponentProduction>();

        while (await reader.ReadAsync())
        {
            productions.Add(MapComponentProduction(reader));
        }

        return productions;
    }

    public async Task<ComponentProduction?> GetProductionByIdAsync(int id)
    {
        const string sql = @"
            SELECT cp.*, c.name as component_name
            FROM component_production cp
            INNER JOIN components c ON cp.component_id = c.id
            WHERE cp.id = @Id";

        using var connection = new MySqlConnection(_connectionString);
        await connection.OpenAsync();

        using var command = new MySqlCommand(sql, connection);
        command.Parameters.AddWithValue("@Id", id);

        using var reader = await command.ExecuteReaderAsync();

        if (await reader.ReadAsync())
        {
            return MapComponentProduction(reader);
        }

        return null;
    }

    public async Task<ComponentProduction> CreateProductionAsync(ComponentProduction production)
    {
        const string sql = @"
            INSERT INTO component_production 
            (component_id, produced_amount, production_date, expiration_date, 
             batch_number, cost, notes, active, created_at)
            VALUES 
            (@ComponentId, @ProducedAmount, @ProductionDate, @ExpirationDate,
             @BatchNumber, @Cost, @Notes, @Active, @CreatedAt);
            SELECT LAST_INSERT_ID();";

        using var connection = new MySqlConnection(_connectionString);
        await connection.OpenAsync();

        using var command = new MySqlCommand(sql, connection);
        command.Parameters.AddWithValue("@ComponentId", production.ComponentId);
        command.Parameters.AddWithValue("@ProducedAmount", production.ProducedAmount);
        command.Parameters.AddWithValue("@ProductionDate", production.ProductionDate);
        command.Parameters.AddWithValue("@ExpirationDate", production.ExpirationDate ?? (object)DBNull.Value);
        command.Parameters.AddWithValue("@BatchNumber", production.BatchNumber ?? (object)DBNull.Value);
        command.Parameters.AddWithValue("@Cost", production.Cost);
        command.Parameters.AddWithValue("@Notes", production.Notes ?? (object)DBNull.Value);
        command.Parameters.AddWithValue("@Active", production.IsActive);
        command.Parameters.AddWithValue("@CreatedAt", DateTime.UtcNow);

        var id = Convert.ToInt32(await command.ExecuteScalarAsync());
        production.Id = id;

        return production;
    }

    public async Task<ComponentProduction> UpdateProductionAsync(ComponentProduction production)
    {
        const string sql = @"
            UPDATE component_production 
            SET produced_amount = @ProducedAmount,
                expiration_date = @ExpirationDate,
                cost = @Cost,
                notes = @Notes,
                active = @Active
            WHERE id = @Id";

        using var connection = new MySqlConnection(_connectionString);
        await connection.OpenAsync();

        using var command = new MySqlCommand(sql, connection);
        command.Parameters.AddWithValue("@Id", production.Id);
        command.Parameters.AddWithValue("@ProducedAmount", production.ProducedAmount);
        command.Parameters.AddWithValue("@ExpirationDate", production.ExpirationDate ?? (object)DBNull.Value);
        command.Parameters.AddWithValue("@Cost", production.Cost);
        command.Parameters.AddWithValue("@Notes", production.Notes ?? (object)DBNull.Value);
        command.Parameters.AddWithValue("@Active", production.IsActive);

        await command.ExecuteNonQueryAsync();

        return production;
    }

    public async Task<bool> ConsumeProductionAsync(int productionId, decimal amountConsumed)
    {
        const string sql = @"
            UPDATE component_production 
            SET produced_amount = produced_amount - @AmountConsumed
            WHERE id = @Id AND produced_amount >= @AmountConsumed";

        using var connection = new MySqlConnection(_connectionString);
        await connection.OpenAsync();

        using var command = new MySqlCommand(sql, connection);
        command.Parameters.AddWithValue("@Id", productionId);
        command.Parameters.AddWithValue("@AmountConsumed", amountConsumed);

        var rowsAffected = await command.ExecuteNonQueryAsync();
        return rowsAffected > 0;
    }

    // Private mapping methods
    private Component MapComponent(MySqlDataReader reader)
    {
        // Helper method to safely get column ordinal
        int GetColumnOrdinal(string columnName)
        {
            try
            {
                return reader.GetOrdinal(columnName);
            }
            catch
            {
                return -1;
            }
        }
        
        var supplyCategoryIdOrdinal = GetColumnOrdinal("supply_category_id");
        var minimumStockOrdinal = GetColumnOrdinal("minimum_stock");
        
        return new Component
        {
            Id = reader.GetInt32("id"),
            Name = reader.GetString("name"),
            Description = reader.IsDBNull(reader.GetOrdinal("description")) ? null : reader.GetString("description"),
            BusinessId = reader.GetInt32("business_id"),
            StoreId = reader.IsDBNull(reader.GetOrdinal("store_id")) ? null : reader.GetInt32("store_id"),
            UnitMeasureId = reader.GetInt32("unit_measure_id"),
            PreparationTime = reader.IsDBNull(reader.GetOrdinal("preparation_time")) ? null : reader.GetInt32("preparation_time"),
            TimeUnitId = reader.IsDBNull(reader.GetOrdinal("time_unit_id")) ? null : reader.GetInt32("time_unit_id"),
            YieldAmount = reader.GetDecimal("yield_amount"),
            SupplyCategoryId = supplyCategoryIdOrdinal >= 0 && !reader.IsDBNull(supplyCategoryIdOrdinal) ? reader.GetInt32(supplyCategoryIdOrdinal) : null,
            MinimumStock = minimumStockOrdinal >= 0 && !reader.IsDBNull(minimumStockOrdinal) ? reader.GetInt32(minimumStockOrdinal) : 0,
            Active = reader.GetBoolean("active"),
            CreatedAt = reader.GetDateTime("created_at"),
            UpdatedAt = reader.IsDBNull(reader.GetOrdinal("updated_at")) ? null : reader.GetDateTime("updated_at"),
            // Display properties from JOIN
            UnitMeasureName = reader.IsDBNull(reader.GetOrdinal("unit_measure_name")) ? null : reader.GetString("unit_measure_name"),
            UnitMeasureSymbol = reader.IsDBNull(reader.GetOrdinal("unit_measure_symbol")) ? null : reader.GetString("unit_measure_symbol")
        };
    }

    private ComponentSupply MapComponentSupply(MySqlDataReader reader)
    {
        var supply = new ComponentSupply
        {
            Id = Convert.ToInt32(reader["id"]),
            ComponentId = Convert.ToInt32(reader["component_id"]),
            SupplyId = reader.IsDBNull(reader.GetOrdinal("supply_id")) ? null : Convert.ToInt32(reader["supply_id"]),
            SubComponentId = reader.IsDBNull(reader.GetOrdinal("sub_component_id")) ? null : Convert.ToInt32(reader["sub_component_id"]),
            Quantity = Convert.ToDecimal(reader["quantity"]),
            Order = Convert.ToInt32(reader["order"]),
            ItemType = Convert.ToString(reader["item_type"]) ?? "supply",
            IsOptional = Convert.ToBoolean(reader["is_optional"])
        };

        // Load navigation properties if they exist in the result set
        if (supply.SupplyId.HasValue && !reader.IsDBNull(reader.GetOrdinal("supply_name")))
        {
            supply.Supply = new Supply
            {
                Id = supply.SupplyId.Value,
                Name = reader.GetString("supply_name"),
                UnitMeasureId = 0 // Will be set by the symbol if needed
            };

            // Add UnitMeasure navigation property
            if (!reader.IsDBNull(reader.GetOrdinal("supply_unit_symbol")))
            {
                supply.Supply.UnitMeasure = new UnitMeasure
                {
                    Symbol = reader.GetString("supply_unit_symbol")
                };
            }
        }

        if (supply.SubComponentId.HasValue && !reader.IsDBNull(reader.GetOrdinal("component_name")))
        {
            supply.SubComponent = new Component
            {
                Id = supply.SubComponentId.Value,
                Name = reader.GetString("component_name"),
                UnitMeasureSymbol = reader.IsDBNull(reader.GetOrdinal("component_unit_symbol")) ? null : reader.GetString("component_unit_symbol")
            };
        }

        return supply;
    }

    private ComponentProduction MapComponentProduction(MySqlDataReader reader)
    {
        return new ComponentProduction
        {
            Id = reader.GetInt32("id"),
            ComponentId = reader.GetInt32("component_id"),
            ProducedAmount = reader.GetDecimal("produced_amount"),
            ProductionDate = reader.GetDateTime("production_date"),
            ExpirationDate = reader.IsDBNull(reader.GetOrdinal("expiration_date")) ? null : reader.GetDateTime("expiration_date"),
            BatchNumber = reader.IsDBNull(reader.GetOrdinal("batch_number")) ? null : reader.GetString("batch_number"),
            Cost = reader.IsDBNull(reader.GetOrdinal("cost")) ? 0 : reader.GetDecimal("cost"),
            Notes = reader.IsDBNull(reader.GetOrdinal("notes")) ? null : reader.GetString("notes"),
            IsActive = reader.GetBoolean("active"),
            CreatedAt = reader.GetDateTime("created_at")
        };
    }
}
