using GPInventory.Application.Interfaces;
using GPInventory.Domain.Entities;
using GPInventory.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using MySqlConnector;

namespace GPInventory.Infrastructure.Repositories;

public class ComponentProductionRepository : IComponentProductionRepository
{
    private readonly ApplicationDbContext _context;

    public ComponentProductionRepository(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<ComponentProduction> CreateAsync(ComponentProduction componentProduction)
    {
        var sql = @"
            INSERT INTO component_production (
                component_id, 
                process_done_id, 
                business_id, 
                store_id, 
                produced_amount, 
                production_date, 
                expiration_date, 
                batch_number, 
                cost, 
                notes, 
                component_production_id, 
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
            
            // Parameters
            var p0 = command.CreateParameter();
            p0.ParameterName = "@p0";
            p0.Value = componentProduction.ComponentId;
            command.Parameters.Add(p0);
            
            var p1 = command.CreateParameter();
            p1.ParameterName = "@p1";
            p1.Value = (object?)componentProduction.ProcessDoneId ?? DBNull.Value;
            command.Parameters.Add(p1);
            
            var p2 = command.CreateParameter();
            p2.ParameterName = "@p2";
            p2.Value = componentProduction.BusinessId;
            command.Parameters.Add(p2);
            
            var p3 = command.CreateParameter();
            p3.ParameterName = "@p3";
            p3.Value = componentProduction.StoreId;
            command.Parameters.Add(p3);
            
            var p4 = command.CreateParameter();
            p4.ParameterName = "@p4";
            p4.Value = componentProduction.ProducedAmount;
            command.Parameters.Add(p4);
            
            var p5 = command.CreateParameter();
            p5.ParameterName = "@p5";
            p5.Value = (object?)componentProduction.ProductionDate ?? DBNull.Value;
            command.Parameters.Add(p5);
            
            var p6 = command.CreateParameter();
            p6.ParameterName = "@p6";
            p6.Value = (object?)componentProduction.ExpirationDate ?? DBNull.Value;
            command.Parameters.Add(p6);
            
            var p7 = command.CreateParameter();
            p7.ParameterName = "@p7";
            p7.Value = (object?)componentProduction.BatchNumber ?? DBNull.Value;
            command.Parameters.Add(p7);
            
            var p8 = command.CreateParameter();
            p8.ParameterName = "@p8";
            p8.Value = componentProduction.Cost;
            command.Parameters.Add(p8);
            
            var p9 = command.CreateParameter();
            p9.ParameterName = "@p9";
            p9.Value = (object?)componentProduction.Notes ?? DBNull.Value;
            command.Parameters.Add(p9);
            
            var p10 = command.CreateParameter();
            p10.ParameterName = "@p10";
            p10.Value = (object?)componentProduction.ComponentProductionId ?? DBNull.Value;
            command.Parameters.Add(p10);
            
            var p11 = command.CreateParameter();
            p11.ParameterName = "@p11";
            p11.Value = (object?)componentProduction.CreatedByUserId ?? DBNull.Value;
            command.Parameters.Add(p11);
            
            var p12 = command.CreateParameter();
            p12.ParameterName = "@p12";
            p12.Value = componentProduction.IsActive;
            command.Parameters.Add(p12);
            
            var p13 = command.CreateParameter();
            p13.ParameterName = "@p13";
            p13.Value = componentProduction.CreatedAt;
            command.Parameters.Add(p13);
            
            var p14 = command.CreateParameter();
            p14.ParameterName = "@p14";
            p14.Value = componentProduction.UpdatedAt;
            command.Parameters.Add(p14);
            
            var result = await command.ExecuteScalarAsync();
            componentProduction.Id = Convert.ToInt32(result);
            
            return componentProduction;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error in CreateAsync: {ex.Message}");
            throw;
        }
        finally
        {
            if (shouldCloseConnection)
                await connection.CloseAsync();
        }
    }

    public async Task<IEnumerable<ComponentProduction>> GetByComponentIdAsync(int componentId)
    {
        // Devolver TODAS las producciones (padres e hijos) incluyendo inactivas para el historial completo
        // Los hijos son necesarios para calcular el stock disponible de cada producción padre
        var sql = @"
            SELECT id, component_id, produced_amount, production_date, expiration_date, 
                   batch_number, cost, notes, is_active, created_at, updated_at, 
                   process_done_id, business_id, store_id, component_production_id, created_by_user_id
            FROM component_production 
            WHERE component_id = @p0
            ORDER BY created_at DESC";

        var connection = _context.Database.GetDbConnection();
        var shouldCloseConnection = connection.State == System.Data.ConnectionState.Closed;
        
        if (shouldCloseConnection)
            await connection.OpenAsync();
        
        try
        {
            using var command = connection.CreateCommand();
            command.CommandText = sql;
            
            var parameter = command.CreateParameter();
            parameter.ParameterName = "@p0";
            parameter.Value = componentId;
            command.Parameters.Add(parameter);

            var results = new List<ComponentProduction>();
            using var reader = await command.ExecuteReaderAsync();

            while (await reader.ReadAsync())
            {
                var componentProduction = new ComponentProduction
                {
                    Id = reader.GetInt32(0),
                    ComponentId = reader.GetInt32(1),
                    ProducedAmount = reader.GetDecimal(2),
                    ProductionDate = reader.IsDBNull(3) ? null : reader.GetDateTime(3),
                    ExpirationDate = reader.IsDBNull(4) ? null : reader.GetDateTime(4),
                    BatchNumber = reader.IsDBNull(5) ? null : reader.GetString(5),
                    Cost = reader.GetDecimal(6),
                    Notes = reader.IsDBNull(7) ? null : reader.GetString(7),
                    IsActive = reader.GetBoolean(8),
                    CreatedAt = reader.GetDateTime(9),
                    UpdatedAt = reader.GetDateTime(10),
                    ProcessDoneId = reader.IsDBNull(11) ? null : reader.GetInt32(11),
                    BusinessId = reader.GetInt32(12),
                    StoreId = reader.GetInt32(13),
                    ComponentProductionId = reader.IsDBNull(14) ? null : reader.GetInt32(14),
                    CreatedByUserId = reader.IsDBNull(15) ? null : reader.GetInt32(15)
                };
                results.Add(componentProduction);
            }

            return results;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error in GetByComponentIdAsync: {ex.Message}");
            throw;
        }
        finally
        {
            if (shouldCloseConnection)
                await connection.CloseAsync();
        }
    }

    public async Task<decimal> GetCurrentStockAsync(int componentId)
    {
        // Usar SQL raw para calcular el stock disponible real usando lógica FIFO
        // Solo contar producciones padre (component_production_id IS NULL) que estén activas
        // y restar sus hijos (registros negativos)
        var sql = @"
            SELECT 
                COALESCE(SUM(
                    parent.produced_amount + COALESCE(
                        (SELECT SUM(child.produced_amount) 
                         FROM component_production child 
                         WHERE child.component_production_id = parent.id 
                         AND child.is_active = 1), 
                        0
                    )
                ), 0) as available_stock
            FROM component_production parent
            WHERE parent.component_id = @p0
            AND parent.component_production_id IS NULL
            AND parent.is_active = 1
            AND parent.produced_amount > 0";

        var connection = _context.Database.GetDbConnection();
        var shouldCloseConnection = connection.State == System.Data.ConnectionState.Closed;
        
        if (shouldCloseConnection)
            await connection.OpenAsync();
        
        try
        {
            using var command = connection.CreateCommand();
            
            command.CommandText = sql;
            
            var parameter = command.CreateParameter();
            parameter.ParameterName = "@p0";
            parameter.Value = componentId;
            command.Parameters.Add(parameter);
            
            var result = await command.ExecuteScalarAsync();
            
            return result != null ? Convert.ToDecimal(result) : 0;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error in GetTotalAvailableByComponentIdAsync: {ex.Message}");
            throw;
        }
        finally
        {
            if (shouldCloseConnection)
                await connection.CloseAsync();
        }
    }

    public async Task<IEnumerable<ComponentProduction>> GetByProcessDoneIdAsync(int processDoneId)
    {
        // Use raw SQL to avoid EF Core mapping issues
        using var command = _context.Database.GetDbConnection().CreateCommand();
        command.CommandText = @"
            SELECT id, component_id, produced_amount, production_date, expiration_date, 
                   batch_number, cost, notes, is_active, created_at, updated_at, 
                   process_done_id, business_id, store_id, component_production_id, created_by_user_id
            FROM component_production 
            WHERE process_done_id = @processDoneId AND is_active = 1
            ORDER BY created_at";

        var parameter = command.CreateParameter();
        parameter.ParameterName = "@processDoneId";
        parameter.Value = processDoneId;
        command.Parameters.Add(parameter);

        await _context.Database.OpenConnectionAsync();

        var results = new List<ComponentProduction>();
        using var reader = await command.ExecuteReaderAsync();

        while (await reader.ReadAsync())
        {
            var componentProduction = new ComponentProduction
            {
                Id = reader.GetInt32(0),
                ComponentId = reader.GetInt32(1),
                ProducedAmount = reader.GetDecimal(2),
                ProductionDate = reader.IsDBNull(3) ? null : reader.GetDateTime(3),
                ExpirationDate = reader.IsDBNull(4) ? null : reader.GetDateTime(4),
                BatchNumber = reader.IsDBNull(5) ? null : reader.GetString(5),
                Cost = reader.GetDecimal(6),
                Notes = reader.IsDBNull(7) ? null : reader.GetString(7),
                IsActive = reader.GetBoolean(8),
                CreatedAt = reader.GetDateTime(9),
                UpdatedAt = reader.GetDateTime(10),
                ProcessDoneId = reader.IsDBNull(11) ? null : reader.GetInt32(11),
                BusinessId = reader.GetInt32(12),
                StoreId = reader.GetInt32(13),
                ComponentProductionId = reader.IsDBNull(14) ? null : reader.GetInt32(14),
                CreatedByUserId = reader.IsDBNull(15) ? null : reader.GetInt32(15)
            };
            results.Add(componentProduction);
        }

        return results;
    }

    public async Task<IEnumerable<ComponentProduction>> GetAllAsync()
    {
        try
        {
            var productions = new List<ComponentProduction>();
            var connectionString = _context.Database.GetConnectionString();
            using var connection = new MySqlConnection(connectionString);
            await connection.OpenAsync();
            
            var query = @"
                SELECT 
                    id,
                    component_id,
                    process_done_id,
                    business_id,
                    store_id,
                    produced_amount,
                    production_date,
                    cost,
                    notes,
                    component_production_id,
                    created_by_user_id,
                    is_active,
                    created_at
                FROM component_production
                WHERE is_active = 1
                ORDER BY created_at DESC";
            
            using var command = new MySqlCommand(query, connection);
            using var reader = await command.ExecuteReaderAsync();
            
            while (await reader.ReadAsync())
            {
                var production = new ComponentProduction
                {
                    Id = reader.GetInt32(reader.GetOrdinal("id")),
                    ComponentId = reader.GetInt32(reader.GetOrdinal("component_id")),
                    ProcessDoneId = reader.IsDBNull(reader.GetOrdinal("process_done_id")) ? null : reader.GetInt32(reader.GetOrdinal("process_done_id")),
                    BusinessId = reader.GetInt32(reader.GetOrdinal("business_id")),
                    StoreId = reader.IsDBNull(reader.GetOrdinal("store_id")) ? 0 : reader.GetInt32(reader.GetOrdinal("store_id")),
                    ProducedAmount = reader.GetDecimal(reader.GetOrdinal("produced_amount")),
                    ProductionDate = reader.IsDBNull(reader.GetOrdinal("production_date")) ? null : reader.GetDateTime(reader.GetOrdinal("production_date")),
                    Cost = reader.GetDecimal(reader.GetOrdinal("cost")),
                    Notes = reader.IsDBNull(reader.GetOrdinal("notes")) ? null : reader.GetString(reader.GetOrdinal("notes")),
                    ComponentProductionId = reader.IsDBNull(reader.GetOrdinal("component_production_id")) ? null : reader.GetInt32(reader.GetOrdinal("component_production_id")),
                    CreatedByUserId = reader.IsDBNull(reader.GetOrdinal("created_by_user_id")) ? null : reader.GetInt32(reader.GetOrdinal("created_by_user_id")),
                    IsActive = reader.GetBoolean(reader.GetOrdinal("is_active")),
                    CreatedAt = reader.GetDateTime(reader.GetOrdinal("created_at"))
                };
                
                productions.Add(production);
            }
            
            return productions;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error in GetAllAsync: {ex.Message}");
            throw;
        }
    }

    public async Task<IEnumerable<ComponentProduction>> GetAvailableProductionsByComponentIdAsync(int componentId)
    {
        // Query que calcula la cantidad disponible de cada lote (padre)
        // restando la suma de sus consumos (hijos con component_production_id = padre.id)
        var sql = @"
            SELECT 
                parent.id,
                parent.component_id,
                parent.process_done_id,
                parent.business_id,
                parent.store_id,
                parent.produced_amount,
                parent.production_date,
                parent.expiration_date,
                parent.batch_number,
                parent.cost,
                parent.notes,
                parent.component_production_id,
                parent.is_active,
                parent.created_at,
                parent.updated_at,
                parent.created_by_user_id,
                parent.produced_amount + COALESCE(
                    (SELECT SUM(child.produced_amount) 
                     FROM component_production child 
                     WHERE child.component_production_id = parent.id 
                     AND child.is_active = 1), 
                    0
                ) as available_amount
            FROM component_production parent
            WHERE parent.component_id = @p0
            AND parent.component_production_id IS NULL
            AND parent.is_active = 1
            AND parent.produced_amount > 0
            HAVING available_amount > 0
            ORDER BY parent.created_at ASC";

        var connection = _context.Database.GetDbConnection();
        var shouldCloseConnection = connection.State == System.Data.ConnectionState.Closed;
        
        if (shouldCloseConnection)
            await connection.OpenAsync();

        try
        {
            using var command = connection.CreateCommand();
            command.CommandText = sql;
            
            var parameter = command.CreateParameter();
            parameter.ParameterName = "@p0";
            parameter.Value = componentId;
            command.Parameters.Add(parameter);

            var productions = new List<ComponentProduction>();
            using var reader = await command.ExecuteReaderAsync();

            while (await reader.ReadAsync())
            {
                var production = new ComponentProduction
                {
                    Id = reader.GetInt32(reader.GetOrdinal("id")),
                    ComponentId = reader.GetInt32(reader.GetOrdinal("component_id")),
                    ProcessDoneId = reader.IsDBNull(reader.GetOrdinal("process_done_id")) ? null : reader.GetInt32(reader.GetOrdinal("process_done_id")),
                    BusinessId = reader.GetInt32(reader.GetOrdinal("business_id")),
                    StoreId = reader.IsDBNull(reader.GetOrdinal("store_id")) ? 0 : reader.GetInt32(reader.GetOrdinal("store_id")),
                    ProducedAmount = reader.GetDecimal(reader.GetOrdinal("available_amount")), // ⭐ USAR CANTIDAD DISPONIBLE
                    ProductionDate = reader.IsDBNull(reader.GetOrdinal("production_date")) ? null : reader.GetDateTime(reader.GetOrdinal("production_date")),
                    ExpirationDate = reader.IsDBNull(reader.GetOrdinal("expiration_date")) ? null : reader.GetDateTime(reader.GetOrdinal("expiration_date")),
                    BatchNumber = reader.IsDBNull(reader.GetOrdinal("batch_number")) ? null : reader.GetString(reader.GetOrdinal("batch_number")),
                    Cost = reader.GetDecimal(reader.GetOrdinal("cost")),
                    Notes = reader.IsDBNull(reader.GetOrdinal("notes")) ? null : reader.GetString(reader.GetOrdinal("notes")),
                    ComponentProductionId = reader.IsDBNull(reader.GetOrdinal("component_production_id")) ? null : reader.GetInt32(reader.GetOrdinal("component_production_id")),
                    IsActive = reader.GetBoolean(reader.GetOrdinal("is_active")),
                    CreatedAt = reader.GetDateTime(reader.GetOrdinal("created_at")),
                    UpdatedAt = reader.GetDateTime(reader.GetOrdinal("updated_at")),
                    CreatedByUserId = reader.IsDBNull(reader.GetOrdinal("created_by_user_id")) ? null : reader.GetInt32(reader.GetOrdinal("created_by_user_id"))
                };
                productions.Add(production);
            }

            return productions;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error in GetAvailableProductionsByComponentIdAsync: {ex.Message}");
            throw;
        }
        finally
        {
            if (shouldCloseConnection)
                await connection.CloseAsync();
        }
    }

    public async Task<ComponentProduction?> GetByIdAsync(int id)
    {
        try
        {
            var connectionString = _context.Database.GetConnectionString();
            using var connection = new MySqlConnection(connectionString);
            await connection.OpenAsync();
            
            var query = @"
                SELECT 
                    id,
                    component_id,
                    process_done_id,
                    business_id,
                    store_id,
                    produced_amount,
                    production_date,
                    cost,
                    notes,
                    component_production_id,
                    created_by_user_id,
                    is_active,
                    created_at
                FROM component_production
                WHERE id = @id";
            
            using var command = new MySqlCommand(query, connection);
            command.Parameters.AddWithValue("@id", id);
            
            using var reader = await command.ExecuteReaderAsync();
            if (await reader.ReadAsync())
            {
                return new ComponentProduction
                {
                    Id = reader.GetInt32(reader.GetOrdinal("id")),
                    ComponentId = reader.GetInt32(reader.GetOrdinal("component_id")),
                    ProcessDoneId = reader.IsDBNull(reader.GetOrdinal("process_done_id")) ? null : reader.GetInt32(reader.GetOrdinal("process_done_id")),
                    BusinessId = reader.GetInt32(reader.GetOrdinal("business_id")),
                    StoreId = reader.IsDBNull(reader.GetOrdinal("store_id")) ? 0 : reader.GetInt32(reader.GetOrdinal("store_id")),
                    ProducedAmount = reader.GetDecimal(reader.GetOrdinal("produced_amount")),
                    ProductionDate = reader.IsDBNull(reader.GetOrdinal("production_date")) ? null : reader.GetDateTime(reader.GetOrdinal("production_date")),
                    Cost = reader.GetDecimal(reader.GetOrdinal("cost")),
                    Notes = reader.IsDBNull(reader.GetOrdinal("notes")) ? null : reader.GetString(reader.GetOrdinal("notes")),
                    ComponentProductionId = reader.IsDBNull(reader.GetOrdinal("component_production_id")) ? null : reader.GetInt32(reader.GetOrdinal("component_production_id")),
                    CreatedByUserId = reader.IsDBNull(reader.GetOrdinal("created_by_user_id")) ? null : reader.GetInt32(reader.GetOrdinal("created_by_user_id")),
                    IsActive = reader.GetBoolean(reader.GetOrdinal("is_active")),
                    CreatedAt = reader.GetDateTime(reader.GetOrdinal("created_at"))
                };
            }
            
            return null;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error in GetByIdAsync: {ex.Message}");
            throw;
        }
    }

    public async Task UpdateAsync(ComponentProduction componentProduction)
    {
        var sql = @"
            UPDATE component_production 
            SET 
                component_id = @p0,
                process_done_id = @p1,
                business_id = @p2,
                store_id = @p3,
                produced_amount = @p4,
                production_date = @p5,
                expiration_date = @p6,
                batch_number = @p7,
                cost = @p8,
                notes = @p9,
                component_production_id = @p10,
                created_by_user_id = @p11,
                is_active = @p12,
                updated_at = @p13
            WHERE id = @p14";

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
            p0.Value = componentProduction.ComponentId;
            command.Parameters.Add(p0);
            
            var p1 = command.CreateParameter();
            p1.ParameterName = "@p1";
            p1.Value = (object?)componentProduction.ProcessDoneId ?? DBNull.Value;
            command.Parameters.Add(p1);
            
            var p2 = command.CreateParameter();
            p2.ParameterName = "@p2";
            p2.Value = componentProduction.BusinessId;
            command.Parameters.Add(p2);
            
            var p3 = command.CreateParameter();
            p3.ParameterName = "@p3";
            p3.Value = componentProduction.StoreId;
            command.Parameters.Add(p3);
            
            var p4 = command.CreateParameter();
            p4.ParameterName = "@p4";
            p4.Value = componentProduction.ProducedAmount;
            command.Parameters.Add(p4);
            
            var p5 = command.CreateParameter();
            p5.ParameterName = "@p5";
            p5.Value = (object?)componentProduction.ProductionDate ?? DBNull.Value;
            command.Parameters.Add(p5);
            
            var p6 = command.CreateParameter();
            p6.ParameterName = "@p6";
            p6.Value = (object?)componentProduction.ExpirationDate ?? DBNull.Value;
            command.Parameters.Add(p6);
            
            var p7 = command.CreateParameter();
            p7.ParameterName = "@p7";
            p7.Value = (object?)componentProduction.BatchNumber ?? DBNull.Value;
            command.Parameters.Add(p7);
            
            var p8 = command.CreateParameter();
            p8.ParameterName = "@p8";
            p8.Value = componentProduction.Cost;
            command.Parameters.Add(p8);
            
            var p9 = command.CreateParameter();
            p9.ParameterName = "@p9";
            p9.Value = (object?)componentProduction.Notes ?? DBNull.Value;
            command.Parameters.Add(p9);
            
            var p10 = command.CreateParameter();
            p10.ParameterName = "@p10";
            p10.Value = (object?)componentProduction.ComponentProductionId ?? DBNull.Value;
            command.Parameters.Add(p10);
            
            var p11 = command.CreateParameter();
            p11.ParameterName = "@p11";
            p11.Value = (object?)componentProduction.CreatedByUserId ?? DBNull.Value;
            command.Parameters.Add(p11);
            
            var p12 = command.CreateParameter();
            p12.ParameterName = "@p12";
            p12.Value = componentProduction.IsActive;
            command.Parameters.Add(p12);
            
            var p13 = command.CreateParameter();
            p13.ParameterName = "@p13";
            p13.Value = DateTime.Now;
            command.Parameters.Add(p13);
            
            var p14 = command.CreateParameter();
            p14.ParameterName = "@p14";
            p14.Value = componentProduction.Id;
            command.Parameters.Add(p14);
            
            await command.ExecuteNonQueryAsync();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error in UpdateAsync: {ex.Message}");
            throw;
        }
        finally
        {
            if (shouldCloseConnection)
                await connection.CloseAsync();
        }
    }

