using GPInventory.Application.Interfaces;
using GPInventory.Domain.Entities;
using GPInventory.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace GPInventory.Infrastructure.Repositories;

public class ProviderRepository : IProviderRepository
{
    private readonly ApplicationDbContext _context;

    public ProviderRepository(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Provider?> GetByIdAsync(int id)
    {
        Provider? provider = null;

        await _context.Database.OpenConnectionAsync();

        try
        {
            using var command = _context.Database.GetDbConnection().CreateCommand();
            command.CommandText = @"
                SELECT 
                    p.id,
                    p.name,
                    p.id_store,
                    p.id_business,
                    p.contact,
                    p.address,
                    p.mail,
                    p.prefix,
                    p.active,
                    p.is_self,
                    p.created_at,
                    p.updated_at
                FROM provider p
                WHERE p.id = @providerId";

            var providerIdParam = command.CreateParameter();
            providerIdParam.ParameterName = "@providerId";
            providerIdParam.Value = id;
            command.Parameters.Add(providerIdParam);

            using var reader = await command.ExecuteReaderAsync();
            if (await reader.ReadAsync())
            {
                provider = new Provider(
                    name: reader.GetString(1),
                    businessId: reader.GetInt32(3),
                    storeId: reader.IsDBNull(2) ? null : reader.GetInt32(2)
                );

                // Set Id via reflection
                typeof(Provider).GetProperty("Id")?.SetValue(provider, reader.GetInt32(0));

                // Set additional properties
                provider.Contact = reader.IsDBNull(4) ? null : reader.GetInt32(4);
                provider.Address = reader.IsDBNull(5) ? null : reader.GetString(5);
                provider.Mail = reader.IsDBNull(6) ? null : reader.GetString(6);
                provider.Prefix = reader.IsDBNull(7) ? null : reader.GetString(7);
                provider.Active = reader.GetBoolean(8);
                provider.IsSelf = reader.GetBoolean(9);
                provider.CreatedAt = reader.GetDateTime(10);
                provider.UpdatedAt = reader.GetDateTime(11);
            }
        }
        finally
        {
            await _context.Database.CloseConnectionAsync();
        }

        return provider;
    }

    public async Task<IEnumerable<Provider>> GetAllAsync()
    {
        var providers = new List<Provider>();

        await _context.Database.OpenConnectionAsync();

        try
        {
            using var command = _context.Database.GetDbConnection().CreateCommand();
            command.CommandText = @"
                SELECT 
                    p.id,
                    p.name,
                    p.id_store,
                    p.id_business,
                    p.contact,
                    p.address,
                    p.mail,
                    p.prefix,
                    p.active,
                    p.is_self,
                    p.created_at,
                    p.updated_at
                FROM provider p
                ORDER BY p.name";

            using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                var provider = new Provider(
                    name: reader.GetString(1),
                    businessId: reader.GetInt32(3),
                    storeId: reader.IsDBNull(2) ? null : reader.GetInt32(2)
                );

                typeof(Provider).GetProperty("Id")?.SetValue(provider, reader.GetInt32(0));
                provider.Contact = reader.IsDBNull(4) ? null : reader.GetInt32(4);
                provider.Address = reader.IsDBNull(5) ? null : reader.GetString(5);
                provider.Mail = reader.IsDBNull(6) ? null : reader.GetString(6);
                provider.Prefix = reader.IsDBNull(7) ? null : reader.GetString(7);
                provider.Active = reader.GetBoolean(8);
                provider.IsSelf = reader.GetBoolean(9);
                provider.CreatedAt = reader.GetDateTime(10);
                provider.UpdatedAt = reader.GetDateTime(11);

                providers.Add(provider);
            }
        }
        finally
        {
            await _context.Database.CloseConnectionAsync();
        }

        return providers;
    }

