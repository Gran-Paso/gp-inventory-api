using AutoMapper;
using GPInventory.Application.DTOs.Payments;
using GPInventory.Application.Interfaces;
using GPInventory.Domain.Entities;

namespace GPInventory.Application.Services;

public class PaymentPlanService : IPaymentPlanService
{
    private readonly IPaymentPlanRepository _repository;
    private readonly IMapper _mapper;

    public PaymentPlanService(
        IPaymentPlanRepository repository,
        IMapper mapper)
    {
        _repository = repository;
        _mapper = mapper;
    }

    public async Task<PaymentPlanDto> GetPaymentPlanByIdAsync(int id)
    {
        var plan = await _repository.GetByIdAsync(id);
        if (plan == null)
            throw new KeyNotFoundException($"Payment plan with id {id} not found");

        return _mapper.Map<PaymentPlanDto>(plan);
    }

    public async Task<IEnumerable<PaymentPlanDto>> GetPaymentPlansByFixedExpenseAsync(int fixedExpenseId)
    {
        var plans = await _repository.GetByFixedExpenseIdAsync(fixedExpenseId);
        return _mapper.Map<IEnumerable<PaymentPlanDto>>(plans);
    }

    public async Task<PaymentPlanDto> CreatePaymentPlanAsync(CreatePaymentPlanDto createDto)
    {
        Console.WriteLine($"[PaymentPlanService] CreatePaymentPlanAsync - DTO received:");
        Console.WriteLine($"  ExpenseId: {createDto.ExpenseId}");
        Console.WriteLine($"  FixedExpenseId: {createDto.FixedExpenseId}");
        Console.WriteLine($"  PaymentTypeId: {createDto.PaymentTypeId}");
        Console.WriteLine($"  ExpressedInUf: {createDto.ExpressedInUf}");
        Console.WriteLine($"  BankEntityId: {createDto.BankEntityId}");
        Console.WriteLine($"  InstallmentsCount: {createDto.InstallmentsCount}");
        Console.WriteLine($"  StartDate: {createDto.StartDate}");
        
        var entity = _mapper.Map<PaymentPlan>(createDto);
        
        Console.WriteLine($"[PaymentPlanService] After mapping to PaymentPlan entity:");
        Console.WriteLine($"  ExpenseId: {entity.ExpenseId}");
        Console.WriteLine($"  FixedExpenseId: {entity.FixedExpenseId}");
        Console.WriteLine($"  PaymentTypeId: {entity.PaymentTypeId}");
        Console.WriteLine($"  ExpressedInUf: {entity.ExpressedInUf}");
        Console.WriteLine($"  BankEntityId: {entity.BankEntityId}");
        Console.WriteLine($"  InstallmentsCount: {entity.InstallmentsCount}");
        Console.WriteLine($"  StartDate: {entity.StartDate}");
        
        var created = await _repository.CreateAsync(entity);
        
        Console.WriteLine($"[PaymentPlanService] PaymentPlan created successfully with ID: {created.Id}");
        
        return _mapper.Map<PaymentPlanDto>(created);
    }

    public async Task DeletePaymentPlanAsync(int id)
    {
        await _repository.DeleteAsync(id);
    }
}
