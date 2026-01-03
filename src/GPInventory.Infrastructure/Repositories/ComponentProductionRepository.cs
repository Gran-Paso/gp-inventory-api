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

        await _context.Database.OpenConnectionAsync();
        
        try
        {
            using var connection = _context.Database.GetDbConnection();
            using var command = connection.CreateCommand();
            
            command.CommandText = sql;
            
            var parameter = command.CreateParameter();
            parameter.ParameterName = "@p0";
            parameter.Value = componentId;
            command.Parameters.Add(parameter);
            
            var result = await command.ExecuteScalarAsync();
            
            await _context.Database.CloseConnectionAsync();
            
            return result != null ? Convert.ToDecimal(result) : 0;
        }
        catch
        {
            await _context.Database.CloseConnectionAsync();
            throw;
        }
    }

    public async Task<IEnumerable<ComponentProduction>> GetByProcessDoneIdAsync(int processDoneId)
    {
        return await _context.ComponentProductions
            .Where(cp => cp.ProcessDoneId == processDoneId && cp.IsActive)
            .ToListAsync();
    }

    public async Task<IEnumerable<ComponentProduction>> GetAllAsync()
    {
        return await _context.ComponentProductions
            .Include(cp => cp.Component)
            .Where(cp => cp.IsActive)
            .OrderByDescending(cp => cp.CreatedAt)
            .ToListAsync();
    }
}
