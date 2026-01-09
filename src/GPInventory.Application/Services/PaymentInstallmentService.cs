using AutoMapper;
using GPInventory.Application.DTOs.Payments;
using GPInventory.Application.Interfaces;
using GPInventory.Domain.Entities;

namespace GPInventory.Application.Services;

public class PaymentInstallmentService : IPaymentInstallmentService
{
    private readonly IPaymentInstallmentRepository _repository;
    private readonly IPaymentPlanRepository _paymentPlanRepository;
    private readonly IExpenseRepository _expenseRepository;
    private readonly IMapper _mapper;

    public PaymentInstallmentService(
        IPaymentInstallmentRepository repository,
        IPaymentPlanRepository paymentPlanRepository,
        IExpenseRepository expenseRepository,
        IMapper mapper)
    {
        _repository = repository;
        _paymentPlanRepository = paymentPlanRepository;
        _expenseRepository = expenseRepository;
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

    public async Task<PaymentInstallmentDto> PayInstallmentAsync(int id, PayInstallmentDto payDto)
    {
        // Obtener la cuota
        var installment = await _repository.GetByIdAsync(id);
        if (installment == null)
            throw new KeyNotFoundException($"Cuota con ID {id} no encontrada");

        // Verificar si ya está pagada
        if (installment.Status == "pagado" || installment.Status == "paid")
            throw new InvalidOperationException("Esta cuota ya está pagada");

        // Validar que tenga un monto
        if (installment.AmountClp <= 0)
            throw new InvalidOperationException("La cuota no tiene un monto válido");

        // Obtener el payment plan para acceder al expense original
        var paymentPlan = await _paymentPlanRepository.GetByIdAsync(installment.PaymentPlanId);
        if (paymentPlan == null)
            throw new KeyNotFoundException("Plan de pago no encontrado");

        if (!paymentPlan.ExpenseId.HasValue)
            throw new InvalidOperationException("El plan de pago no tiene un gasto asociado");

        // Obtener el expense original para copiar algunos datos
        var originalExpense = await _expenseRepository.GetByIdAsync(paymentPlan.ExpenseId.Value);
        if (originalExpense == null)
            throw new KeyNotFoundException("Gasto original no encontrado");

        // Crear un nuevo expense para registrar el pago de la cuota
        var paymentExpense = new Expense
        {
            Date = payDto.PaymentDate,
            SubcategoryId = originalExpense.SubcategoryId,
            Amount = (int)Math.Round(installment.AmountClp),
            Description = $"Pago cuota {installment.InstallmentNumber} - {originalExpense.Description}",
            BusinessId = originalExpense.BusinessId,
            StoreId = originalExpense.StoreId,
            ExpenseTypeId = originalExpense.ExpenseTypeId,
            ProviderId = originalExpense.ProviderId,
            IsFixed = false
        };

        var createdExpense = await _expenseRepository.AddAsync(paymentExpense);

        // Actualizar la cuota
        installment.Status = "pagado";
        installment.PaidDate = payDto.PaymentDate;
        installment.PaymentMethodId = payDto.PaymentMethodId;
        installment.ExpenseId = createdExpense.Id;
        installment.UpdatedAt = DateTime.UtcNow;

        await _repository.UpdateAsync(installment);

        return _mapper.Map<PaymentInstallmentDto>(installment);
    }

    public async Task DeleteInstallmentAsync(int id)
    {
        await _repository.DeleteAsync(id);
    }

    public async Task<InstallmentsSummaryDto> GetInstallmentsSummaryAsync(List<int>? businessIds = null)
    {
        var installments = await _repository.GetAllInstallmentsAsync(businessIds);
        
        var summary = new InstallmentsSummaryDto
        {
            TotalInstallments = installments.Count(),
            PendingInstallments = installments.Count(i => i.Status == "pending"),
            PaidInstallments = installments.Count(i => i.Status == "paid"),
            OverdueInstallments = installments.Count(i => i.Status == "overdue" || (i.Status == "pending" && i.DueDate < DateTime.Now)),
            TotalPending = installments.Where(i => i.Status == "pending").Sum(i => i.AmountClp),
            TotalPaid = installments.Where(i => i.Status == "paid").Sum(i => i.AmountClp),
            TotalOverdue = installments.Where(i => i.Status == "overdue" || (i.Status == "pending" && i.DueDate < DateTime.Now)).Sum(i => i.AmountClp)
        };

        return summary;
    }
}
