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
    private readonly IManufactureRepository _manufactureRepository;
    private readonly IUserRepository _userRepository;

    public ProcessDoneService(
        IProcessDoneRepository processDoneRepository, 
        IProcessRepository processRepository, 
        ISupplyEntryRepository supplyEntryRepository, 
        IStockRepository stockRepository, 
        IComponentProductionRepository componentProductionRepository,
        IManufactureRepository manufactureRepository,
        IUserRepository userRepository)
    {
        _processDoneRepository = processDoneRepository;
        _processRepository = processRepository;
        _supplyEntryRepository = supplyEntryRepository;
        _stockRepository = stockRepository;
        _componentProductionRepository = componentProductionRepository;
        _manufactureRepository = manufactureRepository;
        _userRepository = userRepository;
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
        var dtos = processDones.Select(MapToDto).ToList();
        
        // Enriquecer con nombres de usuarios
        await EnrichWithUserNamesAsync(dtos);
        
        return dtos;
    }

    public async Task<ProcessDoneDto> CreateProcessDoneAsync(CreateProcessDoneDto createProcessDoneDto)
    {
        try
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
                createProcessDoneDto.Notes,
                createProcessDoneDto.CreatedByUserId
            );

            var createdProcessDone = await _processDoneRepository.CreateAsync(processDone);

            // ⭐ ORDEN CORRECTO: 
            // 1. Procesar consumo de insumos (supplies)
            foreach (var supplyUsage in createProcessDoneDto.SupplyUsages)
            {
                await ProcessSupplyConsumptionAsync(supplyUsage, createdProcessDone.Id, createProcessDoneDto.CreatedByUserId);
            }

            // 2. Procesar consumo de componentes
            await ProcessAllComponentConsumptionAsync(createProcessDoneDto.ComponentUsages, createdProcessDone.Id, process, createProcessDoneDto.CreatedByUserId);

            // 3. DESPUÉS de consumir todo, calcular el costo TOTAL (supplies + componentes) y crear Manufacture
            await CalculateAndUpdateTotalCostAsync(createdProcessDone.Id);
            await CreateManufactureFromProcessDoneAsync(createdProcessDone.Id, createProcessDoneDto.StoreId, createProcessDoneDto.CreatedByUserId);

            // Recargar con detalles
            var processWithDetails = await _processDoneRepository.GetByIdWithDetailsAsync(createdProcessDone.Id);
            return MapToDtoWithDetails(processWithDetails!); // ⭐ Usar mapeo con detalles
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error creating process done: {ex.Message}");
            Console.WriteLine($"Stack trace: {ex.StackTrace}");
            throw;
        }
    }

    /// <summary>
    /// Completa todo el proceso de una vez, procesando todos los insumos automáticamente
    /// </summary>
    public async Task<ProcessDoneDto> CompleteFullProcessAsync(int processId, int amountProduced, string? notes = null, int? createdByUserId = null)
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
            ComponentUsages = componentUsages,
            CreatedByUserId = createdByUserId
        };

        var processDoneDto = await CreateProcessDoneAsync(createProcessDoneDto);
        
        // ⭐ Crear el ComponentProduction positivo del componente producido
        // Se crea DESPUÉS de ProcessAllSupplyConsumptionAsync para tener el costo correcto
        if (process.Product != null)
        {
            // Obtener el costo total de la manufactura recién creada
            var manufacture = await _manufactureRepository.GetByProcessDoneIdAsync(processDoneDto.Id);
            var totalCost = manufacture?.FirstOrDefault()?.Cost ?? 0;
            
            var componentProduction = new ComponentProduction
            {
                ComponentId = process.Product.Id,
                ProcessDoneId = processDoneDto.Id,
                BusinessId = process.Product.BusinessId,
                StoreId = process.StoreId,
                ProducedAmount = amountProduced,
                ProductionDate = DateTime.UtcNow,
                Cost = totalCost, // Usar el costo TOTAL de la manufactura (insumos + componentes)
                Notes = notes ?? $"Producción completada del proceso {process.Name}",
                CreatedByUserId = createdByUserId, // ⭐ Usuario responsable
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            };

            await _componentProductionRepository.CreateAsync(componentProduction);
        }
        
        return processDoneDto;
    }

    /// <summary>
    /// Calcula el costo TOTAL (supplies + componentes) y actualiza el ProcessDone
    /// </summary>
    private async Task CalculateAndUpdateTotalCostAsync(int processDoneId)
    {
        // Obtener el ProcessDone
        var processDone = await _processDoneRepository.GetByIdAsync(processDoneId);
        if (processDone == null) return;
        
        // 1. Calcular costo de SUPPLIES
        var supplyEntries = await _supplyEntryRepository.GetByProcessDoneIdAsync(processDoneId);
        decimal totalSupplyCost = 0m;
        
        foreach (var supplyEntry in supplyEntries)
        {
            if (supplyEntry.Amount < 0) // Solo consumos
            {
                totalSupplyCost += Math.Abs(supplyEntry.Amount) * supplyEntry.UnitCost;
            }
        }
        
        // 2. Calcular costo de COMPONENTES
        var componentProductions = await _componentProductionRepository.GetByProcessDoneIdAsync(processDoneId);
        decimal totalComponentCost = 0m;
        
        if (componentProductions != null)
        {
            foreach (var componentProduction in componentProductions)
            {
                if (componentProduction.ProducedAmount < 0) // Solo consumos
                {
                    totalComponentCost += componentProduction.Cost;
                }
            }
        }
        
        // 3. Costo TOTAL = supplies + componentes
        decimal totalCost = totalSupplyCost + totalComponentCost;
        
        // 4. Actualizar ProcessDone con el costo TOTAL (no unitario)
        await _processDoneRepository.UpdateCostAsync(processDoneId, totalCost);
    }

    /// <summary>
    /// Crea un registro de Manufacture después de completar un proceso
    /// Si se proporciona storeId, también crea el stock en la tienda con el proveedor por defecto
    /// </summary>
    private async Task CreateManufactureFromProcessDoneAsync(int processDoneId, int? storeId = null, int? createdByUserId = null)
    {
        // Obtener el ProcessDone y el Process asociado
        var processDone = await _processDoneRepository.GetByIdAsync(processDoneId);
        if (processDone == null) return;

        var process = await _processRepository.GetByIdWithDetailsAsync(processDone.ProcessId);
        if (process == null) return;

        // Calcular el costo total de los insumos utilizados
        var supplyEntries = await _supplyEntryRepository.GetByProcessDoneIdAsync(processDoneId);
        decimal totalSupplyCost = 0m;
        
        foreach (var supplyEntry in supplyEntries)
        {
            if (supplyEntry.Amount < 0)
            {
                totalSupplyCost += Math.Abs(supplyEntry.Amount) * supplyEntry.UnitCost;
            }
        }

        // Calcular el costo total de los componentes utilizados
        var componentProductions = await _componentProductionRepository.GetByProcessDoneIdAsync(processDoneId);
        decimal totalComponentCost = 0m;
        
        if (componentProductions != null)
        {
            foreach (var componentProduction in componentProductions)
            {
                if (componentProduction.ProducedAmount < 0) // Consumos
                {
                    totalComponentCost += componentProduction.Cost;
                }
            }
        }

        // Costo total = insumos + componentes
        decimal totalCost = totalSupplyCost + totalComponentCost;

        // Crear el registro de Manufacture
        var manufacture = new Manufacture
        {
            ProductId = process.ProductId,
            ProcessDoneId = processDone.Id,
            BusinessId = process.Product.BusinessId,
            StoreId = storeId, // Asignar la tienda si se proporcionó
            Amount = processDone.Amount,
            Cost = (int)Math.Round(totalCost),
            Date = DateTime.UtcNow,
            Notes = $"Producción del proceso: {process.Name}. Insumos: {totalSupplyCost:C}, Componentes: {totalComponentCost:C}, Total: {totalCost:C}",
            Status = storeId.HasValue ? "sent" : "pending", // Si tiene tienda, marcarlo como "sent"
            IsActive = !storeId.HasValue, // Si se envía directamente, IsActive = false; si queda en fábrica, IsActive = true
            CreatedByUserId = createdByUserId,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        var createdManufacture = await _manufactureRepository.AddAsync(manufacture);

        // Si se proporcionó una tienda, crear automáticamente el stock
        if (storeId.HasValue && createdManufacture != null)
        {
            await CreateStockFromManufactureAsync(createdManufacture, storeId.Value, process.Product.BusinessId);
        }
    }

    /// <summary>
    /// Crea un registro de Stock en la tienda desde un Manufacture
    /// Utiliza el primer proveedor activo del negocio
    /// </summary>
    private async Task CreateStockFromManufactureAsync(Manufacture manufacture, int storeId, int businessId)
    {
        try
        {
            // Obtener conexión desde el repositorio
            var connection = await _manufactureRepository.GetDbConnectionAsync();
            if (connection.State != System.Data.ConnectionState.Open)
            {
                await connection.OpenAsync();
            }
            
            int? providerId = null;
            using (var command = connection.CreateCommand())
            {
                command.CommandText = @"
                    SELECT id 
                    FROM provider 
                    WHERE id_business = @businessId 
                      AND active = 1 
                    ORDER BY id ASC 
                    LIMIT 1";
                
                var param = command.CreateParameter();
                param.ParameterName = "@businessId";
                param.Value = businessId;
                command.Parameters.Add(param);
                
                var result = await command.ExecuteScalarAsync();
                if (result != null)
                {
                    providerId = Convert.ToInt32(result);
                }
            }

            if (!providerId.HasValue)
            {
                Console.WriteLine($"Warning: No active provider found for business {businessId}. Stock not created.");
                return;
            }

            // Crear el stock en la tienda usando raw SQL
            int? stockId = null;
            using (var insertCommand = connection.CreateCommand())
            {
                insertCommand.CommandText = @"
                    INSERT INTO stock (
                        product_id, id_store, provider_id, business_id, 
                        amount, cost, date, notes, is_active, created_at, updated_at
                    ) VALUES (
                        @productId, @storeId, @providerId, @businessId,
                        @amount, @cost, @date, @notes, 1, @createdAt, @updatedAt
                    );
                    SELECT LAST_INSERT_ID();";
                
                var productIdParam = insertCommand.CreateParameter();
                productIdParam.ParameterName = "@productId";
                productIdParam.Value = manufacture.ProductId;
                insertCommand.Parameters.Add(productIdParam);
                
                var storeIdParam = insertCommand.CreateParameter();
                storeIdParam.ParameterName = "@storeId";
                storeIdParam.Value = storeId;
                insertCommand.Parameters.Add(storeIdParam);
                
                var providerIdParam = insertCommand.CreateParameter();
                providerIdParam.ParameterName = "@providerId";
                providerIdParam.Value = providerId.Value;
                insertCommand.Parameters.Add(providerIdParam);
                
                var businessIdParam = insertCommand.CreateParameter();
                businessIdParam.ParameterName = "@businessId";
                businessIdParam.Value = businessId;
                insertCommand.Parameters.Add(businessIdParam);
                
                var amountParam = insertCommand.CreateParameter();
                amountParam.ParameterName = "@amount";
                amountParam.Value = manufacture.Amount;
                insertCommand.Parameters.Add(amountParam);
                
                var costParam = insertCommand.CreateParameter();
                costParam.ParameterName = "@cost";
                costParam.Value = manufacture.Cost ?? 0;
                insertCommand.Parameters.Add(costParam);
                
                var dateParam = insertCommand.CreateParameter();
                dateParam.ParameterName = "@date";
                dateParam.Value = manufacture.Date;
                insertCommand.Parameters.Add(dateParam);
                
                var notesParam = insertCommand.CreateParameter();
                notesParam.ParameterName = "@notes";
                notesParam.Value = $"Stock generado automáticamente desde manufactura ID {manufacture.Id}";
                insertCommand.Parameters.Add(notesParam);
                
                var createdAtParam = insertCommand.CreateParameter();
                createdAtParam.ParameterName = "@createdAt";
                createdAtParam.Value = DateTime.UtcNow;
                insertCommand.Parameters.Add(createdAtParam);
                
                var updatedAtParam = insertCommand.CreateParameter();
                updatedAtParam.ParameterName = "@updatedAt";
                updatedAtParam.Value = DateTime.UtcNow;
                insertCommand.Parameters.Add(updatedAtParam);
                
                var result = await insertCommand.ExecuteScalarAsync();
                if (result != null)
                {
                    stockId = Convert.ToInt32(result);
                }
            }

            // Actualizar el Manufacture con el StockId
            if (stockId.HasValue)
            {
                using (var updateCommand = connection.CreateCommand())
                {
                    updateCommand.CommandText = @"
                        UPDATE manufacture 
                        SET stock_id = @stockId, 
                            status = 'completed',
                            updated_at = @updatedAt
                        WHERE id = @manufactureId";
                    
                    var stockIdParam = updateCommand.CreateParameter();
                    stockIdParam.ParameterName = "@stockId";
                    stockIdParam.Value = stockId.Value;
                    updateCommand.Parameters.Add(stockIdParam);
                    
                    var updatedAtParam = updateCommand.CreateParameter();
                    updatedAtParam.ParameterName = "@updatedAt";
                    updatedAtParam.Value = DateTime.UtcNow;
                    updateCommand.Parameters.Add(updatedAtParam);
                    
                    var manufactureIdParam = updateCommand.CreateParameter();
                    manufactureIdParam.ParameterName = "@manufactureId";
                    manufactureIdParam.Value = manufacture.Id;
                    updateCommand.Parameters.Add(manufactureIdParam);
                    
                    await updateCommand.ExecuteNonQueryAsync();
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error creating stock from manufacture: {ex.Message}");
            // No lanzamos la excepción para no interrumpir el flujo principal
        }
    }

    /// <summary>
    /// Procesa el consumo de un insumo usando algoritmo FIFO con autoreferencia
    /// </summary>
    private async Task ProcessSupplyConsumptionAsync(CreateSupplyUsageDto supplyUsage, int processDoneId, int? createdByUserId = null)
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
                availableEntry.Id,                 // ⭐ Referencia al stock original
                createdByUserId                    // ⭐ Usuario responsable
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
                
                // Crear Manufacture en lugar de Stock
                // Los productos quedan en fábrica hasta que se envíen a las tiendas
                var manufacture = new Manufacture
                {
                    ProductId = process.ProductId,
                    ProcessDoneId = processDone.Id,
                    BusinessId = process.Product.BusinessId,
                    Amount = amount, // Cantidad producida (positivo)
                    Cost = (int)Math.Round(totalSupplyCost), // Costo total de los insumos
                    Date = DateTime.UtcNow,
                    Notes = $"Producción del proceso: {process.Name}. Costo total insumos: {totalSupplyCost:C}. Costo unitario: {unitCost:C}",
                    Status = "pending", // Pendiente de envío a tienda
                    IsActive = true
                };
                
                await _manufactureRepository.AddAsync(manufacture);
            }
        }
        
        await _processDoneRepository.UpdateAsync(processDone);
        
        var updatedProcessDone = await _processDoneRepository.GetByIdWithDetailsAsync(id);
        return MapToDto(updatedProcessDone!);
    }

    public async Task<ProcessDoneDto> AddSupplyEntryAsync(int processDoneId, CreateSupplyUsageDto supplyUsage, int? createdByUserId = null)
    {
        var processDone = await _processDoneRepository.GetByIdAsync(processDoneId);
        if (processDone == null)
            throw new KeyNotFoundException($"ProcessDone with ID {processDoneId} not found");

        // Procesar el consumo con algoritmo FIFO
        await ProcessSupplyConsumptionAsync(supplyUsage, processDoneId, createdByUserId);

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
            CreatedByUserId = processDone.CreatedByUserId,
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
            CreatedByUserId = processDone.CreatedByUserId,
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
    private async Task ProcessAllComponentConsumptionAsync(List<CreateComponentUsageDto> componentUsages, int processDoneId, Process process, int? createdByUserId = null)
    {
        if (componentUsages == null || !componentUsages.Any())
            return;

        foreach (var componentUsage in componentUsages)
        {
            await ProcessComponentConsumptionAsync(componentUsage, processDoneId, process, createdByUserId);
        }
    }

    /// <summary>
    /// Procesa el consumo de un componente usando algoritmo FIFO con autoreferencia
    /// </summary>
    private async Task ProcessComponentConsumptionAsync(CreateComponentUsageDto componentUsage, int processDoneId, Process process, int? createdByUserId = null)
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
                CreatedByUserId = createdByUserId, // ⭐ Usuario responsable
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
    
    private async Task EnrichWithUserNamesAsync(List<ProcessDoneDto> dtos)
    {
        var userIds = dtos
            .Where(d => d.CreatedByUserId.HasValue)
            .Select(d => d.CreatedByUserId!.Value)
            .Distinct()
            .ToList();
        
        if (!userIds.Any()) return;
        
        var userNames = await _userRepository.GetUserNamesByIdsAsync(userIds);
        
        foreach (var dto in dtos)
        {
            if (dto.CreatedByUserId.HasValue && userNames.TryGetValue(dto.CreatedByUserId.Value, out var userName))
            {
                dto.CreatedByUserName = userName;
            }
        }
    }
}
