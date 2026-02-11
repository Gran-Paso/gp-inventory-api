using GPInventory.Application.DTOs.Components;
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
    private readonly IUserRepository _userRepository;
    private readonly ApplicationDbContext _context;
    private readonly ILogger<ComponentInventoryController> _logger;

    public ComponentInventoryController(
        IComponentProductionRepository componentProductionRepository,
        IComponentRepository componentRepository,
        ISupplyEntryRepository supplyEntryRepository,
        IUserRepository userRepository,
        ApplicationDbContext context,
        ILogger<ComponentInventoryController> logger)
    {
        _componentProductionRepository = componentProductionRepository;
        _componentRepository = componentRepository;
        _supplyEntryRepository = supplyEntryRepository;
        _userRepository = userRepository;
        _context = context;
        _logger = logger;
    }

    private int? GetUserIdFromClaims()
    {
        var userIdClaim = User.FindFirst("sub")
            ?? User.FindFirst("user_id")
            ?? User.FindFirst("userId")
            ?? User.FindFirst("id")
            ?? User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier);

        if (userIdClaim != null && int.TryParse(userIdClaim.Value, out int userId))
        {
            return userId;
        }
        return null;
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

            var (currentStock, totalValue) = await _componentProductionRepository.GetStockAndValueAsync(componentId);

            return Ok(new ComponentInventoryDto
            {
                Id = componentId,
                ComponentId = componentId,
                CurrentStock = currentStock,
                TotalValue = totalValue,
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
    public async Task<ActionResult<IEnumerable<ComponentProductionDto>>> GetComponentProductions(int componentId)
    {
        try
        {
            // Obtener el componente para tener la unidad de medida
            var component = await _componentRepository.GetByIdAsync(componentId);
            if (component == null)
            {
                return NotFound(new { message = "Componente no encontrado" });
            }

            var productions = (await _componentProductionRepository.GetByComponentIdAsync(componentId)).ToList();

            // Load user names for all productions
            var userIds = productions
                .Where(p => p.CreatedByUserId.HasValue)
                .Select(p => p.CreatedByUserId!.Value)
                .Distinct()
                .ToList();

            var users = await _userRepository.GetUserNamesByIdsAsync(userIds);

            // Map to DTOs with user names and component info
            var dtos = productions.Select(p => new ComponentProductionDto
            {
                Id = p.Id,
                ComponentId = p.ComponentId,
                ComponentName = component.Name,
                ComponentUnitMeasureSymbol = component.UnitMeasureSymbol,
                ProducedAmount = p.ProducedAmount,
                ProductionDate = p.ProductionDate ?? DateTime.MinValue,
                ExpirationDate = p.ExpirationDate,
                BatchNumber = p.BatchNumber,
                Cost = p.Cost,
                Notes = p.Notes,
                CreatedByUserId = p.CreatedByUserId,
                CreatedByUserName = p.CreatedByUserId.HasValue && users.TryGetValue(p.CreatedByUserId.Value, out var userName) ? userName : null,
                Active = p.IsActive,
                CreatedAt = p.CreatedAt,
                ComponentProductionId = p.ComponentProductionId
            }).ToList();

            return Ok(dtos);
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

            decimal totalCost = 0m;

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
                Cost = 0, // Se calculará después de procesar los consumos
                Notes = dto.Notes,
                CreatedByUserId = dto.CreatedByUserId,
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
                        var supplyCost = await ConsumeSupplyWithFIFOAsync(consumption.ItemId, consumption.Quantity, dto.CreatedByUserId, created.Id);
                        totalCost += supplyCost;
                    }
                    else if (consumption.ItemType == "component")
                    {
                        // Consumir componente usando FIFO (producción negativa con referencia al padre)
                        var componentCost = await ConsumeComponentWithFIFOAsync(
                            consumption.ItemId,
                            consumption.Quantity,
                            dto.BusinessId,
                            dto.StoreId,
                            $"Consumo por producción de componente: {component.Name}",
                            dto.CreatedByUserId
                        );
                        totalCost += componentCost;
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
                        // ⭐ Consumir insumo usando FIFO (entrada negativa con referencia al padre)
                        var supplyCost = await ConsumeSupplyWithFIFOAsync(ingredient.SupplyId.Value, quantityToConsume, dto.CreatedByUserId, created.Id);
                        totalCost += supplyCost;
                    }
                    else if (ingredient.ItemType == "component" && ingredient.SubComponentId.HasValue)
                    {
                        // ⭐ Consumir sub-componente usando FIFO (producción negativa con referencia al padre)
                        var componentCost = await ConsumeComponentWithFIFOAsync(
                            ingredient.SubComponentId.Value,
                            quantityToConsume,
                            dto.BusinessId,
                            dto.StoreId,
                            $"Consumo por producción de componente: {component.Name}",
                            dto.CreatedByUserId
                        );
                        totalCost += componentCost;
                    }
                }
            }

            // Actualizar el costo total de la producción
            created.Cost = totalCost;
            await _componentProductionRepository.UpdateAsync(created);

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
    /// Simular consumo FIFO para calcular costo real sin guardar (para preview en frontend)
    /// </summary>
    [HttpPost("simulate-fifo-cost")]
    public async Task<ActionResult<FifoSimulationResultDto>> SimulateFifoCost([FromBody] FifoSimulationRequest request)
    {
        try
        {
            var result = new FifoSimulationResultDto
            {
                SupplyCosts = new List<SupplyFifoSimulationDto>(),
                ComponentCosts = new List<ComponentFifoSimulationDto>(),
                TotalCost = 0
            };

            // Simular consumos de supplies
            if (request.SupplyConsumptions != null)
            {
                foreach (var consumption in request.SupplyConsumptions)
                {
                    var simulation = await SimulateSupplyFifoAsync(consumption.SupplyId, consumption.Quantity);
                    result.SupplyCosts.Add(simulation);
                    result.TotalCost += simulation.TotalCost;
                }
            }

            // Simular consumos de componentes
            if (request.ComponentConsumptions != null)
            {
                foreach (var consumption in request.ComponentConsumptions)
                {
                    var simulation = await SimulateComponentFifoAsync(consumption.ComponentId, consumption.Quantity);
                    result.ComponentCosts.Add(simulation);
                    result.TotalCost += simulation.TotalCost;
                }
            }

            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Error simulando costo FIFO");
            return StatusCode(500, new { message = "Error al simular costo FIFO", error = ex.Message });
        }
    }

    /// <summary>
    /// Simular consumo FIFO de supply sin guardar
    /// </summary>
    private async Task<SupplyFifoSimulationDto> SimulateSupplyFifoAsync(int supplyId, decimal quantityToConsume)
    {
        var result = new SupplyFifoSimulationDto
        {
            SupplyId = supplyId,
            RequestedQuantity = quantityToConsume,
            LotDetails = new List<FifoLotDetailDto>(),
            TotalCost = 0
        };

        var remainingQuantity = quantityToConsume;
        var availableEntries = await _supplyEntryRepository.GetAvailableEntriesBySupplyIdAsync(supplyId);

        if (!availableEntries.Any())
        {
            result.HasSufficientStock = false;
            return result;
        }

        foreach (var entry in availableEntries)
        {
            if (remainingQuantity <= 0) break;

            var consumeFromThis = Math.Min(remainingQuantity, entry.Amount);
            var costFromThis = consumeFromThis * entry.UnitCost;

            result.LotDetails.Add(new FifoLotDetailDto
            {
                LotId = entry.Id,
                QuantityFromLot = consumeFromThis,
                UnitCost = entry.UnitCost,
                CostFromLot = costFromThis
            });

            result.TotalCost += costFromThis;
            remainingQuantity -= consumeFromThis;
        }

        result.HasSufficientStock = remainingQuantity == 0;
        return result;
    }

    /// <summary>
    /// Simular consumo FIFO de componente sin guardar
    /// </summary>
    private async Task<ComponentFifoSimulationDto> SimulateComponentFifoAsync(int componentId, decimal quantityToConsume)
    {
        var result = new ComponentFifoSimulationDto
        {
            ComponentId = componentId,
            RequestedQuantity = quantityToConsume,
            LotDetails = new List<FifoLotDetailDto>(),
            TotalCost = 0
        };

        var remainingQuantity = quantityToConsume;
        var availableProductions = await _componentProductionRepository.GetAvailableProductionsByComponentIdAsync(componentId);

        if (!availableProductions.Any())
        {
            result.HasSufficientStock = false;
            return result;
        }

        foreach (var production in availableProductions)
        {
            if (remainingQuantity <= 0) break;

            var consumeFromThis = Math.Min(remainingQuantity, production.ProducedAmount);
            
            // Calcular costo unitario del lote original
            var originalRecord = await _componentProductionRepository.GetByIdAsync(production.Id);
            var originalAmount = originalRecord?.ProducedAmount ?? production.ProducedAmount;
            var costPerUnit = originalAmount > 0 ? production.Cost / originalAmount : 0;
            var costFromThis = consumeFromThis * costPerUnit;

            result.LotDetails.Add(new FifoLotDetailDto
            {
                LotId = production.Id,
                QuantityFromLot = consumeFromThis,
                UnitCost = costPerUnit,
                CostFromLot = costFromThis
            });

            result.TotalCost += costFromThis;
            remainingQuantity -= consumeFromThis;
        }

        result.HasSufficientStock = remainingQuantity == 0;
        return result;
    }

    /// <summary>
    /// Obtener trazabilidad FIFO de una producción de componente (qué lotes se consumieron)
    /// </summary>
    [HttpGet("production/{productionId}/traceability")]
    public async Task<ActionResult<ProductionTraceabilityDto>> GetProductionTraceability(int productionId)
    {
        try
        {
            await _context.Database.OpenConnectionAsync();

            try
            {
                using var connection = _context.Database.GetDbConnection();

                var result = new ProductionTraceabilityDto
                {
                    ProductionId = productionId,
                    SupplyConsumptions = new List<SupplyConsumptionDto>(),
                    ComponentConsumptions = new List<ComponentConsumptionDto>()
                };

                // 1. Obtener consumos de insumos (supply_entry negativos vinculados a esta producción)
                // ⭐ Usar referencia directa component_production_id para trazabilidad precisa
                var supplyQuery = @"
                    SELECT 
                        se.id as consumption_id,
                        se.supply_entry_id as source_entry_id,
                        CAST(ABS(se.amount) AS DECIMAL(18,2)) as quantity_consumed,
                        se.unit_cost,
                        CAST(parent.amount AS DECIMAL(18,2)) as original_quantity,
                        s.id as supply_id,
                        s.name as supply_name,
                        um.symbol as unit_measure_symbol,
                        se.created_at
                    FROM supply_entry se
                    JOIN supply_entry parent ON se.supply_entry_id = parent.id
                    JOIN supplies s ON se.supply_id = s.id
                    JOIN unit_measures um on s.unit_measure_id = um.id
                    WHERE se.amount < 0
                    AND se.component_production_id = @productionId
                    ORDER BY se.created_at";

                using var supplyCmd = connection.CreateCommand();
                supplyCmd.CommandText = supplyQuery;
                var productionIdParam1 = supplyCmd.CreateParameter();
                productionIdParam1.ParameterName = "@productionId";
                productionIdParam1.Value = productionId;
                supplyCmd.Parameters.Add(productionIdParam1);

                using var supplyReader = await supplyCmd.ExecuteReaderAsync();
                while (await supplyReader.ReadAsync())
                {
                    result.SupplyConsumptions.Add(new SupplyConsumptionDto
                    {
                        ConsumptionId = supplyReader.GetInt32(0),
                        SourceEntryId = supplyReader.GetInt32(1),
                        QuantityConsumed = supplyReader.GetDecimal(2),
                        UnitCost = supplyReader.GetDecimal(3),
                        OriginalQuantity = supplyReader.GetDecimal(4),
                        SupplyId = supplyReader.GetInt32(5),
                        SupplyName = supplyReader.GetString(6),
                        UnitMeasureSymbol = supplyReader.GetString(7),
                        ConsumedAt = supplyReader.GetDateTime(8)
                    });
                }
                await supplyReader.CloseAsync();

                // 2. Obtener consumos de componentes (component_production negativos vinculados a esta producción)
                // ⭐ Usar timestamp exacto con ventana de ±2 segundos para vincular consumos a la producción
                var componentQuery = @"
                    SELECT 
                        cp_consumption.id as consumption_id,
                        cp_consumption.component_production_id as source_production_id,
                        ABS(cp_consumption.produced_amount) as quantity_consumed,
                        cp_consumption.cost as total_cost,
                        cp_source.produced_amount as original_quantity,
                        c.id as component_id,
                        c.name as component_name,
                        um.symbol as unit_measure_symbol,
                        cp_consumption.created_at
                    FROM component_production cp_consumption
                    JOIN component_production cp_source ON cp_consumption.component_production_id = cp_source.id
                    JOIN components c ON cp_consumption.component_id = c.id
                    JOIN component_production cp_target ON cp_consumption.created_at >= DATE_SUB(cp_target.created_at, INTERVAL 10 SECOND)
                        AND cp_consumption.created_at <= cp_target.created_at
                    JOIN unit_measures um on c.unit_measure_id = um.id
                    WHERE cp_consumption.produced_amount < 0
                    AND cp_target.id = @productionId
                    AND cp_target.produced_amount > 0
                    ORDER BY cp_consumption.created_at;";

                using var componentCmd = connection.CreateCommand();
                componentCmd.CommandText = componentQuery;
                var productionIdParam2 = componentCmd.CreateParameter();
                productionIdParam2.ParameterName = "@productionId";
                productionIdParam2.Value = productionId;
                componentCmd.Parameters.Add(productionIdParam2);

                using var componentReader = await componentCmd.ExecuteReaderAsync();
                while (await componentReader.ReadAsync())
                {
                    var quantityConsumed = componentReader.GetDecimal(2);
                    var totalCost = componentReader.GetDecimal(3);
                    var unitCost = quantityConsumed > 0 ? totalCost / quantityConsumed : 0;

                    result.ComponentConsumptions.Add(new ComponentConsumptionDto
                    {
                        ConsumptionId = componentReader.GetInt32(0),
                        SourceProductionId = componentReader.GetInt32(1),
                        QuantityConsumed = quantityConsumed,
                        UnitCost = unitCost,
                        OriginalQuantity = componentReader.GetDecimal(4),
                        ComponentId = componentReader.GetInt32(5),
                        ComponentName = componentReader.GetString(6),
                        UnitMeasureSymbol = componentReader.GetString(7),
                        ConsumedAt = componentReader.GetDateTime(8)
                    });
                }
                await componentReader.CloseAsync();

                await _context.Database.CloseConnectionAsync();

                return Ok(result);
            }
            catch (Exception ex)
            {
                await _context.Database.CloseConnectionAsync();
                _logger.LogError(ex, "❌ Error obteniendo trazabilidad de producción {productionId}", productionId);
                throw;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Error obteniendo trazabilidad de producción {productionId}", productionId);
            return StatusCode(500, new { message = "Error al obtener trazabilidad", error = ex.Message });
        }
    }

    /// <summary>
    /// Consume insumos usando algoritmo FIFO con autoreferencia
    /// Retorna el costo total de los insumos consumidos
    /// </summary>
    private async Task<decimal> ConsumeSupplyWithFIFOAsync(int supplyId, decimal quantityToConsume, int? createdByUserId = null, int? componentProductionId = null)
    {
        var remainingQuantity = quantityToConsume;
        decimal totalCost = 0m;

        // Obtener todos los supply_entry disponibles para este insumo (FIFO)
        // ⭐ Este método ya devuelve la cantidad disponible real (restando consumos previos)
        var availableEntries = await _supplyEntryRepository.GetAvailableEntriesBySupplyIdAsync(supplyId);

        if (!availableEntries.Any())
            throw new InvalidOperationException($"No hay stock disponible para el insumo ID {supplyId}");

        var entriesToUpdate = new List<SupplyEntry>(); // Entradas que necesitan actualización de active

        foreach (var availableEntry in availableEntries)
        {
            if (remainingQuantity <= 0) break;

            // ⭐ availableEntry.Amount ya contiene la cantidad disponible real (original - consumos)
            // Determinar cuánto consumir de esta entrada
            var consumeFromThisEntry = Math.Min(remainingQuantity, availableEntry.Amount);

            // Calcular el costo de esta porción
            totalCost += consumeFromThisEntry * availableEntry.UnitCost;

            // Crear supply_entry negativo con referencia al stock original usando el constructor correcto
            var supplyEntry = new SupplyEntry(
                availableEntry.UnitCost,           // Usar el costo del stock original
                -(int)consumeFromThisEntry,        // Cantidad negativa
                availableEntry.ProviderId,         // Usar el mismo proveedor
                supplyId,                          // SupplyId
                null,                              // ProcessDoneId (null para producción de componentes)
                availableEntry.Id,                 // ⭐ Referencia al stock original (supply_entry_id)
                createdByUserId,                   // ⭐ Usuario responsable
                componentProductionId              // ⭐ Referencia a la producción de componente
            );

            await _supplyEntryRepository.CreateAsync(supplyEntry);

            // ⭐ VALIDACIÓN CRÍTICA: Si esta entrada se queda completamente vacía, marcarla como inactiva
            var remainingInEntry = availableEntry.Amount - consumeFromThisEntry;
            if (remainingInEntry == 0)
            {
                // Obtener la entrada original para actualizar su estado
                var originalEntry = await _supplyEntryRepository.GetByIdAsync(availableEntry.Id);
                if (originalEntry != null)
                {
                    originalEntry.IsActive = false; // ⭐ Marcar como inactiva cuando se agota
                    entriesToUpdate.Add(originalEntry);
                }
            }

            // Reducir la cantidad pendiente
            remainingQuantity -= consumeFromThisEntry;
        }

        // ⭐ Actualizar todas las entradas que se quedaron vacías
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

        return totalCost;
    }

    /// <summary>
    /// Consume componentes usando algoritmo FIFO con autoreferencia
    /// Retorna el costo total de los componentes consumidos
    /// </summary>
    private async Task<decimal> ConsumeComponentWithFIFOAsync(int componentId, decimal quantityToConsume, int businessId, int storeId, string? notes = null, int? createdByUserId = null)
    {
        var remainingQuantity = quantityToConsume;
        decimal totalCost = 0m;

        // Obtener todos los component_production disponibles para este componente (FIFO)
        // ⭐ Este método ya devuelve la cantidad disponible real (restando consumos previos)
        var availableProductions = await _componentProductionRepository.GetAvailableProductionsByComponentIdAsync(componentId);

        if (!availableProductions.Any())
            throw new InvalidOperationException($"No hay stock disponible para el componente ID {componentId}");

        var productionsToUpdate = new List<ComponentProduction>(); // Producciones que necesitan actualización de active

        foreach (var availableProduction in availableProductions)
        {
            if (remainingQuantity <= 0) break;

            // ⭐ availableProduction.ProducedAmount ya contiene la cantidad disponible real
            // Determinar cuánto consumir de esta producción
            var consumeFromThisProduction = Math.Min(remainingQuantity, availableProduction.ProducedAmount);

            // ⭐ CRITICAL FIX: Obtener la cantidad ORIGINAL del lote para calcular costo unitario correcto
            // La cantidad disponible (ProducedAmount) ya tiene los consumos restados, pero el Cost es del lote completo
            // Necesitamos obtener el registro padre original para saber su cantidad inicial
            var originalProductionRecord = await _componentProductionRepository.GetByIdAsync(availableProduction.Id);
            var originalAmount = originalProductionRecord?.ProducedAmount ?? availableProduction.ProducedAmount;

            // Calcular costo unitario basado en la cantidad ORIGINAL del lote, no la disponible
            var costPerUnit = originalAmount > 0
                ? availableProduction.Cost / originalAmount
                : 0;

            // Calcular el costo de esta porción
            var costFromThisProduction = costPerUnit * consumeFromThisProduction;
            totalCost += costFromThisProduction;

            // Crear component_production negativo con referencia al lote original
            var componentConsumption = new ComponentProduction
            {
                ComponentId = componentId,
                ProcessDoneId = null,
                BusinessId = businessId,
                StoreId = storeId,
                ProducedAmount = -consumeFromThisProduction, // Cantidad negativa
                ProductionDate = DateTime.Now,
                Cost = costFromThisProduction, // Costo proporcional
                Notes = notes,
                ComponentProductionId = availableProduction.Id, // ⭐ Referencia al lote original (FIFO)
                CreatedByUserId = createdByUserId, // ⭐ Usuario responsable
                IsActive = true,
                CreatedAt = DateTime.Now,
                UpdatedAt = DateTime.Now
            };

            await _componentProductionRepository.CreateAsync(componentConsumption);

            // ⭐ VALIDACIÓN CRÍTICA: Si esta producción se queda completamente vacía, marcarla como inactiva
            var remainingInProduction = availableProduction.ProducedAmount - consumeFromThisProduction;
            if (remainingInProduction == 0)
            {
                // Obtener la producción original para actualizar su estado
                var originalProduction = await _componentProductionRepository.GetByIdAsync(availableProduction.Id);
                if (originalProduction != null)
                {
                    originalProduction.IsActive = false; // ⭐ Marcar como inactiva cuando se agota
                    productionsToUpdate.Add(originalProduction);
                }
            }

            // Reducir la cantidad pendiente
            remainingQuantity -= consumeFromThisProduction;
        }

        // ⭐ Actualizar todas las producciones que se quedaron vacías
        foreach (var production in productionsToUpdate)
        {
            await _componentProductionRepository.UpdateAsync(production);
        }

        // Verificar que se pudo consumir toda la cantidad necesaria
        if (remainingQuantity > 0)
            throw new InvalidOperationException(
                $"Stock insuficiente para el componente ID {componentId}. " +
                $"Faltan {remainingQuantity} unidades"
            );

        return totalCost;
    }

    /// <summary>
    /// Remover stock de una producción de componente específica (agregar registro negativo con FIFO)
    /// </summary>
    [HttpPost("production/{productionId}/remove")]
    public async Task<ActionResult<object>> RemoveStockFromProduction(int productionId, [FromBody] RemoveComponentStockRequest request)
    {
        try
        {
            // ⭐ Obtener usuario responsable
            var userId = GetUserIdFromClaims();

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

                var originalAmount = reader.GetDecimal(1);
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

                var removedAmount = Convert.ToDecimal(await removalCmd.ExecuteScalarAsync());

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
                        notes,
                        created_by_user_id
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
                        @notes,
                        @createdByUserId
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

                var createdByUserIdParam = insertCmd.CreateParameter();
                createdByUserIdParam.ParameterName = "@createdByUserId";
                createdByUserIdParam.Value = (object?)userId ?? DBNull.Value;
                insertCmd.Parameters.Add(createdByUserIdParam);

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
    public decimal TotalValue { get; set; }
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
    public int? CreatedByUserId { get; set; }
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
    public decimal Amount { get; set; }
    public string? Notes { get; set; }
}

// Traceability DTOs
public class ProductionTraceabilityDto
{
    public int ProductionId { get; set; }
    public List<SupplyConsumptionDto> SupplyConsumptions { get; set; } = new();
    public List<ComponentConsumptionDto> ComponentConsumptions { get; set; } = new();
}

public class SupplyConsumptionDto
{
    public int ConsumptionId { get; set; }
    public int SourceEntryId { get; set; }
    public decimal QuantityConsumed { get; set; }
    public decimal UnitCost { get; set; }
    public decimal OriginalQuantity { get; set; }
    public int SupplyId { get; set; }
    public string SupplyName { get; set; } = string.Empty;
    public string UnitMeasureSymbol { get; set; } = string.Empty;
    public DateTime ConsumedAt { get; set; }
}

public class ComponentConsumptionDto
{
    public int ConsumptionId { get; set; }
    public int SourceProductionId { get; set; }
    public decimal QuantityConsumed { get; set; }
    public decimal UnitCost { get; set; }
    public decimal OriginalQuantity { get; set; }
    public int ComponentId { get; set; }
    public string ComponentName { get; set; } = string.Empty;
    public string UnitMeasureSymbol { get; set; } = string.Empty;
    public DateTime ConsumedAt { get; set; }
}

// FIFO Simulation DTOs
public class FifoSimulationRequest
{
    public List<SupplyConsumptionSimRequest>? SupplyConsumptions { get; set; }
    public List<ComponentConsumptionSimRequest>? ComponentConsumptions { get; set; }
}

public class SupplyConsumptionSimRequest
{
    public int SupplyId { get; set; }
    public decimal Quantity { get; set; }
}

public class ComponentConsumptionSimRequest
{
    public int ComponentId { get; set; }
    public decimal Quantity { get; set; }
}

public class FifoSimulationResultDto
{
    public List<SupplyFifoSimulationDto> SupplyCosts { get; set; } = new();
    public List<ComponentFifoSimulationDto> ComponentCosts { get; set; } = new();
    public decimal TotalCost { get; set; }
}

public class SupplyFifoSimulationDto
{
    public int SupplyId { get; set; }
    public decimal RequestedQuantity { get; set; }
    public bool HasSufficientStock { get; set; }
    public List<FifoLotDetailDto> LotDetails { get; set; } = new();
    public decimal TotalCost { get; set; }
}

public class ComponentFifoSimulationDto
{
    public int ComponentId { get; set; }
    public decimal RequestedQuantity { get; set; }
    public bool HasSufficientStock { get; set; }
    public List<FifoLotDetailDto> LotDetails { get; set; } = new();
    public decimal TotalCost { get; set; }
}

public class FifoLotDetailDto
{
    public int LotId { get; set; }
    public decimal QuantityFromLot { get; set; }
    public decimal UnitCost { get; set; }
    public decimal CostFromLot { get; set; }
}
