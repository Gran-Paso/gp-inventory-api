using GPInventory.Domain.Entities;

namespace GPInventory.Application.Interfaces;

public interface IPaymentInstallmentRepository
{
    Task<PaymentInstallment> CreateAsync(PaymentInstallment entity);
    Task<IEnumerable<PaymentInstallment>> CreateBulkAsync(IEnumerable<PaymentInstallment> entities);
    Task<IEnumerable<PaymentInstallment>> GetByPaymentPlanIdAsync(int paymentPlanId);
    Task<PaymentInstallment?> GetByIdAsync(int id);
    Task UpdateAsync(PaymentInstallment entity);
    Task DeleteAsync(int id);
    Task<IEnumerable<PaymentInstallment>> GetAllInstallmentsAsync(List<int>? businessIds = null);
}
