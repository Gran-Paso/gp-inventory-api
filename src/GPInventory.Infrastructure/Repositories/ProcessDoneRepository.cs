using GPInventory.Application.Interfaces;
using GPInventory.Domain.Entities;
using GPInventory.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using MySqlConnector;

namespace GPInventory.Infrastructure.Repositories;

public class ProcessDoneRepository : IProcessDoneRepository
{
    private readonly ApplicationDbContext _context;

    public ProcessDoneRepository(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<ProcessDone?> GetByIdAsync(int id)
    {
        return await _context.ProcessDones
            .FirstOrDefaultAsync(pd => pd.Id == id);
    }

    public async Task<ProcessDone?> GetByIdWithDetailsAsync(int id)
    {
        // Use ADO.NET to avoid EF Core trying to map non-existent columns
        var connectionString = _context.Database.GetConnectionString();
        using var connection = new MySqlConnection(connectionString);
        await connection.OpenAsync();
        
        // Get ProcessDone with Process details
        var query = @"
            SELECT 
                pd.id,
                pd.process_id,
                pd.stage,
                pd.start_date,
                pd.end_date,
                pd.stock_id,
                pd.amount,
                pd.cost,
                pd.completed_at,
                pd.notes,
                p.id as process_id_detail,
                p.name as process_name,
                p.description as process_description,
                p.product_id,
                p.production_time,
                p.time_unit_id,
                p.store_id
            FROM process_done pd
            LEFT JOIN processes p ON pd.process_id = p.id
            WHERE pd.id = @id";
        
        using var command = new MySqlCommand(query, connection);
        command.Parameters.AddWithValue("@id", id);
        
        ProcessDone? processDone = null;
        
        using var reader = await command.ExecuteReaderAsync();
        if (await reader.ReadAsync())
        {
            processDone = new ProcessDone
            {
                Id = reader.GetInt32("id"),
                ProcessId = reader.GetInt32("process_id"),
                Stage = reader["stage"] != DBNull.Value ? reader.GetInt32("stage") : 0,
                StartDate = reader["start_date"] as DateTime?,
                EndDate = reader["end_date"] as DateTime?,
                StockId = reader["stock_id"] as int?,
                Amount = reader.GetInt32("amount"),
                Cost = reader["cost"] != DBNull.Value ? reader.GetDecimal("cost") : 0m,
                CompletedAt = reader.GetDateTime("completed_at"),
                Notes = reader["notes"] as string
            };
            
            // Manually set the Process navigation property if data exists
            if (reader["process_id_detail"] != DBNull.Value)
            {
                processDone.Process = new Process
                {
                    Id = reader.GetInt32("process_id_detail"),
                    Name = reader.GetString("process_name"),
                    Description = reader["process_description"] as string,
                    ProductId = reader.GetInt32("product_id"),
                    ProductionTime = reader.GetInt32("production_time"),
                    TimeUnitId = reader.GetInt32("time_unit_id"),
                    StoreId = reader.GetInt32("store_id")
                };
            }
        }
        
        // Load SupplyEntries separately using a new connection
        if (processDone != null)
        {
            using var supplyConnection = new MySqlConnection(connectionString);
            await supplyConnection.OpenAsync();
            
            var supplyQuery = @"
                SELECT id, unit_cost, amount, provider_id, supply_id, process_done_id, created_at, updated_at
                FROM supply_entry 
                WHERE process_done_id = @processDoneId";
            
            using var supplyCommand = new MySqlCommand(supplyQuery, supplyConnection);
            supplyCommand.Parameters.AddWithValue("@processDoneId", processDone.Id);
            
            var supplyEntries = new List<SupplyEntry>();
            using var supplyReader = await supplyCommand.ExecuteReaderAsync();
            
            while (await supplyReader.ReadAsync())
            {
                var supplyEntry = new SupplyEntry
                {
                    Id = supplyReader.GetInt32("id"),
                    UnitCost = supplyReader.GetInt32("unit_cost"),
                    Amount = supplyReader.GetInt32("amount"),
                    ProviderId = supplyReader.GetInt32("provider_id"),
                    SupplyId = supplyReader.GetInt32("supply_id"),
                    ProcessDoneId = supplyReader["process_done_id"] as int?,
                    CreatedAt = supplyReader.GetDateTime("created_at"),
                    UpdatedAt = supplyReader.GetDateTime("updated_at")
                };
                supplyEntries.Add(supplyEntry);
            }
            
            processDone.SupplyEntries = supplyEntries;
        }
        
        return processDone;
    }

    public async Task<IEnumerable<ProcessDone>> GetAllAsync()
    {
        return await _context.ProcessDones
            .Include(pd => pd.Process)
                .ThenInclude(p => p.Product)
            .Include(pd => pd.Process)
                .ThenInclude(p => p.Store)
            .Include(pd => pd.SupplyEntries)
            .OrderByDescending(pd => pd.CompletedAt)
            .ToListAsync();
    }

    public async Task<IEnumerable<ProcessDone>> GetByProcessIdAsync(int processId)
    {
        // Simplified query - just get ProcessDone records without JOIN
        var processDones = new List<ProcessDone>();
        
        var connectionString = _context.Database.GetConnectionString();
        using var connection = new MySqlConnection(connectionString);
        await connection.OpenAsync();
        
        var query = @"
            SELECT 
                pd.id,
                pd.process_id,
                pd.stage,
                pd.start_date,
                pd.end_date,
                pd.stock_id,
                pd.amount,
                pd.cost,
                pd.completed_at,
                pd.notes
            FROM process_done pd
            WHERE pd.process_id = @processId
            ORDER BY pd.completed_at DESC";
        
        using var command = new MySqlCommand(query, connection);
        command.Parameters.AddWithValue("@processId", processId);
        
        using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            var processDone = new ProcessDone
            {
                Id = reader.GetInt32("id"),
                ProcessId = reader.GetInt32("process_id"),
                Stage = reader["stage"] != DBNull.Value ? reader.GetInt32("stage") : 0,
                StartDate = reader["start_date"] as DateTime?,
                EndDate = reader["end_date"] as DateTime?,
                StockId = reader["stock_id"] as int?,
                Amount = reader.GetInt32("amount"),
                Cost = reader["cost"] != DBNull.Value ? reader.GetDecimal("cost") : 0m,
                CompletedAt = reader.GetDateTime("completed_at"),
                Notes = reader["notes"] as string
            };
            
            processDones.Add(processDone);
        }
        
        return processDones;
    }

