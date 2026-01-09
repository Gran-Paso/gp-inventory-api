using AutoMapper;
using GPInventory.Application.DTOs.Expenses;
using GPInventory.Application.Helpers;
using GPInventory.Application.Interfaces;
using GPInventory.Domain.Entities;
using System.Globalization;
using GPInventory.Application.DTOs.Payments;

namespace GPInventory.Application.Services;

public class ExpenseService : IExpenseService
{
    private readonly IExpenseRepository _expenseRepository;
    private readonly IExpenseSqlRepository _expenseSqlRepository;
    private readonly IFixedExpenseRepository _fixedExpenseRepository;
    private readonly IExpenseCategoryRepository _categoryRepository;
    private readonly IExpenseSubcategoryRepository _subcategoryRepository;
    private readonly IRecurrenceTypeRepository _recurrenceTypeRepository;
    private readonly IPaymentPlanRepository _paymentPlanRepository;
    private readonly IPaymentInstallmentRepository _paymentInstallmentRepository;
    private readonly IMapper _mapper;

    public ExpenseService(
        IExpenseRepository expenseRepository,
        IExpenseSqlRepository expenseSqlRepository,
        IFixedExpenseRepository fixedExpenseRepository,
        IExpenseCategoryRepository categoryRepository,
        IExpenseSubcategoryRepository subcategoryRepository,
        IRecurrenceTypeRepository recurrenceTypeRepository,
        IPaymentPlanRepository paymentPlanRepository,
        IPaymentInstallmentRepository paymentInstallmentRepository,
        IMapper mapper)
    {
        _expenseRepository = expenseRepository;
        _expenseSqlRepository = expenseSqlRepository;
        _fixedExpenseRepository = fixedExpenseRepository;
        _categoryRepository = categoryRepository;
        _subcategoryRepository = subcategoryRepository;
        _recurrenceTypeRepository = recurrenceTypeRepository;
        _paymentPlanRepository = paymentPlanRepository;
        _paymentInstallmentRepository = paymentInstallmentRepository;
        _mapper = mapper;
    }

    // Categorías y subcategorías
    public async Task<IEnumerable<ExpenseCategoryDto>> GetCategoriesAsync()
    {
        try
        {
            var categories = await _categoryRepository.GetAllAsync();
            return _mapper.Map<IEnumerable<ExpenseCategoryDto>>(categories);
        }
        catch (Exception ex)
        {
            // Log detailed error information
            Console.WriteLine($"GetCategoriesAsync Error: {ex.Message}");
            Console.WriteLine($"Stack Trace: {ex.StackTrace}");
            if (ex.InnerException != null)
            {
                Console.WriteLine($"Inner Exception: {ex.InnerException.Message}");
            }
            
            throw new ApplicationException($"Error al obtener categorías: {ex.Message}", ex);
        }
    }

