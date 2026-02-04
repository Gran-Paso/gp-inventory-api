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

        // Verificar que el expense original existe
        var originalExpense = await _expenseRepository.GetByIdAsync(paymentPlan.ExpenseId.Value);
        if (originalExpense == null)
            throw new KeyNotFoundException("Gasto original no encontrado");

        // Actualizar la cuota para asociarla con el expense original (no crear uno nuevo)
        installment.Status = "pagado";
        installment.PaidDate = payDto.PaymentDate;
        installment.PaymentMethodId = payDto.PaymentMethodId;
        installment.ExpenseId = paymentPlan.ExpenseId.Value; // Usar el expense original del payment plan
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
        var allInstallments = await _repository.GetAllInstallmentsAsync(businessIds);
        var installmentsList = allInstallments.ToList();
        
        var now = DateTime.Now;
        
        // Calcular conteos basados en el estado real de las cuotas
        var paidInstallments = installmentsList.Where(i => i.Status == "paid" || i.Status == "pagado").ToList();
        var overdueInstallments = installmentsList.Where(i => 
            (i.Status == "pendiente" || i.Status == "overdue") && 
            i.DueDate.Date < now.Date
        ).ToList();
        var pendingInstallments = installmentsList.Where(i => 
            (i.Status == "pendiente") && 
            i.DueDate.Date >= now.Date
        ).ToList();
        
        // Obtener todos los expenses
        var allExpenses = await _expenseRepository.GetExpensesWithDetailsAsync(
            businessId: null,
            businessIds: businessIds?.ToArray(),
            page: 1,
            pageSize: int.MaxValue
        );
        
        // Separar expenses con y sin payment plan
        decimal totalPending = 0;
        decimal totalPaid = 0;
        decimal totalOverdue = 0;
        decimal totalCommitted = 0;
        int singlePaymentsCount = 0;
        
        foreach (var expense in allExpenses)
        {
            var paymentPlans = await _paymentPlanRepository.GetByExpenseIdAsync(expense.Id);
            
            if (!paymentPlans.Any())
            {
                // Pago único - contar como pagado
                totalPaid += expense.Amount;
                totalCommitted += expense.Amount;
                singlePaymentsCount++;
            }
            else
            {
                // Tiene payment plan - usar monto original del expense para evitar errores de redondeo
                var paymentPlan = paymentPlans.First();
                var installments = await _repository.GetByPaymentPlanIdAsync(paymentPlan.Id);
                var installmentsPlan = installments.ToList();
                
                // Verificar el estado del plan
                var paidCount = installmentsPlan.Count(i => i.Status == "paid" || i.Status == "pagado");
                var overdueCount = installmentsPlan.Count(i => 
                    (i.Status == "pendiente" || i.Status == "overdue") && i.DueDate.Date < now.Date
                );
                var pendingCount = installmentsPlan.Count(i => 
                    (i.Status == "pendiente") && i.DueDate.Date >= now.Date
                );
                
                // Calcular proporciones basadas en el monto original del expense
                if (installmentsPlan.Count > 0)
                {
                    decimal paidPortion = (decimal)paidCount / installmentsPlan.Count;
                    decimal overduePortion = (decimal)overdueCount / installmentsPlan.Count;
                    decimal pendingPortion = (decimal)pendingCount / installmentsPlan.Count;
                    
                    totalPaid += expense.Amount * paidPortion;
                    totalOverdue += expense.Amount * overduePortion;
                    totalPending += expense.Amount * pendingPortion;
                }
                
                totalCommitted += expense.Amount;
            }
        }
        
        var summary = new InstallmentsSummaryDto
        {
            TotalInstallments = installmentsList.Count + singlePaymentsCount,
            PendingInstallments = pendingInstallments.Count,
            PaidInstallments = paidInstallments.Count + singlePaymentsCount,
            OverdueInstallments = overdueInstallments.Count,
            TotalPending = totalPending,
            TotalPaid = totalPaid,
            TotalOverdue = totalOverdue
        };

        return summary;
    }
}
