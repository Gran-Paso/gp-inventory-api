using GPInventory.Application.DTOs.Production;

namespace GPInventory.Application.Interfaces;

public interface IProcessDoneService
{
    Task<ProcessDoneDto> GetProcessDoneByIdAsync(int id);
    Task<IEnumerable<ProcessDoneDto>> GetAllProcessDonesAsync();
    Task<IEnumerable<ProcessDoneDto>> GetProcessDonesByProcessIdAsync(int processId);
    Task<ProcessDoneDto> CreateProcessDoneAsync(CreateProcessDoneDto createProcessDoneDto);
    Task DeleteProcessDoneAsync(int id);
}
