using GPInventory.Application.Interfaces;
using GPInventory.Domain.Entities;
using GPInventory.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

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
        _context.ComponentProductions.Add(componentProduction);
        await _context.SaveChangesAsync();
        return componentProduction;
    }

    public async Task<IEnumerable<ComponentProduction>> GetByComponentIdAsync(int componentId)
    {
        // Devolver TODAS las producciones (padres e hijos) para permitir cálculos FIFO en el frontend
        // Los hijos son necesarios para calcular el stock disponible de cada producción padre
        return await _context.ComponentProductions
            .Where(cp => cp.ComponentId == componentId && cp.IsActive)
            .OrderByDescending(cp => cp.CreatedAt)
            .ToListAsync();
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
                   process_done_id, business_id, store_id, component_production_id
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
                ComponentProductionId = reader.IsDBNull(14) ? null : reader.GetInt32(14)
            };
            results.Add(componentProduction);
        }

        return results;
    }

    public async Task<IEnumerable<ComponentProduction>> GetAllAsync()
    {
        return await _context.ComponentProductions
            .Include(cp => cp.Component)
            .Where(cp => cp.IsActive)
            .OrderByDescending(cp => cp.CreatedAt)
            .ToListAsync();
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
                    StoreId = reader.GetInt32(reader.GetOrdinal("store_id")),
                    ProducedAmount = reader.GetDecimal(reader.GetOrdinal("available_amount")), // ⭐ USAR CANTIDAD DISPONIBLE
                    ProductionDate = reader.IsDBNull(reader.GetOrdinal("production_date")) ? null : reader.GetDateTime(reader.GetOrdinal("production_date")),
                    ExpirationDate = reader.IsDBNull(reader.GetOrdinal("expiration_date")) ? null : reader.GetDateTime(reader.GetOrdinal("expiration_date")),
                    BatchNumber = reader.IsDBNull(reader.GetOrdinal("batch_number")) ? null : reader.GetString(reader.GetOrdinal("batch_number")),
                    Cost = reader.GetDecimal(reader.GetOrdinal("cost")),
                    Notes = reader.IsDBNull(reader.GetOrdinal("notes")) ? null : reader.GetString(reader.GetOrdinal("notes")),
                    ComponentProductionId = reader.IsDBNull(reader.GetOrdinal("component_production_id")) ? null : reader.GetInt32(reader.GetOrdinal("component_production_id")),
                    IsActive = reader.GetBoolean(reader.GetOrdinal("is_active")),
                    CreatedAt = reader.GetDateTime(reader.GetOrdinal("created_at")),
                    UpdatedAt = reader.GetDateTime(reader.GetOrdinal("updated_at"))
                };
                productions.Add(production);
            }

            return productions;
        }
        finally
        {
            if (shouldCloseConnection)
                await connection.CloseAsync();
        }
    }

    public async Task<ComponentProduction?> GetByIdAsync(int id)
    {
        return await _context.ComponentProductions
            .FirstOrDefaultAsync(cp => cp.Id == id);
    }

    public async Task UpdateAsync(ComponentProduction componentProduction)
    {
        _context.ComponentProductions.Update(componentProduction);
        await _context.SaveChangesAsync();
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
        finally
        {
            if (shouldCloseConnection)
                await connection.CloseAsync();
        }
    }
}
