using GPInventory.Domain.Entities;

namespace GPInventory.Application.Interfaces;

public interface IProcessDoneRepository
{
    Task<ProcessDone?> GetByIdAsync(int id);
    Task<ProcessDone?> GetByIdWithDetailsAsync(int id);
    Task<IEnumerable<ProcessDone>> GetAllAsync();
    Task<IEnumerable<ProcessDone>> GetByProcessIdAsync(int processId);
    Task<ProcessDone> CreateAsync(ProcessDone processDone);
    Task<ProcessDone> UpdateAsync(ProcessDone processDone);
    Task DeleteAsync(int id);
}
