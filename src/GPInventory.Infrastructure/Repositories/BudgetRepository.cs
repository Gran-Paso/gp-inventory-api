using GPInventory.Application.DTOs.Budgets;
using GPInventory.Application.Interfaces;
using GPInventory.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using MySqlConnector;
using System.Data;

namespace GPInventory.Infrastructure.Repositories;

public class BudgetRepository : IBudgetRepository
{
    private readonly ApplicationDbContext _context;

    public BudgetRepository(ApplicationDbContext context)
    {
        _context = context;
    }

    private async Task EnsureConnectionOpenAsync(System.Data.Common.DbConnection connection)
    {
        if (connection.State != System.Data.ConnectionState.Open)
        {
            await connection.OpenAsync();
        }
    }

    public async Task<List<BudgetDto>> GetBudgetsAsync(int? storeId, int? businessId, int? year, string? status)
    {
        var connection = _context.Database.GetDbConnection();
        await EnsureConnectionOpenAsync(connection);

        using var command = connection.CreateCommand();
        
        var query = @"
            SELECT 
                b.id,
                b.name,
                b.year,
                b.total_amount,
                b.business_id,
                b.store_id,
                b.status,
                b.created_at,
                b.updated_at,
                COALESCE(allocations.total_allocated, 0) as total_allocated,
                COALESCE(expenses.total_used, 0) as total_used,
                (b.total_amount - COALESCE(expenses.total_used, 0)) as remaining_amount,
                CASE 
                    WHEN b.total_amount > 0 
                    THEN (COALESCE(expenses.total_used, 0) / b.total_amount) * 100 
                    ELSE 0 
                END as usage_percentage
            FROM budgets b
            LEFT JOIN (
                SELECT 
                    ba.budget_id,
                    SUM(
                        CASE 
                            WHEN ba.fixed_amount IS NOT NULL THEN ba.fixed_amount
                            ELSE b2.total_amount * (ba.percentage / 100)
                        END
                    ) as total_allocated
                FROM budget_allocations ba
                JOIN budgets b2 ON ba.budget_id = b2.id
                GROUP BY ba.budget_id
            ) allocations ON b.id = allocations.budget_id
            LEFT JOIN (
                SELECT 
                    b3.id as budget_id,
                    SUM(e.amount) as total_used
                FROM budgets b3
                JOIN budget_allocations ba3 ON b3.id = ba3.budget_id
                JOIN expenses e ON e.expense_type_id = ba3.expense_type_id 
                    AND YEAR(e.date) = b3.year
                    AND (b3.business_id IS NULL OR e.business_id = b3.business_id)
                    AND (b3.store_id IS NULL OR e.store_id = b3.store_id)
                    AND e.amount IS NOT NULL 
                    AND e.date IS NOT NULL
                GROUP BY b3.id
            ) expenses ON b.id = expenses.budget_id
            WHERE 1=1";

        if (storeId.HasValue)
        {
            query += " AND b.store_id = @storeId";
        }

        if (businessId.HasValue)
        {
            query += " AND b.business_id = @businessId";
        }

        if (year.HasValue)
        {
            query += " AND b.year = @year";
        }

        if (!string.IsNullOrEmpty(status))
        {
            query += " AND b.status = @status";
        }

        query += " ORDER BY b.year DESC, b.created_at DESC";

        command.CommandText = query;

        if (storeId.HasValue)
            command.Parameters.Add(new MySqlParameter("@storeId", storeId.Value));
        if (businessId.HasValue)
            command.Parameters.Add(new MySqlParameter("@businessId", businessId.Value));
        if (year.HasValue)
            command.Parameters.Add(new MySqlParameter("@year", year.Value));
        if (!string.IsNullOrEmpty(status))
            command.Parameters.Add(new MySqlParameter("@status", status));

        var budgets = new List<BudgetDto>();

        using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            budgets.Add(new BudgetDto
            {
                Id = reader.GetInt32("id"),
                Name = reader.GetString("name"),
                Year = reader.GetInt32("year"),
                TotalAmount = reader.GetDecimal("total_amount"),
                BusinessId = reader.IsDBNull("business_id") ? null : reader.GetInt32("business_id"),
                StoreId = reader.IsDBNull("store_id") ? null : reader.GetInt32("store_id"),
                Status = reader.GetString("status"),
                CreatedAt = reader.GetDateTime("created_at"),
                UpdatedAt = reader.GetDateTime("updated_at"),
                TotalAllocated = reader.GetDecimal("total_allocated"),
                TotalUsed = reader.GetDecimal("total_used"),
                RemainingAmount = reader.GetDecimal("remaining_amount"),
                UsagePercentage = reader.GetDecimal("usage_percentage")
            });
        }