    public async Task<ProcessDone> CreateAsync(ProcessDone processDone)
    {
        _context.ProcessDones.Add(processDone);
        await _context.SaveChangesAsync();
        return processDone;
    }

    public async Task<ProcessDone> UpdateAsync(ProcessDone processDone)
    {
        // Desconectar cualquier entidad que esté siendo rastreada con el mismo ID
        var tracked = _context.ChangeTracker.Entries<ProcessDone>()
            .FirstOrDefault(e => e.Entity.Id == processDone.Id);
        
        if (tracked != null)
        {
            _context.Entry(tracked.Entity).State = EntityState.Detached;
        }
        
        // Ahora actualizar la entidad
        _context.ProcessDones.Update(processDone);
        await _context.SaveChangesAsync();
        return processDone;
    }
    
    /// <summary>
    /// Actualiza solo el campo Cost de un ProcessDone sin cargar la entidad completa
    /// </summary>
    public async Task UpdateCostAsync(int processDoneId, decimal cost)
    {
        var connectionString = _context.Database.GetConnectionString();
        using var connection = new MySqlConnection(connectionString);
        await connection.OpenAsync();
        
        var query = @"
            UPDATE process_done 
            SET cost = @cost, updated_at = @updatedAt 
            WHERE id = @id";
        
        using var command = new MySqlCommand(query, connection);
        command.Parameters.AddWithValue("@id", processDoneId);
        command.Parameters.AddWithValue("@cost", cost);
        command.Parameters.AddWithValue("@updatedAt", DateTime.UtcNow);
        
        await command.ExecuteNonQueryAsync();
    }

    public async Task DeleteAsync(int id)
    {
        var processDone = await GetByIdAsync(id);
        if (processDone != null)
        {
            _context.ProcessDones.Remove(processDone);
            await _context.SaveChangesAsync();
        }
    }
}
