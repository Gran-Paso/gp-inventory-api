namespace GPInventory.Domain.Entities;

public class InstallmentDocument
{
    public int Id { get; set; }
    public int PaymentInstallmentId { get; set; }
    public string FileName { get; set; } = string.Empty;
    public string FilePath { get; set; } = string.Empty;
    public string FileType { get; set; } = string.Empty; // image/jpeg, application/pdf, etc.
    public double FileSize { get; set; } // in bytes
    public string? Notes { get; set; }
    public DateTime UploadedAt { get; set; }
    
    // Navigation property
    public PaymentInstallment? PaymentInstallment { get; set; }
}
