namespace GPInventory.Domain.Enums;

/// <summary>
/// Estado de una asistencia programada o registrada
/// </summary>
public enum AttendanceStatus
{
    /// <summary>
    /// Clase agendada/reservada
    /// </summary>
    Scheduled = 0,
    
    /// <summary>
    /// Asistencia confirmada
    /// </summary>
    Confirmed = 1,
    
    /// <summary>
    /// Cliente asistió a la clase
    /// </summary>
    Attended = 2,
    
    /// <summary>
    /// Cliente no asistió
    /// </summary>
    Absent = 3,
    
    /// <summary>
    /// Asistencia cancelada
    /// </summary>
    Cancelled = 4
}
