namespace GPInventory.Domain.Enums;

/// <summary>
/// Define si el cliente paga el período de facturación antes de que empiece
/// (pre-pago) o al cierre del mismo (diferido).
/// </summary>
public enum PlanPaymentTiming
{
    /// <summary>El cliente paga antes de que inicie el período.</summary>
    PrePay = 0,

    /// <summary>El cliente paga al terminar el período (permitido hasta la fecha de vencimiento).</summary>
    Deferred = 1,

    /// <summary>El plan acepta cualquiera de los dos modos (se configura por cliente).</summary>
    Both = 2
}
