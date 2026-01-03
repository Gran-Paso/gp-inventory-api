using GPInventory.Application.Interfaces;
using GPInventory.Domain.Entities;
using GPInventory.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using MySqlConnector;

namespace GPInventory.Infrastructure.Repositories;

public class SupplyEntryRepository : ISupplyEntryRepository
{
    private readonly ApplicationDbContext _context;

    public SupplyEntryRepository(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<IEnumerable<SupplyEntry>> GetAllAsync()
    {
        // Use ADO.NET directly to bypass EF Core model completely
        using var command = _context.Database.GetDbConnection().CreateCommand();
        command.CommandText = @"
            SELECT id, amount, created_at, process_done_id, provider_id, supply_id, unit_cost, updated_at
            FROM supply_entry 
            ORDER BY created_at DESC";

        await _context.Database.OpenConnectionAsync();

        var results = new List<SupplyEntry>();
        using var reader = await command.ExecuteReaderAsync();

        while (await reader.ReadAsync())
        {
            var supplyEntry = new SupplyEntry
            {
                Id = reader.GetInt32(0), // id
                Amount = reader.GetInt32(1), // amount
                CreatedAt = reader.GetDateTime(2), // created_at
                ProcessDoneId = reader.IsDBNull(3) ? null : reader.GetInt32(3), // process_done_id
                ProviderId = reader.GetInt32(4), // provider_id
                SupplyId = reader.GetInt32(5), // supply_id
                UnitCost = reader.GetDecimal(6), // unit_cost - ⭐ CORREGIDO: GetDecimal para preservar decimales
                UpdatedAt = reader.GetDateTime(7) // updated_at
            };
            results.Add(supplyEntry);
        }

        return results;
    }

    public async Task<IEnumerable<SupplyEntry>> GetAllWithDetailsAsync()
    {
        // Use ADO.NET directly to bypass EF Core navigation issues
        using var command = _context.Database.GetDbConnection().CreateCommand();
        command.CommandText = @"
            SELECT id, amount, created_at, process_done_id, provider_id, supply_id, unit_cost, updated_at
            FROM supply_entry 
            ORDER BY created_at DESC";

        await _context.Database.OpenConnectionAsync();

        var results = new List<SupplyEntry>();
        using var reader = await command.ExecuteReaderAsync();

        while (await reader.ReadAsync())
        {
            var supplyEntry = new SupplyEntry
            {
                Id = reader.GetInt32(0), // id
                Amount = reader.GetInt32(1), // amount
                CreatedAt = reader.GetDateTime(2), // created_at
                ProcessDoneId = reader.IsDBNull(3) ? null : reader.GetInt32(3), // process_done_id
                ProviderId = reader.GetInt32(4), // provider_id
                SupplyId = reader.GetInt32(5), // supply_id
                UnitCost = reader.GetInt32(6), // unit_cost
                UpdatedAt = reader.GetDateTime(7) // updated_at
            };
            results.Add(supplyEntry);
        }

        return results;
    }

    public async Task<SupplyEntry?> GetByIdAsync(int id)
    {
        // Use ADO.NET directly to bypass EF Core navigation issues
        using var command = _context.Database.GetDbConnection().CreateCommand();
        command.CommandText = @"
            SELECT id, amount, created_at, process_done_id, provider_id, supply_id, unit_cost, updated_at
            FROM supply_entry 
            WHERE id = @id";

        var parameter = command.CreateParameter();
        parameter.ParameterName = "@id";
        parameter.Value = id;
        command.Parameters.Add(parameter);

        await _context.Database.OpenConnectionAsync();

        using var reader = await command.ExecuteReaderAsync();

        if (await reader.ReadAsync())
        {
            return new SupplyEntry
            {
                Id = reader.GetInt32(0), // id
                Amount = reader.GetInt32(1), // amount
                CreatedAt = reader.GetDateTime(2), // created_at
                ProcessDoneId = reader.IsDBNull(3) ? null : reader.GetInt32(3), // process_done_id
                ProviderId = reader.GetInt32(4), // provider_id
                SupplyId = reader.GetInt32(5), // supply_id
                UnitCost = reader.GetDecimal(6), // unit_cost - ⭐ CORREGIDO: GetDecimal para preservar decimales
                UpdatedAt = reader.GetDateTime(7) // updated_at
            };
        }

        return null;
    }

    public async Task<IEnumerable<SupplyEntry>> GetBySupplyIdAsync(int supplyId)
    {
        // Use ADO.NET directly to bypass EF Core navigation issues
        using var command = _context.Database.GetDbConnection().CreateCommand();
        command.CommandText = @"
            SELECT id, amount, created_at, process_done_id, provider_id, supply_id, unit_cost, updated_at
            FROM supply_entry 
            WHERE supply_id = @supplyId
            ORDER BY created_at DESC";

        var parameter = command.CreateParameter();
        parameter.ParameterName = "@supplyId";
        parameter.Value = supplyId;
        command.Parameters.Add(parameter);

        await _context.Database.OpenConnectionAsync();

        var results = new List<SupplyEntry>();
        using var reader = await command.ExecuteReaderAsync();

        while (await reader.ReadAsync())
        {
            var supplyEntry = new SupplyEntry
            {
                Id = reader.GetInt32(0), // id
                Amount = reader.GetInt32(1), // amount
                CreatedAt = reader.GetDateTime(2), // created_at
                ProcessDoneId = reader.IsDBNull(3) ? null : reader.GetInt32(3), // process_done_id
                ProviderId = reader.GetInt32(4), // provider_id
                SupplyId = reader.GetInt32(5), // supply_id
                UnitCost = reader.GetDecimal(6), // unit_cost - ⭐ CORREGIDO: GetDecimal para preservar decimales
                UpdatedAt = reader.GetDateTime(7) // updated_at
            };
            results.Add(supplyEntry);
        }

        return results;
    }

    public async Task<IEnumerable<SupplyEntry>> GetByProcessDoneIdAsync(int processDoneId)
    {
        // Use ADO.NET directly to bypass EF Core navigation issues
        using var command = _context.Database.GetDbConnection().CreateCommand();
        command.CommandText = @"
            SELECT id, amount, created_at, process_done_id, provider_id, supply_id, unit_cost, updated_at
            FROM supply_entry 
            WHERE process_done_id = @processDoneId
            ORDER BY created_at DESC";

        var parameter = command.CreateParameter();
        parameter.ParameterName = "@processDoneId";
        parameter.Value = processDoneId;
        command.Parameters.Add(parameter);

        await _context.Database.OpenConnectionAsync();

        var results = new List<SupplyEntry>();
        using var reader = await command.ExecuteReaderAsync();

        while (await reader.ReadAsync())
        {
            var supplyEntry = new SupplyEntry
            {
                Id = reader.GetInt32(0), // id
                Amount = reader.GetInt32(1), // amount
                CreatedAt = reader.GetDateTime(2), // created_at
                ProcessDoneId = reader.IsDBNull(3) ? null : reader.GetInt32(3), // process_done_id
                ProviderId = reader.GetInt32(4), // provider_id
                SupplyId = reader.GetInt32(5), // supply_id
                UnitCost = reader.GetDecimal(6), // unit_cost - ⭐ CORREGIDO: GetDecimal para preservar decimales
                UpdatedAt = reader.GetDateTime(7) // updated_at
            };
            results.Add(supplyEntry);
        }

        return results;
    }

    public async Task<decimal> GetCurrentStockAsync(int supplyId)
    {
        using var connection = _context.Database.GetDbConnection();
        await connection.OpenAsync();

        using var command = connection.CreateCommand();
        command.CommandText = @"
            SELECT 
                COALESCE(SUM(CASE WHEN process_done_id IS NULL THEN amount ELSE 0 END), 0) as total_incoming,
                COALESCE(SUM(CASE WHEN process_done_id IS NOT NULL THEN amount ELSE 0 END), 0) as total_outgoing
            FROM supply_entry 
            WHERE supply_id = @supplyId";

        var parameter = command.CreateParameter();
        parameter.ParameterName = "@supplyId";
        parameter.Value = supplyId;
        command.Parameters.Add(parameter);

        using var reader = await command.ExecuteReaderAsync();
        if (await reader.ReadAsync())
        {
            var totalIncoming = reader.GetInt32(0); // total_incoming
            var totalOutgoing = reader.GetInt32(1); // total_outgoing (ya incluye valores negativos)
            return totalIncoming + totalOutgoing; // sumar porque totalOutgoing ya es negativo
        }

        return 0;
    }

    public async Task<SupplyEntry> CreateAsync(SupplyEntry supplyEntry)
    {
        // Use ADO.NET to avoid EF Core auto-mapping issues
        var connectionString = _context.Database.GetConnectionString();
        using var connection = new MySqlConnection(connectionString);
        await connection.OpenAsync();

        var query = @"
            INSERT INTO supply_entry (unit_cost, amount, provider_id, supply_id, process_done_id, supply_entry_id, active, created_at, updated_at)
            VALUES (@unitCost, @amount, @providerId, @supplyId, @processDoneId, @supplyEntryId, @active, @createdAt, @updatedAt);
            SELECT LAST_INSERT_ID();";

        using var command = new MySqlCommand(query, connection);

        var now = DateTime.UtcNow;
        command.Parameters.AddWithValue("@unitCost", supplyEntry.UnitCost);
        command.Parameters.AddWithValue("@amount", supplyEntry.Amount);
        command.Parameters.AddWithValue("@providerId", supplyEntry.ProviderId);
        command.Parameters.AddWithValue("@supplyId", supplyEntry.SupplyId);
        command.Parameters.AddWithValue("@processDoneId", supplyEntry.ProcessDoneId.HasValue ? supplyEntry.ProcessDoneId.Value : DBNull.Value);
        command.Parameters.AddWithValue("@supplyEntryId", supplyEntry.ReferenceToSupplyEntry.HasValue ? supplyEntry.ReferenceToSupplyEntry.Value : DBNull.Value); // ⭐ AUTOREFERENCIA
        // Usar IsActive directamente de la entidad (ya configurado por el constructor)
        command.Parameters.AddWithValue("@active", supplyEntry.IsActive);
        command.Parameters.AddWithValue("@createdAt", now);
        command.Parameters.AddWithValue("@updatedAt", now);

        var newId = await command.ExecuteScalarAsync();

        // Return the created entity with updated values
        supplyEntry.Id = Convert.ToInt32(newId);
        supplyEntry.CreatedAt = now;
        supplyEntry.UpdatedAt = now;

        return supplyEntry;
    }

    public async Task<SupplyEntry> UpdateAsync(SupplyEntry supplyEntry)
    {
        // Use ADO.NET to avoid EF Core auto-mapping issues
        var connectionString = _context.Database.GetConnectionString();
        using var connection = new MySqlConnection(connectionString);
        await connection.OpenAsync();

        var query = @"
            UPDATE supply_entry 
            SET unit_cost = @unitCost, 
                amount = @amount, 
                provider_id = @providerId, 
                supply_id = @supplyId, 
                process_done_id = @processDoneId, 
                supply_entry_id = @supplyEntryId,
                active = @active,
                updated_at = @updatedAt
            WHERE id = @id";

        using var command = new MySqlCommand(query, connection);

        var now = DateTime.UtcNow;
        command.Parameters.AddWithValue("@id", supplyEntry.Id);
        command.Parameters.AddWithValue("@unitCost", supplyEntry.UnitCost);
        command.Parameters.AddWithValue("@amount", supplyEntry.Amount);
        command.Parameters.AddWithValue("@providerId", supplyEntry.ProviderId);
        command.Parameters.AddWithValue("@supplyId", supplyEntry.SupplyId);
        command.Parameters.AddWithValue("@processDoneId", supplyEntry.ProcessDoneId.HasValue ? supplyEntry.ProcessDoneId.Value : DBNull.Value);
        command.Parameters.AddWithValue("@supplyEntryId", supplyEntry.ReferenceToSupplyEntry.HasValue ? supplyEntry.ReferenceToSupplyEntry.Value : DBNull.Value); // ⭐ AUTOREFERENCIA
        command.Parameters.AddWithValue("@active", supplyEntry.IsActive); // Usar IsActive de la entidad
        command.Parameters.AddWithValue("@updatedAt", now);

        await command.ExecuteNonQueryAsync();

        supplyEntry.UpdatedAt = now;
        return supplyEntry;
    }

    public async Task DeleteAsync(int id)
    {
        // Use ADO.NET to avoid EF Core auto-mapping issues
        var connectionString = _context.Database.GetConnectionString();
        using var connection = new MySqlConnection(connectionString);
        await connection.OpenAsync();

        var query = "DELETE FROM supply_entry WHERE id = @id";
        using var command = new MySqlCommand(query, connection);
        command.Parameters.AddWithValue("@id", id);

        await command.ExecuteNonQueryAsync();
    }

    public async Task<IEnumerable<SupplyEntry>> GetSupplyHistoryAsync(int? supplyEntryId, int supplyId)
    {
        var supplyEntries = new List<SupplyEntry>();

        using var connection = _context.Database.GetDbConnection();
        await connection.OpenAsync();

        using var command = connection.CreateCommand();

        if (supplyEntryId == null || supplyEntryId == 0)
        {
            command.CommandText = @"
            SELECT 
                se.id, 
                se.supply_id, 
                se.unit_cost, 
                se.amount, 
                se.provider_id, 
                se.process_done_id, 
                se.created_at, 
                se.updated_at,
                se.supply_entry_id,
                se.active,
                s.name as supply_name,
                um.name as unit_measure_name,
                p.name as provider_name,
                pd.id as process_done_id_full,
                pd.process_id as process_id,
                pd.notes as process_notes,
                pd.completed_at as process_completed_at
            FROM supply_entry se 
            LEFT JOIN supplies s ON se.supply_id = s.id
            LEFT JOIN unit_measures um ON s.unit_measure_id = um.id
            LEFT JOIN provider p ON se.provider_id = p.id
            LEFT JOIN process_done pd ON se.process_done_id = pd.id
            WHERE se.supply_id = @supplyId
            ORDER BY se.created_at DESC";
        }
        else
        {
            command.CommandText = @"
            SELECT 
                se.id, 
                se.supply_id, 
                se.unit_cost, 
                se.amount, 
                se.provider_id, 
                se.process_done_id, 
                se.created_at, 
                se.updated_at,
                se.supply_entry_id,
                se.active,
                s.name as supply_name,
                um.name as unit_measure_name,
                p.name as provider_name,
                pd.id as process_done_id_full,
                pd.process_id as process_id,
                pd.notes as process_notes,
                pd.completed_at as process_completed_at
            FROM supply_entry se 
            LEFT JOIN supplies s ON se.supply_id = s.id
            LEFT JOIN unit_measures um ON s.unit_measure_id = um.id
            LEFT JOIN providers p ON se.provider_id = p.id
            LEFT JOIN process_done pd ON se.process_done_id = pd.id
            WHERE se.supply_entry_id = @supplyEntryId OR se.supply_id = @supplyId
            ORDER BY se.created_at DESC";
        }

        var parameter = command.CreateParameter();
        parameter.ParameterName = "@supplyId";
        parameter.Value = supplyId;
        command.Parameters.Add(parameter);

        var parameter2 = command.CreateParameter();
        parameter2.ParameterName = "@supplyEntryId";
        parameter2.Value = supplyEntryId;
        command.Parameters.Add(parameter2);

        using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            var supplyEntry = new SupplyEntry
            {
                Id = reader.GetInt32(0), // id
                SupplyId = reader.GetInt32(1), // supply_id
                UnitCost = reader.GetDecimal(2), // unit_cost (decimal)
                Amount = reader.GetInt32(3), // amount (int)
                ProviderId = reader.GetInt32(4), // provider_id
                ProcessDoneId = reader.IsDBNull(5) ? (int?)null : reader.GetInt32(5), // process_done_id
                CreatedAt = reader.GetDateTime(6), // created_at
                UpdatedAt = reader.GetDateTime(7), // updated_at
                ReferenceToSupplyEntry = reader.IsDBNull(8) ? (int?)null : reader.GetInt32(8), // supply_entry_id
                IsActive = !reader.IsDBNull(9) && reader.GetBoolean(9) // active
            };

            // Agregar información de Supply
            if (!reader.IsDBNull(10))
            {
                supplyEntry.Supply = new Supply
                {
                    Id = supplyEntry.SupplyId,
                    Name = reader.GetString(10), // supply_name
                    UnitMeasure = !reader.IsDBNull(11) ? new UnitMeasure
                    {
                        Name = reader.GetString(11) // unit_measure_name
                    } : null
                };
            }

            // Agregar información de Provider
            if (!reader.IsDBNull(12))
            {
                supplyEntry.Provider = new Provider
                {
                    Id = supplyEntry.ProviderId,
                    Name = reader.GetString(12) // provider_name
                };
            }

            // Si hay un process_done_id, crear el ProcessDone con la información disponible
            if (!reader.IsDBNull(13))
            {
                supplyEntry.ProcessDone = new ProcessDone
                {
                    Id = reader.GetInt32(13), // process_done_id_full
                    ProcessId = reader.IsDBNull(14) ? 0 : reader.GetInt32(14), // process_id
                    Notes = reader.IsDBNull(15) ? null : reader.GetString(15), // process_notes
                    CompletedAt = reader.IsDBNull(16) ? DateTime.MinValue : reader.GetDateTime(16) // process_completed_at
                };
            }

            supplyEntries.Add(supplyEntry);
        }

        return supplyEntries;
    }

