using GPInventory.Application.DTOs.Production;
using GPInventory.Application.Interfaces;
using GPInventory.Domain.Entities;
using GPInventory.Application.DTOs.Components;

namespace GPInventory.Application.Services;

public class ProcessService : IProcessService
{
    private readonly IProcessRepository _processRepository;
    private readonly ISupplyRepository _supplyRepository;
    private readonly IComponentRepository _componentRepository;

    public ProcessService(IProcessRepository processRepository, ISupplyRepository supplyRepository, IComponentRepository componentRepository)
    {
        _processRepository = processRepository;
        _supplyRepository = supplyRepository;
        _componentRepository = componentRepository;
    }

    public async Task<ProcessDto> GetProcessByIdAsync(int id)
    {
        var process = await _processRepository.GetByIdWithDetailsAsync(id);
        if (process == null)
            throw new KeyNotFoundException($"Process with ID {id} not found");

        return MapToDto(process);
    }

    public async Task<IEnumerable<ProcessDto>> GetAllProcessesAsync()
    {
        var processes = await _processRepository.GetProcessesWithDetailsAsync();
        return processes.Select(MapToDto);
    }

    public async Task<IEnumerable<ProcessDto>> GetProcessesByStoreIdAsync(int storeId)
    {
        var processes = await _processRepository.GetProcessesWithDetailsAsync(new[] { storeId });
        return processes.Select(MapToDto);
    }

    public async Task<IEnumerable<ProcessDto>> GetProcessesByProductIdAsync(int productId)
    {
        var processes = await _processRepository.GetProcessesWithDetailsAsync();
        return processes.Where(p => p.ProductId == productId).Select(MapToDto);
    }

    public async Task<IEnumerable<ProcessDto>> GetActiveProcessesAsync(int storeId)
    {
        var processes = await _processRepository.GetProcessesWithDetailsAsync(new[] { storeId });
        return processes.Where(p => p.IsActive).Select(MapToDto);
    }

    public async Task<ProcessDto> CreateProcessAsync(CreateProcessDto createProcessDto)
    {
        // Verificar si ya existe un proceso con el mismo nombre en la tienda
        var existingProcess = await _processRepository.GetByNameAsync(createProcessDto.Name, createProcessDto.StoreId);
        if (existingProcess != null)
            throw new InvalidOperationException($"A process with name '{createProcessDto.Name}' already exists in this store");

        // Crear el proceso
        var process = new Process(
            createProcessDto.ProductId,
            createProcessDto.Name,
            createProcessDto.ProductionTime,
            createProcessDto.TimeUnitId,
            createProcessDto.StoreId,
            createProcessDto.Description
        );

        var createdProcess = await _processRepository.CreateAsync(process);

        // Crear las relaciones ProcessSupply
        if (createProcessDto.ProcessSupplies.Any())
        {
            foreach (var supplyDto in createProcessDto.ProcessSupplies)
            {
                var processSupply = new ProcessSupply(createdProcess.Id, supplyDto.SupplyId, supplyDto.Order);
                createdProcess.ProcessSupplies.Add(processSupply);
            }
        }

        // Crear las relaciones ProcessComponent
        if (createProcessDto.ProcessComponents.Any())
        {
            foreach (var componentDto in createProcessDto.ProcessComponents)
            {
                var processComponent = new ProcessComponent(createdProcess.Id, componentDto.ComponentId, componentDto.Order);
                createdProcess.ProcessComponents.Add(processComponent);
            }
        }

        if (createProcessDto.ProcessSupplies.Any() || createProcessDto.ProcessComponents.Any())
        {
            await _processRepository.UpdateAsync(createdProcess);
        }

        // Recargar con detalles
        var processWithDetails = await _processRepository.GetByIdWithDetailsAsync(createdProcess.Id);
        return MapToDto(processWithDetails!);
    }

