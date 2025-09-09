using GPInventory.Application.DTOs.Production;

namespace GPInventory.Application.Interfaces;

public interface ISupplyEntryService
{
    Task<IEnumerable<SupplyEntryDto>> GetAllAsync();
    Task<SupplyEntryDto?> GetByIdAsync(int id);
    Task<IEnumerable<SupplyEntryDto>> GetBySupplyIdAsync(int supplyId);
    Task<IEnumerable<SupplyEntryDto>> GetByProcessDoneIdAsync(int processDoneId);
    Task<SupplyStockDto?> GetSupplyStockAsync(int supplyId);
    Task<IEnumerable<SupplyStockDto>> GetAllSupplyStocksAsync(int? businessId = null);
    Task<SupplyEntryDto> CreateAsync(CreateSupplyEntryDto createDto);
    Task<SupplyEntryDto> UpdateAsync(int id, UpdateSupplyEntryDto updateDto);
    Task DeleteAsync(int id);
    Task<IEnumerable<SupplyEntryDto>> GetSupplyHistoryAsync(int supplyEntryId, int supplyId);
}
