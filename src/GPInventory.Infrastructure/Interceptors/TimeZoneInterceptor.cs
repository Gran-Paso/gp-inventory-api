using Microsoft.EntityFrameworkCore.Diagnostics;
using System.Data.Common;

namespace GPInventory.Infrastructure.Interceptors;

/// <summary>
/// Interceptor que ejecuta SET time_zone = 'America/Santiago' en cada conexión MySQL,
/// para que CURRENT_TIMESTAMP y NOW() usen la hora local de Chile en vez de UTC.
/// </summary>
public class TimeZoneInterceptor : DbConnectionInterceptor
{
    private const string SetTimeZone = "SET time_zone = 'America/Santiago';";

    public override void ConnectionOpened(DbConnection connection, ConnectionEndEventData eventData)
    {
        using var command = connection.CreateCommand();
        command.CommandText = SetTimeZone;
        command.ExecuteNonQuery();
    }

    public override async Task ConnectionOpenedAsync(DbConnection connection, ConnectionEndEventData eventData, CancellationToken cancellationToken = default)
    {
        using var command = connection.CreateCommand();
        command.CommandText = SetTimeZone;
        await command.ExecuteNonQueryAsync(cancellationToken);
    }
}
