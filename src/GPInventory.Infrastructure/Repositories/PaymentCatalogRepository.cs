using GPInventory.Application.Interfaces;
using GPInventory.Domain.Entities;
using Microsoft.Extensions.Configuration;
using MySqlConnector;

namespace GPInventory.Infrastructure.Repositories;

public class PaymentCatalogRepository : IPaymentCatalogRepository
{
    private readonly string _connectionString;

    public PaymentCatalogRepository(IConfiguration configuration)
    {
        _connectionString = configuration.GetConnectionString("DefaultConnection") 
            ?? throw new ArgumentNullException(nameof(configuration));
    }

    public async Task<IEnumerable<ReceiptType>> GetReceiptTypesAsync()
    {
        var receiptTypes = new List<ReceiptType>();
        const string sql = @"
            SELECT id, name
            FROM receipt_types
            ORDER BY id";

        using var connection = new MySqlConnection(_connectionString);
        using var command = new MySqlCommand(sql, connection);

        await connection.OpenAsync();
        using var reader = await command.ExecuteReaderAsync();

        while (await reader.ReadAsync())
        {
            receiptTypes.Add(new ReceiptType
            {
                Id = reader.GetInt32(0),
                Name = reader.GetString(1)
            });
        }

        return receiptTypes;
    }

    public async Task<IEnumerable<PaymentType>> GetPaymentTypesAsync()
    {
        var paymentTypes = new List<PaymentType>();
        const string sql = @"
            SELECT id, name
            FROM payment_types
            ORDER BY id";

        using var connection = new MySqlConnection(_connectionString);
        using var command = new MySqlCommand(sql, connection);

        await connection.OpenAsync();
        using var reader = await command.ExecuteReaderAsync();

        while (await reader.ReadAsync())
        {
            paymentTypes.Add(new PaymentType
            {
                Id = reader.GetInt32(0),
                Name = reader.GetString(1)
            });
        }

        return paymentTypes;
    }

    public async Task<IEnumerable<PaymentMethod>> GetPaymentMethodsAsync()
    {
        var paymentMethods = new List<PaymentMethod>();
        const string sql = @"
            SELECT id, name
            FROM payment_methods
            ORDER BY id";

        using var connection = new MySqlConnection(_connectionString);
        using var command = new MySqlCommand(sql, connection);

        await connection.OpenAsync();
        using var reader = await command.ExecuteReaderAsync();

        while (await reader.ReadAsync())
        {
            paymentMethods.Add(new PaymentMethod
            {
                Id = reader.GetInt32(0),
                Name = reader.GetString(1)
            });
        }

        return paymentMethods;
    }

    public async Task<IEnumerable<BankEntity>> GetBankEntitiesAsync()
    {
        var bankEntities = new List<BankEntity>();
        const string sql = @"
            SELECT id, name
            FROM bank_entities
            ORDER BY name";

        using var connection = new MySqlConnection(_connectionString);
        using var command = new MySqlCommand(sql, connection);

        await connection.OpenAsync();
        using var reader = await command.ExecuteReaderAsync();

        while (await reader.ReadAsync())
        {
            bankEntities.Add(new BankEntity
            {
                Id = reader.GetInt32(0),
                Name = reader.GetString(1)
            });
        }

        return bankEntities;
    }

    public async Task<IEnumerable<ExpenseSubcategory>> GetExpenseSubcategoriesAsync()
    {
        var subcategories = new List<ExpenseSubcategory>();
        const string sql = @"
            SELECT id, name, expense_category_id
            FROM expense_subcategories
            ORDER BY name";

        using var connection = new MySqlConnection(_connectionString);
        using var command = new MySqlCommand(sql, connection);

        await connection.OpenAsync();
        using var reader = await command.ExecuteReaderAsync();

        while (await reader.ReadAsync())
        {
            subcategories.Add(new ExpenseSubcategory
            {
                Id = reader.GetInt32(0),
                Name = reader.GetString(1),
                ExpenseCategoryId = reader.GetInt32(2)
            });
        }

        return subcategories;
    }

    public async Task<BankEntity> CreateBankEntityAsync(BankEntity entity)
    {
        const string sql = @"
            INSERT INTO bank_entities (name)
            VALUES (@name)";

        using var connection = new MySqlConnection(_connectionString);
        using var command = new MySqlCommand(sql, connection);

        command.Parameters.AddWithValue("@name", entity.Name);

        await connection.OpenAsync();
        await command.ExecuteNonQueryAsync();
        entity.Id = (int)command.LastInsertedId;

        return entity;
    }
}