    public async Task<ProcessDto> UpdateProcessAsync(int id, UpdateProcessDto updateProcessDto)
    {
        var process = await _processRepository.GetByIdWithDetailsAsync(id);
        if (process == null)
            throw new KeyNotFoundException($"Process with ID {id} not found");

        // Verificar si el nombre ya existe en otra proceso de la misma tienda
        var existingProcess = await _processRepository.GetByNameAsync(updateProcessDto.Name, process.StoreId);
        if (existingProcess != null && existingProcess.Id != id)
            throw new InvalidOperationException($"A process with name '{updateProcessDto.Name}' already exists in this store");

        // Actualizar propiedades
        process.Name = updateProcessDto.Name;
        process.Description = updateProcessDto.Description;
        process.ProductionTime = updateProcessDto.ProductionTime;
        process.TimeUnitId = updateProcessDto.TimeUnitId;

        // Actualizar ProcessSupplies (eliminar existentes y crear nuevos)
        process.ProcessSupplies.Clear();
        foreach (var supplyDto in updateProcessDto.ProcessSupplies)
        {
            var processSupply = new ProcessSupply(process.Id, supplyDto.SupplyId, supplyDto.Order);
            process.ProcessSupplies.Add(processSupply);
        }

        // Actualizar ProcessComponents (eliminar existentes y crear nuevos)
        process.ProcessComponents.Clear();
        foreach (var componentDto in updateProcessDto.ProcessComponents)
        {
            var processComponent = new ProcessComponent(process.Id, componentDto.ComponentId, componentDto.Order);
            process.ProcessComponents.Add(processComponent);
        }

        var updatedProcess = await _processRepository.UpdateAsync(process);
        return MapToDto(updatedProcess);
    }

    public async Task DeleteProcessAsync(int id)
    {
        // Obtener el proceso con sus dependencias
        var process = await _processRepository.GetByIdWithDetailsAsync(id);
        if (process == null)
            throw new KeyNotFoundException($"Process with ID {id} not found");

        // Si el proceso tiene ProcessDones, no permitir eliminación
        if (process.ProcessDones?.Any() == true)
        {
            throw new InvalidOperationException("Cannot delete process: Process has completed executions. Consider deactivating it instead.");
        }

        // Si el proceso tiene ProcessSupplies o ProcessComponents, eliminarlos primero
        if (process.ProcessSupplies?.Any() == true || process.ProcessComponents?.Any() == true)
        {
            // Limpiar las supplies y components del proceso
            process.ProcessSupplies?.Clear();
            process.ProcessComponents.Clear();
            await _processRepository.UpdateAsync(process);
        }

        // Ahora eliminar el proceso
        await _processRepository.DeleteAsync(id);
    }

    public async Task<ProcessDto> DeactivateProcessAsync(int id)
    {
        var process = await _processRepository.GetByIdAsync(id);
        if (process == null)
        {
            throw new InvalidOperationException($"Process with id {id} not found");
        }

        // Desactivar el proceso estableciendo IsActive a false
        process.IsActive = false;
        await _processRepository.UpdateAsync(process);
        
        return MapToDto(process);
    }

    public async Task<ProcessDto> ActivateProcessAsync(int id)
    {
        var process = await _processRepository.GetByIdAsync(id);
        if (process == null)
        {
            throw new InvalidOperationException($"Process with id {id} not found");
        }

        // Activar el proceso estableciendo IsActive a true
        process.IsActive = true;
        await _processRepository.UpdateAsync(process);
        
        return MapToDto(process);
    }

    public async Task<IEnumerable<ProcessDto>> GetProcessesWithDetailsAsync(int[]? storeIds = null, int? businessId = null)
    {
        var processes = await _processRepository.GetProcessesWithDetailsAsync(storeIds, businessId);
        var processDtos = new List<ProcessDto>();

        foreach (var process in processes)
        {
            var dto = MapToDto(process);

            // Populate statistics
            try
            {
                dto.AverageCost = await _processRepository.GetAverageCostAsync(process.Id);
                
                var lastExecution = await _processRepository.GetLastExecutionAsync(process.Id);
                if (lastExecution.HasValue)
                {
                    dto.LastExecutionDate = lastExecution.Value.date;
                    dto.LastExecutionUser = lastExecution.Value.userName;
                    dto.LastExecutionAmount = lastExecution.Value.amount;
                }

                var stockStatus = await _processRepository.GetProcessSuppliesStockStatusAsync(process.Id);
                dto.StockStatus = (ProcessStockStatus)stockStatus;

                dto.ExecutionCount = await _processRepository.GetExecutionCountAsync(process.Id);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error loading statistics for process {process.Id}: {ex.Message}");
                // Continue with default values
            }

            processDtos.Add(dto);
        }

        return processDtos;
    }

