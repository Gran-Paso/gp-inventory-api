using GPInventory.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GPInventory.Api.Controllers;

[ApiController]
[Route("api/installment-documents")]
[Authorize]
public class InstallmentDocumentsController : ControllerBase
{
    private readonly IInstallmentDocumentService _documentService;
    private readonly ILogger<InstallmentDocumentsController> _logger;
    private readonly IWebHostEnvironment _environment;

    public InstallmentDocumentsController(
        IInstallmentDocumentService documentService,
        ILogger<InstallmentDocumentsController> logger,
        IWebHostEnvironment environment)
    {
        _documentService = documentService;
        _logger = logger;
        _environment = environment;
    }

    [HttpGet("installment/{installmentId}")]
    public async Task<IActionResult> GetByInstallment(int installmentId)
    {
        try
        {
            _logger.LogInformation("GET installment-documents/installment/{InstallmentId}", installmentId);
            
            var documents = await _documentService.GetDocumentsByInstallmentIdAsync(installmentId);
            
            var documentsList = documents.ToList();
            _logger.LogInformation("Returning {Count} documents for installment {InstallmentId}", documentsList.Count, installmentId);
            
            return Ok(documentsList);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving documents for installment: {InstallmentId}", installmentId);
            return StatusCode(500, new { message = "Error al obtener los documentos" });
        }
    }

    [HttpPost("upload")]
    [RequestSizeLimit(10 * 1024 * 1024)] // 10MB limit
    public async Task<IActionResult> Upload([FromForm] int installmentId, [FromForm] string? notes, IFormFile file)
    {
        try
        {
            if (file == null || file.Length == 0)
            {
                return BadRequest(new { message = "No se proporcionó ningún archivo" });
            }

            // Validate file type
            var allowedTypes = new[] { "image/jpeg", "image/png", "image/jpg", "image/gif", "application/pdf" };
            if (!allowedTypes.Contains(file.ContentType.ToLower()))
            {
                return BadRequest(new { message = "Tipo de archivo no permitido. Solo imágenes y PDF." });
            }

            // Validate file size (10MB)
            if (file.Length > 10 * 1024 * 1024)
            {
                return BadRequest(new { message = "El archivo es demasiado grande. Máximo 10MB." });
            }

            // Create uploads directory if it doesn't exist
            var uploadsPath = Path.Combine(_environment.ContentRootPath, "uploads", "installments");
            if (!Directory.Exists(uploadsPath))
            {
                Directory.CreateDirectory(uploadsPath);
            }

            // Generate unique filename
            var fileExtension = Path.GetExtension(file.FileName);
            var uniqueFileName = $"{installmentId}_{Guid.NewGuid()}{fileExtension}";
            var filePath = Path.Combine(uploadsPath, uniqueFileName);

            // Save file
            using (var stream = new FileStream(filePath, FileMode.Create))
            {
                await file.CopyToAsync(stream);
            }

            // Save document record
            var document = await _documentService.UploadDocumentAsync(
                installmentId,
                file.FileName,
                filePath,
                file.ContentType,
                file.Length,
                notes
            );

            return Ok(document);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error uploading document for installment: {InstallmentId}", installmentId);
            return StatusCode(500, new { message = "Error al subir el documento" });
        }
    }

    [HttpGet("{id}/download")]
    public async Task<IActionResult> Download(int id)
    {
        try
        {
            var filePath = await _documentService.GetDocumentPathAsync(id);
            
            if (!System.IO.File.Exists(filePath))
            {
                return NotFound(new { message = "Archivo no encontrado" });
            }

            var memory = new MemoryStream();
            using (var stream = new FileStream(filePath, FileMode.Open))
            {
                await stream.CopyToAsync(memory);
            }
            memory.Position = 0;

            var contentType = "application/octet-stream";
            var fileName = Path.GetFileName(filePath);

            return File(memory, contentType, fileName);
        }
        catch (KeyNotFoundException ex)
        {
            _logger.LogWarning(ex, "Document not found: {Id}", id);
            return NotFound(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error downloading document: {Id}", id);
            return StatusCode(500, new { message = "Error al descargar el documento" });
        }
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(int id)
    {
        try
        {
            // Get file path before deleting record
            var filePath = await _documentService.GetDocumentPathAsync(id);
            
            // Delete from database
            await _documentService.DeleteDocumentAsync(id);
            
            // Delete physical file
            if (System.IO.File.Exists(filePath))
            {
                System.IO.File.Delete(filePath);
            }

            return NoContent();
        }
        catch (KeyNotFoundException ex)
        {
            _logger.LogWarning(ex, "Document not found: {Id}", id);
            return NotFound(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting document: {Id}", id);
            return StatusCode(500, new { message = "Error al eliminar el documento" });
        }
    }
}
