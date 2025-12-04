using GPInventory.Application.Interfaces;
using GPInventory.Domain.Entities;
using Microsoft.Extensions.Configuration;
using MySqlConnector;

namespace GPInventory.Infrastructure.Repositories;

public class PaymentPlanRepository : IPaymentPlanRepository
{
    private readonly string _connectionString;

    public PaymentPlanRepository(IConfiguration configuration)
    {
        _connectionString = configuration.GetConnectionString("DefaultConnection") 
            ?? throw new ArgumentNullException(nameof(configuration));
    }

    public async Task<PaymentPlan?> GetByIdAsync(int id)
    {
        const string sql = @"
            SELECT id, expense_id, fixed_expense_id, type, 
                   expressed_in_uf, bank_entity_id, installments_count, 
                   start_date, created_at
            FROM payment_plan
            WHERE id = @id";

        using var connection = new MySqlConnection(_connectionString);
        using var command = new MySqlCommand(sql, connection);
        command.Parameters.AddWithValue("@id", id);

        await connection.OpenAsync();
        using var reader = await command.ExecuteReaderAsync();

        if (await reader.ReadAsync())
        {
            return new PaymentPlan
            {
                Id = reader.GetInt32(0),
                ExpenseId = reader.IsDBNull(1) ? null : reader.GetInt32(1),
                FixedExpenseId = reader.IsDBNull(2) ? null : reader.GetInt32(2),
                PaymentTypeId = reader.IsDBNull(3) ? 0 : reader.GetInt32(3),
                ExpressedInUf = reader.GetBoolean(4),
                BankEntityId = reader.IsDBNull(5) ? null : reader.GetInt32(5),
                InstallmentsCount = reader.GetInt32(6),
                StartDate = reader.GetDateTime(7),
                CreatedAt = DateTime.TryParse(reader.GetString(8), out var created) ? created : DateTime.UtcNow
            };
        }

        return null;
    }

    public async Task<IEnumerable<PaymentPlan>> GetByExpenseIdAsync(int expenseId)
    {
        var plans = new List<PaymentPlan>();
        const string sql = @"
            SELECT id, expense_id, fixed_expense_id, type, 
                   expressed_in_uf, bank_entity_id, installments_count, 
                   start_date, created_at
            FROM payment_plan
            WHERE expense_id = @expenseId
            ORDER BY start_date DESC";

        using var connection = new MySqlConnection(_connectionString);
        using var command = new MySqlCommand(sql, connection);
        command.Parameters.AddWithValue("@expenseId", expenseId);

        await connection.OpenAsync();
        using var reader = await command.ExecuteReaderAsync();

        while (await reader.ReadAsync())
        {
            plans.Add(new PaymentPlan
            {
                Id = reader.GetInt32(0),
                ExpenseId = reader.IsDBNull(1) ? null : reader.GetInt32(1),
                FixedExpenseId = reader.IsDBNull(2) ? null : reader.GetInt32(2),
                PaymentTypeId = reader.IsDBNull(3) ? 0 : reader.GetInt32(3),
                ExpressedInUf = reader.GetBoolean(4),
                BankEntityId = reader.IsDBNull(5) ? null : reader.GetInt32(5),
                InstallmentsCount = reader.GetInt32(6),
                StartDate = reader.GetDateTime(7),
                CreatedAt = DateTime.TryParse(reader.GetString(8), out var created) ? created : DateTime.UtcNow
            });
        }

        return plans;
    }

    public async Task<IEnumerable<PaymentPlan>> GetByFixedExpenseIdAsync(int fixedExpenseId)
    {
        var plans = new List<PaymentPlan>();
        const string sql = @"
            SELECT id, expense_id, fixed_expense_id, type, 
                   expressed_in_uf, bank_entity_id, installments_count, 
                   start_date, created_at
            FROM payment_plan
            WHERE fixed_expense_id = @fixedExpenseId
            ORDER BY start_date DESC";

        using var connection = new MySqlConnection(_connectionString);
        using var command = new MySqlCommand(sql, connection);
        command.Parameters.AddWithValue("@fixedExpenseId", fixedExpenseId);

        await connection.OpenAsync();
        using var reader = await command.ExecuteReaderAsync();

        while (await reader.ReadAsync())
        {
            plans.Add(new PaymentPlan
            {
                Id = reader.GetInt32(0),
                ExpenseId = reader.IsDBNull(1) ? null : reader.GetInt32(1),
                FixedExpenseId = reader.IsDBNull(2) ? null : reader.GetInt32(2),
                PaymentTypeId = reader.IsDBNull(3) ? 0 : reader.GetInt32(3),
                ExpressedInUf = reader.GetBoolean(4),
                BankEntityId = reader.IsDBNull(5) ? null : reader.GetInt32(5),
                InstallmentsCount = reader.GetInt32(6),
                StartDate = reader.GetDateTime(7),
                CreatedAt = DateTime.TryParse(reader.GetString(8), out var created) ? created : DateTime.UtcNow
            });
        }

        return plans;
    }

    public async Task<PaymentPlan> CreateAsync(PaymentPlan entity)
    {
        const string sql = @"
            INSERT INTO payment_plan 
            (expense_id, fixed_expense_id, type, expressed_in_uf, 
             bank_entity_id, installments_count, start_date, created_at)
            VALUES 
            (@expenseId, @fixedExpenseId, @type, @expressedInUf, 
             @bankEntityId, @installmentsCount, @startDate, @createdAt)";

        using var connection = new MySqlConnection(_connectionString);
        using var command = new MySqlCommand(sql, connection);

        var createdAt = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss");

        command.Parameters.AddWithValue("@expenseId", (object?)entity.ExpenseId ?? DBNull.Value);
        command.Parameters.AddWithValue("@fixedExpenseId", (object?)entity.FixedExpenseId ?? DBNull.Value);
        command.Parameters.AddWithValue("@type", entity.PaymentTypeId);
        command.Parameters.AddWithValue("@expressedInUf", entity.ExpressedInUf);
        command.Parameters.AddWithValue("@bankEntityId", (object?)entity.BankEntityId ?? DBNull.Value);
        command.Parameters.AddWithValue("@installmentsCount", entity.InstallmentsCount);
        command.Parameters.AddWithValue("@startDate", entity.StartDate);
        command.Parameters.AddWithValue("@createdAt", createdAt);

        await connection.OpenAsync();
        await command.ExecuteNonQueryAsync();
        entity.Id = (int)command.LastInsertedId;

        return entity;
    }

    public async Task DeleteAsync(int id)
    {
        const string sql = @"
            DELETE FROM payment_plan 
            WHERE id = @id";

        using var connection = new MySqlConnection(_connectionString);
        using var command = new MySqlCommand(sql, connection);

        command.Parameters.AddWithValue("@id", id);

        await connection.OpenAsync();
        await command.ExecuteNonQueryAsync();
    }
}
