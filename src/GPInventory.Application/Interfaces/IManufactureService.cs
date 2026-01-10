using GPInventory.Application.DTOs.Production;

namespace GPInventory.Application.Interfaces;

public interface IManufactureService
{
    Task<ManufactureDto> GetByIdAsync(int id);
    Task<IEnumerable<ManufactureDto>> GetAllAsync();
    Task<IEnumerable<ManufactureDto>> GetByBusinessIdAsync(int businessId);
    Task<IEnumerable<ManufactureDto>> GetByProcessDoneIdAsync(int processDoneId);
    Task<IEnumerable<ManufactureDto>> GetPendingAsync(int businessId);
    Task<IEnumerable<ProcessDoneSummaryDto>> GetProcessDoneSummariesAsync(int businessId);
    Task<ManufactureDto> CreateAsync(CreateManufactureDto createDto);
    Task<ManufactureDto> UpdateAsync(int id, UpdateManufactureDto updateDto);
    Task DeleteAsync(int id);
}
