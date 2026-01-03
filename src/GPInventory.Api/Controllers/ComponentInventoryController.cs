using GPInventory.Application.Interfaces;
using GPInventory.Domain.Entities;
using GPInventory.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace GPInventory.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class ComponentInventoryController : ControllerBase
{
    private readonly IComponentProductionRepository _componentProductionRepository;
    private readonly IComponentRepository _componentRepository;
    private readonly ISupplyEntryRepository _supplyEntryRepository;
    private readonly ApplicationDbContext _context;
    private readonly ILogger<ComponentInventoryController> _logger;

    public ComponentInventoryController(
        IComponentProductionRepository componentProductionRepository,
        IComponentRepository componentRepository,
        ISupplyEntryRepository supplyEntryRepository,
        ApplicationDbContext context,
        ILogger<ComponentInventoryController> logger)
    {
        _componentProductionRepository = componentProductionRepository;
        _componentRepository = componentRepository;
        _supplyEntryRepository = supplyEntryRepository;
        _context = context;
        _logger = logger;
    }

    /// <summary>
    /// Obtener el stock actual de un componente
    /// </summary>
    [HttpGet("{componentId}")]
    public async Task<ActionResult<ComponentInventoryDto>> GetComponentInventory(int componentId)
    {
        try
        {
            // Verificar que el componente existe
            var component = await _componentRepository.GetByIdAsync(componentId);
            if (component == null)
            {
                return NotFound(new { message = "Componente no encontrado" });
            }

            var currentStock = await _componentProductionRepository.GetCurrentStockAsync(componentId);
            
            return Ok(new ComponentInventoryDto
            {
                Id = componentId,
                ComponentId = componentId,
                CurrentStock = currentStock,
                ComponentName = component.Name
            });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { message = "Error al obtener el inventario del componente", error = ex.Message });
        }
    }

    /// <summary>
    /// Obtener todas las producciones de un componente
    /// </summary>
    [HttpGet("{componentId}/productions")]
    public async Task<ActionResult<IEnumerable<ComponentProduction>>> GetComponentProductions(int componentId)
    {
        try
        {
            var productions = await _componentProductionRepository.GetByComponentIdAsync(componentId);
            return Ok(productions);
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { message = "Error al obtener las producciones del componente", error = ex.Message });
        }
    }

    /// <summary>
    /// Registrar una nueva producción de componente
    /// </summary>
    [HttpPost("production")]
    public async Task<ActionResult<ComponentProduction>> CreateProduction([FromBody] CreateComponentProductionDto dto)
    {
        try
        {
            // Obtener el componente con sus ingredientes
            var component = await _componentRepository.GetByIdWithSuppliesAsync(dto.ComponentId);
            if (component == null)
            {
                return NotFound(new { message = "Componente no encontrado" });
            }

            // Crear la producción del componente (cantidad positiva)
            var production = new ComponentProduction
            {
                ComponentId = dto.ComponentId,
                ProcessDoneId = dto.ProcessDoneId,
                BusinessId = dto.BusinessId,
                StoreId = dto.StoreId,
                ProducedAmount = dto.ProducedAmount,
                ProductionDate = dto.ProductionDate ?? DateTime.Now,
                ExpirationDate = dto.ExpirationDate,
                BatchNumber = dto.BatchNumber,
                Cost = dto.Cost ?? 0,
                Notes = dto.Notes,
                IsActive = true,
                CreatedAt = DateTime.Now
            };

            var created = await _componentProductionRepository.CreateAsync(production);

            // Procesar consumo de ingredientes
            if (dto.IngredientConsumptions != null && dto.IngredientConsumptions.Any())
            {
                // Usar las cantidades proporcionadas por el usuario
                foreach (var consumption in dto.IngredientConsumptions)
                {
                    if (consumption.ItemType == "supply")
                    {
                        // Consumir insumo usando FIFO (entrada negativa con referencia al padre)
                        await ConsumeSupplyWithFIFOAsync(consumption.ItemId, consumption.Quantity);
                    }
                    else if (consumption.ItemType == "component")
                    {
                        // Consumir sub-componente (producción negativa)
                        var subComponentConsumption = new ComponentProduction
                        {
                            ComponentId = consumption.ItemId,
                            ProcessDoneId = null,
                            BusinessId = dto.BusinessId,
                            StoreId = dto.StoreId,
                            ProducedAmount = -consumption.Quantity, // Cantidad negativa
                            ProductionDate = DateTime.Now,
                            Notes = $"Consumo por producción de componente: {component.Name}",
                            IsActive = true,
                            CreatedAt = DateTime.Now
                        };

                        await _componentProductionRepository.CreateAsync(subComponentConsumption);
                    }
                }
            }
            else if (component.Supplies != null && component.Supplies.Any())
            {
                // Fallback: usar las cantidades de la receta si no se proporcionan cantidades personalizadas
                foreach (var ingredient in component.Supplies)
                {
                    var quantityToConsume = ingredient.Quantity * dto.ProducedAmount;

                    if (ingredient.ItemType == "supply" && ingredient.SupplyId.HasValue)
                    {
                        // Consumir insumo usando FIFO (entrada negativa con referencia al padre)
                        await ConsumeSupplyWithFIFOAsync(ingredient.SupplyId.Value, quantityToConsume);
                    }
                    else if (ingredient.ItemType == "component" && ingredient.SubComponentId.HasValue)
                    {
                        // Consumir sub-componente (producción negativa)
                        var subComponentConsumption = new ComponentProduction
                        {
                            ComponentId = ingredient.SubComponentId.Value,
                            ProcessDoneId = null,
                            BusinessId = dto.BusinessId,
                            StoreId = dto.StoreId,
                            ProducedAmount = -quantityToConsume, // Cantidad negativa
                            ProductionDate = DateTime.Now,
                            Notes = $"Consumo por producción de componente: {component.Name}",
                            IsActive = true,
                            CreatedAt = DateTime.Now
                        };

                        await _componentProductionRepository.CreateAsync(subComponentConsumption);
                    }
                }
            }

            return CreatedAtAction(nameof(GetComponentInventory), new { componentId = created.ComponentId }, created);
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { message = "Error al registrar la producción", error = ex.Message });
        }
    }

    /// <summary>
    /// Obtener todas las producciones de componentes
    /// </summary>
    [HttpGet("productions")]
    public async Task<ActionResult<IEnumerable<ComponentProduction>>> GetAllProductions()
    {
        try
        {
            var productions = await _componentProductionRepository.GetAllAsync();
            return Ok(productions);
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { message = "Error al obtener las producciones", error = ex.Message });
        }
    }

    /// <summary>
    /// Consume insumos usando algoritmo FIFO con autoreferencia
    /// </summary>
    private async Task ConsumeSupplyWithFIFOAsync(int supplyId, decimal quantityToConsume)
    {
        var remainingQuantity = quantityToConsume;
        
        // Obtener todos los supply_entry disponibles para este insumo (FIFO)
        var availableEntries = await _supplyEntryRepository.GetAvailableEntriesBySupplyIdAsync(supplyId);
        
        if (!availableEntries.Any())
            throw new InvalidOperationException($"No hay stock disponible para el insumo ID {supplyId}");
        
        var entriesToUpdate = new List<SupplyEntry>(); // Entradas que necesitan actualización de active
        
        foreach (var availableEntry in availableEntries)
        {
            if (remainingQuantity <= 0) break;
            
            // Determinar cuánto consumir de esta entrada
            var consumeFromThisEntry = Math.Min(remainingQuantity, availableEntry.Amount);
            
            // Crear supply_entry negativo con referencia al stock original usando el constructor correcto
            var supplyEntry = new SupplyEntry(
                availableEntry.UnitCost,           // Usar el costo del stock original
                -(int)consumeFromThisEntry,        // Cantidad negativa
                availableEntry.ProviderId,         // Usar el mismo proveedor
                supplyId,                          // SupplyId
                null,                              // ProcessDoneId (null para producción de componentes)
                availableEntry.Id                  // ⭐ Referencia al stock original (supply_entry_id)
            );
            
            await _supplyEntryRepository.CreateAsync(supplyEntry);
            
            // Si esta entrada se queda completamente vacía, marcarla para desactivar
            var remainingInEntry = availableEntry.Amount - consumeFromThisEntry;
            if (remainingInEntry == 0)
            {
                // Obtener la entrada original para actualizar su estado
                var originalEntry = await _supplyEntryRepository.GetByIdAsync(availableEntry.Id);
                if (originalEntry != null)
                {
                    originalEntry.IsActive = false; // Marcar como inactiva
                    entriesToUpdate.Add(originalEntry);
                }
            }
            
            // Reducir la cantidad pendiente
            remainingQuantity -= consumeFromThisEntry;
        }
        
        // Actualizar todas las entradas que se quedaron vacías
        foreach (var entry in entriesToUpdate)
        {
            await _supplyEntryRepository.UpdateAsync(entry);
        }
        
        // Verificar que se pudo consumir toda la cantidad necesaria
        if (remainingQuantity > 0)
            throw new InvalidOperationException(
                $"Stock insuficiente para el insumo ID {supplyId}. " +
                $"Faltan {remainingQuantity} unidades"
            );
    }

    /// <summary>
    /// Remover stock de una producción de componente específica (agregar registro negativo con FIFO)
    /// </summary>
    [HttpPost("production/{productionId}/remove")]
    public async Task<ActionResult<object>> RemoveStockFromProduction(int productionId, [FromBody] RemoveComponentStockRequest request)
    {
        try
        {
            _logger.LogInformation("🔄 Removiendo {amount} unidades de la producción de componente {productionId}", request.Amount, productionId);

            await _context.Database.OpenConnectionAsync();
            
            try
            {
                using var connection = _context.Database.GetDbConnection();
                
                // Verificar que la producción existe y calcular el stock disponible real
                var productionQuery = @"
                    SELECT 
                        cp.id,
                        cp.produced_amount,
                        cp.component_id,
                        cp.business_id,
                        cp.store_id,
                        cp.is_active
                    FROM component_production cp
                    WHERE cp.id = @productionId 
                    AND cp.produced_amount > 0 
                    AND cp.is_active = 1
                    AND cp.component_production_id IS NULL";

                using var cmd = connection.CreateCommand();
                cmd.CommandText = productionQuery;
                var productionIdParam = cmd.CreateParameter();
                productionIdParam.ParameterName = "@productionId";
                productionIdParam.Value = productionId;
                cmd.Parameters.Add(productionIdParam);

                using var reader = await cmd.ExecuteReaderAsync();
                
                if (!await reader.ReadAsync())
                {
                    await reader.CloseAsync();
                    return BadRequest(new { message = "Producción no encontrada o sin stock disponible" });
                }

                var originalAmount = reader.GetInt32(1);
                var componentId = reader.GetInt32(2);
                var businessId = reader.GetInt32(3);
                var storeId = reader.GetInt32(4);

                await reader.CloseAsync();

                // Calcular cuánto se ha removido de esta producción (registros negativos)
                var removalsQuery = @"
                    SELECT COALESCE(SUM(ABS(produced_amount)), 0) as total_removed
                    FROM component_production
                    WHERE component_production_id = @productionId 
                    AND produced_amount < 0
                    AND is_active = 1";

                using var removalCmd = connection.CreateCommand();
                removalCmd.CommandText = removalsQuery;
                var removalProductionIdParam = removalCmd.CreateParameter();
                removalProductionIdParam.ParameterName = "@productionId";
                removalProductionIdParam.Value = productionId;
                removalCmd.Parameters.Add(removalProductionIdParam);

                var removedAmount = Convert.ToInt32(await removalCmd.ExecuteScalarAsync());

                // Calcular el stock disponible real
                var availableInProduction = originalAmount - removedAmount;

                _logger.LogInformation("📦 Producción {productionId} - Original: {original}, Removido: {removed}, Disponible: {available}", 
                    productionId, originalAmount, removedAmount, availableInProduction);

                if (availableInProduction < request.Amount)
                {
                    return BadRequest(new { message = $"Stock insuficiente. Disponible: {availableInProduction}, Solicitado: {request.Amount}" });
                }

                // Crear registro negativo vinculado a la producción original
                var removeStockQuery = @"
                    INSERT INTO component_production (
                        component_id, 
                        business_id, 
                        store_id, 
                        produced_amount, 
                        component_production_id, 
                        is_active, 
                        production_date, 
                        created_at, 
                        updated_at,
                        notes
                    )
                    VALUES (
                        @componentId, 
                        @businessId, 
                        @storeId, 
                        @amount, 
                        @componentProductionId, 
                        1, 
                        NOW(), 
                        NOW(), 
                        NOW(),
                        @notes
                    )";

                using var insertCmd = connection.CreateCommand();
                insertCmd.CommandText = removeStockQuery;
                
                var amountParam = insertCmd.CreateParameter();
                amountParam.ParameterName = "@amount";
                amountParam.Value = -request.Amount;
                insertCmd.Parameters.Add(amountParam);

                var componentIdParam = insertCmd.CreateParameter();
                componentIdParam.ParameterName = "@componentId";
                componentIdParam.Value = componentId;
                insertCmd.Parameters.Add(componentIdParam);

                var businessIdParam = insertCmd.CreateParameter();
                businessIdParam.ParameterName = "@businessId";
                businessIdParam.Value = businessId;
                insertCmd.Parameters.Add(businessIdParam);

                var storeIdParam = insertCmd.CreateParameter();
                storeIdParam.ParameterName = "@storeId";
                storeIdParam.Value = storeId;
                insertCmd.Parameters.Add(storeIdParam);

                var componentProductionIdParam = insertCmd.CreateParameter();
                componentProductionIdParam.ParameterName = "@componentProductionId";
                componentProductionIdParam.Value = productionId;
                insertCmd.Parameters.Add(componentProductionIdParam);

                var notesParam = insertCmd.CreateParameter();
                notesParam.ParameterName = "@notes";
                notesParam.Value = request.Notes ?? "Stock removido manualmente";
                insertCmd.Parameters.Add(notesParam);

                await insertCmd.ExecuteNonQueryAsync();

                _logger.LogInformation("✅ Registro negativo creado para la producción {productionId}", productionId);

                // Si la producción se queda vacía, desactivarla
                if (availableInProduction == request.Amount)
                {
                    var deactivateQuery = @"
                        UPDATE component_production 
                        SET is_active = 0, updated_at = NOW()
                        WHERE id = @productionId";

                    using var deactivateCmd = connection.CreateCommand();
                    deactivateCmd.CommandText = deactivateQuery;
                    var deactivateIdParam = deactivateCmd.CreateParameter();
                    deactivateIdParam.ParameterName = "@productionId";
                    deactivateIdParam.Value = productionId;
                    deactivateCmd.Parameters.Add(deactivateIdParam);

                    await deactivateCmd.ExecuteNonQueryAsync();

                    _logger.LogInformation("🔒 Producción {productionId} desactivada (stock agotado)", productionId);
                }

                await _context.Database.CloseConnectionAsync();

                return Ok(new
                {
                    message = "Stock removido exitosamente",
                    removedAmount = request.Amount,
                    productionId = productionId
                });
            }
            catch (Exception ex)
            {
                await _context.Database.CloseConnectionAsync();
                _logger.LogError(ex, "❌ Error removiendo stock de la producción {productionId}", productionId);
                throw;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Error removiendo stock de la producción {productionId}", productionId);
            return StatusCode(500, new { message = "Error al remover stock", error = ex.Message });
        }
    }
}

// DTOs
public class ComponentInventoryDto
{
    public int Id { get; set; }
    public int ComponentId { get; set; }
    public decimal CurrentStock { get; set; }
    public string? ComponentName { get; set; }
}

public class CreateComponentProductionDto
{
    public int ComponentId { get; set; }
    public int? ProcessDoneId { get; set; }
    public int BusinessId { get; set; }
    public int StoreId { get; set; }
    public decimal ProducedAmount { get; set; }
    public DateTime? ProductionDate { get; set; }
    public DateTime? ExpirationDate { get; set; }
    public string? BatchNumber { get; set; }
    public decimal? Cost { get; set; }
    public string? Notes { get; set; }
    public List<IngredientConsumptionDto>? IngredientConsumptions { get; set; }
}

public class IngredientConsumptionDto
{
    public string ItemType { get; set; } = "supply"; // 'supply' | 'component'
    public int ItemId { get; set; }
    public decimal Quantity { get; set; }
}

public class RemoveComponentStockRequest
{
    public int Amount { get; set; }
    public string? Notes { get; set; }
}
