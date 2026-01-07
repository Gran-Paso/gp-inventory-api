using GPInventory.Application.DTOs.Production;
using GPInventory.Application.Interfaces;
using GPInventory.Domain.Entities;

namespace GPInventory.Application.Services;

public class ProcessDoneService : IProcessDoneService
{
    private readonly IProcessDoneRepository _processDoneRepository;
    private readonly IProcessRepository _processRepository;
    private readonly ISupplyEntryRepository _supplyEntryRepository;
    private readonly IStockRepository _stockRepository;
    private readonly IComponentProductionRepository _componentProductionRepository;

    public ProcessDoneService(IProcessDoneRepository processDoneRepository, IProcessRepository processRepository, ISupplyEntryRepository supplyEntryRepository, IStockRepository stockRepository, IComponentProductionRepository componentProductionRepository)
    {
        _processDoneRepository = processDoneRepository;
        _processRepository = processRepository;
        _supplyEntryRepository = supplyEntryRepository;
        _stockRepository = stockRepository;
        _componentProductionRepository = componentProductionRepository;
    }

    public async Task<ProcessDoneDto> GetProcessDoneByIdAsync(int id)
    {
        var processDone = await _processDoneRepository.GetByIdWithDetailsAsync(id);
        if (processDone == null)
            throw new KeyNotFoundException($"ProcessDone with ID {id} not found");

        return MapToDtoWithDetails(processDone); // ⭐ Usar mapeo con detalles para obtener SupplyUsages
    }

    public async Task<IEnumerable<ProcessDoneDto>> GetAllProcessDonesAsync()
    {
        var processDones = await _processDoneRepository.GetAllAsync();
        return processDones.Select(MapToDto);
    }

    public async Task<IEnumerable<ProcessDoneDto>> GetProcessDonesByProcessIdAsync(int processId)
    {
        var processDones = await _processDoneRepository.GetByProcessIdAsync(processId);
        return processDones.Select(MapToDto);
    }

    public async Task<ProcessDoneDto> CreateProcessDoneAsync(CreateProcessDoneDto createProcessDoneDto)
    {
        // Verificar que el proceso existe
        var process = await _processRepository.GetByIdAsync(createProcessDoneDto.ProcessId);
        if (process == null)
            throw new InvalidOperationException($"Process with ID {createProcessDoneDto.ProcessId} not found");

        // Crear el ProcessDone
        var processDone = new ProcessDone(
            createProcessDoneDto.ProcessId,
            createProcessDoneDto.Amount,
            createProcessDoneDto.Stage,
            createProcessDoneDto.StartDate,
            createProcessDoneDto.EndDate,
            DateTime.UtcNow,
            createProcessDoneDto.Notes
        );

        var createdProcessDone = await _processDoneRepository.CreateAsync(processDone);

        // Procesar todos los insumos en una sola operación para evitar problemas de concurrencia
        await ProcessAllSupplyConsumptionAsync(createProcessDoneDto.SupplyUsages, createdProcessDone.Id);

        // Procesar todos los componentes usados
        await ProcessAllComponentConsumptionAsync(createProcessDoneDto.ComponentUsages, createdProcessDone.Id, process);

        // Recargar con detalles
        var processWithDetails = await _processDoneRepository.GetByIdWithDetailsAsync(createdProcessDone.Id);
        return MapToDtoWithDetails(processWithDetails!); // ⭐ Usar mapeo con detalles
    }

