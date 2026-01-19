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
        try
        {
            var connectionString = _context.Database.GetConnectionString();
            using var connection = new MySqlConnection(connectionString);
            await connection.OpenAsync();
            
            var query = @"
                SELECT 
                    id,
                    process_id,
                    stage,
                    start_date,
                    end_date,
                    stock_id,
                    amount,
                    completed_at,
                    notes,
                    cost,
                    created_by_user_id,
                    active,
                    updated_at
                FROM process_done
                WHERE id = @id";
            
            using var command = new MySqlCommand(query, connection);
            command.Parameters.AddWithValue("@id", id);
            
            using var reader = await command.ExecuteReaderAsync();
            if (await reader.ReadAsync())
            {
                return new ProcessDone(
                    reader.GetInt32(reader.GetOrdinal("process_id")),
                    reader.GetInt32(reader.GetOrdinal("amount")),
                    reader.GetInt32(reader.GetOrdinal("stage")),
                    reader.IsDBNull(reader.GetOrdinal("start_date")) ? null : reader.GetDateTime(reader.GetOrdinal("start_date")),
                    reader.IsDBNull(reader.GetOrdinal("end_date")) ? null : reader.GetDateTime(reader.GetOrdinal("end_date")),
                    reader.GetDateTime(reader.GetOrdinal("completed_at")),
                    reader.IsDBNull(reader.GetOrdinal("notes")) ? null : reader.GetString(reader.GetOrdinal("notes")),
                    reader.IsDBNull(reader.GetOrdinal("created_by_user_id")) ? null : reader.GetInt32(reader.GetOrdinal("created_by_user_id"))
                )
                {
                    Id = reader.GetInt32(reader.GetOrdinal("id")),
                    StockId = reader.IsDBNull(reader.GetOrdinal("stock_id")) ? null : reader.GetInt32(reader.GetOrdinal("stock_id")),
                    Cost = reader.GetDecimal(reader.GetOrdinal("cost")),
                    IsActive = reader.IsDBNull(reader.GetOrdinal("active")) ? true : reader.GetBoolean(reader.GetOrdinal("active")),
                    UpdatedAt = reader.GetDateTime(reader.GetOrdinal("updated_at"))
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
        try
        {
            var processDones = new List<ProcessDone>();
            var connectionString = _context.Database.GetConnectionString();
            using var connection = new MySqlConnection(connectionString);
            await connection.OpenAsync();
            
            var query = @"
                SELECT 
                    id,
                    process_id,
                    stage,
                    start_date,
                    end_date,
                    stock_id,
                    amount,
                    completed_at,
                    notes,
                    cost,
                    created_by_user_id,
                    active,
                    updated_at
                FROM process_done
                ORDER BY completed_at DESC";
            
            using var command = new MySqlCommand(query, connection);
            using var reader = await command.ExecuteReaderAsync();
            
            while (await reader.ReadAsync())
            {
                var processDone = new ProcessDone(
                    reader.GetInt32(reader.GetOrdinal("process_id")),
                    reader.GetInt32(reader.GetOrdinal("amount")),
                    reader.GetInt32(reader.GetOrdinal("stage")),
                    reader.IsDBNull(reader.GetOrdinal("start_date")) ? null : reader.GetDateTime(reader.GetOrdinal("start_date")),
                    reader.IsDBNull(reader.GetOrdinal("end_date")) ? null : reader.GetDateTime(reader.GetOrdinal("end_date")),
                    reader.GetDateTime(reader.GetOrdinal("completed_at")),
                    reader.IsDBNull(reader.GetOrdinal("notes")) ? null : reader.GetString(reader.GetOrdinal("notes")),
                    reader.IsDBNull(reader.GetOrdinal("created_by_user_id")) ? null : reader.GetInt32(reader.GetOrdinal("created_by_user_id"))
                )
                {
                    Id = reader.GetInt32(reader.GetOrdinal("id")),
                    StockId = reader.IsDBNull(reader.GetOrdinal("stock_id")) ? null : reader.GetInt32(reader.GetOrdinal("stock_id")),
                    Cost = reader.GetDecimal(reader.GetOrdinal("cost")),
                    IsActive = reader.IsDBNull(reader.GetOrdinal("active")) ? true : reader.GetBoolean(reader.GetOrdinal("active")),
                    UpdatedAt = reader.GetDateTime(reader.GetOrdinal("updated_at"))
                };
                
                processDones.Add(processDone);
            }
            
            return processDones;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error in GetAllAsync: {ex.Message}");
            throw;
        }
    }

    public async Task<IEnumerable<ProcessDone>> GetByProcessIdAsync(int processId)
    {
        try
        {
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
                    pd.completed_at,
                    pd.notes,
                    pd.cost,
                    pd.created_by_user_id,
                    pd.active,
                    pd.updated_at
                FROM process_done pd
                WHERE pd.process_id = @processId
                ORDER BY pd.completed_at DESC";
            
            using var command = new MySqlCommand(query, connection);
            command.Parameters.AddWithValue("@processId", processId);
            
            using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                var processDone = new ProcessDone(
                    reader.GetInt32(reader.GetOrdinal("process_id")),
                    reader.GetInt32(reader.GetOrdinal("amount")),
                    reader.GetInt32(reader.GetOrdinal("stage")),
                    reader.IsDBNull(reader.GetOrdinal("start_date")) ? null : reader.GetDateTime(reader.GetOrdinal("start_date")),
                    reader.IsDBNull(reader.GetOrdinal("end_date")) ? null : reader.GetDateTime(reader.GetOrdinal("end_date")),
                    reader.GetDateTime(reader.GetOrdinal("completed_at")),
                    reader.IsDBNull(reader.GetOrdinal("notes")) ? null : reader.GetString(reader.GetOrdinal("notes")),
                    reader.IsDBNull(reader.GetOrdinal("created_by_user_id")) ? null : reader.GetInt32(reader.GetOrdinal("created_by_user_id"))
                )
                {
                    Id = reader.GetInt32(reader.GetOrdinal("id")),
                    StockId = reader.IsDBNull(reader.GetOrdinal("stock_id")) ? null : reader.GetInt32(reader.GetOrdinal("stock_id")),
                    Cost = reader.GetDecimal(reader.GetOrdinal("cost")),
                    IsActive = reader.IsDBNull(reader.GetOrdinal("active")) ? true : reader.GetBoolean(reader.GetOrdinal("active")),
                    UpdatedAt = reader.GetDateTime(reader.GetOrdinal("updated_at"))
                };
                
                processDones.Add(processDone);
            }
            
            return processDones;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error in GetByProcessIdAsync: {ex.Message}");
            throw;
        }
    }

    public async Task<ProcessDone> CreateAsync(ProcessDone processDone)
    {
        var sql = @"
            INSERT INTO process_done (
                process_id, 
                stage, 
                start_date, 
                end_date, 
                stock_id, 
                amount, 
                completed_at, 
                notes, 
                cost,
                created_by_user_id,
                active, 
                updated_at
            ) VALUES (
                @p0, @p1, @p2, @p3, @p4, @p5, @p6, @p7, @p8, @p9, @p10, @p11
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
            p0.Value = processDone.ProcessId;
            command.Parameters.Add(p0);
            
            var p1 = command.CreateParameter();
            p1.ParameterName = "@p1";
            p1.Value = processDone.Stage;
            command.Parameters.Add(p1);
            
            var p2 = command.CreateParameter();
            p2.ParameterName = "@p2";
            p2.Value = (object?)processDone.StartDate ?? DBNull.Value;
            command.Parameters.Add(p2);
            
            var p3 = command.CreateParameter();
            p3.ParameterName = "@p3";
            p3.Value = (object?)processDone.EndDate ?? DBNull.Value;
            command.Parameters.Add(p3);
            
            var p4 = command.CreateParameter();
            p4.ParameterName = "@p4";
            p4.Value = (object?)processDone.StockId ?? DBNull.Value;
            command.Parameters.Add(p4);
            
            var p5 = command.CreateParameter();
            p5.ParameterName = "@p5";
            p5.Value = processDone.Amount;
            command.Parameters.Add(p5);
            
            var p6 = command.CreateParameter();
            p6.ParameterName = "@p6";
            p6.Value = processDone.CompletedAt;
            command.Parameters.Add(p6);
            
            var p7 = command.CreateParameter();
            p7.ParameterName = "@p7";
            p7.Value = (object?)processDone.Notes ?? DBNull.Value;
            command.Parameters.Add(p7);
            
            var p8 = command.CreateParameter();
            p8.ParameterName = "@p8";
            p8.Value = processDone.Cost;
            command.Parameters.Add(p8);
            
            var p9 = command.CreateParameter();
            p9.ParameterName = "@p9";
            p9.Value = (object?)processDone.CreatedByUserId ?? DBNull.Value;
            command.Parameters.Add(p9);
            
            var p10 = command.CreateParameter();
            p10.ParameterName = "@p10";
            p10.Value = processDone.IsActive;
            command.Parameters.Add(p10);
            
            var p11 = command.CreateParameter();
            p11.ParameterName = "@p11";
            p11.Value = processDone.UpdatedAt;
            command.Parameters.Add(p11);
            
            var result = await command.ExecuteScalarAsync();
            processDone.Id = Convert.ToInt32(result);
            
            return processDone;
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

    public async Task<ProcessDone> UpdateAsync(ProcessDone processDone)
    {
        try
        {
            var connectionString = _context.Database.GetConnectionString();
            using var connection = new MySqlConnection(connectionString);
            await connection.OpenAsync();
            
            var query = @"
                UPDATE process_done 
                SET 
                    process_id = @processId,
                    stage = @stage,
                    start_date = @startDate,
                    end_date = @endDate,
                    stock_id = @stockId,
                    amount = @amount,
                    completed_at = @completedAt,
                    notes = @notes,
                    cost = @cost,
                    active = @active,
                    updated_at = @updatedAt
                WHERE id = @id";
            
            using var command = new MySqlCommand(query, connection);
            command.Parameters.AddWithValue("@id", processDone.Id);
            command.Parameters.AddWithValue("@processId", processDone.ProcessId);
            command.Parameters.AddWithValue("@stage", processDone.Stage);
            command.Parameters.AddWithValue("@startDate", (object?)processDone.StartDate ?? DBNull.Value);
            command.Parameters.AddWithValue("@endDate", (object?)processDone.EndDate ?? DBNull.Value);
            command.Parameters.AddWithValue("@stockId", (object?)processDone.StockId ?? DBNull.Value);
            command.Parameters.AddWithValue("@amount", processDone.Amount);
            command.Parameters.AddWithValue("@completedAt", processDone.CompletedAt);
            command.Parameters.AddWithValue("@notes", (object?)processDone.Notes ?? DBNull.Value);
            command.Parameters.AddWithValue("@cost", processDone.Cost);
            command.Parameters.AddWithValue("@active", processDone.IsActive);
            command.Parameters.AddWithValue("@updatedAt", DateTime.UtcNow);
            
            await command.ExecuteNonQueryAsync();
            return processDone;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error in UpdateAsync: {ex.Message}");
            throw;
        }
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
        try
        {
            var connectionString = _context.Database.GetConnectionString();
            using var connection = new MySqlConnection(connectionString);
            await connection.OpenAsync();
            
            var query = "DELETE FROM process_done WHERE id = @id";
            
            using var command = new MySqlCommand(query, connection);
            command.Parameters.AddWithValue("@id", id);
            
            await command.ExecuteNonQueryAsync();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error in DeleteAsync: {ex.Message}");
            throw;
        }
    }
}
