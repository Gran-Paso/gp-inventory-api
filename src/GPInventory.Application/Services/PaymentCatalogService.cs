using AutoMapper;
using GPInventory.Application.DTOs.Payments;
using GPInventory.Application.Interfaces;
using GPInventory.Domain.Entities;

namespace GPInventory.Application.Services;

public class PaymentCatalogService : IPaymentCatalogService
{
    private readonly IPaymentCatalogRepository _repository;
    private readonly IMapper _mapper;

    public PaymentCatalogService(
        IPaymentCatalogRepository repository,
        IMapper mapper)
    {
        _repository = repository;
        _mapper = mapper;
    }

    public async Task<IEnumerable<ReceiptTypeDto>> GetReceiptTypesAsync()
    {
        var receiptTypes = await _repository.GetReceiptTypesAsync();
        return _mapper.Map<IEnumerable<ReceiptTypeDto>>(receiptTypes);
    }

    public async Task<IEnumerable<PaymentTypeDto>> GetPaymentTypesAsync()
    {
        var paymentTypes = await _repository.GetPaymentTypesAsync();
        return _mapper.Map<IEnumerable<PaymentTypeDto>>(paymentTypes);
    }

    public async Task<IEnumerable<PaymentMethodDto>> GetPaymentMethodsAsync()
    {
        var paymentMethods = await _repository.GetPaymentMethodsAsync();
        return _mapper.Map<IEnumerable<PaymentMethodDto>>(paymentMethods);
    }

    public async Task<IEnumerable<BankEntityDto>> GetBankEntitiesAsync()
    {
        var bankEntities = await _repository.GetBankEntitiesAsync();
        return _mapper.Map<IEnumerable<BankEntityDto>>(bankEntities);
    }

    public async Task<BankEntityDto> CreateBankEntityAsync(CreateBankEntityDto createDto)
    {
        var entity = _mapper.Map<BankEntity>(createDto);
        var created = await _repository.CreateBankEntityAsync(entity);
        return _mapper.Map<BankEntityDto>(created);
    }
}
