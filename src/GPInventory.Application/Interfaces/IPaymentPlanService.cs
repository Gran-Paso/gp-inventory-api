using GPInventory.Application.DTOs.Payments;

namespace GPInventory.Application.Interfaces;

public interface IPaymentPlanService
{
    Task<PaymentPlanDto> GetPaymentPlanByIdAsync(int id);
    Task<IEnumerable<PaymentPlanDto>> GetPaymentPlansByFixedExpenseAsync(int fixedExpenseId);
    Task<PaymentPlanDto> CreatePaymentPlanAsync(CreatePaymentPlanDto createDto);
    Task DeletePaymentPlanAsync(int id);
}
