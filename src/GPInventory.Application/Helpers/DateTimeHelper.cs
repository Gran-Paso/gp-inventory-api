namespace GPInventory.Application.Helpers;

/// <summary>
/// Helper para obtener la hora actual en la zona horaria de Chile (America/Santiago).
/// Usar SantiagoNow en vez de DateTime.Now o DateTime.UtcNow para fechas de negocio.
/// </summary>
public static class DateTimeHelper
{
    private static readonly TimeZoneInfo SantiagoTz = TimeZoneInfo.FindSystemTimeZoneById(
        OperatingSystem.IsWindows() ? "Pacific SA Standard Time" : "America/Santiago");

    /// <summary>
    /// Fecha y hora actual en zona horaria America/Santiago.
    /// </summary>
    public static DateTime SantiagoNow =>
        TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, SantiagoTz);

    /// <summary>
    /// Fecha actual (sin hora) en zona horaria America/Santiago.
    /// </summary>
    public static DateTime SantiagoToday => SantiagoNow.Date;
}
