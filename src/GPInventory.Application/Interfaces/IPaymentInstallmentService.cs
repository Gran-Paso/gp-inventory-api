using GPInventory.Application.DTOs.Payments;

namespace GPInventory.Application.Interfaces;

public interface IPaymentInstallmentService
{
    Task<PaymentInstallmentDto> CreateInstallmentAsync(CreateInstallmentDto createDto);
    Task<IEnumerable<PaymentInstallmentDto>> CreateInstallmentsBulkAsync(CreateInstallmentsBulkDto createDto);
    Task<IEnumerable<PaymentInstallmentDto>> GetInstallmentsByPaymentPlanAsync(int paymentPlanId);
    Task<PaymentInstallmentDto> UpdateInstallmentStatusAsync(int id, UpdateInstallmentStatusDto updateDto);
    Task DeleteInstallmentAsync(int id);
    Task<InstallmentsSummaryDto> GetInstallmentsSummaryAsync(List<int>? businessIds = null);
}
