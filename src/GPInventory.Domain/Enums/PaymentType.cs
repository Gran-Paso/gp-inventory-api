namespace GPInventory.Domain.Enums;

/// <summary>
/// Tipo de pago (modalidad)
/// </summary>
public enum PaymentType
{
    /// <summary>
    /// Pago al contado (pago único)
    /// </summary>
    Cash = 1,
    
    /// <summary>
    /// Pago en cuotas/parcializado
    /// </summary>
    Installments = 2
}
