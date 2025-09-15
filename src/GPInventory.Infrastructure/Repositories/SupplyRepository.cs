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
        return await _context.Supplies
            .FirstOrDefaultAsync(s => s.Id == id);
    }

    public async Task<Supply?> GetByIdWithDetailsAsync(int id)
    {
        // Get the basic supply data using Entity Framework
        var supply = await _context.Supplies.FirstOrDefaultAsync(s => s.Id == id);
        
        if (supply == null)
            return null;

        // Manually load related entities to avoid EF mapping issues
        supply.UnitMeasure = await _context.UnitMeasures
            .FirstOrDefaultAsync(um => um.Id == supply.UnitMeasureId) ?? new UnitMeasure();

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
            using var command = _context.Database.GetDbConnection().CreateCommand();
            command.CommandText = @"
                SELECT se.id as Id, se.unit_cost as UnitCost, se.amount as Amount, se.provider_id as ProviderId, se.supply_id as SupplyId, 
                       se.process_done_id as ProcessDoneId, se.created_at as CreatedAt, se.updated_at as UpdatedAt
                FROM supply_entry se
                LEFT JOIN process_done pd ON se.process_done_id = pd.id
                WHERE se.supply_id = @supplyId 
                  AND se.active = 1 
                  AND (se.process_done_id IS NULL OR pd.active = 1)";

            var parameter = command.CreateParameter();
            parameter.ParameterName = "@supplyId";
            parameter.Value = id;
            command.Parameters.Add(parameter);

            await _context.Database.OpenConnectionAsync();
            
            using var reader = await command.ExecuteReaderAsync();
            
            while (await reader.ReadAsync())
            {
                var amount = Convert.ToInt32(reader.GetValue(2)); // se.Amount
                totalStock += amount; // Sum all amounts to calculate total stock
                
                var supplyEntry = new SupplyEntry
                {
                    Id = reader.GetInt32(0), // se.Id
                    UnitCost = Convert.ToDecimal(reader.GetValue(1)), // se.UnitCost - safe conversion
                    Amount = amount, // se.Amount - safe conversion
                    ProviderId = reader.GetInt32(3), // se.ProviderId
                    SupplyId = reader.GetInt32(4), // se.SupplyId
                    ProcessDoneId = reader.IsDBNull(5) ? null : reader.GetInt32(5), // se.ProcessDoneId
                    CreatedAt = reader.IsDBNull(6) ? DateTime.UtcNow : reader.GetDateTime(6), // se.CreatedAt
                    UpdatedAt = reader.IsDBNull(7) ? DateTime.UtcNow : reader.GetDateTime(7) // se.UpdatedAt
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
        var supplies = await _context.Supplies
            .Where(s => s.BusinessId == businessId)
            .ToListAsync();

        // Manually populate the UnitMeasure navigation property
        foreach (var supply in supplies)
        {
            supply.UnitMeasure = await _context.UnitMeasures
                .FirstOrDefaultAsync(um => um.Id == supply.UnitMeasureId) ?? new UnitMeasure();
        }

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
        _context.Supplies.Update(entity);
        await _context.SaveChangesAsync();
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
        return await _context.Supplies
            .FirstOrDefaultAsync(s => s.Name == name && s.BusinessId == businessId);
    }
}