    public async Task<SupplyEntry?> GetFirstEntryBySupplyIdAsync(int supplyId)
    {
        using var connection = _context.Database.GetDbConnection();
        await connection.OpenAsync();

        using var command = connection.CreateCommand();
        command.CommandText = @"
            SELECT id, amount, created_at, process_done_id, provider_id, supply_id, unit_cost, updated_at, active
            FROM supply_entry 
            WHERE supply_id = @supplyId and process_done_id IS NULL and amount > 0 and active = 1
            ORDER BY created_at ASC
            LIMIT 1";

        var parameter = command.CreateParameter();
        parameter.ParameterName = "@supplyId";
        parameter.Value = supplyId;
        command.Parameters.Add(parameter);

        using var reader = await command.ExecuteReaderAsync();
        if (await reader.ReadAsync())
        {
            SupplyEntry entry = new SupplyEntry()
            {
                Id = reader.GetInt32(0),
                Amount = reader.GetInt32(1),
                CreatedAt = reader.GetDateTime(2),
                ProcessDoneId = reader.IsDBNull(3) ? (int?)null : reader.GetInt32(3),
                ProviderId = reader.GetInt32(4),
                SupplyId = reader.GetInt32(5),
                UnitCost = reader.GetDecimal(6),
                UpdatedAt = reader.GetDateTime(7),
                IsActive = reader.GetBoolean(8)
            };

            // Obtener todos los supply entries asociados al mismo supply
            await connection.CloseAsync();
            var allRelatedEntries = await GetSupplyHistoryAsync((int?)entry.Id, supplyId);
            
            // Restar el amount del entry principal de los relatedEntries
            var remainingAmount = entry.Amount;
            foreach (var relatedEntry in allRelatedEntries.Where(e => e.Id != entry.Id && e.Amount < 0))
            {
                var consumedAmount = Math.Abs(relatedEntry.Amount);
                remainingAmount -= consumedAmount;
            }
            
            // Actualizar el amount del entry con lo que queda después de las restas
            entry.Amount = remainingAmount;
            
            return entry;
            
        }

        return null;

    }

