using System.Data;
using GPInventory.Application.DTOs.Expenses;
using GPInventory.Application.Interfaces;
using MySqlConnector;
using Microsoft.Extensions.Configuration;

namespace GPInventory.Infrastructure.Repositories;

public class ExpenseSqlRepository : IExpenseSqlRepository
{
    private readonly string _connectionString;

    public ExpenseSqlRepository(IConfiguration configuration)
    {
        _connectionString = configuration.GetConnectionString("DefaultConnection") 
            ?? throw new ArgumentNullException(nameof(configuration));
    }

    public async Task<List<ExpenseWithDetailsDto>> GetExpensesByTypeAsync(
        int businessId, 
        int? expenseTypeId = null,
        int? categoryId = null,
        int? subcategoryId = null,
        DateTime? startDate = null,
        DateTime? endDate = null,
        int page = 1,
        int pageSize = 50)
    {
        var expenses = new List<ExpenseWithDetailsDto>();
        
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
                s.name as store_name,
                sc.id as subcategory_id,
                sc.name as subcategory_name,
                c.id as category_id,
                c.name as category_name,
                et.id as expense_type_id_detail,
                et.name as expense_type_name,
                et.code as expense_type_code
            FROM expenses e
            INNER JOIN expense_subcategory sc ON e.subcategory_id = sc.id
            INNER JOIN expense_category c ON sc.expense_category_id = c.id
            LEFT JOIN store s ON e.store_id = s.id
            LEFT JOIN expense_types et ON e.expense_type_id = et.id
            WHERE e.business_id = @BusinessId
                AND (@ExpenseTypeId IS NULL OR e.expense_type_id = @ExpenseTypeId)
                AND (@CategoryId IS NULL OR c.id = @CategoryId)
                AND (@SubcategoryId IS NULL OR sc.id = @SubcategoryId)
                AND (@StartDate IS NULL OR e.date >= @StartDate)
                AND (@EndDate IS NULL OR e.date <= @EndDate)
            ORDER BY e.date DESC
            LIMIT @PageSize OFFSET @Offset";

        using var connection = new MySqlConnection(_connectionString);
        using var command = new MySqlCommand(sql, connection);
        
        command.Parameters.AddWithValue("@BusinessId", businessId);
        command.Parameters.AddWithValue("@ExpenseTypeId", (object?)expenseTypeId ?? DBNull.Value);
        command.Parameters.AddWithValue("@CategoryId", (object?)categoryId ?? DBNull.Value);
        command.Parameters.AddWithValue("@SubcategoryId", (object?)subcategoryId ?? DBNull.Value);
        command.Parameters.AddWithValue("@StartDate", (object?)startDate ?? DBNull.Value);
        command.Parameters.AddWithValue("@EndDate", (object?)endDate ?? DBNull.Value);
        command.Parameters.AddWithValue("@Offset", (page - 1) * pageSize);
        command.Parameters.AddWithValue("@PageSize", pageSize);

        await connection.OpenAsync();
        using var reader = await command.ExecuteReaderAsync();
        
        while (await reader.ReadAsync())
        {
            var expense = new ExpenseWithDetailsDto
            {
                Id = reader.GetInt32("id"),
                Date = reader.GetDateTime("date"),
                Amount = reader.GetDecimal("amount"),
                Description = reader.GetString("description"),
                IsFixed = reader.IsDBNull("is_fixed") ? null : reader.GetBoolean("is_fixed"),
                FixedExpenseId = reader.IsDBNull("fixed_expense_id") ? null : reader.GetInt32("fixed_expense_id"),
                BusinessId = reader.GetInt32("business_id"),
                StoreId = reader.IsDBNull("store_id") ? null : reader.GetInt32("store_id"),
                StoreName = reader.IsDBNull("store_name") ? null : reader.GetString("store_name"),
                CreatedAt = reader.GetDateTime("date"), // Using date as created_at fallback
                ExpenseTypeId = reader.IsDBNull("expense_type_id") ? null : reader.GetInt32("expense_type_id"),
                
                // Subcategory details
                Subcategory = new ExpenseSubcategoryDto
                {
                    Id = reader.GetInt32("subcategory_id"),
                    Name = reader.GetString("subcategory_name"),
                    ExpenseCategoryId = reader.GetInt32("category_id")
                },
                
                // Category details
                Category = new ExpenseCategoryDto
                {
                    Id = reader.GetInt32("category_id"),
                    Name = reader.GetString("category_name")
                }
            };
            
            expenses.Add(expense);
        }

        return expenses;
    }

    public async Task<ExpenseSummaryDto> GetExpenseSummaryByTypeAsync(
        int businessId,
        int? expenseTypeId = null,
        DateTime? startDate = null,
        DateTime? endDate = null)
    {
        var sql = @"
            SELECT 
                COUNT(*) as TotalCount,
                COALESCE(SUM(e.amount), 0) as TotalAmount,
                COUNT(CASE WHEN e.is_fixed = 1 THEN 1 END) as FixedCount,
                COALESCE(SUM(CASE WHEN e.is_fixed = 1 THEN e.amount ELSE 0 END), 0) as FixedAmount,
                COUNT(CASE WHEN e.is_fixed = 0 OR e.is_fixed IS NULL THEN 1 END) as VariableCount,
                COALESCE(SUM(CASE WHEN e.is_fixed = 0 OR e.is_fixed IS NULL THEN e.amount ELSE 0 END), 0) as VariableAmount
            FROM expenses e
            WHERE e.business_id = @BusinessId
                AND (@ExpenseTypeId IS NULL OR e.expense_type_id = @ExpenseTypeId)
                AND (@StartDate IS NULL OR e.date >= @StartDate)
                AND (@EndDate IS NULL OR e.date <= @EndDate)";

        using var connection = new MySqlConnection(_connectionString);
        using var command = new MySqlCommand(sql, connection);
        
        command.Parameters.AddWithValue("@BusinessId", businessId);
        command.Parameters.AddWithValue("@ExpenseTypeId", (object?)expenseTypeId ?? DBNull.Value);
        command.Parameters.AddWithValue("@StartDate", (object?)startDate ?? DBNull.Value);
        command.Parameters.AddWithValue("@EndDate", (object?)endDate ?? DBNull.Value);

        await connection.OpenAsync();
        using var reader = await command.ExecuteReaderAsync();
        
        if (await reader.ReadAsync())
        {
            return new ExpenseSummaryDto
            {
                TotalCount = reader.GetInt32("TotalCount"),
                TotalAmount = reader.GetDecimal("TotalAmount"),
                ExpensesCount = reader.GetInt32("FixedCount"),
                ExpensesAmount = reader.GetDecimal("FixedAmount"),
                VariableExpensesCount = reader.GetInt32("VariableCount"),
                VariableExpensesAmount = reader.GetDecimal("VariableAmount"),
                PeriodStart = startDate,
                PeriodEnd = endDate,
                ExpensesByCategory = new List<ExpenseByCategoryDto>(),
                MonthlyExpenses = new List<MonthlyExpenseDto>()
            };
        }

        return new ExpenseSummaryDto();
    }
}