    /// <summary>
    /// Completa todo el proceso de una vez, procesando todos los insumos automáticamente
    /// </summary>
    public async Task<ProcessDoneDto> CompleteFullProcessAsync(int processId, int amountProduced, string? notes = null)
    {
        // Obtener el proceso con sus insumos
        var process = await _processRepository.GetByIdWithDetailsAsync(processId);
        if (process == null)
            throw new InvalidOperationException($"Process with ID {processId} not found");

        if (!process.ProcessSupplies?.Any() ?? true)
            throw new InvalidOperationException($"Process {processId} has no supplies configured");

        // Crear las SupplyUsages automáticamente basado en la configuración del proceso
        var supplyUsages = new List<CreateSupplyUsageDto>();
        
        if (process.ProcessSupplies != null)
        {
            foreach (var processSupply in process.ProcessSupplies.OrderBy(ps => ps.Order))
            {
                // Por ahora, usar la cantidad base configurada en el proceso
                // En el futuro, esto podría calcularse proporcionalmente basado en amountProduced
                supplyUsages.Add(new CreateSupplyUsageDto
                {
                    SupplyId = processSupply.SupplyId,
                    QuantityUsed = amountProduced, // Usar la cantidad producida como base
                    UnitCost = 0 // Se calculará automáticamente usando FIFO
                });
            }
        }

        // ⭐ AGREGAR: Crear las ComponentUsages automáticamente basado en la configuración del proceso
        var componentUsages = new List<CreateComponentUsageDto>();
        
        if (process.ProcessComponents != null && process.ProcessComponents.Any())
        {
            foreach (var processComponent in process.ProcessComponents)
            {
                // Por ahora, asumir que se necesita 1 componente por cada unidad producida
                // TODO: En el futuro, agregar un campo QuantityNeeded a ProcessComponent
                var quantityNeeded = 1 * amountProduced;
                
                componentUsages.Add(new CreateComponentUsageDto
                {
                    ComponentId = processComponent.ComponentId,
                    QuantityUsed = quantityNeeded,
                    UnitCost = 0 // Se obtendrá del stock actual del componente
                });
            }
        }

        // Crear el ProcessDone usando el método existente
        var createProcessDoneDto = new CreateProcessDoneDto
        {
            ProcessId = processId,
            Amount = amountProduced,
            Stage = 999, // Indicar que es completado automáticamente
            StartDate = DateTime.UtcNow,
            EndDate = DateTime.UtcNow, // Completado inmediatamente
            Notes = notes ?? "Proceso completado automáticamente",
            SupplyUsages = supplyUsages,
            ComponentUsages = componentUsages
        };

        var processDoneDto = await CreateProcessDoneAsync(createProcessDoneDto);
        
        // ⭐ AGREGAR: Crear el ComponentProduction positivo del componente producido
        if (process.Product != null)
        {
            var componentProduction = new ComponentProduction
            {
                ComponentId = process.Product.Id,
                ProcessDoneId = processDoneDto.Id,
                BusinessId = process.Product.BusinessId,
                StoreId = process.StoreId,
                ProducedAmount = amountProduced, // Positivo para stock producido
                ProductionDate = DateTime.UtcNow,
                Cost = processDoneDto.Cost, // Usar el costo calculado del proceso
                Notes = notes ?? $"Producción completada del proceso {process.Name}",
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            };

            await _componentProductionRepository.CreateAsync(componentProduction);
        }
        
        return processDoneDto;
    }

    /// <summary>
    /// Procesa el consumo de todos los insumos usando algoritmo FIFO con autoreferencia (versión optimizada)
    /// </summary>
    private async Task ProcessAllSupplyConsumptionAsync(List<CreateSupplyUsageDto> supplyUsages, int processDoneId)
    {
        foreach (var supplyUsage in supplyUsages)
        {
            await ProcessSupplyConsumptionAsync(supplyUsage, processDoneId);
        }
        
        // Al terminar de procesar todos los insumos, calcular el costo unitario
        await CalculateAndUpdateUnitCostAsync(processDoneId);
    }
    
    /// <summary>
    /// Calcula el costo unitario total de los insumos y actualiza el ProcessDone
    /// </summary>
    private async Task CalculateAndUpdateUnitCostAsync(int processDoneId)
    {
        // Calcular el costo total directamente desde la base de datos sin cargar la entidad completa
        decimal totalSupplyCost = 0m;
        var processDoneAmount = 0;
        
        // Obtener solo los datos necesarios para el cálculo
        var supplyEntries = await _supplyEntryRepository.GetByProcessDoneIdAsync(processDoneId);
        
        // Obtener el amount del ProcessDone directamente
        var processDone = await _processDoneRepository.GetByIdAsync(processDoneId);
        if (processDone == null) return;
        
        processDoneAmount = processDone.Amount;
        
        // Calcular el costo total de los insumos consumidos
        foreach (var supplyEntry in supplyEntries)
        {
            // Solo considerar entradas negativas (consumos) para este proceso
            if (supplyEntry.Amount < 0)
            {
                // Calcular costo: cantidad consumida (valor absoluto) × costo unitario
                totalSupplyCost += Math.Abs(supplyEntry.Amount) * supplyEntry.UnitCost;
            }
        }
        
        // Calcular costo unitario: costo total ÷ cantidad producida
        decimal unitCost = processDoneAmount > 0 ? totalSupplyCost / processDoneAmount : 0;
        
        // Actualizar solo el campo Cost sin problemas de tracking
        await _processDoneRepository.UpdateCostAsync(processDoneId, unitCost);
    }