    public async Task<IEnumerable<SupplyEntry>> GetAvailableEntriesBySupplyIdAsync(int supplyId)
    {
        // Consulta simplificada: solo obtener entradas activas con stock positivo
        // ya que la lógica de negocio marca como active=false las entradas vacías
        var rawSql = @"
            SELECT id, amount, created_at, process_done_id, provider_id, supply_id, unit_cost, updated_at, active
            FROM supply_entry 
            WHERE supply_id = {0}
              AND process_done_id IS NULL 
              AND amount > 0 
              AND active = 1
            ORDER BY created_at ASC";

        try
        {
            // Usar FromSqlRaw con Entity Framework
            var stockEntries = await _context.Database
                .SqlQueryRaw<SupplyEntryRawData>(rawSql, supplyId)
                .ToListAsync();

            // Convertir a SupplyEntry
            var availableEntries = stockEntries.Select(se => new SupplyEntry
            {
                Id = se.id,
                Amount = se.amount,
                UnitCost = se.unit_cost,
                SupplyId = se.supply_id,
                ProviderId = se.provider_id,
                ProcessDoneId = se.process_done_id,
                CreatedAt = se.created_at,
                UpdatedAt = se.updated_at,
                IsActive = se.active == 1
            }).ToList();

            return availableEntries;
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Error getting available entries for supply {supplyId}: {ex.Message}", ex);
        }
    }

    // Clase helper para mapear resultados SQL crudos
    private class SupplyEntryRawData
    {
        public int id { get; set; }
        public int amount { get; set; }
        public DateTime created_at { get; set; }
        public int? process_done_id { get; set; }
        public int provider_id { get; set; }
        public int supply_id { get; set; }
        public decimal unit_cost { get; set; }
        public DateTime updated_at { get; set; }
        public int active { get; set; }
    }

    // Clase helper para mapear resultado de suma consumida
    private class ConsumedAmountResult
    {
        public int Value { get; set; }
    }
}
