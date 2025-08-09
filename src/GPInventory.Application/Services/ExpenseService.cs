using AutoMapper;
using GPInventory.Application.DTOs.Expenses;
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
            var expense = _mapper.Map<Expense>(createExpenseDto);
            
            // Asegurar que IsFixed tenga un valor válido, convirtiendo NULL a false
            expense.IsFixed = createExpenseDto.IsFixed ?? false;
            
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
                
            return _mapper.Map<IEnumerable<FixedExpenseWithDetailsDto>>(fixedExpenses);
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

            return _mapper.Map<FixedExpenseDto>(fixedExpense);
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
    public async Task<ExpenseSummaryDto> GetExpenseSummaryAsync(int businessId, ExpenseFiltersDto filters)
    {
        try
        {
            var totalAmount = await _expenseRepository.GetTotalExpensesAmountAsync(businessId, filters.StartDate, filters.EndDate);
            var fixedExpensesAmount = await _fixedExpenseRepository.GetTotalFixedExpensesAmountAsync(businessId); // Solo gastos fijos activos
            
            var expensesByCategory = await _expenseRepository.GetExpensesByCategoryAsync(businessId, filters.StartDate, filters.EndDate);
            var monthlyExpenses = await _expenseRepository.GetMonthlyExpensesAsync(businessId, filters.StartDate, filters.EndDate);

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
}
