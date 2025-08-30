using GPInventory.Application.DTOs.Production;

namespace GPInventory.Application.Interfaces;

public interface IProcessService
{
    Task<ProcessDto> GetProcessByIdAsync(int id);
    Task<IEnumerable<ProcessDto>> GetAllProcessesAsync();
    Task<IEnumerable<ProcessDto>> GetProcessesByStoreIdAsync(int storeId);
    Task<IEnumerable<ProcessDto>> GetProcessesByProductIdAsync(int productId);
    Task<IEnumerable<ProcessDto>> GetActiveProcessesAsync(int storeId);
    Task<ProcessDto> CreateProcessAsync(CreateProcessDto createProcessDto);
    Task<ProcessDto> UpdateProcessAsync(int id, UpdateProcessDto updateProcessDto);
    Task DeleteProcessAsync(int id);
    Task<ProcessDto> DeactivateProcessAsync(int id);
    Task<IEnumerable<ProcessDto>> GetProcessesWithDetailsAsync(int[]? storeIds = null, int? businessId = null);
}
