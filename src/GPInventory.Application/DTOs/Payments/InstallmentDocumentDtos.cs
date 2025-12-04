namespace GPInventory.Application.DTOs.Payments;

public class InstallmentDocumentDto
{
    public int Id { get; set; }
    public int PaymentInstallmentId { get; set; }
    public string FileName { get; set; } = string.Empty;
    public string FilePath { get; set; } = string.Empty;
    public string FileType { get; set; } = string.Empty;
    public long FileSize { get; set; }
    public string? Notes { get; set; }
    public DateTime UploadedAt { get; set; }
}

public class UploadInstallmentDocumentDto
{
    public int PaymentInstallmentId { get; set; }
    public string? Notes { get; set; }
    // El archivo se enviará como IFormFile en el controller
}
