namespace GPInventory.Application.Common;

/// <summary>
/// Helper para manejar fechas en zona horaria de Chile
/// </summary>
public static class DateTimeHelper
{
    private static readonly TimeZoneInfo ChileTimeZone = TimeZoneInfo.FindSystemTimeZoneById("America/Santiago");

    /// <summary>
    /// Obtiene la fecha y hora actual en zona horaria de Chile
    /// </summary>
    public static DateTime GetChileNow()
    {
        return TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, ChileTimeZone);
    }

    /// <summary>
    /// Convierte una fecha UTC a hora de Chile
    /// </summary>
    public static DateTime ConvertToChileTime(DateTime utcDateTime)
    {
        if (utcDateTime.Kind != DateTimeKind.Utc)
        {
            utcDateTime = DateTime.SpecifyKind(utcDateTime, DateTimeKind.Utc);
        }
        return TimeZoneInfo.ConvertTimeFromUtc(utcDateTime, ChileTimeZone);
    }

    /// <summary>
    /// Convierte una fecha de Chile a UTC
    /// </summary>
    public static DateTime ConvertToUtc(DateTime chileDateTime)
    {
        return TimeZoneInfo.ConvertTimeToUtc(chileDateTime, ChileTimeZone);
    }
}