    /// <summary>
    /// Procesa el consumo de un insumo usando algoritmo FIFO con autoreferencia
    /// </summary>
    private async Task ProcessSupplyConsumptionAsync(CreateSupplyUsageDto supplyUsage, int processDoneId)
    {
        var remainingQuantity = supplyUsage.QuantityUsed;
        
        // Obtener todos los supply_entry disponibles para este insumo (FIFO)
        var availableEntries = await _supplyEntryRepository.GetAvailableEntriesBySupplyIdAsync(supplyUsage.SupplyId);
        
        if (!availableEntries.Any())
            throw new InvalidOperationException($"No stock available for Supply ID {supplyUsage.SupplyId}");
        
        var entriesToUpdate = new List<SupplyEntry>(); // Entradas que necesitan actualización de active
        
        foreach (var availableEntry in availableEntries)
        {
            if (remainingQuantity <= 0) break;
            
            // Determinar cuánto consumir de esta entrada
            var consumeFromThisEntry = Math.Min(remainingQuantity, availableEntry.Amount);
            
            // Crear supply_entry negativo con referencia al stock original
            var supplyEntry = new SupplyEntry(
                availableEntry.UnitCost,           // Usar el costo del stock original
                -(int)consumeFromThisEntry,        // Cantidad negativa
                1,                                 // ProviderId por defecto
                supplyUsage.SupplyId,
                processDoneId,
                availableEntry.Id                  // ⭐ Referencia al stock original
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
                $"Insufficient stock for Supply ID {supplyUsage.SupplyId}. " +
                $"Missing {remainingQuantity} units"
            );
    }

    public async Task<ProcessDoneDto> UpdateProcessDoneStageAsync(int id, int stage)
    {
        var processDone = await _processDoneRepository.GetByIdAsync(id);
        if (processDone == null)
            throw new KeyNotFoundException($"ProcessDone with ID {id} not found");

        processDone.Stage = stage;
        processDone.UpdatedAt = DateTime.UtcNow;
        
        await _processDoneRepository.UpdateAsync(processDone);
        
        var updatedProcessDone = await _processDoneRepository.GetByIdWithDetailsAsync(id);
        return MapToDto(updatedProcessDone!);
    }

    public async Task<ProcessDoneDto> UpdateProcessDoneAmountAsync(int id, int amount, bool isLastSupply = false)
    {
        var processDone = await _processDoneRepository.GetByIdAsync(id);
        if (processDone == null)
            throw new KeyNotFoundException($"ProcessDone with ID {id} not found");

        processDone.Amount = amount;
        processDone.UpdatedAt = DateTime.UtcNow;
        
        // Si es el último insumo, también actualizar el end_date y crear el stock
        if (isLastSupply)
        {
            processDone.EndDate = DateTime.UtcNow;
            
            // Obtener el Process para conocer ProductId y StoreId
            var process = await _processRepository.GetByIdAsync(processDone.ProcessId);
            if (process != null)
            {
                // Calcular el costo total de los insumos utilizados
                var processWithDetails = await _processDoneRepository.GetByIdWithDetailsAsync(id);
                var totalSupplyCost = 0m;
                
                if (processWithDetails?.SupplyEntries != null)
                {
                    foreach (var supplyEntry in processWithDetails.SupplyEntries)
                    {
                        // Solo considerar entradas negativas (consumos) para este proceso
                        if (supplyEntry.Amount < 0)
                        {
                            // Calcular costo: cantidad consumida (valor absoluto) * costo unitario
                            totalSupplyCost += Math.Abs(supplyEntry.Amount) * supplyEntry.UnitCost;
                        }
                    }
                }
                
                // Calcular costo unitario: costo total / cantidad producida
                var unitCost = amount > 0 ? totalSupplyCost / amount : 0;
                
                // Crear entrada de Stock con flow_id = 2 (producción)
                var stock = new Stock
                {
                    ProductId = process.ProductId,
                    FlowTypeId = 2, // Producción
                    Amount = amount, // Cantidad producida (positivo)
                    Cost = (int)Math.Round(totalSupplyCost), // Costo total de los insumos
                    StoreId = process.StoreId,
                    Date = DateTime.UtcNow,
                    Notes = $"Producción del proceso: {process.Name}. Costo total insumos: {totalSupplyCost:C}. Costo unitario: {(amount > 0 ? totalSupplyCost / amount : 0):C}",
                    IsActive = true // ✅ Establecer como activo
                };
                
                await _stockRepository.AddAsync(stock);
            }
        }
        
        await _processDoneRepository.UpdateAsync(processDone);
        
        var updatedProcessDone = await _processDoneRepository.GetByIdWithDetailsAsync(id);
        return MapToDto(updatedProcessDone!);
    }

    public async Task<ProcessDoneDto> AddSupplyEntryAsync(int processDoneId, CreateSupplyUsageDto supplyUsage)
    {
        var processDone = await _processDoneRepository.GetByIdAsync(processDoneId);
        if (processDone == null)
            throw new KeyNotFoundException($"ProcessDone with ID {processDoneId} not found");

        // Procesar el consumo con algoritmo FIFO
        await ProcessSupplyConsumptionAsync(supplyUsage, processDoneId);

        // Recargar con detalles
        var processWithDetails = await _processDoneRepository.GetByIdWithDetailsAsync(processDoneId);
        return MapToDto(processWithDetails!);
    }

    public async Task DeleteProcessDoneAsync(int id)
    {
        var processDone = await _processDoneRepository.GetByIdAsync(id);
        if (processDone == null)
            throw new KeyNotFoundException($"ProcessDone with ID {id} not found");

        await _processDoneRepository.DeleteAsync(id);
    }

    private static ProcessDoneDto MapToDto(ProcessDone processDone)
    {
        return new ProcessDoneDto
        {
            Id = processDone.Id,
            ProcessId = processDone.ProcessId,
            Stage = processDone.Stage,
            StartDate = processDone.StartDate,
            EndDate = processDone.EndDate,
            StockId = processDone.StockId,
            Amount = processDone.Amount,
            Cost = processDone.Cost,
            CompletedAt = processDone.CompletedAt,
            Notes = processDone.Notes,
            CreatedAt = processDone.CreatedAt,
            UpdatedAt = processDone.UpdatedAt,
            IsActive = processDone.IsActive,
            Process = processDone.Process != null ? new ProcessDto
            {
                Id = processDone.Process.Id,
                Name = processDone.Process.Name,
                Description = processDone.Process.Description,
                ProductId = processDone.Process.ProductId,
                ProductionTime = processDone.Process.ProductionTime,
                TimeUnitId = processDone.Process.TimeUnitId,
                StoreId = processDone.Process.StoreId,
                CreatedAt = processDone.Process.CreatedAt,
                UpdatedAt = processDone.Process.UpdatedAt,
                IsActive = processDone.Process.IsActive
            } : null,
            SupplyUsages = new List<SupplyUsageDto>() // ⭐ Lista vacía para mejor rendimiento
        };
    }
    
    private static ProcessDoneDto MapToDtoWithDetails(ProcessDone processDone)
    {
        return new ProcessDoneDto
        {
            Id = processDone.Id,
            ProcessId = processDone.ProcessId,
            Stage = processDone.Stage,
            StartDate = processDone.StartDate,
            EndDate = processDone.EndDate,
            StockId = processDone.StockId,
            Amount = processDone.Amount,
            Cost = processDone.Cost,
            CompletedAt = processDone.CompletedAt,
            Notes = processDone.Notes,
            CreatedAt = processDone.CreatedAt,
            UpdatedAt = processDone.UpdatedAt,
            IsActive = processDone.IsActive,
            Process = processDone.Process != null ? new ProcessDto
            {
                Id = processDone.Process.Id,
                Name = processDone.Process.Name,
                Description = processDone.Process.Description,
                ProductId = processDone.Process.ProductId,
                ProductionTime = processDone.Process.ProductionTime,
                TimeUnitId = processDone.Process.TimeUnitId,
                StoreId = processDone.Process.StoreId,
                CreatedAt = processDone.Process.CreatedAt,
                UpdatedAt = processDone.Process.UpdatedAt,
                IsActive = processDone.Process.IsActive
            } : null,
            SupplyUsages = processDone.SupplyEntries?.Where(se => se.Amount < 0).Select(se => new SupplyUsageDto
            {
                Id = se.Id,
                SupplyId = se.SupplyId,
                QuantityUsed = Math.Abs(se.Amount), // Usar valor absoluto para mostrar cantidad positiva
                UnitCost = se.UnitCost,
                TotalCost = Math.Abs(se.Amount) * se.UnitCost, // Costo total positivo
                SupplyName = $"Insumo {se.SupplyId}" // Temporal, podríamos hacer una consulta separada si necesitamos el nombre
            }).ToList() ?? new List<SupplyUsageDto>()
        };
    }

    /// <summary>
    /// Procesa el consumo de todos los componentes (registra entradas negativas de producción)
    /// </summary>
    private async Task ProcessAllComponentConsumptionAsync(List<CreateComponentUsageDto> componentUsages, int processDoneId, Process process)
    {
        if (componentUsages == null || !componentUsages.Any())
            return;

        foreach (var componentUsage in componentUsages)
        {
            await ProcessComponentConsumptionAsync(componentUsage, processDoneId, process);
        }
    }

    /// <summary>
    /// Procesa el consumo de un componente usando algoritmo FIFO con autoreferencia
    /// </summary>
    private async Task ProcessComponentConsumptionAsync(CreateComponentUsageDto componentUsage, int processDoneId, Process process)
    {
        var remainingQuantity = componentUsage.QuantityUsed;
        
        // Obtener todos los component_production disponibles para este componente (FIFO)
        var availableProductions = await _componentProductionRepository.GetAvailableProductionsByComponentIdAsync(componentUsage.ComponentId);
        
        if (!availableProductions.Any())
            throw new InvalidOperationException($"No stock available for Component ID {componentUsage.ComponentId}");
        
        var productionsToUpdate = new List<ComponentProduction>(); // Producciones que necesitan actualización
        
        foreach (var availableProduction in availableProductions)
        {
            if (remainingQuantity <= 0) break;
            
            // Determinar cuánto consumir de esta producción
            var consumeFromThisProduction = Math.Min(remainingQuantity, availableProduction.ProducedAmount);
            
            // Calcular costo proporcional del stock consumido
            var costPerUnit = availableProduction.ProducedAmount > 0 
                ? availableProduction.Cost / availableProduction.ProducedAmount 
                : 0;
            
            // Crear component_production negativo con referencia al lote original
            var componentProduction = new ComponentProduction
            {
                ComponentId = componentUsage.ComponentId,
                ProcessDoneId = processDoneId,
                BusinessId = process.Product?.BusinessId ?? availableProduction.BusinessId,
                StoreId = process.StoreId,
                ProducedAmount = -consumeFromThisProduction, // Cantidad negativa
                ProductionDate = DateTime.Now,
                Cost = costPerUnit * consumeFromThisProduction, // Costo proporcional
                Notes = $"Consumido en proceso {process.Name}",
                ComponentProductionId = availableProduction.Id, // ⭐ Referencia al lote original (FIFO)
                IsActive = true,
                CreatedAt = DateTime.Now
            };

            await _componentProductionRepository.CreateAsync(componentProduction);
            
            // Si esta producción se queda completamente vacía, marcarla para desactivar
            var remainingInProduction = availableProduction.ProducedAmount - consumeFromThisProduction;
            if (remainingInProduction == 0)
            {
                // Obtener la producción original para actualizar su estado
                var originalProduction = await _componentProductionRepository.GetByIdAsync(availableProduction.Id);
                if (originalProduction != null)
                {
                    originalProduction.IsActive = false; // Marcar como inactiva
                    productionsToUpdate.Add(originalProduction);
                }
            }
            
            // Reducir la cantidad pendiente
            remainingQuantity -= consumeFromThisProduction;
        }
        
        // Actualizar todas las producciones que se quedaron vacías
        foreach (var production in productionsToUpdate)
        {
            await _componentProductionRepository.UpdateAsync(production);
        }
        
        if (remainingQuantity > 0)
            throw new InvalidOperationException($"Insufficient stock for Component ID {componentUsage.ComponentId}. Missing: {remainingQuantity}");
    }
}
