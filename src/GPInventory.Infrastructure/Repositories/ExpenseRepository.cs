using GPInventory.Application.Interfaces;
using GPInventory.Domain.Entities;
using GPInventory.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using MySqlConnector;

namespace GPInventory.Infrastructure.Repositories;

public class ExpenseRepository : IExpenseRepository
{
    private readonly ApplicationDbContext _context;
    private readonly DbSet<Expense> _dbSet;

    public ExpenseRepository(ApplicationDbContext context)
    {
        _context = context;
        _dbSet = context.Set<Expense>();
    }

    public async Task<Expense?> GetByIdAsync(int id)
    {
        return await _dbSet.FindAsync(id);
    }

    public async Task<Expense?> GetByIdWithDetailsAsync(int id)
    {
        var sql = @"
            SELECT 
                e.id,
                e.date,
                e.subcategory_id,
                e.amount,
                e.description,
                e.is_fixed,
                e.fixed_expense_id,
                e.business_id,
                e.store_id,
                e.notes,
                e.expense_type_id,
                e.provider_id,
                s.id as sub_id,
                s.name as subcategory_name,
                s.expense_category_id,
                c.id as cat_id,
                c.name as category_name,
                p.id as prov_id,
                p.name as provider_name,
                p.contact as provider_contact,
                p.mail as provider_mail,
                p.address as provider_address
            FROM expenses e
            LEFT JOIN expense_subcategory s ON e.subcategory_id = s.id
            LEFT JOIN expense_category c ON s.expense_category_id = c.id
            LEFT JOIN provider p ON e.provider_id = p.id
            WHERE e.id = @id";

        using var command = _context.Database.GetDbConnection().CreateCommand();
        command.CommandText = sql;
        command.Parameters.Add(new MySqlParameter("@id", id));
        
        await _context.Database.OpenConnectionAsync();
        
        try
        {
            using var reader = await command.ExecuteReaderAsync();
            
            if (!await reader.ReadAsync())
                return null;
            
            var expense = new Expense
            {
                Id = reader.GetInt32(reader.GetOrdinal("id")),
                Date = reader.GetDateTime(reader.GetOrdinal("date")),
                Amount = (int)reader.GetDecimal(reader.GetOrdinal("amount")),
                Description = reader.IsDBNull(reader.GetOrdinal("description")) ? string.Empty : reader.GetString(reader.GetOrdinal("description")),
                BusinessId = reader.GetInt32(reader.GetOrdinal("business_id")),
                StoreId = reader.IsDBNull(reader.GetOrdinal("store_id")) ? null : reader.GetInt32(reader.GetOrdinal("store_id")),
                SubcategoryId = reader.GetInt32(reader.GetOrdinal("subcategory_id")),
                ProviderId = reader.IsDBNull(reader.GetOrdinal("provider_id")) ? null : reader.GetInt32(reader.GetOrdinal("provider_id")),
                IsFixed = reader.IsDBNull(reader.GetOrdinal("is_fixed")) ? null : reader.GetBoolean(reader.GetOrdinal("is_fixed")),
                FixedExpenseId = reader.IsDBNull(reader.GetOrdinal("fixed_expense_id")) ? null : reader.GetInt32(reader.GetOrdinal("fixed_expense_id")),
                ExpenseTypeId = reader.IsDBNull(reader.GetOrdinal("expense_type_id")) ? null : reader.GetInt32(reader.GetOrdinal("expense_type_id")),
                CreatedAt = DateTime.UtcNow.ToString("o"), // No hay created_at en la tabla
                Notes = reader.IsDBNull(reader.GetOrdinal("notes")) ? string.Empty : reader.GetString(reader.GetOrdinal("notes"))
            };
            
            // Cargar subcategory si existe
            if (!reader.IsDBNull(reader.GetOrdinal("sub_id")))
            {
                expense.ExpenseSubcategory = new ExpenseSubcategory
                {
                    Id = reader.GetInt32(reader.GetOrdinal("sub_id")),
                    Name = reader.GetString(reader.GetOrdinal("subcategory_name")),
                    ExpenseCategoryId = reader.GetInt32(reader.GetOrdinal("expense_category_id"))
                };
                
                // Cargar category
                if (!reader.IsDBNull(reader.GetOrdinal("cat_id")))
                {
                    expense.ExpenseSubcategory.ExpenseCategory = new ExpenseCategory
                    {
                        Id = reader.GetInt32(reader.GetOrdinal("cat_id")),
                        Name = reader.GetString(reader.GetOrdinal("category_name"))
                    };
                }
            }
            
            // Cargar provider si existe
            if (!reader.IsDBNull(reader.GetOrdinal("prov_id")))
            {
                expense.Provider = new Provider
                {
                    Id = reader.GetInt32(reader.GetOrdinal("prov_id")),
                    Name = reader.GetString(reader.GetOrdinal("provider_name")),
                    Contact = reader.IsDBNull(reader.GetOrdinal("provider_contact")) ? null : reader.GetInt32(reader.GetOrdinal("provider_contact")),
                    Mail = reader.IsDBNull(reader.GetOrdinal("provider_mail")) ? null : reader.GetString(reader.GetOrdinal("provider_mail")),
                    Address = reader.IsDBNull(reader.GetOrdinal("provider_address")) ? null : reader.GetString(reader.GetOrdinal("provider_address"))
                };
            }
            
            return expense;
        }
        finally
        {
            await _context.Database.CloseConnectionAsync();
        }
    }

