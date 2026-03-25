using GPInventory.Domain.Enums;

namespace GPInventory.Application.Helpers;

/// <summary>
/// Clase helper para calcular montos de servicios con IVA
/// </summary>
public static class ServiceCalculator
{
    private const decimal IVA_RATE = 0.19m; // 19%

    /// <summary>
    /// Calcula los montos finales de una venta de servicio
    /// </summary>
    /// <param name="basePrice">Precio base del servicio</param>
    /// <param name="pricingType">Tipo de pricing (Fixed o PerEnrollment)</param>
    /// <param name="isTaxable">Si está afecto a IVA</param>
    /// <param name="enrollmentCount">Cantidad de inscripciones (requerido solo para PerEnrollment)</param>
    /// <returns>Tupla con (AmountNet, AmountIva, TotalAmount)</returns>
    public static (decimal AmountNet, decimal AmountIva, decimal TotalAmount) CalculateAmounts(
        decimal basePrice,
        PricingType pricingType,
        bool isTaxable,
        int? enrollmentCount = null)
    {
        // Validar enrollment_count para servicios per_enrollment
        if (pricingType == PricingType.PerEnrollment && (!enrollmentCount.HasValue || enrollmentCount.Value <= 0))
        {
            throw new ArgumentException("EnrollmentCount es requerido y debe ser mayor a 0 para servicios con pricing_type='per_enrollment'");
        }

        // Calcular monto neto
        decimal amountNet;
        if (pricingType == PricingType.Fixed)
        {
            amountNet = basePrice;
        }
        else // PerEnrollment
        {
            amountNet = basePrice * enrollmentCount!.Value;
        }

        // Calcular IVA y total
        decimal amountIva = 0m;
        decimal totalAmount;

        if (isTaxable)
        {
            amountIva = Math.Round(amountNet * IVA_RATE, 2);
            totalAmount = amountNet + amountIva;
        }
        else
        {
            amountIva = 0m;
            totalAmount = amountNet;
        }

        return (
            Math.Round(amountNet, 2),
            Math.Round(amountIva, 2),
            Math.Round(totalAmount, 2)
        );
    }

    /// <summary>
    /// Calcula el monto neto a partir del total con IVA
    /// </summary>
    public static decimal CalculateNetFromTotal(decimal totalAmount)
    {
        return Math.Round(totalAmount / (1 + IVA_RATE), 2);
    }

    /// <summary>
    /// Calcula el IVA a partir del total
    /// </summary>
    public static decimal CalculateIvaFromTotal(decimal totalAmount)
    {
        decimal net = CalculateNetFromTotal(totalAmount);
        return Math.Round(totalAmount - net, 2);
    }
}
