namespace GPInventory.Application.Helpers;

public static class RecurrenceHelper
{
    public enum RecurrenceType
    {
        Monthly = 1,    // mensual - Cada mes
        Bimonthly = 2,  // bimestral - Cada 2 meses
        Quarterly = 3,  // trimestral - Cada 3 meses
        Semiannual = 4, // semestral - Cada 6 meses
        Annual = 5,     // anual - Cada 12 meses
        OneTime = 6     // único - Sólo una vez
    }

    /// <summary>
    /// Calcula la próxima fecha de vencimiento basada en la fecha de inicio y el tipo de recurrencia
    /// </summary>
    public static DateTime CalculateNextDueDate(DateTime startDate, int recurrenceTypeId, DateTime? lastPaymentDate = null)
    {
        var recurrenceType = (RecurrenceType)recurrenceTypeId;
        
        var enableApiDebug = Environment.GetEnvironmentVariable("ENABLE_API_DEBUG") == "true";
        
        if (enableApiDebug)
        {
            Console.WriteLine($"🔍 RecurrenceHelper.CalculateNextDueDate - Input:");
            Console.WriteLine($"   - startDate: {startDate}");
            Console.WriteLine($"   - recurrenceTypeId: {recurrenceTypeId} ({recurrenceType})");
            Console.WriteLine($"   - lastPaymentDate: {lastPaymentDate}");
        }
        
        // Para gastos únicos, la fecha de vencimiento es la fecha de inicio
        if (recurrenceType == RecurrenceType.OneTime)
        {
            if (enableApiDebug)
            {
                Console.WriteLine($"🔍 OneTime expense, returning startDate: {startDate}");
            }
            return startDate;
        }

        // Si no hay último pago (no hay gastos asociados), calcular el siguiente período desde startDate
        if (lastPaymentDate == null)
        {
            var nextDateFromStart = recurrenceType switch
            {
                RecurrenceType.Monthly => startDate.AddMonths(1),
                RecurrenceType.Bimonthly => startDate.AddMonths(2),
                RecurrenceType.Quarterly => startDate.AddMonths(3),
                RecurrenceType.Semiannual => startDate.AddMonths(6),
                RecurrenceType.Annual => startDate.AddYears(1),
                _ => startDate.AddMonths(1)
            };
            
            if (enableApiDebug)
            {
                Console.WriteLine($"🔍 No lastPaymentDate, calculated next period from startDate: {nextDateFromStart}");
            }
            return nextDateFromStart;
        }

        // Si hay último pago, calcular la siguiente fecha desde ese pago
        var nextDate = recurrenceType switch
        {
            RecurrenceType.Monthly => lastPaymentDate.Value.AddMonths(1),
            RecurrenceType.Bimonthly => lastPaymentDate.Value.AddMonths(2),
            RecurrenceType.Quarterly => lastPaymentDate.Value.AddMonths(3),
            RecurrenceType.Semiannual => lastPaymentDate.Value.AddMonths(6),
            RecurrenceType.Annual => lastPaymentDate.Value.AddYears(1),
            _ => lastPaymentDate.Value.AddMonths(1)
        };
        
        if (enableApiDebug)
        {
            Console.WriteLine($"🔍 Has lastPaymentDate, calculated nextDate: {nextDate}");
        }
        return nextDate;
    }

    /// <summary>
    /// Obtiene el rango de fechas del período actual para verificar si existe un pago
    /// </summary>
    public static (DateTime StartPeriod, DateTime EndPeriod) GetCurrentPeriodRange(DateTime startDate, int recurrenceTypeId, DateTime? lastPaymentDate = null)
    {
        var recurrenceType = (RecurrenceType)recurrenceTypeId;
        var currentDate = DateTime.Now.Date;
        
        // Para gastos únicos, el período es solo el día de inicio
        if (recurrenceType == RecurrenceType.OneTime)
        {
            return (startDate.Date, startDate.Date);
        }

        // Si no hay último pago, usar el período desde la fecha de inicio
        if (lastPaymentDate == null)
        {
            // El período va desde la fecha de inicio hasta la fecha de inicio (mismo día)
            // ya que la próxima fecha de vencimiento es el mismo startDate
            return (startDate.Date, startDate.Date);
        }

        // Si hay último pago, calcular el período actual
        var periodStart = lastPaymentDate.Value.Date;
        var periodEnd = CalculateNextDueDate(startDate, recurrenceTypeId, lastPaymentDate).AddDays(-1);
        
        return (periodStart, periodEnd);
    }

    /// <summary>
    /// Verifica si un gasto fijo está al día basándose en si existe un expense en el período actual
    /// </summary>
    public static bool IsUpToDate(DateTime startDate, int recurrenceTypeId, DateTime? lastPaymentDate, DateTime? lastExpenseDate)
    {
        var recurrenceType = (RecurrenceType)recurrenceTypeId;
        
        // Si no hay expenses asociados, este método no debería ser llamado
        // La lógica para manejar esto debe estar en el service
        if (!lastExpenseDate.HasValue)
        {
            return false;
        }
        
        // Para gastos únicos, está al día si ya se pagó una vez
        if (recurrenceType == RecurrenceType.OneTime)
        {
            return lastExpenseDate.Value.Date >= startDate.Date;
        }

        // Obtener el período actual
        var (periodStart, periodEnd) = GetCurrentPeriodRange(startDate, recurrenceTypeId, lastPaymentDate);
        
        // Verificar si hay un expense en el período actual
        return lastExpenseDate.Value.Date >= periodStart && 
               lastExpenseDate.Value.Date <= periodEnd;
    }
}
