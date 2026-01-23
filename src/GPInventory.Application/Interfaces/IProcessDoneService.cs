using GPInventory.Application.DTOs.Production;

namespace GPInventory.Application.Interfaces;

public interface IProcessDoneService
{
    Task<ProcessDoneDto> GetProcessDoneByIdAsync(int id);
    Task<IEnumerable<ProcessDoneDto>> GetAllProcessDonesAsync();
    Task<IEnumerable<ProcessDoneDto>> GetProcessDonesByProcessIdAsync(int processId);
    Task<ProcessDoneDto> CreateProcessDoneAsync(CreateProcessDoneDto createProcessDoneDto);
    Task<ProcessDoneDto> UpdateProcessDoneStageAsync(int id, int stage);
    Task<ProcessDoneDto> UpdateProcessDoneAmountAsync(int id, int amount, bool isLastSupply = false);
    Task<ProcessDoneDto> AddSupplyEntryAsync(int processDoneId, CreateSupplyUsageDto supplyUsage, int? createdByUserId = null);
    Task DeleteProcessDoneAsync(int id);
}
