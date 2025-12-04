using AutoMapper;
using GPInventory.Application.DTOs.Payments;
using GPInventory.Application.Interfaces;
using GPInventory.Domain.Entities;

namespace GPInventory.Application.Services;

public class PaymentInstallmentService : IPaymentInstallmentService
{
    private readonly IPaymentInstallmentRepository _repository;
    private readonly IMapper _mapper;

    public PaymentInstallmentService(
        IPaymentInstallmentRepository repository,
        IMapper mapper)
    {
        _repository = repository;
        _mapper = mapper;
    }

    public async Task<PaymentInstallmentDto> CreateInstallmentAsync(CreateInstallmentDto createDto)
    {
        var entity = _mapper.Map<PaymentInstallment>(createDto);
        var created = await _repository.CreateAsync(entity);
        return _mapper.Map<PaymentInstallmentDto>(created);
    }

    public async Task<IEnumerable<PaymentInstallmentDto>> CreateInstallmentsBulkAsync(CreateInstallmentsBulkDto createDto)
    {
        var entities = _mapper.Map<IEnumerable<PaymentInstallment>>(createDto.Installments);
        var created = await _repository.CreateBulkAsync(entities);
        return _mapper.Map<IEnumerable<PaymentInstallmentDto>>(created);
    }

    public async Task<IEnumerable<PaymentInstallmentDto>> GetInstallmentsByPaymentPlanAsync(int paymentPlanId)
    {
        var installments = await _repository.GetByPaymentPlanIdAsync(paymentPlanId);
        return _mapper.Map<IEnumerable<PaymentInstallmentDto>>(installments);
    }

    public async Task<PaymentInstallmentDto> UpdateInstallmentStatusAsync(int id, UpdateInstallmentStatusDto updateDto)
    {
        var installment = await _repository.GetByIdAsync(id);
        if (installment == null)
            throw new KeyNotFoundException($"Payment installment with id {id} not found");

        installment.Status = updateDto.Status;
        installment.PaidDate = updateDto.PaidDate;
        installment.PaymentMethodId = updateDto.PaymentMethodId;
        installment.ExpenseId = updateDto.ExpenseId;

        await _repository.UpdateAsync(installment);

        return _mapper.Map<PaymentInstallmentDto>(installment);
    }

    public async Task DeleteInstallmentAsync(int id)
    {
        await _repository.DeleteAsync(id);
    }
}
