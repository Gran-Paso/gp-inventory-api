using GPInventory.Application.DTOs.Production;
using GPInventory.Application.Interfaces;
using GPInventory.Domain.Entities;

namespace GPInventory.Application.Services;

public class SupplyEntryService : ISupplyEntryService
{
    private readonly ISupplyEntryRepository _repository;
    private readonly ISupplyRepository _supplyRepository;
    private readonly IUnitMeasureRepository _unitMeasureRepository;

    public SupplyEntryService(
        ISupplyEntryRepository repository,
        ISupplyRepository supplyRepository,
        IUnitMeasureRepository unitMeasureRepository)
    {
        _repository = repository;
        _supplyRepository = supplyRepository;
        _unitMeasureRepository = unitMeasureRepository;
    }

    public async Task<IEnumerable<SupplyEntryDto>> GetAllAsync()
    {
        var supplyEntries = await _repository.GetAllAsync();
        return supplyEntries.Select(MapToDto);
    }

    public async Task<SupplyEntryDto?> GetByIdAsync(int id)
    {
        var supplyEntry = await _repository.GetByIdAsync(id);
        return supplyEntry != null ? MapToDto(supplyEntry) : null;
    }

    public async Task<IEnumerable<SupplyEntryDto>> GetBySupplyIdAsync(int supplyId)
    {
        var supplyEntries = await _repository.GetBySupplyIdAsync(supplyId);
        return supplyEntries.Select(MapToDto);
    }

    public async Task<IEnumerable<SupplyEntryDto>> GetByProcessDoneIdAsync(int processDoneId)
    {
        var supplyEntries = await _repository.GetByProcessDoneIdAsync(processDoneId);
        return supplyEntries.Select(MapToDto);
    }

    public async Task<SupplyStockDto?> GetSupplyStockAsync(int supplyId)
    {
        var supply = await _supplyRepository.GetByIdAsync(supplyId);
        if (supply == null) return null;

        var currentStock = await _repository.GetCurrentStockAsync(supplyId);
        var supplyEntries = await _repository.GetBySupplyIdAsync(supplyId);
        
        var unitMeasure = await _unitMeasureRepository.GetByIdAsync(supply.UnitMeasureId);

        var totalIncoming = supplyEntries.Where(se => se.Amount > 0).Sum(se => se.Amount);
        var totalOutgoing = Math.Abs(supplyEntries.Where(se => se.Amount < 0).Sum(se => se.Amount));

        return new SupplyStockDto
        {
            SupplyId = supply.Id,
            SupplyName = supply.Name,
            CurrentStock = currentStock,
            UnitMeasureName = unitMeasure?.Name ?? "Unknown",
            UnitMeasureSymbol = unitMeasure?.Symbol,
            TotalIncoming = totalIncoming,
            TotalOutgoing = totalOutgoing
        };
    }

    public async Task<IEnumerable<SupplyStockDto>> GetAllSupplyStocksAsync()
    {
        var supplies = await _supplyRepository.GetAllAsync();
        var stockList = new List<SupplyStockDto>();

        foreach (var supply in supplies)
        {
            var stock = await GetSupplyStockAsync(supply.Id);
            if (stock != null)
            {
                stockList.Add(stock);
            }
        }

        return stockList.OrderBy(s => s.SupplyName);
    }

    public async Task<SupplyEntryDto> CreateAsync(CreateSupplyEntryDto createDto)
    {
        var supplyEntry = new SupplyEntry(
            createDto.UnitCost,
            createDto.Amount,
            createDto.ProviderId,
            createDto.SupplyId,
            createDto.ProcessDoneId
        );

        var created = await _repository.CreateAsync(supplyEntry);
        
        // Return a simple DTO without navigation properties for creation
        return new SupplyEntryDto
        {
            Id = created.Id,
            UnitCost = created.UnitCost,
            Amount = created.Amount,
            ProviderId = created.ProviderId,
            SupplyId = created.SupplyId,
            ProcessDoneId = created.ProcessDoneId,
            CreatedAt = created.CreatedAt,
            UpdatedAt = created.UpdatedAt
        };
    }

    public async Task<SupplyEntryDto> UpdateAsync(int id, UpdateSupplyEntryDto updateDto)
    {
        var supplyEntry = await _repository.GetByIdAsync(id);
        if (supplyEntry == null)
            throw new InvalidOperationException($"SupplyEntry with id {id} not found");

        supplyEntry.UnitCost = updateDto.UnitCost;
        supplyEntry.Amount = updateDto.Amount;
        supplyEntry.ProviderId = updateDto.ProviderId;

        var updated = await _repository.UpdateAsync(supplyEntry);
        return MapToDto(updated);
    }

    public async Task DeleteAsync(int id)
    {
        await _repository.DeleteAsync(id);
    }

    public async Task<IEnumerable<SupplyEntryDto>> GetSupplyHistoryAsync(int supplyId)
    {
        var supplyEntries = await _repository.GetSupplyHistoryAsync(supplyId);
        return supplyEntries.Select(MapToDto);
    }

    private static SupplyEntryDto MapToDto(SupplyEntry supplyEntry)
    {
        return new SupplyEntryDto
        {
            Id = supplyEntry.Id,
            UnitCost = supplyEntry.UnitCost,
            Amount = supplyEntry.Amount,
            ProviderId = supplyEntry.ProviderId,
            SupplyId = supplyEntry.SupplyId,
            ProcessDoneId = supplyEntry.ProcessDoneId,
            CreatedAt = supplyEntry.CreatedAt,
            UpdatedAt = supplyEntry.UpdatedAt,
            Provider = supplyEntry.Provider != null ? new ProviderDto
            {
                Id = supplyEntry.Provider.Id,
                Name = supplyEntry.Provider.Name,
                StoreId = supplyEntry.Provider.StoreId
            } : null,
            Supply = supplyEntry.Supply != null ? new SupplyDto
            {
                Id = supplyEntry.Supply.Id,
                Name = supplyEntry.Supply.Name,
                Description = supplyEntry.Supply.Description,
                UnitMeasureId = supplyEntry.Supply.UnitMeasureId,
                FixedExpenseId = supplyEntry.Supply.FixedExpenseId,
                Active = supplyEntry.Supply.Active,
                BusinessId = supplyEntry.Supply.BusinessId,
                StoreId = supplyEntry.Supply.StoreId,
                CreatedAt = supplyEntry.Supply.CreatedAt,
                UpdatedAt = supplyEntry.Supply.UpdatedAt
            } : null,
            ProcessDone = supplyEntry.ProcessDone != null ? new ProcessDoneDto
            {
                Id = supplyEntry.ProcessDone.Id,
                ProcessId = supplyEntry.ProcessDone.ProcessId,
                Quantity = supplyEntry.ProcessDone.Quantity,
                TotalCost = supplyEntry.ProcessDone.TotalCost,
                CompletedAt = supplyEntry.ProcessDone.CompletedAt,
                Notes = supplyEntry.ProcessDone.Notes,
                CreatedAt = supplyEntry.ProcessDone.CreatedAt,
                UpdatedAt = supplyEntry.ProcessDone.UpdatedAt,
                IsActive = true
            } : null
        };
    }
}
