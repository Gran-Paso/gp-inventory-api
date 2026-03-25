namespace GPInventory.Domain.Enums;

/// <summary>
/// Tipo de modelo de pricing para servicios
/// </summary>
public enum PricingType
{
    /// <summary>
    /// Precio fijo total predeterminado
    /// </summary>
    Fixed = 0,
    
    /// <summary>
    /// Precio por inscripción/persona, el total se calcula dinámicamente
    /// </summary>
    PerEnrollment = 1
}
