using GPInventory.Domain.Enums;

namespace GPInventory.Application.Helpers;

public static class StockHelper
{
    /// <summary>
    /// Calcula el estado del stock basándose en el stock actual y el umbral mínimo
    /// </summary>
    /// <param name="currentStock">Stock actual</param>
    /// <param name="minimumStock">Umbral de stock mínimo</param>
    /// <returns>Estado del stock</returns>
    public static StockStatus CalculateStockStatus(decimal currentStock, decimal minimumStock)
    {
        if (currentStock == 0)
        {
            return StockStatus.OutOfStock; // 🔴 Sin stock
        }
        
        if (currentStock < minimumStock)
        {
            return StockStatus.LowStock; // 🟡 Stock bajo
        }
        
        return StockStatus.InStock; // 🟢 En stock
    }
    
    /// <summary>
    /// Obtiene el emoji correspondiente al estado del stock
    /// </summary>
    public static string GetStockStatusEmoji(StockStatus status)
    {
        return status switch
        {
            StockStatus.OutOfStock => "🔴",
            StockStatus.LowStock => "🟡",
            StockStatus.InStock => "🟢",
            _ => "⚪"
        };
    }
    
    /// <summary>
    /// Obtiene el texto correspondiente al estado del stock
    /// </summary>
    public static string GetStockStatusText(StockStatus status)
    {
        return status switch
        {
            StockStatus.OutOfStock => "Sin stock",
            StockStatus.LowStock => "Stock bajo",
            StockStatus.InStock => "En stock",
            _ => "Desconocido"
        };
    }
}
