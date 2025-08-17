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

    public ProcessDoneService(IProcessDoneRepository processDoneRepository, IProcessRepository processRepository, ISupplyEntryRepository supplyEntryRepository, IStockRepository stockRepository)
    {
        _processDoneRepository = processDoneRepository;
        _processRepository = processRepository;
        _supplyEntryRepository = supplyEntryRepository;
        _stockRepository = stockRepository;
    }

    public async Task<ProcessDoneDto> GetProcessDoneByIdAsync(int id)
    {
        var processDone = await _processDoneRepository.GetByIdWithDetailsAsync(id);
        if (processDone == null)
            throw new KeyNotFoundException($"ProcessDone with ID {id} not found");

        return MapToDto(processDone);
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

        // Crear las entradas de suministros (SupplyEntry) para cada insumo utilizado
        foreach (var supplyUsage in createProcessDoneDto.SupplyUsages)
        {
            var supplyEntry = new SupplyEntry(
                (int)supplyUsage.UnitCost,
                -(int)supplyUsage.QuantityUsed, // Negativo porque es un consumo
                1, // ProviderId por defecto, podríamos mejorarlo
                supplyUsage.SupplyId,
                createdProcessDone.Id
            );

            await _supplyEntryRepository.CreateAsync(supplyEntry);
        }

        // Recargar con detalles
        var processWithDetails = await _processDoneRepository.GetByIdWithDetailsAsync(createdProcessDone.Id);
        return MapToDto(processWithDetails!);
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
                    Notes = $"Producción del proceso: {process.Name}. Costo total insumos: {totalSupplyCost:C}. Costo unitario: {(amount > 0 ? totalSupplyCost / amount : 0):C}"
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

        // Crear la entrada de suministro negativa
        var supplyEntry = new SupplyEntry(
            (int)supplyUsage.UnitCost,
            -(int)supplyUsage.QuantityUsed, // Negativo porque es un consumo
            1, // ProviderId por defecto
            supplyUsage.SupplyId,
            processDoneId
        );

        await _supplyEntryRepository.CreateAsync(supplyEntry);

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
            SupplyUsages = processDone.SupplyEntries?.Select(se => new SupplyUsageDto
            {
                Id = se.Id,
                SupplyId = se.SupplyId,
                QuantityUsed = se.Amount,
                UnitCost = se.UnitCost,
                TotalCost = se.Amount * se.UnitCost,
                SupplyName = $"Insumo {se.SupplyId}" // Temporal, podríamos hacer una consulta separada si necesitamos el nombre
            }).ToList() ?? new List<SupplyUsageDto>()
        };
    }
}
