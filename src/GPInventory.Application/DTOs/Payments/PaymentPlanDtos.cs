namespace GPInventory.Application.DTOs.Payments;

public class PaymentPlanDto
{
    public int Id { get; set; }
    public int? ExpenseId { get; set; }
    public int? FixedExpenseId { get; set; }
    public int PaymentTypeId { get; set; }
    public bool ExpressedInUf { get; set; }
    public int? BankEntityId { get; set; }
    public int InstallmentsCount { get; set; }
    public DateTime StartDate { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }

    // Navigation properties
    public string? PaymentTypeName { get; set; }
    public string? BankEntityName { get; set; }
}

public class PaymentPlanWithInstallmentsDto : PaymentPlanDto
{
    public List<PaymentInstallmentDto> Installments { get; set; } = new();
}

public class CreatePaymentPlanDto
{
    public int? ExpenseId { get; set; }
    public int? FixedExpenseId { get; set; }
    public int PaymentTypeId { get; set; }
    public bool ExpressedInUf { get; set; }
    public int? BankEntityId { get; set; }
    public int InstallmentsCount { get; set; }
    public DateTime StartDate { get; set; }
}