    public async Task<IEnumerable<Expense>> GetAllAsync()
    {
        return await _dbSet.ToListAsync();
    }

    public async Task<Expense> AddAsync(Expense entity)
    {
        _dbSet.Add(entity);
        await _context.SaveChangesAsync();
        return entity;
    }

    public async Task UpdateAsync(Expense entity)
    {
        _dbSet.Update(entity);
        await _context.SaveChangesAsync();
    }

    public async Task DeleteAsync(int id)
    {
        var entity = await GetByIdAsync(id);
        if (entity != null)
        {
            _dbSet.Remove(entity);
            await _context.SaveChangesAsync();
        }
    }

    public async Task<IEnumerable<Expense>> GetExpensesWithDetailsAsync(
        int? businessId = null,
        int[]? businessIds = null,
        int? storeId = null,
        int? categoryId = null,
        int? subcategoryId = null,
        DateTime? startDate = null,
        DateTime? endDate = null,
        int? minAmount = null,
        int? maxAmount = null,
        bool? isFixed = null,
        int? expenseTypeId = null,
        int page = 1,
        int pageSize = 10,
        string orderBy = "Date",
        bool orderDescending = true)
    {
        // Validar que se proporcione al menos un business ID
        if (!businessId.HasValue && (businessIds == null || businessIds.Length == 0))
        {
            return new List<Expense>();
        }

        // Construir el SQL dinámicamente
        var sql = @"
            SELECT 
                e.id,
                e.date,
                e.amount,
                e.description,
                e.is_fixed,
                e.fixed_expense_id,
                e.business_id,
                e.store_id,
                e.expense_type_id,
                e.provider_id,
                e.subcategory_id,
                sub.id as sub_id,
                sub.name as subcategory_name,
                sub.expense_category_id,
                cat.id as cat_id,
                cat.name as category_name,
                p.id as prov_id,
                p.name as provider_name
            FROM expenses e
            LEFT JOIN expense_subcategory sub ON e.subcategory_id = sub.id
            LEFT JOIN expense_category cat ON sub.expense_category_id = cat.id
            LEFT JOIN provider p ON e.provider_id = p.id
            WHERE 1=1";

        var parameters = new List<MySqlParameter>();

        // Filtro por business IDs
        if (businessIds != null && businessIds.Length > 0)
        {
            var businessIdParams = string.Join(",", businessIds.Select((_, i) => $"@businessId{i}"));
            sql += $" AND e.business_id IN ({businessIdParams})";
            for (int i = 0; i < businessIds.Length; i++)
            {
                parameters.Add(new MySqlParameter($"@businessId{i}", businessIds[i]));
            }
        }
        else if (businessId.HasValue)
        {
            sql += " AND e.business_id = @businessId";
            parameters.Add(new MySqlParameter("@businessId", businessId.Value));
        }

        // Filtro por store_id
        if (storeId.HasValue)
        {
            sql += " AND e.store_id = @storeId";
            parameters.Add(new MySqlParameter("@storeId", storeId.Value));
        }

        // Filtro por categoría
        if (categoryId.HasValue)
        {
            sql += " AND cat.id = @categoryId";
            parameters.Add(new MySqlParameter("@categoryId", categoryId.Value));
        }

        // Filtro por subcategoría
        if (subcategoryId.HasValue)
        {
            sql += " AND e.subcategory_id = @subcategoryId";
            parameters.Add(new MySqlParameter("@subcategoryId", subcategoryId.Value));
        }

        // Filtro por fecha
        if (startDate.HasValue)
        {
            sql += " AND e.date >= @startDate";
            parameters.Add(new MySqlParameter("@startDate", startDate.Value));
        }

        if (endDate.HasValue)
        {
            sql += " AND e.date <= @endDate";
            parameters.Add(new MySqlParameter("@endDate", endDate.Value));
        }

        // Filtro por monto
        if (minAmount.HasValue)
        {
            sql += " AND e.amount >= @minAmount";
            parameters.Add(new MySqlParameter("@minAmount", minAmount.Value));
        }

        if (maxAmount.HasValue)
        {
            sql += " AND e.amount <= @maxAmount";
            parameters.Add(new MySqlParameter("@maxAmount", maxAmount.Value));
        }

        // Filtro por is_fixed
        if (isFixed.HasValue)
        {
            sql += " AND e.is_fixed = @isFixed";
            parameters.Add(new MySqlParameter("@isFixed", isFixed.Value));
        }

        // Filtro por expense_type_id
        if (expenseTypeId.HasValue)
        {
            sql += " AND e.expense_type_id = @expenseTypeId";
            parameters.Add(new MySqlParameter("@expenseTypeId", expenseTypeId.Value));
        }

        // Ordenamiento
        var orderColumn = orderBy.ToLower() switch
        {
            "amount" => "e.amount",
            "description" => "e.description",
            _ => "e.date"
        };
        var orderDirection = orderDescending ? "DESC" : "ASC";
        sql += $" ORDER BY {orderColumn} {orderDirection}";

        // Paginación
        sql += " LIMIT @pageSize OFFSET @offset";
        parameters.Add(new MySqlParameter("@pageSize", pageSize));
        parameters.Add(new MySqlParameter("@offset", (page - 1) * pageSize));

        // Ejecutar query
        var connection = _context.Database.GetDbConnection();
        await connection.OpenAsync();

        try
        {
            using var command = connection.CreateCommand();
            command.CommandText = sql;
            command.Parameters.AddRange(parameters.ToArray());

            using var reader = await command.ExecuteReaderAsync();
            var expenses = new List<Expense>();

            while (await reader.ReadAsync())
            {
                // Skip expenses without subcategory (data integrity issue)
                if (reader.IsDBNull(reader.GetOrdinal("subcategory_id")))
                    continue;

                var expense = new Expense
                {
                    Id = reader.GetInt32(reader.GetOrdinal("id")),
                    Date = reader.GetDateTime(reader.GetOrdinal("date")),
                    Amount = reader.GetDecimal(reader.GetOrdinal("amount")),
                    Description = reader.IsDBNull(reader.GetOrdinal("description")) ? string.Empty : (reader.GetString(reader.GetOrdinal("description")) ?? string.Empty),
                    IsFixed = reader.IsDBNull(reader.GetOrdinal("is_fixed")) ? null : reader.GetBoolean(reader.GetOrdinal("is_fixed")),
                    FixedExpenseId = reader.IsDBNull(reader.GetOrdinal("fixed_expense_id")) ? null : reader.GetInt32(reader.GetOrdinal("fixed_expense_id")),
                    BusinessId = reader.GetInt32(reader.GetOrdinal("business_id")),
                    StoreId = reader.IsDBNull(reader.GetOrdinal("store_id")) ? null : reader.GetInt32(reader.GetOrdinal("store_id")),
                    ExpenseTypeId = reader.IsDBNull(reader.GetOrdinal("expense_type_id")) ? null : reader.GetInt32(reader.GetOrdinal("expense_type_id")),
                    ProviderId = reader.IsDBNull(reader.GetOrdinal("provider_id")) ? null : reader.GetInt32(reader.GetOrdinal("provider_id")),
                    SubcategoryId = reader.GetInt32(reader.GetOrdinal("subcategory_id"))
                };

                // Cargar subcategory si existe
                if (!reader.IsDBNull(reader.GetOrdinal("sub_id")))
                {
                    expense.ExpenseSubcategory = new ExpenseSubcategory
                    {
                        Id = reader.GetInt32(reader.GetOrdinal("sub_id")),
                        Name = reader.GetString(reader.GetOrdinal("subcategory_name")),
                        ExpenseCategoryId = reader.GetInt32(reader.GetOrdinal("expense_category_id"))
                    };

                    // Cargar category si existe
                    if (!reader.IsDBNull(reader.GetOrdinal("cat_id")))
                    {
                        expense.ExpenseSubcategory.ExpenseCategory = new ExpenseCategory
                        {
                            Id = reader.GetInt32(reader.GetOrdinal("cat_id")),
                            Name = reader.GetString(reader.GetOrdinal("category_name"))
                        };
                    }
                }

                // Cargar provider si existe
                if (!reader.IsDBNull(reader.GetOrdinal("prov_id")))
                {
                    expense.Provider = new Provider
                    {
                        Id = reader.GetInt32(reader.GetOrdinal("prov_id")),
                        Name = reader.GetString(reader.GetOrdinal("provider_name"))
                    };
                }

                expenses.Add(expense);
            }

            return expenses;
        }
        finally
        {
            await connection.CloseAsync();
        }
    }

