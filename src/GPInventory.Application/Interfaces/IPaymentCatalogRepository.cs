using GPInventory.Domain.Entities;

namespace GPInventory.Application.Interfaces;

public interface IPaymentCatalogRepository
{
    Task<IEnumerable<ReceiptType>> GetReceiptTypesAsync();
    Task<IEnumerable<PaymentType>> GetPaymentTypesAsync();
    Task<IEnumerable<PaymentMethod>> GetPaymentMethodsAsync();
    Task<IEnumerable<BankEntity>> GetBankEntitiesAsync();
    Task<BankEntity> CreateBankEntityAsync(BankEntity entity);
}