    public async Task<(decimal stock, decimal totalValue)> GetStockAndValueAsync(int componentId)
    {
        // Calcular el stock disponible y el valor total usando FIFO
        var sql = @"
            SELECT 
                parent.id,
                parent.produced_amount,
                parent.cost,
                COALESCE(
                    parent.produced_amount + COALESCE(
                        (SELECT SUM(child.produced_amount) 
                         FROM component_production child 
                         WHERE child.component_production_id = parent.id 
                         AND child.is_active = 1), 
                        0
                    ), 0
                ) as available_amount
            FROM component_production parent
            WHERE parent.component_id = @p0
            AND parent.component_production_id IS NULL
            AND parent.is_active = 1
            AND parent.produced_amount > 0
            HAVING available_amount > 0
            ORDER BY parent.created_at ASC";

        var connection = _context.Database.GetDbConnection();
        var shouldCloseConnection = connection.State == System.Data.ConnectionState.Closed;
        
        if (shouldCloseConnection)
            await connection.OpenAsync();
        
        try
        {
            using var command = connection.CreateCommand();
            
            command.CommandText = sql;
            
            var parameter = command.CreateParameter();
            parameter.ParameterName = "@p0";
            parameter.Value = componentId;
            command.Parameters.Add(parameter);
            
            decimal totalStock = 0;
            decimal totalValue = 0;
            
            using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                var availableAmount = reader.GetDecimal(reader.GetOrdinal("available_amount"));
                var cost = reader.IsDBNull(reader.GetOrdinal("cost")) ? 0 : reader.GetDecimal(reader.GetOrdinal("cost"));
                var producedAmount = reader.GetDecimal(reader.GetOrdinal("produced_amount"));
                
                // Calcular el costo por unidad
                var costPerUnit = producedAmount > 0 ? cost / producedAmount : 0;
                
                totalStock += availableAmount;
                totalValue += availableAmount * costPerUnit;
            }
            
            return (totalStock, totalValue);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error in GetStockValueByComponentIdAsync: {ex.Message}");
            throw;
        }
        finally
        {
            if (shouldCloseConnection)
                await connection.CloseAsync();
        }
    }
}
