using GPInventory.Domain.Entities;

namespace GPInventory.Application.Interfaces;

public interface IInstallmentDocumentRepository
{
    Task<InstallmentDocument?> GetByIdAsync(int id);
    Task<IEnumerable<InstallmentDocument>> GetByInstallmentIdAsync(int installmentId);
    Task<InstallmentDocument> CreateAsync(InstallmentDocument document);
    Task DeleteAsync(int id);
}