    public async Task<decimal> GetTotalExpensesAmountAsync(int businessId, DateTime? startDate = null, DateTime? endDate = null)
    {
        var query = _dbSet.Where(e => e.BusinessId == businessId);

        if (startDate.HasValue)
            query = query.Where(e => e.Date >= startDate.Value);

        if (endDate.HasValue)
            query = query.Where(e => e.Date <= endDate.Value);

        return (decimal)await query.SumAsync(e => e.Amount);
    }

    public async Task<IEnumerable<(int CategoryId, string CategoryName, decimal TotalAmount, int Count)>> GetExpensesByCategoryAsync(
        int businessId, DateTime? startDate = null, DateTime? endDate = null)
    {
        var query = _dbSet
            .Include(e => e.ExpenseSubcategory)
                .ThenInclude(s => s.ExpenseCategory)
            .Where(e => e.BusinessId == businessId);

        if (startDate.HasValue)
            query = query.Where(e => e.Date >= startDate.Value);

        if (endDate.HasValue)
            query = query.Where(e => e.Date <= endDate.Value);

        return await query
            .GroupBy(e => new { e.ExpenseSubcategory.ExpenseCategory.Id, e.ExpenseSubcategory.ExpenseCategory.Name })
            .Select(g => new
            {
                CategoryId = g.Key.Id,
                CategoryName = g.Key.Name,
                TotalAmount = (decimal)g.Sum(e => e.Amount), // Cast to decimal
                Count = g.Count()
            })
            .Select(x => ValueTuple.Create(x.CategoryId, x.CategoryName, x.TotalAmount, x.Count))
            .ToListAsync();
    }

    public async Task<IEnumerable<(int Year, int Month, decimal TotalAmount, int Count)>> GetMonthlyExpensesAsync(
        int businessId, DateTime? startDate = null, DateTime? endDate = null)
    {
        var query = _dbSet.Where(e => e.BusinessId == businessId);

        if (startDate.HasValue)
            query = query.Where(e => e.Date >= startDate.Value);

        if (endDate.HasValue)
            query = query.Where(e => e.Date <= endDate.Value);

        return await query
            .GroupBy(e => new { e.Date.Year, e.Date.Month })
            .Select(g => new
            {
                Year = g.Key.Year,
                Month = g.Key.Month,
                TotalAmount = (decimal)g.Sum(e => e.Amount), // Cast to decimal
                Count = g.Count()
            })
            .OrderBy(x => x.Year)
            .ThenBy(x => x.Month)
            .Select(x => ValueTuple.Create(x.Year, x.Month, x.TotalAmount, x.Count))
            .ToListAsync();
    }
}