        return budgets;
    }

    public async Task<BudgetDto?> GetBudgetByIdAsync(int id)
    {
        var budgets = await GetBudgetsAsync(null, null, null, null);
        var budget = budgets.FirstOrDefault(b => b.Id == id);

        if (budget != null)
        {
            budget.Allocations = await GetBudgetAllocationsAsync(id);
            budget.MonthlyDistribution = await GetMonthlyDistributionAsync(id);
        }

        return budget;
    }

    public async Task<int> CreateBudgetAsync(CreateBudgetDto createDto)
    {
        var connection = _context.Database.GetDbConnection();
        if (connection.State != System.Data.ConnectionState.Open)
        {
            await EnsureConnectionOpenAsync(connection);
        }

        using var transaction = await connection.BeginTransactionAsync();

        try
        {
            using var command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText = @"
                INSERT INTO budgets (name, year, total_amount, business_id, store_id, description, status)
                VALUES (@name, @year, @totalAmount, @businessId, @storeId, @description, 'DRAFT');
                SELECT LAST_INSERT_ID();";

            command.Parameters.Add(new MySqlParameter("@name", createDto.Name));
            command.Parameters.Add(new MySqlParameter("@year", createDto.Year));
            command.Parameters.Add(new MySqlParameter("@totalAmount", createDto.TotalAmount));
            command.Parameters.Add(new MySqlParameter("@businessId", (object?)createDto.BusinessId ?? DBNull.Value));
            command.Parameters.Add(new MySqlParameter("@storeId", (object?)createDto.StoreId ?? DBNull.Value));
            command.Parameters.Add(new MySqlParameter("@description", (object?)createDto.Description ?? DBNull.Value));

            var budgetId = Convert.ToInt32(await command.ExecuteScalarAsync());

            // Create allocations
            foreach (var allocation in createDto.Allocations)
            {
                await CreateBudgetAllocationInternalAsync(connection, transaction, budgetId, allocation);
            }

            // Create monthly distributions
            foreach (var distribution in createDto.MonthlyDistributions)
            {
                await CreateMonthlyDistributionInternalAsync(connection, transaction, budgetId, distribution);
            }

            await transaction.CommitAsync();
            return budgetId;
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }
    }

    public async Task<bool> UpdateBudgetAsync(int id, UpdateBudgetDto updateDto)
    {
        var connection = _context.Database.GetDbConnection();
        await EnsureConnectionOpenAsync(connection);

        using var transaction = await connection.BeginTransactionAsync();

        try
        {
            var setParts = new List<string>();
            var parameters = new List<MySqlParameter>();

            if (!string.IsNullOrEmpty(updateDto.Name))
            {
                setParts.Add("name = @name");
                parameters.Add(new MySqlParameter("@name", updateDto.Name));
            }

            if (updateDto.Year.HasValue)
            {
                setParts.Add("year = @year");
                parameters.Add(new MySqlParameter("@year", updateDto.Year.Value));
            }

            if (updateDto.TotalAmount.HasValue)
            {
                setParts.Add("total_amount = @totalAmount");
                parameters.Add(new MySqlParameter("@totalAmount", updateDto.TotalAmount.Value));
            }

            if (updateDto.BusinessId.HasValue)
            {
                setParts.Add("business_id = @businessId");
                parameters.Add(new MySqlParameter("@businessId", updateDto.BusinessId.Value));
            }

            if (updateDto.StoreId.HasValue)
            {
                setParts.Add("store_id = @storeId");
                parameters.Add(new MySqlParameter("@storeId", updateDto.StoreId.Value));
            }

            if (!string.IsNullOrEmpty(updateDto.Status))
            {
                setParts.Add("status = @status");
                parameters.Add(new MySqlParameter("@status", updateDto.Status));
            }

            if (setParts.Count > 0)
            {
                using var command = connection.CreateCommand();
                command.Transaction = transaction;
                command.CommandText = $"UPDATE budgets SET {string.Join(", ", setParts)} WHERE id = @id";
                
                parameters.Add(new MySqlParameter("@id", id));
                command.Parameters.AddRange(parameters.ToArray());

                await command.ExecuteNonQueryAsync();
            }

            // Update allocations if provided
            if (updateDto.Allocations != null)
            {
                await DeleteBudgetAllocationsInternalAsync(connection, transaction, id);
                foreach (var allocation in updateDto.Allocations)
                {
                    await CreateBudgetAllocationInternalAsync(connection, transaction, id, allocation);
                }
            }

            // Update monthly distributions if provided
            if (updateDto.MonthlyDistributions != null)
            {
                await DeleteMonthlyDistributionsInternalAsync(connection, transaction, id);
                foreach (var distribution in updateDto.MonthlyDistributions)
                {
                    await CreateMonthlyDistributionInternalAsync(connection, transaction, id, distribution);
                }
            }

            await transaction.CommitAsync();
            return true;
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }
    }

    public async Task<bool> DeleteBudgetAsync(int id)
    {
        var connection = _context.Database.GetDbConnection();
        await EnsureConnectionOpenAsync(connection);

        using var command = connection.CreateCommand();
        command.CommandText = "DELETE FROM budgets WHERE id = @id";
        command.Parameters.Add(new MySqlParameter("@id", id));

        var result = await command.ExecuteNonQueryAsync();
        return result > 0;
    }

    public async Task<List<BudgetAllocationDto>> GetBudgetAllocationsAsync(int budgetId)
    {
        var connection = _context.Database.GetDbConnection();
        await EnsureConnectionOpenAsync(connection);

        using var command = connection.CreateCommand();
        command.CommandText = @"
            SELECT 
                ba.id,
                ba.budget_id,
                ba.expense_type_id,
                ba.percentage,
                ba.fixed_amount,
                et.name as expense_type_name,
                et.code as expense_type_code,
                CASE 
                    WHEN ba.fixed_amount IS NOT NULL THEN ba.fixed_amount
                    ELSE b.total_amount * (ba.percentage / 100)
                END as allocated_amount
            FROM budget_allocations ba
            JOIN expense_types et ON ba.expense_type_id = et.id
            JOIN budgets b ON ba.budget_id = b.id
            WHERE ba.budget_id = @budgetId
            ORDER BY et.name";

        command.Parameters.Add(new MySqlParameter("@budgetId", budgetId));

        var allocations = new List<BudgetAllocationDto>();

        using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            allocations.Add(new BudgetAllocationDto
            {
                Id = reader.GetInt32("id"),
                BudgetId = reader.GetInt32("budget_id"),
                ExpenseTypeId = reader.GetInt32("expense_type_id"),
                Percentage = reader.IsDBNull("percentage") ? null : reader.GetDecimal("percentage"),
                FixedAmount = reader.IsDBNull("fixed_amount") ? null : reader.GetDecimal("fixed_amount"),
                ExpenseTypeName = reader.GetString("expense_type_name"),
                ExpenseTypeCode = reader.GetString("expense_type_code"),
                AllocatedAmount = reader.GetDecimal("allocated_amount")
            });
        }

        return allocations;
    }

    public async Task<List<MonthlyDistributionDto>> GetMonthlyDistributionAsync(int budgetId)
    {
        var connection = _context.Database.GetDbConnection();
        await EnsureConnectionOpenAsync(connection);

        using var command = connection.CreateCommand();
        command.CommandText = @"
            SELECT 
                bmd.id,
                bmd.budget_id,
                bmd.month,
                bmd.percentage,
                bmd.fixed_amount,
                CASE 
                    WHEN bmd.fixed_amount IS NOT NULL THEN bmd.fixed_amount
                    ELSE b.total_amount * (bmd.percentage / 100)
                END as allocated_amount
            FROM budget_monthly_distribution bmd
            JOIN budgets b ON bmd.budget_id = b.id
            WHERE bmd.budget_id = @budgetId
            ORDER BY bmd.month";

        command.Parameters.Add(new MySqlParameter("@budgetId", budgetId));

        var distributions = new List<MonthlyDistributionDto>();

        using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            distributions.Add(new MonthlyDistributionDto
            {
                Id = reader.GetInt32("id"),
                BudgetId = reader.GetInt32("budget_id"),
                Month = reader.GetInt32("month"),
                Percentage = reader.IsDBNull("percentage") ? null : reader.GetDecimal("percentage"),
                FixedAmount = reader.IsDBNull("fixed_amount") ? null : reader.GetDecimal("fixed_amount"),
                AllocatedAmount = reader.GetDecimal("allocated_amount")
            });
        }

        return distributions;
    }

    public async Task<BudgetSummaryDto?> GetBudgetSummaryAsync(int id)
    {
        var budget = await GetBudgetByIdAsync(id);
        if (budget == null)
            return null;

        var summary = new BudgetSummaryDto
        {
            BudgetId = budget.Id,
            BudgetName = budget.Name,
            Year = budget.Year,
            TotalBudget = budget.TotalAmount,
            TotalUsed = budget.TotalUsed,
            RemainingAmount = budget.RemainingAmount,
            UsagePercentage = budget.UsagePercentage
        };

        // Get category summary
        var connection = _context.Database.GetDbConnection();
        await EnsureConnectionOpenAsync(connection);

        using var command = connection.CreateCommand();
        command.CommandText = @"
            SELECT 
                ba.expense_type_id,
                et.name as expense_type_name,
                et.code as expense_type_code,
                CASE 
                    WHEN ba.fixed_amount IS NOT NULL THEN ba.fixed_amount
                    ELSE b.total_amount * (ba.percentage / 100)
                END as allocated_amount,
                COALESCE(ba.percentage, (ba.fixed_amount / b.total_amount) * 100) as percentage,
                COALESCE(SUM(e.amount), 0) as used_amount
            FROM budget_allocations ba
            JOIN expense_types et ON ba.expense_type_id = et.id
            JOIN budgets b ON ba.budget_id = b.id
            LEFT JOIN expenses e ON e.expense_type_id = ba.expense_type_id 
                AND YEAR(e.date) = b.year
                AND (b.business_id IS NULL OR e.business_id = b.business_id)
                AND (b.store_id IS NULL OR e.store_id = b.store_id)
            WHERE ba.budget_id = @budgetId
            GROUP BY ba.expense_type_id, et.name, et.code, ba.percentage, ba.fixed_amount, b.total_amount";

        command.Parameters.Add(new MySqlParameter("@budgetId", id));

        using (var reader = await command.ExecuteReaderAsync())
        {
            while (await reader.ReadAsync())
            {
                var allocatedAmount = reader.GetDecimal("allocated_amount");
                var usedAmount = reader.GetDecimal("used_amount");

                summary.CategorySummary.Add(new CategorySummaryDto
                {
                    ExpenseTypeId = reader.GetInt32("expense_type_id"),
                    ExpenseTypeName = reader.GetString("expense_type_name"),
                    ExpenseTypeCode = reader.GetString("expense_type_code"),
                    AllocatedAmount = allocatedAmount,
                    UsedAmount = usedAmount,
                    RemainingAmount = allocatedAmount - usedAmount,
                    Percentage = reader.GetDecimal("percentage"),
                    UsagePercentage = allocatedAmount > 0 ? (usedAmount / allocatedAmount) * 100 : 0
                });
            }
        }

        // Get monthly summary (after closing the previous reader)
        summary.MonthlySummary = await GetMonthlySummaryAsync(id);

        return summary;
    }

    private async Task<List<MonthlySummaryDto>> GetMonthlySummaryAsync(int budgetId)
    {
        var connection = _context.Database.GetDbConnection();
        await EnsureConnectionOpenAsync(connection);

        using var command = connection.CreateCommand();
        command.CommandText = @"
            SELECT 
                months.month,
                COALESCE(bmd.allocated_amount, b.total_amount / 12) as allocated_amount,
                COALESCE(SUM(e.amount), 0) as used_amount
            FROM budgets b
            CROSS JOIN (
                SELECT 1 as month UNION SELECT 2 UNION SELECT 3 UNION SELECT 4 
                UNION SELECT 5 UNION SELECT 6 UNION SELECT 7 UNION SELECT 8 
                UNION SELECT 9 UNION SELECT 10 UNION SELECT 11 UNION SELECT 12
            ) months
            LEFT JOIN (
                SELECT 
                    month,
                    CASE 
                        WHEN fixed_amount IS NOT NULL THEN fixed_amount
                        ELSE (SELECT total_amount FROM budgets WHERE id = @budgetId) * (percentage / 100)
                    END as allocated_amount
                FROM budget_monthly_distribution
                WHERE budget_id = @budgetId
            ) bmd ON months.month = bmd.month
            LEFT JOIN budget_allocations ba ON b.id = ba.budget_id
            LEFT JOIN expenses e ON e.expense_type_id = ba.expense_type_id 
                AND YEAR(e.date) = b.year
                AND MONTH(e.date) = months.month
                AND (b.business_id IS NULL OR e.business_id = b.business_id)
                AND (b.store_id IS NULL OR e.store_id = b.store_id)
            WHERE b.id = @budgetId
            GROUP BY months.month, bmd.allocated_amount, b.total_amount
            ORDER BY months.month";

        command.Parameters.Add(new MySqlParameter("@budgetId", budgetId));

        var monthNames = new[] { "", "Enero", "Febrero", "Marzo", "Abril", "Mayo", "Junio", 
                                "Julio", "Agosto", "Septiembre", "Octubre", "Noviembre", "Diciembre" };
        var monthlySummary = new List<MonthlySummaryDto>();

        using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            var month = reader.GetInt32("month");
            var allocatedAmount = reader.GetDecimal("allocated_amount");
            var usedAmount = reader.GetDecimal("used_amount");

            monthlySummary.Add(new MonthlySummaryDto
            {
                Month = month,
                MonthName = monthNames[month],
                AllocatedAmount = allocatedAmount,
                UsedAmount = usedAmount,
                RemainingAmount = allocatedAmount - usedAmount,
                UsagePercentage = allocatedAmount > 0 ? (usedAmount / allocatedAmount) * 100 : 0
            });
        }

        return monthlySummary;
    }

    // Internal methods for transactions
    private async Task CreateBudgetAllocationInternalAsync(
        System.Data.Common.DbConnection connection, 
        System.Data.Common.DbTransaction? transaction, 
        int budgetId, 
        CreateBudgetAllocationDto allocationDto)
    {
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = @"
            INSERT INTO budget_allocations (budget_id, expense_type_id, percentage, fixed_amount)
            VALUES (@budgetId, @expenseTypeId, @percentage, @fixedAmount)";

        command.Parameters.Add(new MySqlParameter("@budgetId", budgetId));
        command.Parameters.Add(new MySqlParameter("@expenseTypeId", allocationDto.ExpenseTypeId));
        command.Parameters.Add(new MySqlParameter("@percentage", (object?)allocationDto.GetPercentage() ?? DBNull.Value));
        command.Parameters.Add(new MySqlParameter("@fixedAmount", (object?)allocationDto.FixedAmount ?? DBNull.Value));

        await command.ExecuteNonQueryAsync();
    }

    private async Task CreateMonthlyDistributionInternalAsync(
        System.Data.Common.DbConnection connection, 
        System.Data.Common.DbTransaction? transaction, 
        int budgetId, 
        CreateMonthlyDistributionDto distributionDto)
    {
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = @"
            INSERT INTO budget_monthly_distribution (budget_id, month, percentage, fixed_amount)
            VALUES (@budgetId, @month, @percentage, @fixedAmount)";

        command.Parameters.Add(new MySqlParameter("@budgetId", budgetId));
        command.Parameters.Add(new MySqlParameter("@month", distributionDto.Month));
        command.Parameters.Add(new MySqlParameter("@percentage", (object?)distributionDto.GetPercentage() ?? DBNull.Value));
        command.Parameters.Add(new MySqlParameter("@fixedAmount", (object?)distributionDto.FixedAmount ?? DBNull.Value));

        await command.ExecuteNonQueryAsync();
    }

    private async Task DeleteBudgetAllocationsInternalAsync(System.Data.Common.DbConnection connection, System.Data.Common.DbTransaction? transaction, int budgetId)
    {
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = "DELETE FROM budget_allocations WHERE budget_id = @budgetId";
        command.Parameters.Add(new MySqlParameter("@budgetId", budgetId));
        await command.ExecuteNonQueryAsync();
    }

    private async Task DeleteMonthlyDistributionsInternalAsync(System.Data.Common.DbConnection connection, System.Data.Common.DbTransaction? transaction, int budgetId)
    {
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = "DELETE FROM budget_monthly_distribution WHERE budget_id = @budgetId";
        command.Parameters.Add(new MySqlParameter("@budgetId", budgetId));
        await command.ExecuteNonQueryAsync();
    }

    public async Task<bool> CreateBudgetAllocationAsync(int budgetId, CreateBudgetAllocationDto allocationDto)
    {
        var connection = _context.Database.GetDbConnection();
        await EnsureConnectionOpenAsync(connection);
        await CreateBudgetAllocationInternalAsync(connection, null, budgetId, allocationDto);
        return true;
    }

    public async Task<bool> CreateMonthlyDistributionAsync(int budgetId, CreateMonthlyDistributionDto distributionDto)
    {
        var connection = _context.Database.GetDbConnection();
        await EnsureConnectionOpenAsync(connection);
        await CreateMonthlyDistributionInternalAsync(connection, null, budgetId, distributionDto);
        return true;
    }

    public async Task<bool> DeleteBudgetAllocationsAsync(int budgetId)
    {
        var connection = _context.Database.GetDbConnection();
        await EnsureConnectionOpenAsync(connection);
        await DeleteBudgetAllocationsInternalAsync(connection, null, budgetId);
        return true;
    }

    public async Task<bool> DeleteMonthlyDistributionsAsync(int budgetId)
    {
        var connection = _context.Database.GetDbConnection();
        await EnsureConnectionOpenAsync(connection);
        await DeleteMonthlyDistributionsInternalAsync(connection, null, budgetId);
        return true;
    }
}
