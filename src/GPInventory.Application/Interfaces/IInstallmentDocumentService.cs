using GPInventory.Application.DTOs.Payments;

namespace GPInventory.Application.Interfaces;

public interface IInstallmentDocumentService
{
    Task<IEnumerable<InstallmentDocumentDto>> GetDocumentsByInstallmentIdAsync(int installmentId);
    Task<InstallmentDocumentDto> UploadDocumentAsync(int installmentId, string fileName, string filePath, string fileType, long fileSize, string? notes);
    Task<string> GetDocumentPathAsync(int documentId);
    Task DeleteDocumentAsync(int documentId);
}
