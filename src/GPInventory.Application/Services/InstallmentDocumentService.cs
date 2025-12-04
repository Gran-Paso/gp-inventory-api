using AutoMapper;
using GPInventory.Application.DTOs.Payments;
using GPInventory.Application.Interfaces;
using GPInventory.Domain.Entities;

namespace GPInventory.Application.Services;

public class InstallmentDocumentService : IInstallmentDocumentService
{
    private readonly IInstallmentDocumentRepository _repository;
    private readonly IMapper _mapper;

    public InstallmentDocumentService(
        IInstallmentDocumentRepository repository,
        IMapper mapper)
    {
        _repository = repository;
        _mapper = mapper;
    }

    public async Task<IEnumerable<InstallmentDocumentDto>> GetDocumentsByInstallmentIdAsync(int installmentId)
    {
        Console.WriteLine($"[InstallmentDocumentService] GetDocumentsByInstallmentIdAsync para installmentId: {installmentId}");
        
        var documents = await _repository.GetByInstallmentIdAsync(installmentId);
        
        Console.WriteLine($"[InstallmentDocumentService] Repository devolvió {documents.Count()} documentos");
        
        var dtos = _mapper.Map<IEnumerable<InstallmentDocumentDto>>(documents);
        
        Console.WriteLine($"[InstallmentDocumentService] AutoMapper mapeó {dtos.Count()} DTOs");
        
        return dtos;
    }

    public async Task<InstallmentDocumentDto> UploadDocumentAsync(
        int installmentId, 
        string fileName, 
        string filePath, 
        string fileType, 
        long fileSize, 
        string? notes)
    {
        var document = new InstallmentDocument
        {
            PaymentInstallmentId = installmentId,
            FileName = fileName,
            FilePath = filePath,
            FileType = fileType,
            FileSize = fileSize,
            Notes = notes,
            UploadedAt = DateTime.UtcNow
        };

        var created = await _repository.CreateAsync(document);
        return _mapper.Map<InstallmentDocumentDto>(created);
    }

    public async Task<string> GetDocumentPathAsync(int documentId)
    {
        var document = await _repository.GetByIdAsync(documentId);
        if (document == null)
            throw new KeyNotFoundException($"Document with id {documentId} not found");

        return document.FilePath;
    }

    public async Task DeleteDocumentAsync(int documentId)
    {
        await _repository.DeleteAsync(documentId);
    }
}
