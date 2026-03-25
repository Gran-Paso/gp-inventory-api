namespace GPInventory.Domain.Enums;

/// <summary>
/// Tipo de documento fiscal
/// </summary>
public enum DocumentType
{
    /// <summary>
    /// Sin documento
    /// </summary>
    None = 0,
    
    /// <summary>
    /// Boleta (no desgrava IVA)
    /// </summary>
    Boleta = 1,
    
    /// <summary>
    /// Factura (desgrava IVA)
    /// </summary>
    Factura = 2
}
