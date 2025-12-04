using GPInventory.Application.Interfaces;
using GPInventory.Domain.Entities;
using Microsoft.Extensions.Configuration;
using MySqlConnector;

namespace GPInventory.Infrastructure.Repositories;

public class PaymentInstallmentRepository : IPaymentInstallmentRepository
{
    private readonly string _connectionString;

    public PaymentInstallmentRepository(IConfiguration configuration)
    {
        _connectionString = configuration.GetConnectionString("DefaultConnection") 
            ?? throw new ArgumentNullException(nameof(configuration));
    }

    public async Task<PaymentInstallment?> GetByIdAsync(int id)
    {
        const string sql = @"
            SELECT id, payment_plan_id, installment_number, due_date, 
                   amount_clp, amount_uf, status, paid_date, payment_method_id, 
                   expense_id, created_at
            FROM payment_installment
            WHERE id = @id";

        using var connection = new MySqlConnection(_connectionString);
        using var command = new MySqlCommand(sql, connection);
        command.Parameters.AddWithValue("@id", id);

        await connection.OpenAsync();
        using var reader = await command.ExecuteReaderAsync();

        if (await reader.ReadAsync())
        {
            return MapInstallment(reader);
        }

        return null;
    }

    public async Task<IEnumerable<PaymentInstallment>> GetByPaymentPlanIdAsync(int paymentPlanId)
    {
        var installments = new List<PaymentInstallment>();
        const string sql = @"
            SELECT id, payment_plan_id, installment_number, due_date, 
                   amount_clp, amount_uf, status, paid_date, payment_method_id, 
                   expense_id, created_at
            FROM payment_installment
            WHERE payment_plan_id = @paymentPlanId
            ORDER BY installment_number";

        using var connection = new MySqlConnection(_connectionString);
        using var command = new MySqlCommand(sql, connection);
        command.Parameters.AddWithValue("@paymentPlanId", paymentPlanId);

        await connection.OpenAsync();
        using var reader = await command.ExecuteReaderAsync();

        while (await reader.ReadAsync())
        {
            installments.Add(MapInstallment(reader));
        }

        return installments;
    }

    public async Task<PaymentInstallment> CreateAsync(PaymentInstallment entity)
    {
        const string sql = @"
            INSERT INTO payment_installment 
            (payment_plan_id, installment_number, due_date, amount_clp, amount_uf, 
             status, paid_date, payment_method_id, expense_id, created_at)
            VALUES 
            (@paymentPlanId, @installmentNumber, @dueDate, @amountClp, @amountUf, 
             @status, @paidDate, @paymentMethodId, @expenseId, @createdAt)";

        using var connection = new MySqlConnection(_connectionString);
        using var command = new MySqlCommand(sql, connection);

        AddInstallmentParameters(command, entity);

        await connection.OpenAsync();
        await command.ExecuteNonQueryAsync();
        entity.Id = (int)command.LastInsertedId;

        return entity;
    }

    public async Task<IEnumerable<PaymentInstallment>> CreateBulkAsync(IEnumerable<PaymentInstallment> entities)
    {
        var installmentsList = entities.ToList();
        
        // Validación: verificar que todas las cuotas tengan payment_plan_id
        if (installmentsList.Any(i => i.PaymentPlanId == 0))
        {
            var invalidCount = installmentsList.Count(i => i.PaymentPlanId == 0);
            throw new ArgumentException($"CreateBulkAsync: {invalidCount} installments have PaymentPlanId = 0. All installments must have a valid PaymentPlanId.");
        }
        
        using var connection = new MySqlConnection(_connectionString);
        await connection.OpenAsync();
        
        await using var transaction = await connection.BeginTransactionAsync();

        try
        {
            const string sql = @"
                INSERT INTO payment_installment 
                (payment_plan_id, installment_number, due_date, amount_clp, amount_uf, 
                 status, paid_date, payment_method_id, expense_id, created_at)
                VALUES 
                (@paymentPlanId, @installmentNumber, @dueDate, @amountClp, @amountUf, 
                 @status, @paidDate, @paymentMethodId, @expenseId, @createdAt)";

            foreach (var entity in installmentsList)
            {
                await using var command = new MySqlCommand(sql, connection, (MySqlConnector.MySqlTransaction)transaction);
                AddInstallmentParameters(command, entity);
                await command.ExecuteNonQueryAsync();
                entity.Id = (int)command.LastInsertedId;
            }

            await transaction.CommitAsync();
            return installmentsList;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Error in CreateBulkAsync: {ex.Message}");
            Console.Error.WriteLine($"Stack Trace: {ex.StackTrace}");
            await transaction.RollbackAsync();
            throw;
        }
    }

