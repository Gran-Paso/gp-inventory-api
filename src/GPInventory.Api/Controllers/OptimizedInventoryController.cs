using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using GPInventory.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Cors;

namespace GPInventory.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[EnableCors("AllowFrontend")]
public class OptimizedInventoryController : ControllerBase
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<OptimizedInventoryController> _logger;

    public OptimizedInventoryController(ApplicationDbContext context, ILogger<OptimizedInventoryController> logger)
    {
        _context = context;
        _logger = logger;
    }

    /// <summary>
    /// Versión optimizada del inventario de negocio - Mucho más rápida
    /// </summary>
    /// <param name="businessId">ID del negocio</param>
    /// <returns>Inventario optimizado</returns>
    [HttpGet("inventory/{businessId}")]
    [Authorize]
    public async Task<ActionResult<object>> GetOptimizedBusinessInventory(int businessId)
    {
        try
        {
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();
            _logger.LogInformation("🔄 Obteniendo tiendas del negocio: {businessId}", businessId);

            // Verificar que el negocio existe
            var business = await _context.Businesses.FindAsync(businessId);
            if (business == null)
            {
                return NotFound(new { message = "Negocio no encontrado" });
            }

            // Obtener únicamente información básica de las tiendas (SIN cálculos complejos)
            var storesSummary = await _context.Stores
                .Where(s => s.BusinessId == businessId && s.Active)
                .Select(store => new
                {
                    storeId = store.Id,
                    storeName = store.Name,
                    location = store.Location
                })
                .ToListAsync();

            stopwatch.Stop();
            _logger.LogInformation("✅ Tiendas obtenidas en {elapsed}ms para negocio {businessId}", 
                stopwatch.ElapsedMilliseconds, businessId);

            var result = new
            {
                businessId = businessId,
                executionTimeMs = stopwatch.ElapsedMilliseconds,
                storesSummary = storesSummary,
            };

            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error en inventario optimizado del negocio: {businessId}", businessId);
            return StatusCode(500, new { message = "Error interno del servidor", error = ex.Message });
        }
    }

    /// <summary>
    /// Endpoint específico para inventario por tienda (usado por InventoryV2)
    /// </summary>
    /// <param name="businessId">ID del negocio</param>
    /// <param name="storeId">ID de la tienda</param>
    /// <returns>Productos con stock de la tienda específica</returns>
    [HttpGet("inventory/{businessId}/store/{storeId}")]
    [Authorize]
    public async Task<ActionResult<object>> GetInventoryByStore(int businessId, int storeId)
    {
        return await GetBusinessProductsWithStock(businessId, storeId);
    }

    /// <summary>
    /// Obtiene los productos de un business con su stock por tienda específica usando SQL puro
    /// </summary>
    /// <param name="businessId">ID del negocio</param>
    /// <param name="storeId">ID de la tienda (opcional, si no se especifica muestra resumen de todas)</param>
    /// <returns>Lista de productos con stock por tienda</returns>
    [HttpGet("business/{businessId}/products")]
    [Authorize]
    public async Task<ActionResult<object>> GetBusinessProductsWithStock(int businessId, [FromQuery] int? storeId = null)
    {
        try
        {
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();
            _logger.LogInformation("🔄 Obteniendo productos con stock para negocio: {businessId}, tienda: {storeId}", businessId, storeId);

            // Verificaciones básicas usando SQL directo
            var businessExists = await _context.Database.SqlQueryRaw<int>(
                "SELECT COUNT(*) as Value FROM business WHERE id = {0}", businessId)
                .FirstOrDefaultAsync() > 0;

            if (!businessExists)
            {
                return NotFound(new { message = "Negocio no encontrado" });
            }

            if (storeId.HasValue)
            {
                var storeExists = await _context.Database.SqlQueryRaw<int>(
                    "SELECT COUNT(*) as Value FROM store WHERE id = {0} AND id_business = {1} AND active = 1", 
                    storeId.Value, businessId).FirstOrDefaultAsync() > 0;

                if (!storeExists)
                {
                    return BadRequest(new { message = "Tienda no encontrada o no pertenece al negocio" });
                }
            }

            // Query principal SQL que obtiene todos los datos de una vez
            var sqlQuery = @"
                SELECT 
                    p.id,
                    p.name,
                    p.sku,
                    p.price,
                    COALESCE(fifo_cost_data.fifo_cost, p.cost, 0) as Cost,
                    p.image,
                    COALESCE(p.minimumStock, 0) as StockMin,
                    pt.id as ProductTypeId,
                    pt.name as ProductTypeName,
                    COALESCE(stock_data.current_stock, 0) as CurrentStock,
                    COALESCE(month_sales.total_month_sales, 0) as MonthSales,
                    COALESCE(today_sales.total_today_sales, 0) as TodaySales
                FROM product p
                LEFT JOIN product_type pt ON p.product_type = pt.id
                LEFT JOIN (
                    SELECT 
                        s.product as product_id,
                        SUM(
                            s.amount + COALESCE((
                                SELECT SUM(child.amount)
                                FROM stock child
                                WHERE child.stock_id = s.id
                                AND COALESCE(child.active, 0) = 1
                            ), 0)
                        ) as current_stock
                    FROM stock s
                    INNER JOIN store st ON s.id_store = st.id
                    WHERE st.id_business = {0}
                    AND COALESCE(s.active, 0) = 1
                    AND s.amount > 0
                    AND s.stock_id IS NULL
                    " + (storeId.HasValue ? "AND s.id_store = {1}" : "") + @"
                    GROUP BY s.product
                ) stock_data ON p.id = stock_data.product_id
                LEFT JOIN (
                    SELECT 
                        s.product as product_id,
                        CASE 
                            WHEN SUM(s.amount) > 0 THEN 
                                ROUND(SUM(s.amount * COALESCE(s.cost, 0)) / SUM(s.amount), 2)
                            ELSE COALESCE(p2.cost, 0)
                        END as fifo_cost
                    FROM stock s
                    LEFT JOIN stock sp ON s.stock_id = sp.id
                    LEFT JOIN product p2 ON s.product = p2.id
                    INNER JOIN store st ON s.id_store = st.id
                    WHERE st.id_business = {0}
                    AND s.amount > 0 
                    AND COALESCE(s.active, 0) = 1
                    " + (storeId.HasValue ? "AND s.id_store = {1}" : "") + @"
                    GROUP BY s.product
                ) fifo_cost_data ON p.id = fifo_cost_data.product_id
                LEFT JOIN (
                    SELECT 
                        sd.product as product_id,
                        SUM(CAST(sd.amount AS UNSIGNED)) as total_month_sales
                    FROM sales_detail sd
                    INNER JOIN sales s ON sd.sale = s.id
                    INNER JOIN store st ON s.id_store = st.id
                    WHERE st.id_business = {0}
                    " + (storeId.HasValue ? "AND s.id_store = {1}" : "") + @"
                    AND s.date >= DATE_FORMAT(NOW(), '%Y-%m-01')
                    GROUP BY sd.product
                ) month_sales ON p.id = month_sales.product_id
                LEFT JOIN (
                    SELECT 
                        sd.product as product_id,
                        SUM(CAST(sd.amount AS UNSIGNED)) as total_today_sales
                    FROM sales_detail sd
                    INNER JOIN sales s ON sd.sale = s.id
                    INNER JOIN store st ON s.id_store = st.id
                    WHERE st.id_business = {0}
                    " + (storeId.HasValue ? "AND s.id_store = {1}" : "") + @"
                    AND DATE(s.date) = CURDATE()
                    GROUP BY sd.product
                ) today_sales ON p.id = today_sales.product_id
                WHERE p.business = {0}
                ORDER BY p.name;
            ";

            var queryResults = storeId.HasValue
                ? await _context.Database.SqlQueryRaw<ProductStockResult>(sqlQuery, businessId, storeId.Value).ToListAsync()
                : await _context.Database.SqlQueryRaw<ProductStockResult>(sqlQuery, businessId).ToListAsync();

            // Procesar resultados y calcular estadísticas
            var productResults = queryResults.Select(r => new
            {
                    id = r.Id,
                    name = r.Name,
                    sku = r.Sku,
                    price = r.Price,
                    cost = r.CurrentStock > 0 ? r.Cost : null, // Solo mostrar costo si hay stock
                    image = r.Image,
                    stockMin = r.StockMin,
                    currentStock = r.CurrentStock,
                    stockStatus = GetStockStatus(r.CurrentStock, r.StockMin),
                    stockDifference = r.CurrentStock - r.StockMin,
                monthSales = r.MonthSales,
                todaySales = r.TodaySales,
                productType = r.ProductTypeId != null ? new
                {
                    id = r.ProductTypeId,
                    name = r.ProductTypeName
                } : null
            }).ToList();

            // Calcular estadísticas
            var totalProducts = productResults.Count;
            var outOfStockProducts = productResults.Count(p => p.stockStatus == "out");
            var lowStockProducts = productResults.Count(p => p.stockStatus == "low");

            stopwatch.Stop();

            var result = new
            {
                businessId = businessId,
                storeId = storeId,
                executionTimeMs = stopwatch.ElapsedMilliseconds,
                totalProducts = totalProducts,
                lowStockProducts = lowStockProducts,
                outOfStockProducts = outOfStockProducts,
                products = productResults
            };

            _logger.LogInformation("✅ Productos obtenidos en {elapsed}ms para negocio {businessId}, tienda: {storeId} - {count} productos", 
                stopwatch.ElapsedMilliseconds, businessId, storeId, totalProducts);

            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error obteniendo productos del negocio: {businessId}, tienda: {storeId}", businessId, storeId);
            return StatusCode(500, new { message = "Error interno del servidor", error = ex.Message });
        }
    }

    private static string GetStockStatus(int currentStock, int stockMin)
    {
        if (currentStock <= 0)
            return "out"; // Agotado
        
        if (currentStock <= stockMin)
            return "low"; // Stock bajo
        
        return "ok"; // Stock normal
    }

    /// <summary>
    /// Endpoint alternativo para obtener lotes activos (usado por frontend)
    /// </summary>
    /// <param name="productId">ID del producto</param>
    /// <param name="storeId">ID de la tienda (query parameter)</param>
    /// <returns>Lista de lotes con stock disponible</returns>
    [HttpGet("product/{productId}/lots")]
    [Authorize]
    public async Task<ActionResult<List<StockLotResult>>> GetProductActiveLotsAlt(int productId, [FromQuery] int storeId)
    {
        return await GetProductActiveLots(productId, storeId);
    }

    /// <summary>
    /// Obtener lotes activos con stock positivo de un producto específico
    /// </summary>
    /// <param name="productId">ID del producto</param>
    /// <param name="storeId">ID de la tienda</param>
    /// <returns>Lista de lotes con stock disponible</returns>
    [HttpGet("product/{productId}/store/{storeId}/lots")]
    [Authorize]
    public async Task<ActionResult<List<StockLotResult>>> GetProductActiveLots(int productId, int storeId)
    {
        try
        {
            _logger.LogInformation("🔄 Obteniendo lotes activos para producto {productId} en tienda {storeId}", productId, storeId);

            // Primero verificar si existen registros en stock para este producto
            var stockCountQuery = "SELECT COUNT(*) as Value FROM stock WHERE product = {0} AND id_store = {1}";
            var totalStockCount = await _context.Database.SqlQueryRaw<int>(stockCountQuery, productId, storeId).FirstOrDefaultAsync();
            _logger.LogInformation("📊 Total registros en stock para producto {productId}: {count}", productId, totalStockCount);

            var query = @"
                SELECT 
                    s.id as Id,
                    s.amount as Amount,
                    COALESCE(s.cost, 0) as UnitCost,
                    s.product as ProductId,
                    s.id_store as StoreId,
                    COALESCE(s.created_at, NOW()) as CreatedAt
                FROM stock s
                WHERE s.product = {0} 
                    AND s.id_store = {1}
                    AND s.amount > 0 
                    AND COALESCE(s.active, 0) = 1
                ORDER BY COALESCE(s.created_at, '1900-01-01') ASC"; // FIFO order

            var lots = await _context.Database
                .SqlQueryRaw<StockLotResult>(query, productId, storeId)
                .ToListAsync();

            _logger.LogInformation("✅ Encontrados {count} lotes activos para producto {productId}", lots.Count, productId);
            
            // Log detallado de cada lote
            foreach (var lot in lots)
            {
                _logger.LogInformation("📦 Lote ID: {lotId}, Cantidad: {amount}, Costo: {cost}, Fecha: {date}", 
                    lot.Id, lot.Amount, lot.UnitCost, lot.CreatedAt);
            }

            return Ok(lots);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Error obteniendo lotes del producto {productId}", productId);
            return StatusCode(500, new { message = "Error interno del servidor", error = ex.Message });
        }
    }

    /// <summary>
    /// Endpoint alternativo para remover stock de un lote específico (usado por frontend)
    /// </summary>
    /// <param name="lotId">ID del lote</param>
    /// <param name="request">Datos con la cantidad a remover</param>
    /// <returns>Resultado de la operación</returns>
    [HttpPost("lot/{lotId}/remove")]
    [Authorize]
    public async Task<ActionResult<object>> RemoveStockFromLotAlt(int lotId, [FromBody] RemoveStockQuantityRequest request)
    {
        var removeRequest = new RemoveStockRequest
        {
            LotId = lotId,
            Amount = request.Quantity
        };
        return await RemoveStockFromLot(removeRequest);
    }

    /// <summary>
    /// Remover stock de un lote específico (agregar registro negativo)
    /// </summary>
    /// <param name="request">Datos para remover stock</param>
    /// <returns>Resultado de la operación</returns>
    [HttpPost("stock/remove")]
    [Authorize]
    public async Task<ActionResult<object>> RemoveStockFromLot([FromBody] RemoveStockRequest request)
    {
        try
        {
            _logger.LogInformation("🔄 Removiendo {amount} unidades del lote {lotId}", request.Amount, request.LotId);

            // Verificar que el lote existe y tiene stock suficiente
            var lotQuery = @"
                SELECT s.id, s.amount, s.cost as UnitCost, s.product as ProductId, s.id_store as StoreId
                FROM stock s
                WHERE s.id = {0} AND s.amount > 0 AND COALESCE(s.active, 0) = 1";

            var lot = await _context.Database
                .SqlQueryRaw<StockLotInfo>(lotQuery, request.LotId)
                .FirstOrDefaultAsync();

            if (lot == null)
            {
                return BadRequest(new { message = "Lote no encontrado o sin stock disponible" });
            }

            if (lot.Amount < request.Amount)
            {
                return BadRequest(new { message = $"Stock insuficiente. Disponible: {lot.Amount}, Solicitado: {request.Amount}" });
            }

            // Crear registro negativo vinculado al lote original
            var removeStockQuery = @"
                INSERT INTO stock (amount, cost, product, id_store, stock_id, active, created_at)
                VALUES ({0}, {1}, {2}, {3}, {4}, 1, NOW())";

            await _context.Database.ExecuteSqlRawAsync(
                removeStockQuery,
                -request.Amount, // Cantidad negativa
                lot.UnitCost,    // Mismo costo del lote original
                lot.ProductId,
                lot.StoreId,
                request.LotId    // Referencia al lote padre
            );

            await _context.SaveChangesAsync();

            _logger.LogInformation("✅ Stock removido exitosamente");

            return Ok(new { 
                message = "Stock removido exitosamente",
                removedAmount = request.Amount,
                lotId = request.LotId
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Error removiendo stock del lote {lotId}", request.LotId);
            return StatusCode(500, new { message = "Error interno del servidor", error = ex.Message });
        }
    }

    /// <summary>
    /// Agregar stock a un producto (crear nuevo lote)
    /// </summary>
    /// <param name="request">Datos para agregar stock</param>
    /// <returns>Resultado de la operación</returns>
    [HttpPost("stock/add")]
    [Authorize]
    public async Task<ActionResult<object>> AddStockToProduct([FromBody] AddStockRequest request)
    {
        try
        {
            _logger.LogInformation("🔄 Agregando {amount} unidades al producto {productId} en tienda {storeId}", 
                request.Amount, request.ProductId, request.StoreId);

            // Verificar que el producto existe
            var productExists = await _context.Products.AnyAsync(p => p.Id == request.ProductId && p.IsActive);
            if (!productExists)
            {
                return BadRequest(new { message = "Producto no encontrado" });
            }

            // Verificar que la tienda existe
            var storeExists = await _context.Stores.AnyAsync(s => s.Id == request.StoreId && s.IsActive);
            if (!storeExists)
            {
                return BadRequest(new { message = "Tienda no encontrada" });
            }

            // Crear nuevo lote de stock
            var addStockQuery = @"
                INSERT INTO stock (amount, unit_cost, product_id, store_id, active, created_at)
                VALUES ({0}, {1}, {2}, {3}, 1, NOW())";

            await _context.Database.ExecuteSqlRawAsync(
                addStockQuery,
                request.Amount,
                request.UnitCost,
                request.ProductId,
                request.StoreId
            );

            await _context.SaveChangesAsync();

            _logger.LogInformation("✅ Stock agregado exitosamente");

            return Ok(new { 
                message = "Stock agregado exitosamente",
                addedAmount = request.Amount,
                productId = request.ProductId,
                storeId = request.StoreId
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Error agregando stock al producto {productId}", request.ProductId);
            return StatusCode(500, new { message = "Error interno del servidor", error = ex.Message });
        }
    }
}

/// <summary>
/// Clase para mapear resultados de la query SQL de productos con stock
/// </summary>
public class ProductStockResult
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Sku { get; set; }
    public int Price { get; set; }
    public decimal? Cost { get; set; }
    public string? Image { get; set; }
    public int StockMin { get; set; }
    public int? ProductTypeId { get; set; }
    public string? ProductTypeName { get; set; }
    public int CurrentStock { get; set; }
    public int MonthSales { get; set; }
    public int TodaySales { get; set; }

    private static decimal? CalculateChangePercent(decimal current, decimal previous)
    {
        if (previous <= 0) return null;
        return Math.Round(((current - previous) / previous) * 100, 2);
    }
}

/// <summary>
/// Clase para mapear resultados de lotes de stock
/// </summary>
public class StockLotResult
{
    public int Id { get; set; }
    public int Amount { get; set; }
    public decimal UnitCost { get; set; }
    public int ProductId { get; set; }
    public int StoreId { get; set; }
    public DateTime? CreatedAt { get; set; }
}

/// <summary>
/// Clase para información básica de un lote
/// </summary>
public class StockLotInfo
{
    public int Id { get; set; }
    public int Amount { get; set; }
    public decimal UnitCost { get; set; }
    public int ProductId { get; set; }
    public int StoreId { get; set; }
}

/// <summary>
/// Request para remover stock de un lote
/// </summary>
public class RemoveStockRequest
{
    public int LotId { get; set; }
    public int Amount { get; set; }
}

/// <summary>
/// Request para agregar stock a un producto
/// </summary>
public class AddStockRequest
{
    public int ProductId { get; set; }
    public int StoreId { get; set; }
    public int Amount { get; set; }
    public decimal UnitCost { get; set; }
}

/// <summary>
/// Request para remover stock de un lote específico (versión simplificada)
/// </summary>
public class RemoveStockQuantityRequest
{
    public int Quantity { get; set; }
}
