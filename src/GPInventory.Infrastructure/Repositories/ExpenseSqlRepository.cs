using System.Data;
using GPInventory.Application.DTOs.Expenses;
using GPInventory.Application.DTOs.Payments;
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
        
        // Primero leer todos los expenses y cerrar el reader
        using (var reader = await command.ExecuteReaderAsync())
        {
            while (await reader.ReadAsync())
            {
                var expenseId = reader.GetInt32("id");
                
                var expense = new ExpenseWithDetailsDto
                {
                    Id = expenseId,
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
        } // Reader se cierra aquí automáticamente

        // NUEVO: Cargar payment_plan e installments para cada expense
        // Ahora la conexión está libre para nuevas queries
        foreach (var expense in expenses)
        {
            await LoadPaymentPlanForExpenseAsync(connection, expense);
        }

        return expenses;
    }

    // NUEVO: Método helper para cargar payment_plan con installments usando SQL raw
    private async Task LoadPaymentPlanForExpenseAsync(MySqlConnection connection, ExpenseWithDetailsDto expenseDto)
    {
        try
        {
            Console.WriteLine($"[SQL LoadPaymentPlan] Buscando payment_plan para expense ID: {expenseDto.Id}");
            
            // Query para obtener payment_plan
            var planSql = @"
                SELECT 
                    id,
                    expense_id,
                    fixed_expense_id,
                    type,
                    expressed_in_uf,
                    bank_entity_id,
                    installments_count,
                    start_date,
                    created_at
                FROM payment_plan
                WHERE expense_id = @ExpenseId
                LIMIT 1";
            
            using var planCommand = new MySqlCommand(planSql, connection);
            planCommand.Parameters.AddWithValue("@ExpenseId", expenseDto.Id);
            
            using var planReader = await planCommand.ExecuteReaderAsync();
            
            if (await planReader.ReadAsync())
            {
                var paymentPlanId = planReader.GetInt32("id");
                
                Console.WriteLine($"[SQL LoadPaymentPlan] Payment plan encontrado ID: {paymentPlanId}");
                
                var paymentPlanDto = new PaymentPlanWithInstallmentsDto
                {
                    Id = paymentPlanId,
                    ExpenseId = planReader.IsDBNull(1) ? null : planReader.GetInt32(1),
                    FixedExpenseId = planReader.IsDBNull(2) ? null : planReader.GetInt32(2),
                    PaymentTypeId = planReader.IsDBNull(3) ? 0 : planReader.GetInt32(3),
                    ExpressedInUf = planReader.IsDBNull(4) ? false : planReader.GetBoolean(4),
                    BankEntityId = planReader.IsDBNull(5) ? null : planReader.GetInt32(5),
                    InstallmentsCount = planReader.IsDBNull(6) ? 0 : planReader.GetInt32(6),
                    StartDate = planReader.IsDBNull(7) ? DateTime.UtcNow : planReader.GetDateTime(7),
                    CreatedAt = DateTime.TryParse(planReader.GetString(8), out var createdAt) ? createdAt : DateTime.UtcNow
                };
                
                await planReader.CloseAsync();
                
                // Query para obtener installments
                var installmentsSql = @"
                    SELECT 
                        id,
                        payment_plan_id,
                        installment_number,
                        due_date,
                        amount_clp,
                        amount_uf,
                        status,
                        paid_date,
                        payment_method_id,
                        expense_id,
                        created_at
                    FROM payment_installment
                    WHERE payment_plan_id = @PaymentPlanId
                    ORDER BY installment_number";
                
                using var installmentsCommand = new MySqlCommand(installmentsSql, connection);
                installmentsCommand.Parameters.AddWithValue("@PaymentPlanId", paymentPlanId);
                
                using var installmentsReader = await installmentsCommand.ExecuteReaderAsync();
                
                var installments = new List<PaymentInstallmentDto>();
                
                while (await installmentsReader.ReadAsync())
                {
                    var installment = new PaymentInstallmentDto
                    {
                        Id = installmentsReader.GetInt32("id"),
                        PaymentPlanId = installmentsReader.GetInt32("payment_plan_id"),
                        InstallmentNumber = installmentsReader.GetInt32("installment_number"),
                        DueDate = installmentsReader.GetDateTime("due_date"),
                        AmountClp = installmentsReader.IsDBNull("amount_clp") ? 0 : installmentsReader.GetInt32("amount_clp"),
                        AmountUf = installmentsReader.IsDBNull("amount_uf") ? (decimal?)null : (decimal)installmentsReader.GetFloat("amount_uf"),
                        Status = installmentsReader.GetString("status"), // VARCHAR - será normalizado por AutoMapper
                        PaidDate = installmentsReader.IsDBNull("paid_date") ? null : installmentsReader.GetDateTime("paid_date"),
                        PaymentMethodId = installmentsReader.IsDBNull("payment_method_id") ? null : installmentsReader.GetInt32("payment_method_id"),
                        ExpenseId = installmentsReader.IsDBNull("expense_id") ? null : installmentsReader.GetInt32("expense_id"),
                        CreatedAt = installmentsReader.GetDateTime("created_at")
                    };
                    
                    installments.Add(installment);
                }
                
                Console.WriteLine($"[SQL LoadPaymentPlan] Installments encontradas: {installments.Count}");
                
                paymentPlanDto.Installments = installments;
                expenseDto.PaymentPlan = paymentPlanDto;
                
                Console.WriteLine($"[SQL LoadPaymentPlan] PaymentPlan asignado al expense");
            }
            else
            {
                Console.WriteLine($"[SQL LoadPaymentPlan] No se encontró payment_plan para expense {expenseDto.Id}");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[SQL LoadPaymentPlan] ERROR para expense {expenseDto.Id}: {ex.Message}");
            Console.WriteLine($"[SQL LoadPaymentPlan] Stack trace: {ex.StackTrace}");
            // No lanzar excepción, solo log - el expense se devuelve sin payment_plan
        }
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