    public async Task<IEnumerable<ExpenseSubcategoryDto>> GetSubcategoriesAsync(int? categoryId = null)
    {
        try
        {
            var subcategories = categoryId.HasValue
                ? await _subcategoryRepository.GetSubcategoriesByCategoryAsync(categoryId.Value)
                : await _subcategoryRepository.GetAllAsync();
            
            return _mapper.Map<IEnumerable<ExpenseSubcategoryDto>>(subcategories);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"GetSubcategoriesAsync Error: {ex.Message}");
            Console.WriteLine($"Stack Trace: {ex.StackTrace}");
            if (ex.InnerException != null)
            {
                Console.WriteLine($"Inner Exception: {ex.InnerException.Message}");
            }
            
            throw new ApplicationException($"Error al obtener subcategorías: {ex.Message}", ex);
        }
    }

    public async Task<IEnumerable<RecurrenceTypeDto>> GetRecurrenceTypesAsync()
    {
        try
        {
            var recurrenceTypes = await _recurrenceTypeRepository.GetAllAsync();
            return _mapper.Map<IEnumerable<RecurrenceTypeDto>>(recurrenceTypes);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"GetRecurrenceTypesAsync Error: {ex.Message}");
            Console.WriteLine($"Stack Trace: {ex.StackTrace}");
            if (ex.InnerException != null)
            {
                Console.WriteLine($"Inner Exception: {ex.InnerException.Message}");
            }
            
            throw new ApplicationException($"Error al obtener tipos de recurrencia: {ex.Message}", ex);
        }
    }

    // Gastos
    public async Task<IEnumerable<ExpenseWithDetailsDto>> GetExpensesAsync(ExpenseFiltersDto filters)
    {
        try
        {
            // Validar que se proporcionen business IDs
            if ((filters.BusinessIds == null || filters.BusinessIds.Length == 0) && !filters.BusinessId.HasValue)
            {
                throw new ArgumentException("Se debe proporcionar al menos un ID de negocio");
            }

            // Si se especifica un expense_type_id, usar consulta SQL directa
            if (filters.ExpenseTypeId.HasValue)
            {
                var businessId = filters.BusinessId ?? filters.BusinessIds?.FirstOrDefault() ?? 0;
                if (businessId == 0)
                {
                    throw new ArgumentException("Se debe proporcionar un ID de negocio válido");
                }

                var expensesWithDetails = await _expenseSqlRepository.GetExpensesByTypeAsync(
                    businessId: businessId,
                    expenseTypeId: filters.ExpenseTypeId,
                    categoryId: filters.CategoryId,
                    subcategoryId: filters.SubcategoryId,
                    startDate: filters.StartDate,
                    endDate: filters.EndDate,
                    page: filters.Page,
                    pageSize: filters.PageSize);

                return expensesWithDetails;
            }

            // Fallback a EF Core para compatibilidad
            var expenses = await _expenseRepository.GetExpensesWithDetailsAsync(
                businessId: filters.BusinessId,
                businessIds: filters.BusinessIds,
                storeId: filters.StoreId,
                categoryId: filters.CategoryId,
                subcategoryId: filters.SubcategoryId,
                startDate: filters.StartDate,
                endDate: filters.EndDate,
                minAmount: filters.MinAmount,
                maxAmount: filters.MaxAmount,
                isFixed: filters.IsFixed,
                expenseTypeId: filters.ExpenseTypeId,
                page: filters.Page,
                pageSize: filters.PageSize,
                orderBy: filters.OrderBy ?? "Date",
                orderDescending: filters.OrderDescending);

            var expenseDtos = _mapper.Map<List<ExpenseWithDetailsDto>>(expenses);

            // NUEVO: Cargar payment_plan e installments para cada expense
            foreach (var expenseDto in expenseDtos)
            {
                await LoadPaymentPlanAsync(expenseDto);
            }

            return expenseDtos;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"GetExpensesAsync Error: {ex.Message}");
            Console.WriteLine($"Stack Trace: {ex.StackTrace}");
            if (ex.InnerException != null)
            {
                Console.WriteLine($"Inner Exception: {ex.InnerException.Message}");
            }
            
            throw new ApplicationException($"Error al obtener gastos: {ex.Message}", ex);
        }
    }

    // Método optimizado para listados de items (sin cargar detalles pesados)
    public async Task<IEnumerable<ExpenseListItemDto>> GetExpensesListAsync(ExpenseFiltersDto filters)
    {
        try
        {
            // Validar que se proporcionen business IDs
            if ((filters.BusinessIds == null || filters.BusinessIds.Length == 0) && !filters.BusinessId.HasValue)
            {
                throw new ArgumentException("Se debe proporcionar al menos un ID de negocio");
            }

            // Si se proporciona BusinessId (singular), agregar al array
            if (filters.BusinessId.HasValue && (filters.BusinessIds == null || filters.BusinessIds.Length == 0))
            {
                filters.BusinessIds = new[] { filters.BusinessId.Value };
            }

            var expenses = await _expenseRepository.GetExpensesWithDetailsAsync(
                businessId: null,
                businessIds: filters.BusinessIds,
                storeId: filters.StoreId,
                categoryId: filters.CategoryId,
                subcategoryId: filters.SubcategoryId,
                startDate: filters.StartDate,
                endDate: filters.EndDate,
                minAmount: filters.MinAmount,
                maxAmount: filters.MaxAmount,
                isFixed: filters.IsFixed,
                expenseTypeId: filters.ExpenseTypeId,
                page: filters.Page,
                pageSize: filters.PageSize,
                orderBy: filters.OrderBy ?? "Date",
                orderDescending: filters.OrderDescending);

            // Mapear a DTO ligero
            var result = new List<ExpenseListItemDto>();
            
            foreach (var e in expenses)
            {
                var item = new ExpenseListItemDto
                {
                    Id = e.Id,
                    Date = e.Date,
                    Amount = e.Amount,
                    Description = e.Description,
                    IsFixed = e.IsFixed,
                    BusinessId = e.BusinessId,
                    ExpenseTypeId = e.ExpenseTypeId,
                    CategoryName = e.ExpenseSubcategory?.ExpenseCategory?.Name,
                    SubcategoryName = e.ExpenseSubcategory?.Name,
                    HasProvider = e.ProviderId.HasValue,
                    ProviderName = e.Provider?.Name
                };

                // Obtener información de cuotas si existe payment plan
                var paymentPlans = await _paymentPlanRepository.GetByExpenseIdAsync(e.Id);
                var paymentPlan = paymentPlans.FirstOrDefault();
                
                if (paymentPlan != null)
                {
                    var installments = await _paymentInstallmentRepository.GetByPaymentPlanIdAsync(paymentPlan.Id);
                    var installmentsList = installments.ToList();
                    
                    item.TotalInstallments = installmentsList.Count;
                    item.PaidInstallments = installmentsList.Count(i => i.Status == "pagado" || i.Status == "paid");
                }

                result.Add(item);
            }
            
            return result;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"GetExpensesListAsync Error: {ex.Message}");
            throw new ApplicationException($"Error al obtener lista de gastos: {ex.Message}", ex);
        }
    }

    // Método optimizado para obtener detalles completos de un expense específico
    public async Task<ExpenseWithDetailsDto> GetExpenseWithDetailsAsync(int id)
    {
        try
        {
            // Usar el nuevo método que carga directamente con todas las relaciones
            var expense = await _expenseRepository.GetByIdWithDetailsAsync(id);
            if (expense == null)
                throw new KeyNotFoundException($"Gasto con ID {id} no encontrado");

            var dto = _mapper.Map<ExpenseWithDetailsDto>(expense);
            
            // Cargar payment plan si existe
            await LoadPaymentPlanAsync(dto);
            
            return dto;
        }
        catch (KeyNotFoundException)
        {
            throw;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"GetExpenseWithDetailsAsync Error: {ex.Message}");
            throw new ApplicationException($"Error al obtener detalles del gasto: {ex.Message}", ex);
        }
    }

    public async Task<ExpenseDto> GetExpenseByIdAsync(int id)
    {
        try
        {
            var expense = await _expenseRepository.GetByIdAsync(id);
            if (expense == null)
                throw new KeyNotFoundException($"Gasto con ID {id} no encontrado");

            return _mapper.Map<ExpenseDto>(expense);
        }
        catch (KeyNotFoundException)
        {
            throw; // Re-throw KeyNotFoundException as is
        }
        catch (Exception ex)
        {
            Console.WriteLine($"GetExpenseByIdAsync Error: {ex.Message}");
            Console.WriteLine($"Stack Trace: {ex.StackTrace}");
            if (ex.InnerException != null)
            {
                Console.WriteLine($"Inner Exception: {ex.InnerException.Message}");
            }
            
            throw new ApplicationException($"Error al obtener gasto por ID {id}: {ex.Message}", ex);
        }
    }

    public async Task<ExpenseDto> CreateExpenseAsync(CreateExpenseDto createExpenseDto)
    {
        try
        {
            Console.WriteLine($"CreateExpenseAsync - createExpenseDto.IsFixed: {createExpenseDto.IsFixed}");
            Console.WriteLine($"CreateExpenseAsync - createExpenseDto.FixedExpenseId: {createExpenseDto.FixedExpenseId}");
            Console.WriteLine($"CreateExpenseAsync - createExpenseDto.ExpenseTypeId: {createExpenseDto.ExpenseTypeId}");
            Console.WriteLine($"CreateExpenseAsync - createExpenseDto.PaymentTypeId: {createExpenseDto.PaymentTypeId}");
            Console.WriteLine($"CreateExpenseAsync - createExpenseDto.InstallmentsCount: {createExpenseDto.InstallmentsCount}");
            Console.WriteLine($"CreateExpenseAsync - createExpenseDto JSON: {System.Text.Json.JsonSerializer.Serialize(createExpenseDto)}");
            
            var expense = _mapper.Map<Expense>(createExpenseDto);
            
            Console.WriteLine($"CreateExpenseAsync - After mapping, expense.IsFixed: {expense.IsFixed}");
            Console.WriteLine($"CreateExpenseAsync - After mapping, expense.FixedExpenseId: {expense.FixedExpenseId}");
            Console.WriteLine($"CreateExpenseAsync - After mapping, expense.ExpenseTypeId: {expense.ExpenseTypeId}");
            
            // Asegurar que IsFixed tenga un valor válido, convirtiendo NULL a false
            expense.IsFixed = createExpenseDto.IsFixed ?? false;
            
            Console.WriteLine($"CreateExpenseAsync - After manual assignment, expense.IsFixed: {expense.IsFixed}");
            
            var createdExpense = await _expenseRepository.AddAsync(expense);
            
            // Si viene con datos de payment plan, crear el payment plan
            if (createExpenseDto.PaymentTypeId.HasValue && 
                createExpenseDto.PaymentTypeId.Value == 3 && // 3 = Financiamiento Bancario
                createExpenseDto.InstallmentsCount.HasValue &&
                createExpenseDto.InstallmentsCount.Value > 1)
            {
                Console.WriteLine($"Creating payment plan for expense {createdExpense.Id}");
                
                var paymentPlan = new PaymentPlan
                {
                    ExpenseId = createdExpense.Id,
                    PaymentTypeId = createExpenseDto.PaymentTypeId.Value,
                    InstallmentsCount = createExpenseDto.InstallmentsCount.Value,
                    ExpressedInUf = createExpenseDto.ExpressedInUf ?? false,
                    BankEntityId = createExpenseDto.BankEntityId,
                    StartDate = createExpenseDto.PaymentStartDate ?? DateTime.UtcNow
                };
                
                var createdPaymentPlan = await _paymentPlanRepository.CreateAsync(paymentPlan);
                Console.WriteLine($"Payment plan created with ID: {createdPaymentPlan.Id}");
                
                // Crear las cuotas
                decimal installmentAmount = createdExpense.Amount / createExpenseDto.InstallmentsCount.Value;
                var startDate = createExpenseDto.PaymentStartDate ?? DateTime.UtcNow;
                
                for (int i = 1; i <= createExpenseDto.InstallmentsCount.Value; i++)
                {
                    var installment = new PaymentInstallment
                    {
                        PaymentPlanId = createdPaymentPlan.Id,
                        InstallmentNumber = i,
                        AmountClp = installmentAmount,
                        DueDate = startDate.AddMonths(i - 1),
                        Status = "pendiente"
                    };
                    
                    await _paymentInstallmentRepository.CreateAsync(installment);
                }
                
                Console.WriteLine($"Created {createExpenseDto.InstallmentsCount.Value} installments");
            }
            
            return _mapper.Map<ExpenseDto>(createdExpense);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"CreateExpenseAsync Error: {ex.Message}");
            Console.WriteLine($"Stack Trace: {ex.StackTrace}");
            if (ex.InnerException != null)
            {
                Console.WriteLine($"Inner Exception: {ex.InnerException.Message}");
            }
            
            throw new ApplicationException($"Error al crear gasto: {ex.Message}", ex);
        }
    }

    public async Task<ExpenseDto> UpdateExpenseAsync(int id, UpdateExpenseDto updateExpenseDto)
    {
        try
        {
            var expense = await _expenseRepository.GetByIdAsync(id);
            if (expense == null)
                throw new KeyNotFoundException($"Gasto con ID {id} no encontrado");

            _mapper.Map(updateExpenseDto, expense);
            
            await _expenseRepository.UpdateAsync(expense);
            
            // Recargar la entidad con las relaciones
            var expenseWithDetails = await _expenseRepository.GetExpensesWithDetailsAsync(
                businessId: expense.BusinessId,
                expenseTypeId: null,
                page: 1,
                pageSize: 1);

            var result = expenseWithDetails.FirstOrDefault(e => e.Id == expense.Id);
            return _mapper.Map<ExpenseDto>(result);
        }
        catch (KeyNotFoundException)
        {
            throw; // Re-throw KeyNotFoundException as is
        }
        catch (Exception ex)
        {
            Console.WriteLine($"UpdateExpenseAsync Error: {ex.Message}");
            Console.WriteLine($"Stack Trace: {ex.StackTrace}");
            if (ex.InnerException != null)
            {
                Console.WriteLine($"Inner Exception: {ex.InnerException.Message}");
            }
            
            throw new ApplicationException($"Error al actualizar gasto con ID {id}: {ex.Message}", ex);
        }
    }

    public async Task DeleteExpenseAsync(int id)
    {
        try
        {
            var expense = await _expenseRepository.GetByIdAsync(id);
            if (expense == null)
                throw new KeyNotFoundException($"Gasto con ID {id} no encontrado");

            await _expenseRepository.DeleteAsync(id);
        }
        catch (KeyNotFoundException)
        {
            throw; // Re-throw KeyNotFoundException as is
        }
        catch (Exception ex)
        {
            Console.WriteLine($"DeleteExpenseAsync Error: {ex.Message}");
            Console.WriteLine($"Stack Trace: {ex.StackTrace}");
            if (ex.InnerException != null)
            {
                Console.WriteLine($"Inner Exception: {ex.InnerException.Message}");
            }
            
            throw new ApplicationException($"Error al eliminar gasto con ID {id}: {ex.Message}", ex);
        }
    }

    // Gastos fijos
    
    // Método optimizado para obtener lista de gastos fijos (items ligeros)
    public async Task<IEnumerable<FixedExpenseListItemDto>> GetFixedExpensesListAsync(int[]? businessIds = null, int? expenseTypeId = null)
    {
        try
        {
            Console.WriteLine($"ExpenseService.GetFixedExpensesListAsync called with businessIds: {(businessIds != null ? string.Join(",", businessIds) : "null")}, expenseTypeId: {expenseTypeId}");
            
            if (businessIds == null || businessIds.Length == 0)
            {
                throw new ArgumentException("Se debe proporcionar al menos un ID de negocio");
            }
            
            var fixedExpenses = await _fixedExpenseRepository.GetFixedExpensesWithDetailsAsync(
                businessIds: businessIds, expenseTypeId: expenseTypeId);
                
            var listItems = new List<FixedExpenseListItemDto>();
            
            foreach (var fe in fixedExpenses)
            {
                var item = new FixedExpenseListItemDto
                {
                    Id = fe.Id,
                    Description = fe.AdditionalNote ?? "Sin descripción",
                    Amount = fe.Amount,
                    StartDate = fe.PaymentDate ?? fe.CreatedAt,
                    EndDate = fe.EndDate,
                    BusinessId = fe.BusinessId,
                    ExpenseTypeId = fe.ExpenseTypeId,
                    CategoryName = fe.Subcategory?.ExpenseCategory?.Name ?? "Sin categoría",
                    SubcategoryName = fe.Subcategory?.Name ?? "Sin subcategoría",
                    RecurrenceTypeName = fe.RecurrenceType?.Description ?? "No definido",
                    AssociatedExpensesCount = fe.GeneratedExpenses?.Count ?? 0
                };
                
                // Calcular estado de pago sin cargar todos los detalles
                var (isUpToDate, nextDueDate) = CalculatePaymentStatus(fe);
                item.IsUpToDate = isUpToDate;
                item.NextDueDate = nextDueDate;
                
                listItems.Add(item);
            }
            
            return listItems;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"GetFixedExpensesListAsync Error: {ex.Message}");
            throw new ApplicationException($"Error al obtener lista de gastos fijos: {ex.Message}", ex);
        }
    }
    
    // Método optimizado para obtener detalles completos de un gasto fijo específico
    public async Task<FixedExpenseWithDetailsDto> GetFixedExpenseWithDetailsAsync(int id)
    {
        try
        {
            var fixedExpense = await _fixedExpenseRepository.GetByIdAsync(id);
            if (fixedExpense == null)
                throw new KeyNotFoundException($"Gasto fijo con ID {id} no encontrado");

            // Cargar con todas las relaciones usando el método que ya incluye providers
            var fixedExpenses = await _fixedExpenseRepository.GetFixedExpensesWithDetailsAsync(
                businessIds: new[] { fixedExpense.BusinessId },
                expenseTypeId: fixedExpense.ExpenseTypeId);
                
            var fixedExpenseWithDetails = fixedExpenses.FirstOrDefault(fe => fe.Id == id);
            if (fixedExpenseWithDetails == null)
                throw new KeyNotFoundException($"Gasto fijo con ID {id} no encontrado");

            var dto = _mapper.Map<FixedExpenseWithDetailsDto>(fixedExpenseWithDetails);
            await PopulatePaymentStatusAsync(dto, fixedExpenseWithDetails);
            
            return dto;
        }
        catch (KeyNotFoundException)
        {
            throw;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"GetFixedExpenseWithDetailsAsync Error: {ex.Message}");
            throw new ApplicationException($"Error al obtener detalles del gasto fijo: {ex.Message}", ex);
        }
    }
    
    public async Task<IEnumerable<FixedExpenseWithDetailsDto>> GetFixedExpensesAsync(int[]? businessIds = null, int? expenseTypeId = null)
    {
        try
        {
            Console.WriteLine($"ExpenseService.GetFixedExpensesAsync called with businessIds: {(businessIds != null ? string.Join(",", businessIds) : "null")}, expenseTypeId: {expenseTypeId}");
            
            // Validar que al menos se proporcione un businessId
            if (businessIds == null || businessIds.Length == 0)
            {
                throw new ArgumentException("Se debe proporcionar al menos un ID de negocio");
            }
            
            var fixedExpenses = await _fixedExpenseRepository.GetFixedExpensesWithDetailsAsync(
                businessIds: businessIds, expenseTypeId: expenseTypeId);
                
            var dtos = _mapper.Map<IEnumerable<FixedExpenseWithDetailsDto>>(fixedExpenses);
            
            // Populate payment status for each fixed expense
            foreach (var dto in dtos)
            {
                var fixedExpense = fixedExpenses.First(fe => fe.Id == dto.Id);
                await PopulatePaymentStatusAsync(dto, fixedExpense);
            }
                
            return dtos;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"GetFixedExpensesAsync Error: {ex.Message}");
            Console.WriteLine($"Stack Trace: {ex.StackTrace}");
            if (ex.InnerException != null)
            {
                Console.WriteLine($"Inner Exception: {ex.InnerException.Message}");
            }
            
            throw new ApplicationException($"Error al obtener gastos fijos: {ex.Message}", ex);
        }
    }

    public async Task<FixedExpenseDto> GetFixedExpenseByIdAsync(int id)
    {
        try
        {
            var fixedExpense = await _fixedExpenseRepository.GetByIdAsync(id);
            if (fixedExpense == null)
                throw new KeyNotFoundException($"Gasto fijo con ID {id} no encontrado");

            var dto = _mapper.Map<FixedExpenseDto>(fixedExpense);
            await PopulatePaymentStatusAsync(dto, fixedExpense);
            
            return dto;
        }
        catch (KeyNotFoundException)
        {
            throw; // Re-throw KeyNotFoundException as is
        }
        catch (Exception ex)
        {
            Console.WriteLine($"GetFixedExpenseByIdAsync Error: {ex.Message}");
            Console.WriteLine($"Stack Trace: {ex.StackTrace}");
            if (ex.InnerException != null)
            {
                Console.WriteLine($"Inner Exception: {ex.InnerException.Message}");
            }
            
            throw new ApplicationException($"Error al obtener gasto fijo con ID {id}: {ex.Message}", ex);
        }
    }

    public async Task<FixedExpenseDto> CreateFixedExpenseAsync(CreateFixedExpenseDto createFixedExpenseDto)
    {
        try
        {
            Console.WriteLine($"CreateFixedExpenseAsync called with DTO:");
            Console.WriteLine($"  Description: {createFixedExpenseDto.Description}");
            Console.WriteLine($"  Amount: {createFixedExpenseDto.Amount}");
            Console.WriteLine($"  BusinessId: {createFixedExpenseDto.BusinessId}");
            Console.WriteLine($"  RecurrenceTypeId: {createFixedExpenseDto.RecurrenceTypeId}");
            Console.WriteLine($"  SubcategoryId: {createFixedExpenseDto.SubcategoryId}");
            Console.WriteLine($"  StartDate: {createFixedExpenseDto.StartDate}");
            Console.WriteLine($"  StoreId: {createFixedExpenseDto.StoreId}");
            Console.WriteLine($"  Notes: {createFixedExpenseDto.Notes}");
            
            var fixedExpense = _mapper.Map<FixedExpense>(createFixedExpenseDto);
            
            // Asegurar que IsActive se establece explícitamente
            fixedExpense.IsActive = true;
            
            Console.WriteLine($"Mapped FixedExpense entity:");
            Console.WriteLine($"  AdditionalNote: {fixedExpense.AdditionalNote}");
            Console.WriteLine($"  Amount: {fixedExpense.Amount}");
            Console.WriteLine($"  BusinessId: {fixedExpense.BusinessId}");
            Console.WriteLine($"  RecurrenceTypeId: {fixedExpense.RecurrenceTypeId}");
            Console.WriteLine($"  SubcategoryId: {fixedExpense.SubcategoryId}");
            Console.WriteLine($"  PaymentDate: {fixedExpense.PaymentDate}");
            Console.WriteLine($"  StoreId: {fixedExpense.StoreId}");
            Console.WriteLine($"  IsActive: {fixedExpense.IsActive}");
            Console.WriteLine($"  CreatedAt: {fixedExpense.CreatedAt}");
            Console.WriteLine($"  UpdatedAt: {fixedExpense.UpdatedAt}");
            
            var createdFixedExpense = await _fixedExpenseRepository.AddAsync(fixedExpense);
            return _mapper.Map<FixedExpenseDto>(createdFixedExpense);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"CreateFixedExpenseAsync Error: {ex.Message}");
            Console.WriteLine($"Stack Trace: {ex.StackTrace}");
            if (ex.InnerException != null)
            {
                Console.WriteLine($"Inner Exception: {ex.InnerException.Message}");
            }
            
            throw new ApplicationException($"Error al crear gasto fijo: {ex.Message}", ex);
        }
    }

    public async Task<FixedExpenseDto> UpdateFixedExpenseAsync(int id, UpdateFixedExpenseDto updateFixedExpenseDto)
    {
        try
        {
            var fixedExpense = await _fixedExpenseRepository.GetByIdAsync(id);
            if (fixedExpense == null)
                throw new KeyNotFoundException($"Gasto fijo con ID {id} no encontrado");

            _mapper.Map(updateFixedExpenseDto, fixedExpense);
            fixedExpense.UpdatedAt = DateTime.UtcNow;
            
            await _fixedExpenseRepository.UpdateAsync(fixedExpense);
            
            return _mapper.Map<FixedExpenseDto>(fixedExpense);
        }
        catch (KeyNotFoundException)
        {
            throw; // Re-throw KeyNotFoundException as is
        }
        catch (Exception ex)
        {
            Console.WriteLine($"UpdateFixedExpenseAsync Error: {ex.Message}");
            Console.WriteLine($"Stack Trace: {ex.StackTrace}");
            if (ex.InnerException != null)
            {
                Console.WriteLine($"Inner Exception: {ex.InnerException.Message}");
            }
            
            throw new ApplicationException($"Error al actualizar gasto fijo con ID {id}: {ex.Message}", ex);
        }
    }

    public async Task DeleteFixedExpenseAsync(int id)
    {
        try
        {
            var fixedExpense = await _fixedExpenseRepository.GetByIdAsync(id);
            if (fixedExpense == null)
                throw new KeyNotFoundException($"Gasto fijo con ID {id} no encontrado");

            await _fixedExpenseRepository.DeleteAsync(id);
        }
        catch (KeyNotFoundException)
        {
            throw; // Re-throw KeyNotFoundException as is
        }
        catch (Exception ex)
        {
            Console.WriteLine($"DeleteFixedExpenseAsync Error: {ex.Message}");
            Console.WriteLine($"Stack Trace: {ex.StackTrace}");
            if (ex.InnerException != null)
            {
                Console.WriteLine($"Inner Exception: {ex.InnerException.Message}");
            }
            
            throw new ApplicationException($"Error al eliminar gasto fijo con ID {id}: {ex.Message}", ex);
        }
    }

    // Resumen y reportes
    public async Task<ExpenseSummaryDto> GetExpenseSummaryAsync(int[] businessIds, ExpenseFiltersDto filters)
    {
        try
        {
            // Validar que se proporcionen business IDs
            if (businessIds == null || businessIds.Length == 0)
            {
                throw new ArgumentException("Se debe proporcionar al menos un ID de negocio");
            }

            // Calcular totales combinando datos de todos los negocios
            decimal totalAmount = 0;
            decimal fixedExpensesAmount = 0;
            var allExpensesByCategory = new List<(int CategoryId, string CategoryName, decimal TotalAmount, int Count)>();
            var allMonthlyExpenses = new List<(int Year, int Month, decimal TotalAmount, int Count)>();

            foreach (var businessId in businessIds)
            {
                // Sumar totales de cada negocio
                var businessTotalAmount = await _expenseRepository.GetTotalExpensesAmountAsync(businessId, filters.StartDate, filters.EndDate);
                var businessFixedExpensesAmount = await _fixedExpenseRepository.GetTotalFixedExpensesAmountAsync(businessId);
                
                totalAmount += businessTotalAmount;
                fixedExpensesAmount += businessFixedExpensesAmount;

                // Combinar gastos por categoría
                var businessExpensesByCategory = await _expenseRepository.GetExpensesByCategoryAsync(businessId, filters.StartDate, filters.EndDate);
                allExpensesByCategory.AddRange(businessExpensesByCategory);

                // Combinar gastos mensuales
                var businessMonthlyExpenses = await _expenseRepository.GetMonthlyExpensesAsync(businessId, filters.StartDate, filters.EndDate);
                allMonthlyExpenses.AddRange(businessMonthlyExpenses);
            }

            // Agrupar gastos por categoría combinando datos de todos los negocios
            var expensesByCategory = allExpensesByCategory
                .GroupBy(e => new { e.CategoryId, e.CategoryName })
                .Select(g => new
                {
                    CategoryId = g.Key.CategoryId,
                    CategoryName = g.Key.CategoryName,
                    TotalAmount = g.Sum(e => e.TotalAmount),
                    Count = g.Sum(e => e.Count)
                })
                .ToList();

            // Agrupar gastos mensuales combinando datos de todos los negocios
            var monthlyExpenses = allMonthlyExpenses
                .GroupBy(m => new { m.Year, m.Month })
                .Select(g => new
                {
                    Year = g.Key.Year,
                    Month = g.Key.Month,
                    TotalAmount = g.Sum(m => m.TotalAmount),
                    Count = g.Sum(m => m.Count)
                })
                .OrderBy(m => m.Year)
                .ThenBy(m => m.Month)
                .ToList();

            var summary = new ExpenseSummaryDto
            {
                TotalAmount = totalAmount,
                TotalCount = expensesByCategory.Sum(e => e.Count),
                ExpensesAmount = fixedExpensesAmount,
                ExpensesCount = 0, // TODO: Implementar conteo de gastos fijos
                VariableExpensesAmount = totalAmount - fixedExpensesAmount,
                VariableExpensesCount = expensesByCategory.Sum(e => e.Count), // Por ahora, todos los gastos contados son variables
                PeriodStart = filters.StartDate,
                PeriodEnd = filters.EndDate,
                ExpensesByCategory = expensesByCategory.Select(e => new ExpenseByCategoryDto
                {
                    CategoryId = e.CategoryId,
                    CategoryName = e.CategoryName,
                    TotalAmount = e.TotalAmount,
                    Count = e.Count,
                    Percentage = totalAmount > 0 ? (e.TotalAmount / totalAmount) * 100 : 0
                }).ToList(),
                MonthlyExpenses = monthlyExpenses.Select(m => new MonthlyExpenseDto
                {
                    Year = m.Year,
                    Month = m.Month,
                    MonthName = CultureInfo.CurrentCulture.DateTimeFormat.GetMonthName(m.Month),
                    TotalAmount = m.TotalAmount,
                    Count = m.Count
                }).ToList()
            };

            return summary;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"GetExpenseSummaryAsync Error: {ex.Message}");
            Console.WriteLine($"Stack Trace: {ex.StackTrace}");
            if (ex.InnerException != null)
            {
                Console.WriteLine($"Inner Exception: {ex.InnerException.Message}");
            }
            
            throw new ApplicationException($"Error al obtener resumen de gastos: {ex.Message}", ex);
        }
    }

    public async Task<object> GetMonthlyKPIsAsync(int businessId)
    {
        try
        {
            var now = DateTime.Now;
            var firstDayThisMonth = new DateTime(now.Year, now.Month, 1);
            var lastDayThisMonth = firstDayThisMonth.AddMonths(1).AddDays(-1);
            var firstDayLastMonth = firstDayThisMonth.AddMonths(-1);
            var lastDayLastMonth = firstDayThisMonth.AddDays(-1);

            // Get expenses for this month
            var thisMonthExpenses = await _expenseRepository.GetExpensesWithDetailsAsync(
                businessId: businessId,
                startDate: firstDayThisMonth,
                endDate: lastDayThisMonth,
                page: 1,
                pageSize: int.MaxValue);

            // Get expenses for last month
            var lastMonthExpenses = await _expenseRepository.GetExpensesWithDetailsAsync(
                businessId: businessId,
                startDate: firstDayLastMonth,
                endDate: lastDayLastMonth,
                page: 1,
                pageSize: int.MaxValue);

            var expensesList = thisMonthExpenses.ToList();
            
            // Calculate totals considering payment plans
            decimal totalThisMonth = 0;
            decimal totalLastMonth = 0;
            decimal gastos = 0;
            decimal costos = 0;
            decimal inversiones = 0;
            
            // Process this month expenses
            foreach (var expense in expensesList)
            {
                decimal amountToAdd = 0;
                
                // Check if expense has payment plan
                var paymentPlans = await _paymentPlanRepository.GetByExpenseIdAsync(expense.Id);
                var paymentPlan = paymentPlans.FirstOrDefault();
                
                if (paymentPlan != null)
                {
                    // Has payment plan - get installment for this month
                    var installments = await _paymentInstallmentRepository.GetByPaymentPlanIdAsync(paymentPlan.Id);
                    var thisMonthInstallment = installments.FirstOrDefault(i => 
                        i.DueDate.Year == now.Year && 
                        i.DueDate.Month == now.Month);
                    
                    if (thisMonthInstallment != null)
                    {
                        amountToAdd = thisMonthInstallment.AmountClp;
                    }
                }
                else
                {
                    // No payment plan - use full amount
                    amountToAdd = expense.Amount;
                }
                
                totalThisMonth += amountToAdd;
                
                // Add to type-specific totals
                if (expense.ExpenseTypeId == 1) gastos += amountToAdd;
                else if (expense.ExpenseTypeId == 2) costos += amountToAdd;
                else if (expense.ExpenseTypeId == 3) inversiones += amountToAdd;
            }
            
            // Process last month expenses (same logic)
            foreach (var expense in lastMonthExpenses)
            {
                var paymentPlans = await _paymentPlanRepository.GetByExpenseIdAsync(expense.Id);
                var paymentPlan = paymentPlans.FirstOrDefault();
                
                if (paymentPlan != null)
                {
                    var installments = await _paymentInstallmentRepository.GetByPaymentPlanIdAsync(paymentPlan.Id);
                    var lastMonthInstallment = installments.FirstOrDefault(i => 
                        i.DueDate.Year == firstDayLastMonth.Year && 
                        i.DueDate.Month == firstDayLastMonth.Month);
                    
                    if (lastMonthInstallment != null)
                    {
                        totalLastMonth += lastMonthInstallment.AmountClp;
                    }
                }
                else
                {
                    totalLastMonth += expense.Amount;
                }
            }

            Console.WriteLine($"📊 GetMonthlyKPIs - This month expenses count: {expensesList.Count}");
            Console.WriteLine($"📊 GetMonthlyKPIs - This month total (with payment plans): {totalThisMonth}");
            Console.WriteLine($"📊 GetMonthlyKPIs - Gastos (type 1): {gastos}");
            Console.WriteLine($"📊 GetMonthlyKPIs - Costos (type 2): {costos}");
            Console.WriteLine($"📊 GetMonthlyKPIs - Inversiones (type 3): {inversiones}");

            Console.WriteLine($"📊 GetMonthlyKPIs - Gastos (type 1): {gastos}");
            Console.WriteLine($"📊 GetMonthlyKPIs - Costos (type 2): {costos}");
            Console.WriteLine($"📊 GetMonthlyKPIs - Inversiones (type 3): {inversiones}");

            var totalDistribution = gastos + costos + inversiones;

            // Get fixed expenses for pending/overdue calculations
            var fixedExpenses = await _fixedExpenseRepository.GetFixedExpensesWithDetailsAsync(new[] { businessId });
            
            decimal pendingAmount = 0;
            int overdueCount = 0;
            int upcomingCount = 0;
            
            foreach (var fixedExpense in fixedExpenses)
            {
                // Get associated expenses (payments) for this fixed expense
                var payments = expensesList.Where(e => e.FixedExpenseId == fixedExpense.Id).ToList();
                
                if (payments.Any())
                {
                    var lastPayment = payments.OrderByDescending(p => p.Date).First();
                    var daysSinceLastPayment = (now - lastPayment.Date).Days;
                    
                    // Determine if payment is due based on recurrence
                    bool isOverdue = false;
                    bool isUpcoming = false;
                    
                    switch (fixedExpense.RecurrenceTypeId)
                    {
                        case 1: // mensual
                            isOverdue = daysSinceLastPayment > 30;
                            isUpcoming = daysSinceLastPayment >= 25 && daysSinceLastPayment <= 30;
                            break;
                        case 2: // bimestral
                            isOverdue = daysSinceLastPayment > 60;
                            isUpcoming = daysSinceLastPayment >= 55 && daysSinceLastPayment <= 60;
                            break;
                        case 3: // trimestral
                            isOverdue = daysSinceLastPayment > 90;
                            isUpcoming = daysSinceLastPayment >= 85 && daysSinceLastPayment <= 90;
                            break;
                        case 4: // semestral
                            isOverdue = daysSinceLastPayment > 180;
                            isUpcoming = daysSinceLastPayment >= 175 && daysSinceLastPayment <= 180;
                            break;
                        case 5: // anual
                            isOverdue = daysSinceLastPayment > 365;
                            isUpcoming = daysSinceLastPayment >= 360 && daysSinceLastPayment <= 365;
                            break;
                    }
                    
                    if (isOverdue)
                    {
                        overdueCount++;
                        pendingAmount += fixedExpense.Amount;
                    }
                    else if (isUpcoming)
                    {
                        upcomingCount++;
                    }
                }
                else
                {
                    // No payments yet - this is pending
                    overdueCount++;
                    pendingAmount += fixedExpense.Amount;
                }
            }

            // Calculate budget percentage (TODO: integrate with budget system)
            var budgetExecutedPercentage = 0m; // Placeholder

            // Calculate variation
            var variationVsPreviousMonth = totalThisMonth - totalLastMonth;
            var variationPercentage = totalLastMonth > 0 
                ? ((totalThisMonth - totalLastMonth) / totalLastMonth) * 100 
                : 0;

            return new
            {
                totalThisMonth,
                budgetExecutedPercentage,
                variationVsPreviousMonth,
                variationPercentage,
                pendingAmount,
                overdueCount,
                upcomingCount,
                distribution = new
                {
                    gastos,
                    costos,
                    inversiones,
                    gastosPercentage = totalDistribution > 0 ? ((decimal)gastos / (decimal)totalDistribution) * 100 : 0,
                    costosPercentage = totalDistribution > 0 ? ((decimal)costos / (decimal)totalDistribution) * 100 : 0,
                    inversionesPercentage = totalDistribution > 0 ? ((decimal)inversiones / (decimal)totalDistribution) * 100 : 0
                }
            };
        }
        catch (Exception ex)
        {
            Console.WriteLine($"GetMonthlyKPIsAsync Error: {ex.Message}");
            throw new ApplicationException($"Error al obtener KPIs mensuales: {ex.Message}", ex);
        }
    }

    public async Task<object> GetExpenseTypeKPIsAsync(int businessId, int expenseTypeId)
    {
        try
        {
            var now = DateTime.Now;
            var firstDayThisMonth = new DateTime(now.Year, now.Month, 1);
            var lastDayThisMonth = firstDayThisMonth.AddMonths(1).AddDays(-1);

            // Get expenses for this month filtered by type
            var thisMonthExpenses = await _expenseRepository.GetExpensesWithDetailsAsync(
                businessId: businessId,
                startDate: firstDayThisMonth,
                endDate: lastDayThisMonth,
                page: 1,
                pageSize: int.MaxValue);

            var expensesList = thisMonthExpenses.Where(e => e.ExpenseTypeId == expenseTypeId).ToList();
            var totalPaid = expensesList.Sum(e => e.Amount);

            // Get fixed expenses for this type
            var fixedExpenses = await _fixedExpenseRepository.GetFixedExpensesWithDetailsAsync(new[] { businessId }, expenseTypeId);
            
            decimal pendingAmount = 0;
            int overdueCount = 0;
            int upcomingCount = 0;
            
            foreach (var fixedExpense in fixedExpenses)
            {
                // Get associated expenses (payments) for this fixed expense
                var payments = expensesList.Where(e => e.FixedExpenseId == fixedExpense.Id).ToList();
                
                if (payments.Any())
                {
                    var lastPayment = payments.OrderByDescending(p => p.Date).First();
                    var daysSinceLastPayment = (now - lastPayment.Date).Days;
                    
                    // Determine if payment is due based on recurrence
                    bool isOverdue = false;
                    bool isUpcoming = false;
                    
                    switch (fixedExpense.RecurrenceTypeId)
                    {
                        case 1: // mensual
                            isOverdue = daysSinceLastPayment > 30;
                            isUpcoming = daysSinceLastPayment >= 25 && daysSinceLastPayment <= 30;
                            break;
                        case 2: // bimestral
                            isOverdue = daysSinceLastPayment > 60;
                            isUpcoming = daysSinceLastPayment >= 55 && daysSinceLastPayment <= 60;
                            break;
                        case 3: // trimestral
                            isOverdue = daysSinceLastPayment > 90;
                            isUpcoming = daysSinceLastPayment >= 85 && daysSinceLastPayment <= 90;
                            break;
                        case 4: // semestral
                            isOverdue = daysSinceLastPayment > 180;
                            isUpcoming = daysSinceLastPayment >= 175 && daysSinceLastPayment <= 180;
                            break;
                        case 5: // anual
                            isOverdue = daysSinceLastPayment > 365;
                            isUpcoming = daysSinceLastPayment >= 360 && daysSinceLastPayment <= 365;
                            break;
                    }
                    
                    if (isOverdue)
                    {
                        overdueCount++;
                        pendingAmount += fixedExpense.Amount;
                    }
                    else if (isUpcoming)
                    {
                        upcomingCount++;
                    }
                }
                else
                {
                    // No payments yet - this is pending
                    overdueCount++;
                    pendingAmount += fixedExpense.Amount;
                }
            }

            return new
            {
                totalPaid,
                pendingAmount,
                overdueCount,
                upcomingCount
            };
        }
        catch (Exception ex)
        {
            Console.WriteLine($"GetExpenseTypeKPIsAsync Error: {ex.Message}");
            throw new ApplicationException($"Error al obtener KPIs por tipo: {ex.Message}", ex);
        }
    }

    public async Task<byte[]> ExportExpensesAsync(ExpenseFiltersDto filters)
    {
        try
        {
            var expenses = await _expenseRepository.GetExpensesWithDetailsAsync(
                businessId: filters.BusinessId,
                storeId: filters.StoreId,
                categoryId: filters.CategoryId,
                subcategoryId: filters.SubcategoryId,
                startDate: filters.StartDate,
                endDate: filters.EndDate,
                minAmount: filters.MinAmount,
                maxAmount: filters.MaxAmount,
                isFixed: filters.IsFixed,
                expenseTypeId: filters.ExpenseTypeId,
                page: 1,
                pageSize: int.MaxValue,
                orderBy: filters.OrderBy ?? "Date",
                orderDescending: filters.OrderDescending);

            // Aquí implementarías la lógica de exportación a CSV
            // Por ahora retorno un array vacío como placeholder
            return Array.Empty<byte>();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"ExportExpensesAsync Error: {ex.Message}");
            Console.WriteLine($"Stack Trace: {ex.StackTrace}");
            if (ex.InnerException != null)
            {
                Console.WriteLine($"Inner Exception: {ex.InnerException.Message}");
            }
            
            throw new ApplicationException($"Error al exportar gastos: {ex.Message}", ex);
        }
    }

    private async Task PopulatePaymentStatusAsync(FixedExpenseDto dto, FixedExpense fixedExpense)
    {
        try
        {
            // Get the last expense date for this fixed expense
            var lastExpenseDate = await _fixedExpenseRepository.GetLastExpenseDateForFixedExpenseAsync(fixedExpense.Id);

            // Use PaymentDate if available, otherwise use CreatedAt
            var startDate = fixedExpense.PaymentDate ?? fixedExpense.CreatedAt;
            var currentDate = DateTime.Now.Date;

            // Si no hay expenses asociados (lastExpenseDate es null)
            if (!lastExpenseDate.HasValue)
            {
                // Solo está al día si el StartDate es en el futuro (aún no ha vencido)
                dto.IsUpToDate = startDate.Date > currentDate;
            }
            else
            {
                // Si hay expenses asociados, usar el RecurrenceHelper
                dto.IsUpToDate = RecurrenceHelper.IsUpToDate(
                    startDate,
                    fixedExpense.RecurrenceTypeId,
                    lastExpenseDate, // lastPaymentDate
                    lastExpenseDate  // lastExpenseDate
                );
            }

            dto.NextDueDate = RecurrenceHelper.CalculateNextDueDate(
                startDate,
                fixedExpense.RecurrenceTypeId,
                lastExpenseDate
            );

            dto.LastPaymentDate = lastExpenseDate;
        }
        catch (Exception)
        {
            // If there's an error calculating payment status, set default values
            dto.IsUpToDate = false;
            dto.NextDueDate = DateTime.Now.AddDays(30); // Default to 30 days
            dto.LastPaymentDate = null;
        }
    }
    
    // Método helper para calcular estado de pago sin cargar todas las relaciones
    private (bool IsUpToDate, DateTime NextDueDate) CalculatePaymentStatus(FixedExpense fixedExpense)
    {
        try
        {
            var currentDate = DateTime.Now.Date;
            var startDate = (fixedExpense.PaymentDate ?? fixedExpense.CreatedAt).Date;
            
            // Si no hay gastos generados
            if (fixedExpense.GeneratedExpenses == null || !fixedExpense.GeneratedExpenses.Any())
            {
                var upToDate = startDate > currentDate;
                var nextDue = RecurrenceHelper.CalculateNextDueDate(
                    startDate, 
                    fixedExpense.RecurrenceTypeId, 
                    fixedExpense.PaymentDate ?? fixedExpense.CreatedAt
                );
                return (upToDate, nextDue);
            }
            
            // Obtener fecha del último gasto
            var lastExpenseDate = fixedExpense.GeneratedExpenses.Max(e => e.Date).Date;
            var nextPayment = RecurrenceHelper.CalculateNextDueDate(
                lastExpenseDate, 
                fixedExpense.RecurrenceTypeId,
                fixedExpense.PaymentDate ?? fixedExpense.CreatedAt
            );
            var paymentUpToDate = currentDate < nextPayment;
            
            return (paymentUpToDate, nextPayment);
        }
        catch (Exception)
        {
            // En caso de error, asumir que no está al día
            return (false, DateTime.Now.AddDays(30));
        }
    }

    private async Task PopulatePaymentStatusAsync(FixedExpenseWithDetailsDto dto, FixedExpense fixedExpense)
    {
        try
        {
            var enableApiDebug = Environment.GetEnvironmentVariable("ENABLE_API_DEBUG") == "true";
            
            if (enableApiDebug)
            {
                Console.WriteLine($"🔍 PopulatePaymentStatusAsync - Starting for FixedExpense ID: {fixedExpense.Id}");
                Console.WriteLine($"🔍 FixedExpense.PaymentDate: {fixedExpense.PaymentDate}");
                Console.WriteLine($"🔍 FixedExpense.CreatedAt: {fixedExpense.CreatedAt}");
                Console.WriteLine($"🔍 DTO.StartDate: {dto.StartDate}");
            }
            
            // Populate associated expenses
            if (fixedExpense.GeneratedExpenses != null && fixedExpense.GeneratedExpenses.Any())
            {
                dto.AssociatedExpenses = _mapper.Map<List<ExpenseWithDetailsDto>>(fixedExpense.GeneratedExpenses);
            }
            
            // Get the last expense date for this fixed expense
            var lastExpenseDate = await _fixedExpenseRepository.GetLastExpenseDateForFixedExpenseAsync(fixedExpense.Id);
            
            if (enableApiDebug)
            {
                Console.WriteLine($"🔍 LastExpenseDate: {lastExpenseDate}");
            }
            
            var startDate = dto.StartDate;
            var currentDate = DateTime.Now.Date;
            
            if (enableApiDebug)
            {
                Console.WriteLine($"🔍 Original StartDate: {startDate}");
                Console.WriteLine($"🔍 Current Date: {currentDate}");
                Console.WriteLine($"🔍 RecurrenceTypeId: {fixedExpense.RecurrenceTypeId}");
            }
            
            // Si no hay expenses asociados
            if (fixedExpense.GeneratedExpenses == null || !fixedExpense.GeneratedExpenses.Any())
            {
                if (enableApiDebug)
                {
                    Console.WriteLine($"🔍 No generated expenses found");
                    Console.WriteLine($"🔍 Repository returned StartDate as lastExpenseDate: {lastExpenseDate}");
                }
                
                // isUpToDate = false siempre que StartDate sea mayor al currentDate
                dto.IsUpToDate = startDate.Date > currentDate;
                
                // nextDueDate = mes siguiente al StartDate (según recurrence type)
                dto.NextDueDate = RecurrenceHelper.CalculateNextDueDate(
                    startDate, 
                    fixedExpense.RecurrenceTypeId, 
                    null // No hay último pago, usar StartDate para calcular siguiente período
                );
                
                if (enableApiDebug)
                {
                    Console.WriteLine($"🔍 No expenses - IsUpToDate: {dto.IsUpToDate}");
                    Console.WriteLine($"🔍 No expenses - NextDueDate: {dto.NextDueDate}");
                }
                
                // Para este caso, LastPaymentDate debería ser null porque no hay pagos reales
                dto.LastPaymentDate = null;
            }
            else
            {
                if (enableApiDebug)
                {
                    Console.WriteLine($"🔍 Found {fixedExpense.GeneratedExpenses.Count} generated expenses");
                    Console.WriteLine($"🔍 Real last expense date: {lastExpenseDate}");
                }
                
                // Hay expenses asociados: usar la fecha del último pago como nueva "StartDate"
                // y calcular el siguiente período desde esa fecha
                dto.IsUpToDate = RecurrenceHelper.IsUpToDate(
                    startDate, 
                    fixedExpense.RecurrenceTypeId, 
                    lastExpenseDate, // lastPaymentDate
                    lastExpenseDate  // lastExpenseDate
                );
                
                // NextDueDate = siguiente período desde la fecha del último pago
                dto.NextDueDate = RecurrenceHelper.CalculateNextDueDate(
                    startDate, 
                    fixedExpense.RecurrenceTypeId, 
                    lastExpenseDate // Usar la fecha del último pago real
                );
                
                if (enableApiDebug)
                {
                    Console.WriteLine($"🔍 Has expenses - IsUpToDate: {dto.IsUpToDate}");
                    Console.WriteLine($"🔍 Has expenses - NextDueDate: {dto.NextDueDate}");
                }
                
                // Para este caso, LastPaymentDate es la fecha del último expense real
                dto.LastPaymentDate = lastExpenseDate;
            }
            
            if (enableApiDebug)
            {
                Console.WriteLine($"🔍 Final Result - IsUpToDate: {dto.IsUpToDate}");
                Console.WriteLine($"🔍 Final Result - NextDueDate: {dto.NextDueDate}");
                Console.WriteLine($"🔍 Final Result - LastPaymentDate: {dto.LastPaymentDate}");
                Console.WriteLine($"🔍 PopulatePaymentStatusAsync - Completed successfully");
            }
        }
        catch (Exception ex)
        {
            var enableApiDebug = Environment.GetEnvironmentVariable("ENABLE_API_DEBUG") == "true";
            
            if (enableApiDebug)
            {
                Console.WriteLine($"❌ PopulatePaymentStatusAsync - Error: {ex.Message}");
                Console.WriteLine($"❌ Stack Trace: {ex.StackTrace}");
            }
            
            // If there's an error calculating payment status, set default values
            dto.IsUpToDate = false;
            dto.NextDueDate = DateTime.Now.AddDays(30); // Default to 30 days
            dto.LastPaymentDate = null;
            dto.AssociatedExpenses = new List<ExpenseWithDetailsDto>();
        }
    }

    // NUEVO: Método helper para cargar payment_plan con installments
    private async Task LoadPaymentPlanAsync(ExpenseWithDetailsDto expenseDto)
    {
        try
        {
            Console.WriteLine($"[LoadPaymentPlan] Buscando payment_plan para expense ID: {expenseDto.Id}");
            
            // Buscar si este expense tiene un payment_plan
            var paymentPlans = await _paymentPlanRepository.GetByExpenseIdAsync(expenseDto.Id);
            var paymentPlan = paymentPlans.FirstOrDefault();
            
            Console.WriteLine($"[LoadPaymentPlan] Payment plans encontrados: {paymentPlans.Count()}");
            
            if (paymentPlan != null)
            {
                Console.WriteLine($"[LoadPaymentPlan] Payment plan ID: {paymentPlan.Id}, Type: {paymentPlan.PaymentTypeId}");
                
                // Mapear el payment_plan
                var planDto = _mapper.Map<PaymentPlanWithInstallmentsDto>(paymentPlan);
                
                Console.WriteLine($"[LoadPaymentPlan] Plan mapeado, ID: {planDto.Id}");
                
                // Cargar las installments
                var installments = await _paymentInstallmentRepository.GetByPaymentPlanIdAsync(paymentPlan.Id);
                
                Console.WriteLine($"[LoadPaymentPlan] Installments encontradas: {installments.Count()}");
                
                planDto.Installments = _mapper.Map<List<PaymentInstallmentDto>>(installments);
                
                Console.WriteLine($"[LoadPaymentPlan] Installments mapeadas: {planDto.Installments.Count}");
                
                expenseDto.PaymentPlan = planDto;
                
                Console.WriteLine($"[LoadPaymentPlan] PaymentPlan asignado al expense");
            }
            else
            {
                Console.WriteLine($"[LoadPaymentPlan] No se encontró payment_plan para expense {expenseDto.Id}");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[LoadPaymentPlan] ERROR para expense {expenseDto.Id}: {ex.Message}");
            Console.WriteLine($"[LoadPaymentPlan] Stack trace: {ex.StackTrace}");
            // No lanzar excepción, solo log - el expense se devuelve sin payment_plan
        }
    }

    public async Task<ExpenseTypeChartsDto> GetExpenseTypeChartsAsync(int[] businessIds, int expenseTypeId, ExpenseFiltersDto filters)
    {
        try
        {
            // Configurar filtros para obtener expenses del tipo específico
            var expenseFilters = new ExpenseFiltersDto
            {
                BusinessIds = businessIds,
                ExpenseTypeId = expenseTypeId,
                CategoryId = filters.CategoryId,
                SubcategoryId = filters.SubcategoryId,
                StartDate = filters.StartDate,
                EndDate = filters.EndDate,
                Page = 1,
                PageSize = int.MaxValue
            };

            // Obtener todos los expenses del tipo específico
            var expenses = await GetExpensesAsync(expenseFilters);
            var expensesList = expenses.ToList();

            var result = new ExpenseTypeChartsDto();

            // 1. Distribución por categoría para gráfico de torta
            var totalAmount = expensesList.Sum(e => e.Amount);
            var categoryGroups = expensesList
                .GroupBy(e => new { e.Category.Id, e.Category.Name })
                .Select(g => new CategoryChartDataDto
                {
                    CategoryId = g.Key.Id,
                    CategoryName = g.Key.Name,
                    TotalAmount = g.Sum(e => e.Amount),
                    Count = g.Count(),
                    Percentage = totalAmount > 0 ? (g.Sum(e => e.Amount) / totalAmount * 100) : 0
                })
                .OrderByDescending(c => c.TotalAmount)
                .ToList();
            
            result.CategoryDistribution = categoryGroups;

            // 2. Evolución mensual acumulada para gráfico de línea
            var monthNames = new[] { "Ene", "Feb", "Mar", "Abr", "May", "Jun", "Jul", "Ago", "Sep", "Oct", "Nov", "Dic" };
            var monthlyGroups = expensesList
                .GroupBy(e => e.Date.Month)
                .ToDictionary(g => g.Key, g => g.Sum(e => e.Amount));

            decimal accumulated = 0;
            var monthlyTrend = new List<MonthlyChartDataDto>();
            for (int i = 1; i <= 12; i++)
            {
                var monthAmount = monthlyGroups.GetValueOrDefault(i, 0);
                accumulated += monthAmount;
                monthlyTrend.Add(new MonthlyChartDataDto
                {
                    Month = i,
                    MonthName = monthNames[i - 1],
                    MonthlyAmount = monthAmount,
                    AccumulatedAmount = accumulated
                });
            }
            result.MonthlyTrend = monthlyTrend;

            // 3. Indicadores de estado - Activas vs Finalizadas
            var activeCount = expensesList.Count(e => e.IsFixed == false || e.IsFixed == null);
            var completedCount = expensesList.Count(e => e.IsFixed == true);
            var totalCount = expensesList.Count();

            result.StatusIndicator = new StatusIndicatorDto
            {
                ActiveCount = activeCount,
                CompletedCount = completedCount,
                TotalCount = totalCount,
                ActivePercentage = totalCount > 0 ? ((decimal)activeCount / totalCount * 100) : 0,
                CompletedPercentage = totalCount > 0 ? ((decimal)completedCount / totalCount * 100) : 0
            };

            // 4. Ejecución de presupuesto por categoría (Pro feature)
            // Por ahora retornar lista vacía - se implementará cuando se integre con budgets
            result.BudgetExecution = new List<BudgetExecutionDto>();

            return result;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Error al obtener datos de gráficos para expense type {expenseTypeId}: {ex.Message}");
            Console.Error.WriteLine($"Stack trace: {ex.StackTrace}");
            throw new ApplicationException($"Error al obtener datos de visualizaciones: {ex.Message}", ex);
        }
    }
}
