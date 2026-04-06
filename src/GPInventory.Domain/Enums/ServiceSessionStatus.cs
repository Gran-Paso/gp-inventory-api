namespace GPInventory.Domain.Enums;

/// <summary>
/// Estado de una sesión/clase planificada
/// </summary>
public enum ServiceSessionStatus
{
    /// <summary>
    /// Sesión planificada, aún no iniciada
    /// </summary>
    Scheduled = 0,

    /// <summary>
    /// Sesión en curso
    /// </summary>
    InProgress = 1,

    /// <summary>
    /// Sesión finalizada
    /// </summary>
    Completed = 2,

    /// <summary>
    /// Sesión cancelada
    /// </summary>
    Cancelled = 3
}
