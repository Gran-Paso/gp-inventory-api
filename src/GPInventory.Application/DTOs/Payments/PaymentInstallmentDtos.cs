namespace GPInventory.Application.DTOs.Payments;

public class PaymentInstallmentDto
{
    public int Id { get; set; }
    public int PaymentPlanId { get; set; }
    public int InstallmentNumber { get; set; }
    public DateTime DueDate { get; set; }
    public decimal AmountClp { get; set; }
    public decimal? AmountUf { get; set; }
    public string Status { get; set; } = "pendiente";
    public DateTime? PaidDate { get; set; }
    public int? PaymentMethodId { get; set; }
    public int? ExpenseId { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }

    // Navigation properties
    public string? PaymentMethodName { get; set; }
}

public class CreateInstallmentDto
{
    public int PaymentPlanId { get; set; }
    public int InstallmentNumber { get; set; }
    public DateTime DueDate { get; set; }
    public decimal AmountClp { get; set; }
    public decimal? AmountUf { get; set; }
    public string Status { get; set; } = "pendiente";
    public DateTime? PaidDate { get; set; }
    public int? PaymentMethodId { get; set; }
    public int? ExpenseId { get; set; }
}

public class CreateInstallmentsBulkDto
{
    public List<CreateInstallmentDto> Installments { get; set; } = new();
}

public class UpdateInstallmentStatusDto
{
    public string Status { get; set; } = string.Empty;
    public DateTime? PaidDate { get; set; }
    public int? PaymentMethodId { get; set; }
    public int? ExpenseId { get; set; }
}

public class PayInstallmentDto
{
    public int PaymentMethodId { get; set; }
    public DateTime PaymentDate { get; set; }
}

public class InstallmentsSummaryDto
{
    public decimal TotalPending { get; set; }
    public decimal TotalPaid { get; set; }
    public decimal TotalOverdue { get; set; }
    public int TotalInstallments { get; set; }
    public int PendingInstallments { get; set; }
    public int PaidInstallments { get; set; }
    public int OverdueInstallments { get; set; }
    public int SinglePaymentsCount { get; set; }
    public int InstallmentsOnlyCount { get; set; }
}