    public async Task<IEnumerable<ProcessDto>> GetProcessesWithFiltersAsync(ProcessFilterDto filter)
    {
        // Get all processes with details
        var processes = await GetProcessesWithDetailsAsync(filter.StoreIds, filter.BusinessId);

        // Apply filters
        var query = processes.AsEnumerable();

        // Search filter
        if (!string.IsNullOrWhiteSpace(filter.Search))
        {
            var searchLower = filter.Search.ToLower();
            query = query.Where(p =>
                p.Name.ToLower().Contains(searchLower) ||
                (p.Description != null && p.Description.ToLower().Contains(searchLower)) ||
                (p.Product != null && p.Product.Name.ToLower().Contains(searchLower))
            );
        }

        // IsActive filter
        if (filter.IsActive.HasValue)
        {
            query = query.Where(p => p.IsActive == filter.IsActive.Value);
        }

        // Stock status filter
        if (filter.StockStatus.HasValue)
        {
            query = query.Where(p => p.StockStatus == filter.StockStatus.Value);
        }

        // Apply sorting
        query = filter.SortBy switch
        {
            ProcessSortBy.ExecutionFrequency => filter.SortDescending
                ? query.OrderByDescending(p => p.ExecutionCount)
                : query.OrderBy(p => p.ExecutionCount),

            ProcessSortBy.LastExecutionDate => filter.SortDescending
                ? query.OrderByDescending(p => p.LastExecutionDate ?? DateTime.MinValue)
                : query.OrderBy(p => p.LastExecutionDate ?? DateTime.MinValue),

            ProcessSortBy.ProductionTime => filter.SortDescending
                ? query.OrderByDescending(p => p.ProductionTime)
                : query.OrderBy(p => p.ProductionTime),

            ProcessSortBy.CriticalStock => filter.SortDescending
                ? query.OrderByDescending(p => (int)p.StockStatus)
                : query.OrderBy(p => (int)p.StockStatus),

            _ => filter.SortDescending
                ? query.OrderByDescending(p => p.Name)
                : query.OrderBy(p => p.Name),
        };

        return query.ToList();
    }

    private static ProcessDto MapToDto(Process process)
    {
        return new ProcessDto
        {
            Id = process.Id,
            ProductId = process.ProductId,
            Name = process.Name,
            Description = process.Description,
            ProductionTime = process.ProductionTime,
            TimeUnitId = process.TimeUnitId,
            StoreId = process.StoreId,
            CreatedAt = process.CreatedAt,
            UpdatedAt = process.UpdatedAt,
            IsActive = process.IsActive,
            Product = process.Product != null ? new ProductDto
            {
                Id = process.Product.Id,
                Name = process.Product.Name,
                Price = process.Product.Price
            } : null,
            TimeUnit = process.TimeUnit != null ? new TimeUnitDto
            {
                Id = process.TimeUnit.Id,
                Name = process.TimeUnit.Name,
                Description = process.TimeUnit.Description,
                CreatedAt = process.TimeUnit.CreatedAt,
                UpdatedAt = process.TimeUnit.UpdatedAt,
                IsActive = process.TimeUnit.IsActive
            } : null,
            Store = process.Store != null ? new StoreDto
            {
                Id = process.Store.Id,
                Name = process.Store.Name!,
                Location = process.Store.Location
            } : null,
            ProcessSupplies = process.ProcessSupplies?.Select(ps => new ProcessSupplyDto
            {
                Id = ps.Id,
                ProcessId = ps.ProcessId,
                SupplyId = ps.SupplyId,
                Order = ps.Order,
                CreatedAt = ps.CreatedAt,
                UpdatedAt = ps.UpdatedAt,
                IsActive = ps.IsActive,
                Supply = ps.Supply != null ? new SupplyDto
                {
                    Id = ps.Supply.Id,
                    Name = ps.Supply.Name,
                    Description = ps.Supply.Description,
                    BusinessId = ps.Supply.BusinessId,
                    StoreId = ps.Supply.StoreId,
                    FixedExpenseId = ps.Supply.FixedExpenseId,
                    Active = ps.Supply.Active,
                    CreatedAt = ps.Supply.CreatedAt,
                    UpdatedAt = ps.Supply.UpdatedAt
                } : null
            }).OrderBy(ps => ps.Order).ToList() ?? new List<ProcessSupplyDto>(),
            ProcessComponents = process.ProcessComponents?.Select(pc => new ProcessComponentDto
            {
                Id = pc.Id,
                ProcessId = pc.ProcessId,
                ComponentId = pc.ComponentId,
                Order = pc.Order,
                CreatedAt = pc.CreatedAt,
                UpdatedAt = pc.UpdatedAt,
                IsActive = pc.IsActive,
                Component = pc.Component != null ? new ComponentDto
                {
                    Id = pc.Component.Id,
                    Name = pc.Component.Name,
                    Description = pc.Component.Description,
                    BusinessId = pc.Component.BusinessId,
                    UnitMeasureId = pc.Component.UnitMeasureId,
                    Active = pc.Component.Active,
                    CreatedAt = pc.Component.CreatedAt,
                    UpdatedAt = pc.Component.UpdatedAt
                } : null
            }).OrderBy(pc => pc.Order).ToList() ?? new List<ProcessComponentDto>()
        };
    }
}
