namespace GPInventory.Domain.Enums;

/// <summary>
/// Estado de una venta de servicio
/// </summary>
public enum ServiceSaleStatus
{
    /// <summary>
    /// Venta registrada pero no iniciada
    /// </summary>
    Pending = 0,
    
    /// <summary>
    /// Servicio en ejecución
    /// </summary>
    InProgress = 1,
    
    /// <summary>
    /// Servicio completado exitosamente
    /// </summary>
    Completed = 2,
    
    /// <summary>
    /// Venta cancelada
    /// </summary>
    Cancelled = 3
}
