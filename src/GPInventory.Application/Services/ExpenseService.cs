using AutoMapper;
using GPInventory.Application.DTOs.Expenses;
using GPInventory.Application.Helpers;
using GPInventory.Application.Interfaces;
using GPInventory.Domain.Entities;
using System.Globalization;

namespace GPInventory.Application.Services;

public class ExpenseService : IExpenseService
{
    private readonly IExpenseRepository _expenseRepository;
    private readonly IFixedExpenseRepository _fixedExpenseRepository;
    private readonly IExpenseCategoryRepository _categoryRepository;
    private readonly IExpenseSubcategoryRepository _subcategoryRepository;
    private readonly IRecurrenceTypeRepository _recurrenceTypeRepository;
    private readonly IMapper _mapper;

    public ExpenseService(
        IExpenseRepository expenseRepository,
        IFixedExpenseRepository fixedExpenseRepository,
        IExpenseCategoryRepository categoryRepository,
        IExpenseSubcategoryRepository subcategoryRepository,
        IRecurrenceTypeRepository recurrenceTypeRepository,
        IMapper mapper)
    {
        _expenseRepository = expenseRepository;
        _fixedExpenseRepository = fixedExpenseRepository;
        _categoryRepository = categoryRepository;
        _subcategoryRepository = subcategoryRepository;
        _recurrenceTypeRepository = recurrenceTypeRepository;
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
                page: filters.Page,
                pageSize: filters.PageSize,
                orderBy: filters.OrderBy ?? "Date",
                orderDescending: filters.OrderDescending);

            return _mapper.Map<IEnumerable<ExpenseWithDetailsDto>>(expenses);
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
            Console.WriteLine($"CreateExpenseAsync - createExpenseDto JSON: {System.Text.Json.JsonSerializer.Serialize(createExpenseDto)}");
            
            var expense = _mapper.Map<Expense>(createExpenseDto);
            
            Console.WriteLine($"CreateExpenseAsync - After mapping, expense.IsFixed: {expense.IsFixed}");
            Console.WriteLine($"CreateExpenseAsync - After mapping, expense.FixedExpenseId: {expense.FixedExpenseId}");
            
            // Asegurar que IsFixed tenga un valor válido, convirtiendo NULL a false
            expense.IsFixed = createExpenseDto.IsFixed ?? false;
            
            Console.WriteLine($"CreateExpenseAsync - After manual assignment, expense.IsFixed: {expense.IsFixed}");
            
            var createdExpense = await _expenseRepository.AddAsync(expense);
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
    public async Task<IEnumerable<FixedExpenseWithDetailsDto>> GetFixedExpensesAsync(int[]? businessIds = null)
    {
        try
        {
            Console.WriteLine($"ExpenseService.GetFixedExpensesAsync called with businessIds: {(businessIds != null ? string.Join(",", businessIds) : "null")}");
            
            // Validar que al menos se proporcione un businessId
            if (businessIds == null || businessIds.Length == 0)
            {
                throw new ArgumentException("Se debe proporcionar al menos un ID de negocio");
            }
            
            var fixedExpenses = await _fixedExpenseRepository.GetFixedExpensesWithDetailsAsync(
                businessIds: businessIds);
                
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
                dto.AssociatedExpenses = _mapper.Map<List<ExpenseDto>>(fixedExpense.GeneratedExpenses);
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
            dto.AssociatedExpenses = new List<ExpenseDto>();
        }
    }
}
