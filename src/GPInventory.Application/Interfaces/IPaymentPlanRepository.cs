using GPInventory.Domain.Entities;

namespace GPInventory.Application.Interfaces;

public interface IPaymentPlanRepository
{
    Task<PaymentPlan?> GetByIdAsync(int id);
    Task<IEnumerable<PaymentPlan>> GetByExpenseIdAsync(int expenseId);
    Task<IEnumerable<PaymentPlan>> GetByFixedExpenseIdAsync(int fixedExpenseId);
    Task<PaymentPlan> CreateAsync(PaymentPlan entity);
    Task DeleteAsync(int id);
}
