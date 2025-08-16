using GPInventory.Application.DTOs.Production;
using GPInventory.Application.Interfaces;
using GPInventory.Domain.Entities;

namespace GPInventory.Application.Services;

public class ProcessDoneService : IProcessDoneService
{
    private readonly IProcessDoneRepository _processDoneRepository;
    private readonly IProcessRepository _processRepository;
    private readonly ISupplyEntryService _supplyEntryService;

    public ProcessDoneService(IProcessDoneRepository processDoneRepository, IProcessRepository processRepository, ISupplyEntryService supplyEntryService)
    {
        _processDoneRepository = processDoneRepository;
        _processRepository = processRepository;
        _supplyEntryService = supplyEntryService;
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

        // Calcular el costo total basado en los insumos utilizados
        decimal totalCost = createProcessDoneDto.SupplyUsages.Sum(su => su.QuantityUsed * su.UnitCost);

        // Crear el ProcessDone
        var processDone = new ProcessDone(
            createProcessDoneDto.ProcessId,
            createProcessDoneDto.Quantity,
            totalCost,
            DateTime.UtcNow,
            createProcessDoneDto.Notes
        );

        var createdProcessDone = await _processDoneRepository.CreateAsync(processDone);

        // Crear las entradas de suministros (SupplyEntry) para cada insumo utilizado
        foreach (var supplyUsage in createProcessDoneDto.SupplyUsages)
        {
            var createSupplyEntryDto = new CreateSupplyEntryDto
            {
                UnitCost = supplyUsage.UnitCost,
                Amount = -supplyUsage.QuantityUsed, // Negativo porque es un consumo
                ProviderId = 1, // Valor por defecto, podríamos mejorarlo
                SupplyId = supplyUsage.SupplyId,
                ProcessDoneId = createdProcessDone.Id
            };

            await _supplyEntryService.CreateAsync(createSupplyEntryDto);
        }

        // Recargar con detalles
        var processWithDetails = await _processDoneRepository.GetByIdWithDetailsAsync(createdProcessDone.Id);
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
            Quantity = processDone.Quantity,
            TotalCost = processDone.TotalCost,
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
                SupplyName = se.Supply?.Name
            }).ToList() ?? new List<SupplyUsageDto>()
        };
    }
}
