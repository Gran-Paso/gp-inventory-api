namespace GPInventory.Domain.Enums;

/// <summary>
/// Frecuencia con la que se factura al cliente su plan de servicio.
/// </summary>
public enum BillingFrequency
{
    /// <summary>Se genera un período de facturación por mes.</summary>
    Monthly = 0,

    /// <summary>Se genera un período por cada 3 meses.</summary>
    Quarterly = 1,

    /// <summary>Se genera un período por cada 6 meses.</summary>
    Semiannual = 2,

    /// <summary>Se genera un período por cada 12 meses.</summary>
    Annual = 3
}
