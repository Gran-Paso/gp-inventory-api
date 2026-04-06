namespace GPInventory.Domain.Enums;

/// <summary>
/// Tipo de asistencia a un servicio/clase
/// </summary>
public enum AttendanceType
{
    /// <summary>
    /// Cliente usó un plan/membresía activo
    /// </summary>
    Plan = 0,
    
    /// <summary>
    /// Cliente pagó la clase directamente
    /// </summary>
    Paid = 1,
    
    /// <summary>
    /// Clase cortesía/gratuita
    /// </summary>
    Free = 2,
    
    /// <summary>
    /// Clase de prueba
    /// </summary>
    Trial = 3
}