    public async Task<IEnumerable<Provider>> GetByBusinessIdAsync(int businessId)
    {
        var providers = new List<Provider>();

        await _context.Database.OpenConnectionAsync();

        try
        {
            using var command = _context.Database.GetDbConnection().CreateCommand();
            command.CommandText = @"
                SELECT 
                    p.id,
                    p.name,
                    p.id_store,
                    p.id_business,
                    p.contact,
                    p.address,
                    p.mail,
                    p.prefix,
                    p.active,
                    p.is_self,
                    p.created_at,
                    p.updated_at
                FROM provider p
                WHERE p.id_business = @businessId
                ORDER BY p.name";

            var businessIdParam = command.CreateParameter();
            businessIdParam.ParameterName = "@businessId";
            businessIdParam.Value = businessId;
            command.Parameters.Add(businessIdParam);

            using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                var provider = new Provider(
                    name: reader.GetString(1),
                    businessId: reader.GetInt32(3),
                    storeId: reader.IsDBNull(2) ? null : reader.GetInt32(2)
                );

                typeof(Provider).GetProperty("Id")?.SetValue(provider, reader.GetInt32(0));
                provider.Contact = reader.IsDBNull(4) ? null : reader.GetInt32(4);
                provider.Address = reader.IsDBNull(5) ? null : reader.GetString(5);
                provider.Mail = reader.IsDBNull(6) ? null : reader.GetString(6);
                provider.Prefix = reader.IsDBNull(7) ? null : reader.GetString(7);
                provider.Active = reader.GetBoolean(8);
                provider.IsSelf = reader.GetBoolean(9);
                provider.CreatedAt = reader.GetDateTime(10);
                provider.UpdatedAt = reader.GetDateTime(11);

                providers.Add(provider);
            }
        }
        finally
        {
            await _context.Database.CloseConnectionAsync();
        }

