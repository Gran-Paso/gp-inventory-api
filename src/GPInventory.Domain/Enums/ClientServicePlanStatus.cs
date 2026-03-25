namespace GPInventory.Domain.Enums;

/// <summary>
/// Estado de un plan de servicio del cliente
/// </summary>
public enum ClientServicePlanStatus
{
    /// <summary>
    /// Plan activo y vigente
    /// </summary>
    Active = 0,
    
    /// <summary>
    /// Plan expirado por fecha
    /// </summary>
    Expired = 1,
    
    /// <summary>
    /// Plan cancelado por el cliente o negocio
    /// </summary>
    Cancelled = 2
}
