namespace GPInventory.Domain.Enums;

public enum StockStatus
{
    OutOfStock = 0,    // 🔴 Sin stock (stock = 0)
    LowStock = 1,      // 🟡 Stock bajo (stock < umbral)
    InStock = 2        // 🟢 En stock (stock >= umbral)
}