    public async Task UpdateAsync(PaymentInstallment entity)
    {
        const string sql = @"
            UPDATE payment_installment 
            SET status = @status, 
                paid_date = @paidDate, 
                payment_method_id = @paymentMethodId, 
                expense_id = @expenseId
            WHERE id = @id";

        using var connection = new MySqlConnection(_connectionString);
        using var command = new MySqlCommand(sql, connection);

        command.Parameters.AddWithValue("@id", entity.Id);
        command.Parameters.AddWithValue("@status", entity.Status);
        command.Parameters.AddWithValue("@paidDate", (object?)entity.PaidDate ?? DBNull.Value);
        command.Parameters.AddWithValue("@paymentMethodId", (object?)entity.PaymentMethodId ?? DBNull.Value);
        command.Parameters.AddWithValue("@expenseId", (object?)entity.ExpenseId ?? DBNull.Value);

        await connection.OpenAsync();
        await command.ExecuteNonQueryAsync();
    }

    public async Task DeleteAsync(int id)
    {
        const string sql = @"
            DELETE FROM payment_installment 
            WHERE id = @id";

        using var connection = new MySqlConnection(_connectionString);
        using var command = new MySqlCommand(sql, connection);

        command.Parameters.AddWithValue("@id", id);

        await connection.OpenAsync();
        await command.ExecuteNonQueryAsync();
    }

    private static PaymentInstallment MapInstallment(MySqlDataReader reader)
    {
        return new PaymentInstallment
        {
            Id = reader.GetInt32(0),
            PaymentPlanId = reader.GetInt32(1),
            InstallmentNumber = reader.GetInt32(2),
            DueDate = reader.GetDateTime(3),
            AmountClp = reader.GetInt32(4), // amount_clp es int
            AmountUf = reader.IsDBNull(5) ? null : (decimal)reader.GetFloat(5), // amount_uf es float
            Status = reader.GetString(6),
            PaidDate = reader.IsDBNull(7) ? null : reader.GetDateTime(7),
            PaymentMethodId = reader.IsDBNull(8) ? null : reader.GetInt32(8),
            ExpenseId = reader.IsDBNull(9) ? null : reader.GetInt32(9),
            CreatedAt = reader.GetDateTime(10)
        };
    }

    private static void AddInstallmentParameters(MySqlCommand command, PaymentInstallment entity)
    {
        var createdAt = entity.CreatedAt != default ? entity.CreatedAt : DateTime.UtcNow;

        command.Parameters.AddWithValue("@paymentPlanId", entity.PaymentPlanId);
        command.Parameters.AddWithValue("@installmentNumber", entity.InstallmentNumber);
        command.Parameters.AddWithValue("@dueDate", entity.DueDate);
        command.Parameters.AddWithValue("@amountClp", (int)entity.AmountClp); // convert decimal to int
        command.Parameters.AddWithValue("@amountUf", entity.AmountUf.HasValue ? (float)entity.AmountUf.Value : DBNull.Value);
        command.Parameters.AddWithValue("@status", entity.Status);
        command.Parameters.AddWithValue("@paidDate", (object?)entity.PaidDate ?? DBNull.Value);
        command.Parameters.AddWithValue("@paymentMethodId", (object?)entity.PaymentMethodId ?? DBNull.Value);
        command.Parameters.AddWithValue("@expenseId", (object?)entity.ExpenseId ?? DBNull.Value);
        command.Parameters.AddWithValue("@createdAt", createdAt);
    }
}
