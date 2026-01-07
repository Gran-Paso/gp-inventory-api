using GPInventory.Application.Interfaces;
using GPInventory.Domain.Entities;
using GPInventory.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace GPInventory.Infrastructure.Repositories;

public class FixedExpenseRepository : IFixedExpenseRepository
{
    private readonly ApplicationDbContext _context;
    private readonly DbSet<FixedExpense> _dbSet;

    public FixedExpenseRepository(ApplicationDbContext context)
    {
        _context = context;
        _dbSet = context.Set<FixedExpense>();
    }

    public async Task<FixedExpense?> GetByIdAsync(int id)
    {
        try
        {
            return await _dbSet.FindAsync(id);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"GetByIdAsync Error: {ex.Message}");
            Console.WriteLine($"Stack Trace: {ex.StackTrace}");
            if (ex.InnerException != null)
            {
                Console.WriteLine($"Inner Exception: {ex.InnerException.Message}");
            }
            
            throw new ApplicationException($"Error al obtener gasto fijo con ID {id}: {ex.Message}", ex);
        }
    }

    public async Task<IEnumerable<FixedExpense>> GetAllAsync()
    {
        try
        {
            return await _dbSet.ToListAsync();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"GetAllAsync Error: {ex.Message}");
            Console.WriteLine($"Stack Trace: {ex.StackTrace}");
            if (ex.InnerException != null)
            {
                Console.WriteLine($"Inner Exception: {ex.InnerException.Message}");
            }
            
            throw new ApplicationException($"Error al obtener todos los gastos fijos: {ex.Message}", ex);
        }
    }

    public async Task<FixedExpense> AddAsync(FixedExpense entity)
    {
        try
        {
            Console.WriteLine($"AddAsync called with entity: BusinessId={entity.BusinessId}, Amount={entity.Amount}, AdditionalNote={entity.AdditionalNote}");
            Console.WriteLine($"Entity RecurrenceTypeId: {entity.RecurrenceTypeId}");
            Console.WriteLine($"Entity SubcategoryId: {entity.SubcategoryId}");
            Console.WriteLine($"Entity PaymentDate: {entity.PaymentDate}");
            Console.WriteLine($"Entity IsActive: {entity.IsActive}");
            Console.WriteLine($"Entity CreatedAt: {entity.CreatedAt}");
            Console.WriteLine($"Entity UpdatedAt: {entity.UpdatedAt}");
            
            _dbSet.Add(entity);
            Console.WriteLine("Entity added to DbSet, calling SaveChangesAsync...");
            
            await _context.SaveChangesAsync();
            Console.WriteLine($"SaveChangesAsync completed successfully. Entity ID: {entity.Id}");
            
            return entity;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"AddAsync Error: {ex.Message}");
            Console.WriteLine($"Stack Trace: {ex.StackTrace}");
            if (ex.InnerException != null)
            {
                Console.WriteLine($"Inner Exception: {ex.InnerException.Message}");
                Console.WriteLine($"Inner Exception Stack Trace: {ex.InnerException.StackTrace}");
            }
            
            throw new ApplicationException($"Error al agregar gasto fijo: {ex.Message}", ex);
        }
    }

    public async Task UpdateAsync(FixedExpense entity)
    {
        try
        {
            _dbSet.Update(entity);
            await _context.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"UpdateAsync Error: {ex.Message}");
            Console.WriteLine($"Stack Trace: {ex.StackTrace}");
            if (ex.InnerException != null)
            {
                Console.WriteLine($"Inner Exception: {ex.InnerException.Message}");
            }
            
            throw new ApplicationException($"Error al actualizar gasto fijo: {ex.Message}", ex);
        }
    }

    public async Task DeleteAsync(int id)
    {
        try
        {
            var entity = await GetByIdAsync(id);
            if (entity != null)
            {
                _dbSet.Remove(entity);
                await _context.SaveChangesAsync();
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"DeleteAsync Error: {ex.Message}");
            Console.WriteLine($"Stack Trace: {ex.StackTrace}");
            if (ex.InnerException != null)
            {
                Console.WriteLine($"Inner Exception: {ex.InnerException.Message}");
            }
            
            throw new ApplicationException($"Error al eliminar gasto fijo con ID {id}: {ex.Message}", ex);
        }
    }

    public async Task<IEnumerable<FixedExpense>> GetFixedExpensesWithDetailsAsync(int[]? businessIds = null, int? expenseTypeId = null)
    {
        try
        {
            Console.WriteLine($"GetFixedExpensesWithDetailsAsync called with businessIds: {(businessIds != null ? string.Join(",", businessIds) : "null")}, expenseTypeId: {expenseTypeId}");
            
            // Usar IgnoreAutoIncludes para evitar que EF Core genere columnas incorrectas
            var query = _context.Set<FixedExpense>()
                .IgnoreAutoIncludes()
                .Include(fe => fe.Subcategory)
                    .ThenInclude(s => s!.ExpenseCategory)
                .Include(fe => fe.RecurrenceType)
                .Include(fe => fe.Store)
                .Include(fe => fe.Business)
                .AsQueryable();

            Console.WriteLine($"Base query created");

            // Aplicar filtro de businessIds
            if (businessIds?.Length > 0)
            {
                Console.WriteLine($"Applying businessIds filter with {businessIds.Length} business IDs");
                query = query.Where(fe => businessIds.Contains(fe.BusinessId));
            }
            else
            {
                Console.WriteLine("No businessIds filter applied - will return all businesses");
            }

            // Aplicar filtro de expenseTypeId
            if (expenseTypeId.HasValue)
            {
                Console.WriteLine($"Applying expenseTypeId filter with {expenseTypeId.Value}");
                query = query.Where(fe => fe.ExpenseTypeId == expenseTypeId.Value);
            }
            else
            {
                Console.WriteLine("No expenseTypeId filter applied - will return all expense types");
            }

            // Ordenar por fecha de creación descendente
            query = query.OrderByDescending(fe => fe.CreatedAt);

            var result = await query.ToListAsync();
            
            Console.WriteLine($"Retrieved {result.Count} fixed expenses from database");
            
            // Cargar manualmente los GeneratedExpenses con sus Providers usando raw SQL
            foreach (var fixedExpense in result)
            {
                await LoadGeneratedExpensesWithProvidersAsync(fixedExpense);
            }
            
            if (result.Count == 0)
            {
                Console.WriteLine("No fixed expenses found for the specified business IDs");
            }
            
            return result;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"GetFixedExpensesWithDetailsAsync Error: {ex.Message}");
            Console.WriteLine($"Stack Trace: {ex.StackTrace}");
            if (ex.InnerException != null)
            {
                Console.WriteLine($"Inner Exception: {ex.InnerException.Message}");
            }
            
            throw new ApplicationException($"Error al obtener gastos fijos con detalles: {ex.Message}", ex);
        }
    }

    private async Task LoadGeneratedExpensesWithProvidersAsync(FixedExpense fixedExpense)
    {
        var connection = _context.Database.GetDbConnection();
        await _context.Database.OpenConnectionAsync();

        try
        {
            using var command = connection.CreateCommand();
            command.CommandText = @"
                SELECT 
                    e.id, e.date, e.subcategory_id, e.amount, e.description, 
                    e.is_fixed, e.fixed_expense_id, e.business_id, e.store_id, 
                    e.expense_type_id, e.provider_id,
                    p.id as provider_id_val, p.name as provider_name, p.id_business, 
                    p.id_store as provider_store_id, p.contact, p.address, p.mail, 
                    p.prefix, p.active, p.created_at as provider_created_at, 
                    p.updated_at as provider_updated_at
                FROM expenses e
                LEFT JOIN provider p ON e.provider_id = p.id
                WHERE e.fixed_expense_id = @fixedExpenseId
                ORDER BY e.date DESC";

            var param = command.CreateParameter();
            param.ParameterName = "@fixedExpenseId";
            param.Value = fixedExpense.Id;
            command.Parameters.Add(param);

            using var reader = await command.ExecuteReaderAsync();
            var expenses = new List<Expense>();

            while (await reader.ReadAsync())
            {
                var expense = new Expense
                {
                    Id = reader.GetInt32(0),
                    Date = reader.GetDateTime(1),
                    SubcategoryId = reader.GetInt32(2),
                    Amount = reader.GetInt32(3),
                    Description = reader.GetString(4),
                    IsFixed = !reader.IsDBNull(5) && reader.GetBoolean(5),
                    FixedExpenseId = !reader.IsDBNull(6) ? reader.GetInt32(6) : null,
                    BusinessId = reader.GetInt32(7),
                    StoreId = !reader.IsDBNull(8) ? reader.GetInt32(8) : null,
                    ExpenseTypeId = !reader.IsDBNull(9) ? reader.GetInt32(9) : null,
                    ProviderId = !reader.IsDBNull(10) ? reader.GetInt32(10) : null,
                    CreatedAt = DateTime.UtcNow.ToString("o") // Usar fecha actual como default
                };

                // Cargar el Provider si existe
                if (!reader.IsDBNull(11)) // provider_id_val
                {
                    expense.Provider = new Provider(
                        name: reader.GetString(12), // provider_name
                        businessId: reader.GetInt32(13), // id_business
                        storeId: !reader.IsDBNull(14) ? reader.GetInt32(14) : null // provider_store_id
                    )
                    {
                        Id = reader.GetInt32(11), // provider_id_val
                        Contact = !reader.IsDBNull(15) ? reader.GetInt32(15) : null,
                        Address = !reader.IsDBNull(16) ? reader.GetString(16) : null,
                        Mail = !reader.IsDBNull(17) ? reader.GetString(17) : null,
                        Prefix = !reader.IsDBNull(18) ? reader.GetString(18) : null,
                        Active = !reader.IsDBNull(19) && reader.GetBoolean(19),
                        CreatedAt = reader.GetDateTime(20),
                        UpdatedAt = reader.GetDateTime(21)
                    };
                }

                expenses.Add(expense);
            }

            fixedExpense.GeneratedExpenses = expenses;
        }
        finally
        {
            await _context.Database.CloseConnectionAsync();
        }
    }

    public async Task<decimal> GetTotalFixedExpensesAmountAsync(int businessId)
    {
        try
        {
            var query = _context.Set<FixedExpense>()
                .Where(fe => fe.BusinessId == businessId);

            return await query.SumAsync(fe => fe.Amount);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"GetTotalFixedExpensesAmountAsync Error: {ex.Message}");
            Console.WriteLine($"Stack Trace: {ex.StackTrace}");
            if (ex.InnerException != null)
            {
                Console.WriteLine($"Inner Exception: {ex.InnerException.Message}");
            }
            
            throw new ApplicationException($"Error al obtener monto total de gastos fijos: {ex.Message}", ex);
        }
    }

    public async Task<IEnumerable<(int CategoryId, string CategoryName, decimal TotalAmount, int Count)>> GetFixedExpensesByCategoryAsync(int businessId)
    {
        try
        {
            var query = _context.Set<FixedExpense>()
                .Include(fe => fe.Subcategory!)
                    .ThenInclude(s => s.ExpenseCategory)
                .Where(fe => fe.BusinessId == businessId);

            return await query
                .Where(fe => fe.Subcategory != null) // Solo gastos con subcategoría
                .GroupBy(fe => new { fe.Subcategory!.ExpenseCategoryId, fe.Subcategory.ExpenseCategory.Name })
                .Select(g => new
                {
                    CategoryId = g.Key.ExpenseCategoryId,
                    CategoryName = g.Key.Name,
                    TotalAmount = g.Sum(fe => fe.Amount),
                    Count = g.Count()
                })
                .Select(x => new ValueTuple<int, string, decimal, int>(x.CategoryId, x.CategoryName, x.TotalAmount, x.Count))
                .ToListAsync();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"GetFixedExpensesByCategoryAsync Error: {ex.Message}");
            Console.WriteLine($"Stack Trace: {ex.StackTrace}");
            if (ex.InnerException != null)
            {
                Console.WriteLine($"Inner Exception: {ex.InnerException.Message}");
            }
            
            throw new ApplicationException($"Error al obtener gastos fijos por categoría: {ex.Message}", ex);
        }
    }

    public async Task<IEnumerable<FixedExpense>> GetActiveFixedExpensesForGenerationAsync(DateTime currentDate)
    {
        try
        {
            return await _context.Set<FixedExpense>()
                .Include(fe => fe.Subcategory!)
                    .ThenInclude(s => s.ExpenseCategory)
                .Where(fe => (fe.PaymentDate == null || fe.PaymentDate <= currentDate) &&
                             (fe.EndDate == null || fe.EndDate >= currentDate))
                .ToListAsync();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"GetActiveFixedExpensesForGenerationAsync Error: {ex.Message}");
            Console.WriteLine($"Stack Trace: {ex.StackTrace}");
            if (ex.InnerException != null)
            {
                Console.WriteLine($"Inner Exception: {ex.InnerException.Message}");
            }
            
            throw new ApplicationException($"Error al obtener gastos fijos activos para generación: {ex.Message}", ex);
        }
    }

    public async Task<DateTime?> GetLastExpenseDateForFixedExpenseAsync(int fixedExpenseId)
    {
        try
        {
            // Buscar el último expense asociado a este fixed expense
            var lastExpense = await _context.Set<Expense>()
                .Where(e => e.FixedExpenseId == fixedExpenseId)
                .OrderByDescending(e => e.Date)
                .FirstOrDefaultAsync();

            // Si hay expenses asociados, retornar la fecha del último
            if (lastExpense != null)
            {
                return lastExpense.Date;
            }

            // Si no hay expenses asociados, retornar la StartDate del FixedExpense
            var fixedExpense = await _context.Set<FixedExpense>()
                .Where(fe => fe.Id == fixedExpenseId)
                .FirstOrDefaultAsync();
            
            if (fixedExpense != null)
            {
                // Usar PaymentDate si está disponible, sino CreatedAt
                return fixedExpense.PaymentDate ?? fixedExpense.PaymentDate;
            }

            return null; // Si no se encuentra el FixedExpense
        }
        catch (Exception ex)
        {
            Console.WriteLine($"GetLastExpenseDateForFixedExpenseAsync Error: {ex.Message}");
            Console.WriteLine($"Stack Trace: {ex.StackTrace}");
            if (ex.InnerException != null)
            {
                Console.WriteLine($"Inner Exception: {ex.InnerException.Message}");
            }
            
            throw new ApplicationException($"Error al obtener última fecha de gasto para gasto fijo {fixedExpenseId}: {ex.Message}", ex);
        }
    }
}