        return providers;
    }

    public async Task<IEnumerable<Provider>> GetByStoreIdAsync(int storeId)
    {
        var providers = new List<Provider>();

        await _context.Database.OpenConnectionAsync();

        try
        {
            using var command = _context.Database.GetDbConnection().CreateCommand();
            command.CommandText = @"
                SELECT 
                    p.id,
                    p.name,
                    p.id_store,
                    p.id_business,
                    p.contact,
                    p.address,
                    p.mail,
                    p.prefix,
                    p.active,
                    p.is_self,
                    p.created_at,
                    p.updated_at
                FROM provider p
                WHERE p.id_store = @storeId
                ORDER BY p.name";

            var storeIdParam = command.CreateParameter();
            storeIdParam.ParameterName = "@storeId";
            storeIdParam.Value = storeId;
            command.Parameters.Add(storeIdParam);

            using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                var provider = new Provider(
                    name: reader.GetString(1),
                    businessId: reader.GetInt32(3),
                    storeId: reader.IsDBNull(2) ? null : reader.GetInt32(2)
                );

                typeof(Provider).GetProperty("Id")?.SetValue(provider, reader.GetInt32(0));
                provider.Contact = reader.IsDBNull(4) ? null : reader.GetInt32(4);
                provider.Address = reader.IsDBNull(5) ? null : reader.GetString(5);
                provider.Mail = reader.IsDBNull(6) ? null : reader.GetString(6);
                provider.Prefix = reader.IsDBNull(7) ? null : reader.GetString(7);
                provider.Active = reader.GetBoolean(8);
                provider.IsSelf = reader.GetBoolean(9);
                provider.CreatedAt = reader.GetDateTime(10);
                provider.UpdatedAt = reader.GetDateTime(11);

                providers.Add(provider);
            }
        }
        finally
        {
            await _context.Database.CloseConnectionAsync();
        }

        return providers;
    }

    public async Task<Provider> AddAsync(Provider entity)
    {
        await _context.Database.OpenConnectionAsync();

        try
        {
            using var command = _context.Database.GetDbConnection().CreateCommand();
            command.CommandText = @"
                INSERT INTO provider (name, id_store, id_business, contact, address, mail, prefix, active, is_self, created_at, updated_at)
                VALUES (@name, @storeId, @businessId, @contact, @address, @mail, @prefix, @active, @isSelf, @createdAt, @updatedAt);
                SELECT LAST_INSERT_ID();";

            var nameParam = command.CreateParameter();
            nameParam.ParameterName = "@name";
            nameParam.Value = entity.Name;
            command.Parameters.Add(nameParam);

            var storeIdParam = command.CreateParameter();
            storeIdParam.ParameterName = "@storeId";
            storeIdParam.Value = (object?)entity.StoreId ?? DBNull.Value;
            command.Parameters.Add(storeIdParam);

            var businessIdParam = command.CreateParameter();
            businessIdParam.ParameterName = "@businessId";
            businessIdParam.Value = entity.BusinessId;
            command.Parameters.Add(businessIdParam);

            var contactParam = command.CreateParameter();
            contactParam.ParameterName = "@contact";
            contactParam.Value = (object?)entity.Contact ?? DBNull.Value;
            command.Parameters.Add(contactParam);

            var addressParam = command.CreateParameter();
            addressParam.ParameterName = "@address";
            addressParam.Value = (object?)entity.Address ?? DBNull.Value;
            command.Parameters.Add(addressParam);

            var mailParam = command.CreateParameter();
            mailParam.ParameterName = "@mail";
            mailParam.Value = (object?)entity.Mail ?? DBNull.Value;
            command.Parameters.Add(mailParam);

            var prefixParam = command.CreateParameter();
            prefixParam.ParameterName = "@prefix";
            prefixParam.Value = (object?)entity.Prefix ?? DBNull.Value;
            command.Parameters.Add(prefixParam);

            var activeParam = command.CreateParameter();
            activeParam.ParameterName = "@active";
            activeParam.Value = entity.Active;
            command.Parameters.Add(activeParam);

            var isSelfParam = command.CreateParameter();
            isSelfParam.ParameterName = "@isSelf";
            isSelfParam.Value = entity.IsSelf;
            command.Parameters.Add(isSelfParam);

            var createdAtParam = command.CreateParameter();
            createdAtParam.ParameterName = "@createdAt";
            createdAtParam.Value = entity.CreatedAt;
            command.Parameters.Add(createdAtParam);

            var updatedAtParam = command.CreateParameter();
            updatedAtParam.ParameterName = "@updatedAt";
            updatedAtParam.Value = entity.UpdatedAt;
            command.Parameters.Add(updatedAtParam);

            var newId = Convert.ToInt32(await command.ExecuteScalarAsync());
            typeof(Provider).GetProperty("Id")?.SetValue(entity, newId);
        }
        finally
        {
            await _context.Database.CloseConnectionAsync();
        }

        return entity;
    }

    public async Task UpdateAsync(Provider entity)
    {
        await _context.Database.OpenConnectionAsync();

        try
        {
            using var command = _context.Database.GetDbConnection().CreateCommand();
            command.CommandText = @"
                UPDATE provider 
                SET 
                    name = @name,
                    id_store = @storeId,
                    contact = @contact,
                    address = @address,
                    mail = @mail,
                    prefix = @prefix,
                    active = @active,
                    is_self = @isSelf,
                    updated_at = @updatedAt
                WHERE id = @id";

            var idParam = command.CreateParameter();
            idParam.ParameterName = "@id";
            idParam.Value = entity.Id;
            command.Parameters.Add(idParam);

            var nameParam = command.CreateParameter();
            nameParam.ParameterName = "@name";
            nameParam.Value = entity.Name;
            command.Parameters.Add(nameParam);

            var storeIdParam = command.CreateParameter();
            storeIdParam.ParameterName = "@storeId";
            storeIdParam.Value = (object?)entity.StoreId ?? DBNull.Value;
            command.Parameters.Add(storeIdParam);

            var contactParam = command.CreateParameter();
            contactParam.ParameterName = "@contact";
            contactParam.Value = (object?)entity.Contact ?? DBNull.Value;
            command.Parameters.Add(contactParam);

            var addressParam = command.CreateParameter();
            addressParam.ParameterName = "@address";
            addressParam.Value = (object?)entity.Address ?? DBNull.Value;
            command.Parameters.Add(addressParam);

            var mailParam = command.CreateParameter();
            mailParam.ParameterName = "@mail";
            mailParam.Value = (object?)entity.Mail ?? DBNull.Value;
            command.Parameters.Add(mailParam);

            var prefixParam = command.CreateParameter();
            prefixParam.ParameterName = "@prefix";
            prefixParam.Value = (object?)entity.Prefix ?? DBNull.Value;
            command.Parameters.Add(prefixParam);

            var activeParam = command.CreateParameter();
            activeParam.ParameterName = "@active";
            activeParam.Value = entity.Active;
            command.Parameters.Add(activeParam);

            var isSelfParam = command.CreateParameter();
            isSelfParam.ParameterName = "@isSelf";
            isSelfParam.Value = entity.IsSelf;
            command.Parameters.Add(isSelfParam);

            var updatedAtParam = command.CreateParameter();
            updatedAtParam.ParameterName = "@updatedAt";
            updatedAtParam.Value = DateTime.UtcNow;
            command.Parameters.Add(updatedAtParam);

            await command.ExecuteNonQueryAsync();
        }
        finally
        {
            await _context.Database.CloseConnectionAsync();
        }
    }

    public async Task DeleteAsync(int id)
    {
        await _context.Database.OpenConnectionAsync();

        try
        {
            using var command = _context.Database.GetDbConnection().CreateCommand();
            command.CommandText = @"DELETE FROM provider WHERE id = @id";

            var idParam = command.CreateParameter();
            idParam.ParameterName = "@id";
            idParam.Value = id;
            command.Parameters.Add(idParam);

            await command.ExecuteNonQueryAsync();
        }
        finally
        {
            await _context.Database.CloseConnectionAsync();
        }
    }

    public async Task<bool> ExistsAsync(int id)
    {
        bool exists = false;

        await _context.Database.OpenConnectionAsync();

        try
        {
            using var command = _context.Database.GetDbConnection().CreateCommand();
            command.CommandText = @"SELECT COUNT(1) FROM provider WHERE id = @id";

            var idParam = command.CreateParameter();
            idParam.ParameterName = "@id";
            idParam.Value = id;
            command.Parameters.Add(idParam);

            var count = Convert.ToInt32(await command.ExecuteScalarAsync());
            exists = count > 0;
        }
        finally
        {
            await _context.Database.CloseConnectionAsync();
        }

        return exists;
    }

    public async Task<Provider?> GetByNameAsync(string name, int businessId)
    {
        Provider? provider = null;

        await _context.Database.OpenConnectionAsync();

        try
        {
            using var command = _context.Database.GetDbConnection().CreateCommand();
            command.CommandText = @"
                SELECT 
                    p.id,
                    p.name,
                    p.id_store,
                    p.id_business,
                    p.contact,
                    p.address,
                    p.mail,
                    p.prefix,
                    p.active,
                    p.created_at,
                    p.updated_at
                FROM provider p
                WHERE p.name = @name AND p.id_business = @businessId";

            var nameParam = command.CreateParameter();
            nameParam.ParameterName = "@name";
            nameParam.Value = name;
            command.Parameters.Add(nameParam);

            var businessIdParam = command.CreateParameter();
            businessIdParam.ParameterName = "@businessId";
            businessIdParam.Value = businessId;
            command.Parameters.Add(businessIdParam);

            using var reader = await command.ExecuteReaderAsync();
            if (await reader.ReadAsync())
            {
                provider = new Provider(
                    name: reader.GetString(1),
                    businessId: reader.GetInt32(3),
                    storeId: reader.IsDBNull(2) ? null : reader.GetInt32(2)
                );

                typeof(Provider).GetProperty("Id")?.SetValue(provider, reader.GetInt32(0));
                provider.Contact = reader.IsDBNull(4) ? null : reader.GetInt32(4);
                provider.Address = reader.IsDBNull(5) ? null : reader.GetString(5);
                provider.Mail = reader.IsDBNull(6) ? null : reader.GetString(6);
                provider.Prefix = reader.IsDBNull(7) ? null : reader.GetString(7);
                provider.Active = reader.GetBoolean(8);
                provider.CreatedAt = reader.GetDateTime(9);
                provider.UpdatedAt = reader.GetDateTime(10);
            }
        }
        finally
        {
            await _context.Database.CloseConnectionAsync();
        }

        return provider;
    }
}
