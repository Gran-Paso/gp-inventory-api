using GPInventory.Application.DTOs.Payments;

namespace GPInventory.Application.Interfaces;

public interface IPaymentCatalogService
{
    Task<IEnumerable<ReceiptTypeDto>> GetReceiptTypesAsync();
    Task<IEnumerable<PaymentTypeDto>> GetPaymentTypesAsync();
    Task<IEnumerable<PaymentMethodDto>> GetPaymentMethodsAsync();
    Task<IEnumerable<BankEntityDto>> GetBankEntitiesAsync();
    Task<BankEntityDto> CreateBankEntityAsync(CreateBankEntityDto createDto);
}
