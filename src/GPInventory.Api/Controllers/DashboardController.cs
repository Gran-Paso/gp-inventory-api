using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using GPInventory.Infrastructure.Data;
using GPInventory.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Cors;

namespace GPInventory.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[EnableCors("AllowFrontend")]
[Authorize]
public class DashboardController : ControllerBase
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<DashboardController> _logger;

    public DashboardController(ApplicationDbContext context, ILogger<DashboardController> logger)
    {
        _context = context;
        _logger = logger;
    }

    /// <summary>
    /// Obtiene los KPIs del día actual comparados con el día anterior
    /// </summary>
    /// <param name="businessId">ID del negocio</param>
    /// <param name="storeId">ID de la tienda (opcional)</param>
    /// <returns>KPIs del dashboard con comparativas</returns>
    [HttpGet("kpis/daily")]
    public async Task<ActionResult<DailyKPIsResponse>> GetDailyKPIs(
        [FromQuery] int businessId, 
        [FromQuery] int? storeId = null)
    {
        try
        {
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();
            _logger.LogInformation("🔄 Obteniendo KPIs diarios para negocio: {businessId}, tienda: {storeId}", 
                businessId, storeId);

            // Verificar que el negocio existe
            var businessExists = await _context.Database.SqlQueryRaw<int>(
                "SELECT COUNT(*) as Value FROM business WHERE id = {0}", businessId)
                .FirstOrDefaultAsync() > 0;

            if (!businessExists)
            {
                return NotFound(new { message = "Negocio no encontrado" });
            }

            // Si se especifica tienda, verificar que existe
            if (storeId.HasValue)
            {
                var storeExists = await _context.Database.SqlQueryRaw<int>(
                    "SELECT COUNT(*) as Value FROM store WHERE id = {0} AND id_business = {1} AND active = 1",
                    storeId.Value, businessId)
                    .FirstOrDefaultAsync() > 0;

                if (!storeExists)
                {
                    return BadRequest(new { message = "Tienda no encontrada o no pertenece al negocio" });
                }
            }

            // Query para obtener todos los KPIs en una sola consulta
            // Usamos DATE_ADD para ajustar UTC-3 (zona horaria de Chile)
            var kpisQuery = @"
                SELECT 
                    -- Ingresos del día actual
                    COALESCE(SUM(CASE 
                        WHEN DATE(DATE_ADD(s.date, INTERVAL -3 HOUR)) = DATE(DATE_ADD(NOW(), INTERVAL -3 HOUR))
                        THEN s.total 
                        ELSE 0 
                    END), 0) as TodayRevenue,
                    
                    -- Ingresos del día anterior
                    COALESCE(SUM(CASE 
                        WHEN DATE(DATE_ADD(s.date, INTERVAL -3 HOUR)) = DATE(DATE_ADD(DATE_SUB(NOW(), INTERVAL 1 DAY), INTERVAL -3 HOUR))
                        THEN s.total 
                        ELSE 0 
                    END), 0) as YesterdayRevenue,
                    
                    -- Número de ventas del día actual
                    COUNT(DISTINCT CASE 
                        WHEN DATE(DATE_ADD(s.date, INTERVAL -3 HOUR)) = DATE(DATE_ADD(NOW(), INTERVAL -3 HOUR))
                        THEN s.id 
                    END) as TodaySalesCount,
                    
                    -- Número de ventas del día anterior
                    COUNT(DISTINCT CASE 
                        WHEN DATE(DATE_ADD(s.date, INTERVAL -3 HOUR)) = DATE(DATE_ADD(DATE_SUB(NOW(), INTERVAL 1 DAY), INTERVAL -3 HOUR))
                        THEN s.id 
                    END) as YesterdaySalesCount
                FROM sales s
                INNER JOIN store st ON s.id_store = st.id
                WHERE st.id_business = {0}
                    AND DATE(DATE_ADD(s.date, INTERVAL -3 HOUR)) >= DATE(DATE_ADD(DATE_SUB(NOW(), INTERVAL 1 DAY), INTERVAL -3 HOUR))
                    " + (storeId.HasValue ? "AND s.id_store = {1}" : "");

            var kpisResult = storeId.HasValue
                ? await _context.Database.SqlQueryRaw<KPIsRawData>(kpisQuery, businessId, storeId.Value)
                    .FirstOrDefaultAsync()
                : await _context.Database.SqlQueryRaw<KPIsRawData>(kpisQuery, businessId)
                    .FirstOrDefaultAsync();

            if (kpisResult == null)
            {
                kpisResult = new KPIsRawData();
            }

            // Log para debugging
            _logger.LogInformation("📊 KPIs Query Result - Today: {today}, Yesterday: {yesterday}, TodayCount: {todayCount}, YesterdayCount: {yesterdayCount}",
                kpisResult.TodayRevenue, kpisResult.YesterdayRevenue, kpisResult.TodaySalesCount, kpisResult.YesterdaySalesCount);

            // Query para calcular el capital en stock (al costo)
            // Solo considera stocks padre (stock_id IS NULL) y resta las ventas
            var stockCapitalQuery = @"
                SELECT 
                    COALESCE(SUM(
                        CASE 
                            WHEN s.amount > 0 AND s.active = 1 AND s.stock_id IS NULL THEN
                                GREATEST(
                                    s.amount - COALESCE((
                                        SELECT SUM(CAST(sd.amount AS SIGNED))
                                        FROM sales_detail sd
                                        WHERE sd.stock_id = s.id
                                    ), 0),
                                    0
                                ) * s.cost
                            ELSE 0
                        END
                    ), 0) as TodayStockCapital
                FROM stock s
                INNER JOIN product p ON s.product = p.id
                WHERE p.business = {0}
                    " + (storeId.HasValue ? "AND s.id_store = {1}" : "");

            var stockCapitalResult = storeId.HasValue
                ? await _context.Database.SqlQueryRaw<StockCapitalData>(stockCapitalQuery, businessId, storeId.Value)
                    .FirstOrDefaultAsync()
                : await _context.Database.SqlQueryRaw<StockCapitalData>(stockCapitalQuery, businessId)
                    .FirstOrDefaultAsync();

            decimal todayStockCapital = stockCapitalResult?.TodayStockCapital ?? 0;

            // Query para obtener capital en stock del día anterior (para comparación)
            // Nota: Esto es una aproximación, idealmente debería haber un snapshot histórico
            var yesterdayStockCapital = todayStockCapital; // Por ahora usamos el mismo valor

            // Query para obtener valor potencial de ventas (precio de venta * cantidad en stock)
            // Solo considera stocks padre (stock_id IS NULL) y resta las ventas
            var stockRevenueQuery = @"
                SELECT 
                    COALESCE(SUM(
                        CASE 
                            WHEN s.amount > 0 AND s.active = 1 AND s.stock_id IS NULL THEN
                                GREATEST(
                                    s.amount - COALESCE((
                                        SELECT SUM(CAST(sd.amount AS SIGNED))
                                        FROM sales_detail sd
                                        WHERE sd.stock_id = s.id
                                    ), 0),
                                    0
                                ) * p.price
                            ELSE 0
                        END
                    ), 0) as StockRevenuePotential
                FROM stock s
                INNER JOIN product p ON s.product = p.id
                WHERE p.business = {0}
                    " + (storeId.HasValue ? "AND s.id_store = {1}" : "");

            var stockRevenueResult = storeId.HasValue
                ? await _context.Database.SqlQueryRaw<StockRevenueData>(stockRevenueQuery, businessId, storeId.Value)
                    .FirstOrDefaultAsync()
                : await _context.Database.SqlQueryRaw<StockRevenueData>(stockRevenueQuery, businessId)
                    .FirstOrDefaultAsync();

            decimal todayStockRevenue = stockRevenueResult?.StockRevenuePotential ?? 0;
            var yesterdayStockRevenue = todayStockRevenue; // Por ahora usamos el mismo valor

            // Calcular tickets promedio
            decimal todayTicketAverage = kpisResult.TodaySalesCount > 0
                ? kpisResult.TodayRevenue / kpisResult.TodaySalesCount
                : 0;

            decimal yesterdayTicketAverage = kpisResult.YesterdaySalesCount > 0
                ? kpisResult.YesterdayRevenue / kpisResult.YesterdaySalesCount
                : 0;

            // Calcular porcentajes de cambio
            decimal? revenueChangePercent = CalculateChangePercent(
                kpisResult.TodayRevenue, kpisResult.YesterdayRevenue);

            decimal? ticketAverageChangePercent = CalculateChangePercent(
                todayTicketAverage, yesterdayTicketAverage);

            decimal? stockCapitalChangePercent = CalculateChangePercent(
                todayStockCapital, yesterdayStockCapital);

            decimal? stockRevenueChangePercent = CalculateChangePercent(
                todayStockRevenue, yesterdayStockRevenue);

            // Construir respuesta con los 4 KPIs
            var response = new DailyKPIsResponse
            {
                BusinessId = businessId,
                StoreId = storeId,
                Date = DateTime.UtcNow,
                Kpis = new List<DailyKPI>
                {
                    // 1. Ingresos del día
                    new DailyKPI
                    {
                        Name = "Ingresos del día",
                        Value = kpisResult.TodayRevenue,
                        FormattedValue = FormatCurrency(kpisResult.TodayRevenue),
                        ChangePercentage = revenueChangePercent,
                        ChangeAmount = kpisResult.TodayRevenue - kpisResult.YesterdayRevenue,
                        PreviousValue = kpisResult.YesterdayRevenue,
                        Trend = GetTrend(kpisResult.TodayRevenue, kpisResult.YesterdayRevenue),
                        IsAlert = false
                    },
                    // 2. Ticket promedio
                    new DailyKPI
                    {
                        Name = "Ticket promedio",
                        Value = todayTicketAverage,
                        FormattedValue = FormatCurrency(todayTicketAverage),
                        ChangePercentage = ticketAverageChangePercent,
                        ChangeAmount = todayTicketAverage - yesterdayTicketAverage,
                        PreviousValue = yesterdayTicketAverage,
                        Trend = GetTrend(todayTicketAverage, yesterdayTicketAverage),
                        IsAlert = false
                    },
                    // 3. Capital en stock (costo)
                    new DailyKPI
                    {
                        Name = "Capital en stock",
                        Value = todayStockCapital,
                        FormattedValue = FormatCurrency(todayStockCapital),
                        ChangePercentage = stockCapitalChangePercent,
                        ChangeAmount = todayStockCapital - yesterdayStockCapital,
                        PreviousValue = yesterdayStockCapital,
                        Trend = GetTrend(todayStockCapital, yesterdayStockCapital),
                        IsAlert = false
                    },
                    // 4. Valor potencial de ventas (precio de venta * stock)
                    new DailyKPI
                    {
                        Name = "Valor potencial",
                        Value = todayStockRevenue,
                        FormattedValue = FormatCurrency(todayStockRevenue),
                        ChangePercentage = stockRevenueChangePercent,
                        ChangeAmount = todayStockRevenue - yesterdayStockRevenue,
                        PreviousValue = yesterdayStockRevenue,
                        Trend = GetTrend(todayStockRevenue, yesterdayStockRevenue),
                        IsAlert = false
                    }
                },
                ExecutionTimeMs = stopwatch.ElapsedMilliseconds
            };

            stopwatch.Stop();
            _logger.LogInformation("✅ KPIs diarios obtenidos en {elapsed}ms", stopwatch.ElapsedMilliseconds);

            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Error obteniendo KPIs diarios para negocio: {businessId}", businessId);
            return StatusCode(500, new { message = "Error interno del servidor", error = ex.Message });
        }
    }

    /// <summary>
    /// Obtiene los productos top del día según el criterio seleccionado
    /// </summary>
    /// <param name="businessId">ID del negocio</param>
    /// <param name="storeId">ID de la tienda (opcional)</param>
    /// <param name="criteria">Criterio de ordenamiento: volume, revenue, margin</param>
    /// <param name="limit">Número de productos a retornar (default: 5)</param>
    /// <returns>Lista de productos top ordenados por el criterio seleccionado</returns>
    [HttpGet("top-products")]
    public async Task<ActionResult<TopProductsResponse>> GetTopProducts(
        [FromQuery] int businessId,
        [FromQuery] int? storeId = null,
        [FromQuery] string criteria = "volume",
        [FromQuery] int limit = 5)
    {
        try
        {
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();
            _logger.LogInformation("🔄 Obteniendo productos top para negocio: {businessId}, tienda: {storeId}, criterio: {criteria}", 
                businessId, storeId, criteria);

            // Validar criterio
            var validCriteria = new[] { "volume", "revenue", "margin" };
            if (!validCriteria.Contains(criteria.ToLower()))
            {
                return BadRequest(new { message = "Criterio inválido. Use: volume, revenue o margin" });
            }

            // Validar límite
            if (limit < 1 || limit > 50)
            {
                return BadRequest(new { message = "El límite debe estar entre 1 y 50" });
            }

            // Verificar que el negocio existe
            var businessExists = await _context.Database.SqlQueryRaw<int>(
                "SELECT COUNT(*) as Value FROM business WHERE id = {0}", businessId)
                .FirstOrDefaultAsync() > 0;

            if (!businessExists)
            {
                return NotFound(new { message = "Negocio no encontrado" });
            }

            // Query base para obtener productos con sus ventas del día
            // Usamos el stock_id de sales_detail para obtener el costo correcto (FIFO)
            var topProductsQuery = @"
                SELECT 
                    p.id as ProductId,
                    p.name as ProductName,
                    p.sku as ProductCode,
                    SUM(sd.amount) as TotalUnits,
                    SUM(sd.amount * sd.price) as TotalRevenue,
                    SUM(sd.amount * (sd.price - COALESCE(s.cost, 0))) as TotalMargin,
                    AVG(COALESCE(s.cost, 0)) as AverageCost,
                    AVG(sd.price) as AveragePrice
                FROM sales_detail sd
                INNER JOIN sales sa ON sd.sale = sa.id
                INNER JOIN store st ON sa.id_store = st.id
                INNER JOIN product p ON sd.product = p.id
                LEFT JOIN stock s ON sd.stock_id = s.id
                WHERE st.id_business = {0}
                    AND DATE(sa.date) = CURDATE()
                    " + (storeId.HasValue ? "AND sa.id_store = {1}" : "") + @"
                GROUP BY p.id, p.name, p.sku
                ORDER BY " + GetOrderByClause(criteria) + @"
                LIMIT {" + (storeId.HasValue ? "2" : "1") + "}";

            List<TopProductData> products;
            
            if (storeId.HasValue)
            {
                products = await _context.Database
                    .SqlQueryRaw<TopProductData>(topProductsQuery, businessId, storeId.Value, limit)
                    .ToListAsync();
            }
            else
            {
                products = await _context.Database
                    .SqlQueryRaw<TopProductData>(topProductsQuery, businessId, limit)
                    .ToListAsync();
            }

            // Mapear a respuesta
            var response = new TopProductsResponse
            {
                BusinessId = businessId,
                StoreId = storeId,
                Date = DateTime.UtcNow,
                Criteria = criteria,
                Products = products.Select((p, index) => new TopProduct
                {
                    Rank = index + 1,
                    ProductId = p.ProductId,
                    ProductName = p.ProductName,
                    ProductCode = p.ProductCode,
                    TotalUnits = p.TotalUnits,
                    TotalRevenue = p.TotalRevenue,
                    TotalMargin = p.TotalMargin,
                    MarginPercentage = p.AveragePrice > 0 
                        ? Math.Round(((p.AveragePrice - p.AverageCost) / p.AveragePrice) * 100, 2) 
                        : 0,
                    AverageCost = p.AverageCost,
                    AveragePrice = p.AveragePrice
                }).ToList(),
                ExecutionTimeMs = stopwatch.ElapsedMilliseconds
            };

            stopwatch.Stop();
            _logger.LogInformation("✅ Productos top obtenidos en {elapsed}ms", stopwatch.ElapsedMilliseconds);

            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Error obteniendo productos top para negocio: {businessId}", businessId);
            return StatusCode(500, new { message = "Error interno del servidor", error = ex.Message });
        }
    }

    /// <summary>
    /// Obtiene métricas comparativas entre todas las tiendas del negocio
    /// </summary>
    /// <param name="businessId">ID del negocio</param>
    /// <returns>Comparativa de métricas por tienda con indicadores de salud</returns>
    [HttpGet("stores-comparison")]
    public async Task<ActionResult<StoresComparisonResponse>> GetStoresComparison(
        [FromQuery] int businessId)
    {
        try
        {
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();
            _logger.LogInformation("🔄 Obteniendo comparativa de tiendas para negocio: {businessId}", businessId);

            // Verificar que el negocio existe
            var businessExists = await _context.Database.SqlQueryRaw<int>(
                "SELECT COUNT(*) as Value FROM business WHERE id = {0}", businessId)
                .FirstOrDefaultAsync() > 0;

            if (!businessExists)
            {
                return NotFound(new { message = "Negocio no encontrado" });
            }

            // Query para obtener métricas de todas las tiendas
            // Usamos DATE_ADD para ajustar UTC-3 (zona horaria de Chile)
            var storesQuery = @"
                SELECT 
                    st.id as StoreId,
                    st.name as StoreName,
                    -- Ventas del día
                    COALESCE(SUM(CASE 
                        WHEN DATE(DATE_ADD(s.date, INTERVAL -3 HOUR)) = DATE(DATE_ADD(NOW(), INTERVAL -3 HOUR))
                        THEN s.total 
                    END), 0) as TodayRevenue,
                    -- Ventas de ayer
                    COALESCE(SUM(CASE 
                        WHEN DATE(DATE_ADD(s.date, INTERVAL -3 HOUR)) = DATE(DATE_ADD(DATE_SUB(NOW(), INTERVAL 1 DAY), INTERVAL -3 HOUR))
                        THEN s.total 
                    END), 0) as YesterdayRevenue,
                    -- Número de ventas del día
                    COUNT(DISTINCT CASE 
                        WHEN DATE(DATE_ADD(s.date, INTERVAL -3 HOUR)) = DATE(DATE_ADD(NOW(), INTERVAL -3 HOUR))
                        THEN s.id 
                    END) as TodaySalesCount,
                    -- Número de ventas de ayer
                    COUNT(DISTINCT CASE 
                        WHEN DATE(DATE_ADD(s.date, INTERVAL -3 HOUR)) = DATE(DATE_ADD(DATE_SUB(NOW(), INTERVAL 1 DAY), INTERVAL -3 HOUR))
                        THEN s.id 
                    END) as YesterdaySalesCount,
                    -- Ventas del mes actual
                    COALESCE(SUM(CASE 
                        WHEN MONTH(DATE_ADD(s.date, INTERVAL -3 HOUR)) = MONTH(DATE_ADD(NOW(), INTERVAL -3 HOUR))
                        AND YEAR(DATE_ADD(s.date, INTERVAL -3 HOUR)) = YEAR(DATE_ADD(NOW(), INTERVAL -3 HOUR))
                        THEN s.total 
                    END), 0) as MonthRevenue,
                    -- Ventas del mes anterior
                    COALESCE(SUM(CASE 
                        WHEN MONTH(DATE_ADD(s.date, INTERVAL -3 HOUR)) = MONTH(DATE_ADD(DATE_SUB(NOW(), INTERVAL 1 MONTH), INTERVAL -3 HOUR))
                        AND YEAR(DATE_ADD(s.date, INTERVAL -3 HOUR)) = YEAR(DATE_ADD(DATE_SUB(NOW(), INTERVAL 1 MONTH), INTERVAL -3 HOUR))
                        THEN s.total 
                    END), 0) as LastMonthRevenue
                FROM store st
                LEFT JOIN sales s ON st.id = s.id_store
                WHERE st.id_business = {0}
                    AND st.active = 1
                    AND (s.date IS NULL OR DATE(DATE_ADD(s.date, INTERVAL -3 HOUR)) >= DATE(DATE_ADD(DATE_SUB(NOW(), INTERVAL 60 DAY), INTERVAL -3 HOUR)))
                GROUP BY st.id, st.name
                ORDER BY TodayRevenue DESC";

            var storesData = await _context.Database
                .SqlQueryRaw<StoreMetricsData>(storesQuery, businessId)
                .ToListAsync();

            // Query para obtener stock por tienda
            var stockQuery = @"
                SELECT 
                    st.id as StoreId,
                    -- Capital en stock (costo) - considerando stock disponible real
                    COALESCE(SUM(
                        CASE 
                            WHEN s.amount > 0 AND s.active = 1 AND s.stock_id IS NULL THEN
                                GREATEST(
                                    s.amount - COALESCE((
                                        SELECT SUM(CAST(sd.amount AS SIGNED))
                                        FROM sales_detail sd
                                        WHERE sd.stock_id = s.id
                                    ), 0),
                                    0
                                ) * s.cost
                            ELSE 0
                        END
                    ), 0) as StockCapital,
                    -- Valor potencial (precio) - considerando stock disponible real
                    COALESCE(SUM(
                        CASE 
                            WHEN s.amount > 0 AND s.active = 1 AND s.stock_id IS NULL THEN
                                GREATEST(
                                    s.amount - COALESCE((
                                        SELECT SUM(CAST(sd.amount AS SIGNED))
                                        FROM sales_detail sd
                                        WHERE sd.stock_id = s.id
                                    ), 0),
                                    0
                                ) * p.price
                            ELSE 0
                        END
                    ), 0) as StockValue,
                    -- Productos con stock bajo (mayor a 0 pero menor que minimumStock) - considerando stock disponible real
                    COUNT(DISTINCT CASE 
                        WHEN (
                            SELECT COALESCE(SUM(
                                CASE 
                                    WHEN stock.amount > 0 AND stock.active = 1 AND stock.stock_id IS NULL THEN
                                        GREATEST(
                                            stock.amount - COALESCE((
                                                SELECT SUM(CAST(sd.amount AS SIGNED))
                                                FROM sales_detail sd
                                                WHERE sd.stock_id = stock.id
                                            ), 0),
                                            0
                                        )
                                    ELSE 0
                                END
                            ), 0)
                            FROM stock
                            WHERE stock.product = p.id 
                            AND stock.id_store = st.id
                            AND stock.amount > 0
                            AND stock.active = 1
                            AND stock.stock_id IS NULL
                        ) BETWEEN 1 AND (COALESCE(p.minimumStock, 0) - 1)
                        AND COALESCE(p.minimumStock, 0) > 0
                        THEN p.id
                    END) as LowStockProducts,
                    -- Productos sin stock (stock = 0) - considerando stock disponible real
                    COUNT(DISTINCT CASE 
                        WHEN (
                            SELECT COALESCE(SUM(
                                CASE 
                                    WHEN stock.amount > 0 AND stock.active = 1 AND stock.stock_id IS NULL THEN
                                        GREATEST(
                                            stock.amount - COALESCE((
                                                SELECT SUM(CAST(sd.amount AS SIGNED))
                                                FROM sales_detail sd
                                                WHERE sd.stock_id = stock.id
                                            ), 0),
                                            0
                                        )
                                    ELSE 0
                                END
                            ), 0)
                            FROM stock
                            WHERE stock.product = p.id 
                            AND stock.id_store = st.id
                            AND stock.amount > 0
                            AND stock.active = 1
                            AND stock.stock_id IS NULL
                        ) = 0
                        AND COALESCE(p.minimumStock, 0) > 0
                        THEN p.id
                    END) as OutOfStockProducts,
                    -- Total de unidades en stock disponible
                    CAST(COALESCE(SUM(
                        CASE 
                            WHEN s.amount > 0 AND s.active = 1 AND s.stock_id IS NULL THEN
                                GREATEST(
                                    s.amount - COALESCE((
                                        SELECT SUM(CAST(sd.amount AS SIGNED))
                                        FROM sales_detail sd
                                        WHERE sd.stock_id = s.id
                                    ), 0),
                                    0
                                )
                            ELSE 0
                        END
                    ), 0) AS SIGNED) as TotalUnits
                FROM store st
                CROSS JOIN product p
                LEFT JOIN stock s ON s.product = p.id AND s.id_store = st.id AND s.amount > 0 AND s.active = 1 AND s.stock_id IS NULL
                WHERE st.id_business = {0}
                    AND st.active = 1
                    AND p.business = {0}
                GROUP BY st.id";

            var stockData = await _context.Database
                .SqlQueryRaw<StoreStockData>(stockQuery, businessId)
                .ToListAsync();

            // Obtener configuración de stores para el score
            var storeConfigs = await _context.Stores
                .Where(s => s.BusinessId == businessId && s.Active)
                .ToDictionaryAsync(s => s.Id, s => s);

            // Combinar datos y calcular métricas
            var stores = storesData.Select(store =>
            {
                var stock = stockData.FirstOrDefault(s => s.StoreId == store.StoreId);
                var storeConfig = storeConfigs.GetValueOrDefault(store.StoreId);
                
                // Si no hay configuración, usar valores por defecto
                if (storeConfig == null)
                {
                    _logger.LogWarning("No se encontró configuración para store {StoreId}, usando valores por defecto", store.StoreId);
                    storeConfig = new Store(); // Usa valores por defecto
                }
                
                // Calcular ticket promedio
                decimal todayTicketAverage = store.TodaySalesCount > 0 
                    ? store.TodayRevenue / store.TodaySalesCount 
                    : 0;
                
                decimal yesterdayTicketAverage = store.YesterdaySalesCount > 0 
                    ? store.YesterdayRevenue / store.YesterdaySalesCount 
                    : 0;

                // Calcular cambios porcentuales
                decimal? revenueChange = CalculateChangePercent(store.TodayRevenue, store.YesterdayRevenue);
                decimal? ticketChange = CalculateChangePercent(todayTicketAverage, yesterdayTicketAverage);
                decimal? monthChange = CalculateChangePercent(store.MonthRevenue, store.LastMonthRevenue);

                // Determinar estado de salud de la tienda usando su configuración
                var healthStatus = CalculateStoreHealthStatus(
                    revenueChange,
                    store.TodaySalesCount,
                    stock?.LowStockProducts ?? 0,
                    stock?.OutOfStockProducts ?? 0,
                    storeConfig
                );

                return new StoreComparison
                {
                    StoreId = store.StoreId,
                    StoreName = store.StoreName,
                    TodayRevenue = store.TodayRevenue,
                    YesterdayRevenue = store.YesterdayRevenue,
                    RevenueChangePercent = revenueChange,
                    TodaySalesCount = store.TodaySalesCount,
                    YesterdaySalesCount = store.YesterdaySalesCount,
                    TodayTicketAverage = todayTicketAverage,
                    YesterdayTicketAverage = yesterdayTicketAverage,
                    TicketChangePercent = ticketChange,
                    MonthRevenue = store.MonthRevenue,
                    LastMonthRevenue = store.LastMonthRevenue,
                    MonthChangePercent = monthChange,
                    StockCapital = stock?.StockCapital ?? 0,
                    StockValue = stock?.StockValue ?? 0,
                    LowStockProducts = stock?.LowStockProducts ?? 0,
                    OutOfStockProducts = stock?.OutOfStockProducts ?? 0,
                    TotalUnits = stock?.TotalUnits ?? 0,
                    HealthStatus = healthStatus.Status,
                    HealthScore = healthStatus.Score,
                    HealthIndicators = healthStatus.Indicators
                };
            }).ToList();

            // Calcular totales del negocio
            var totals = new BusinessTotals
            {
                TotalRevenue = stores.Sum(s => s.TodayRevenue),
                TotalSalesCount = stores.Sum(s => s.TodaySalesCount),
                TotalStockCapital = stores.Sum(s => s.StockCapital),
                TotalStockValue = stores.Sum(s => s.StockValue),
                AverageTicket = stores.Sum(s => s.TodaySalesCount) > 0 
                    ? stores.Sum(s => s.TodayRevenue) / stores.Sum(s => s.TodaySalesCount)
                    : 0,
                ActiveStores = stores.Count,
                HealthyStores = stores.Count(s => s.HealthStatus == "healthy"),
                WarningStores = stores.Count(s => s.HealthStatus == "warning"),
                CriticalStores = stores.Count(s => s.HealthStatus == "critical")
            };

            var response = new StoresComparisonResponse
            {
                BusinessId = businessId,
                Date = DateTime.UtcNow,
                Stores = stores,
                Totals = totals,
                ExecutionTimeMs = stopwatch.ElapsedMilliseconds
            };

            stopwatch.Stop();
            _logger.LogInformation("✅ Comparativa de tiendas obtenida en {elapsed}ms", stopwatch.ElapsedMilliseconds);

            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Error obteniendo comparativa de tiendas para negocio: {businessId}", businessId);
            return StatusCode(500, new { message = "Error interno del servidor", error = ex.Message });
        }
    }

    /// <summary>
    /// Calcula el estado de salud de una tienda usando su configuración personalizada
    /// </summary>
    private static (string Status, int Score, List<string> Indicators) CalculateStoreHealthStatus(
        decimal? revenueChange, 
        int todaySalesCount, 
        int lowStockProducts,
        int outOfStockProducts,
        Store store)
    {
        var indicators = new List<string>();
        int score = store.ScoreBase; // Usa el score base configurable

        // Evaluar cambio en ingresos usando umbrales configurables
        if (revenueChange.HasValue)
        {
            if (revenueChange.Value < store.ScoreHighDropThreshold)
            {
                score -= store.ScoreHighDropPenalty;
                indicators.Add("Caída significativa en ventas");
            }
            else if (revenueChange.Value < store.ScoreMediumDropThreshold)
            {
                score -= store.ScoreMediumDropPenalty;
                indicators.Add("Disminución en ventas");
            }
            else if (revenueChange.Value > 20)
            {
                indicators.Add("Crecimiento destacado");
            }
        }
        else if (todaySalesCount == 0)
        {
            score -= store.ScoreNoSalesPenalty;
            indicators.Add("Sin ventas hoy");
        }

        // Evaluar productos sin stock (prioridad máxima)
        if (outOfStockProducts > store.ScoreCriticalStockThreshold)
        {
            score -= store.ScoreCriticalStockPenalty;
            indicators.Add($"{outOfStockProducts} productos sin stock");
        }
        else if (outOfStockProducts > 0)
        {
            score -= (store.ScoreCriticalStockPenalty / 2); // Penalización moderada por productos sin stock
            indicators.Add($"{outOfStockProducts} productos sin stock");
        }

        // Evaluar productos con stock bajo usando umbrales configurables
        if (lowStockProducts > store.ScoreLowStockThreshold)
        {
            score -= store.ScoreLowStockPenalty;
            indicators.Add($"{lowStockProducts} productos con stock bajo");
        }
        else if (lowStockProducts > 0)
        {
            indicators.Add($"{lowStockProducts} productos requieren reabastecimiento");
        }

        // Evaluar volumen de ventas usando umbral configurable
        if (todaySalesCount < store.ScoreLowVolumeThreshold && todaySalesCount > 0)
        {
            score -= store.ScoreLowVolumePenalty;
            indicators.Add("Volumen de ventas bajo");
        }

        // Determinar estado según los umbrales configurables
        string status = score switch
        {
            var s when s >= store.ScoreHealthyThreshold => "healthy",
            var s when s >= store.ScoreWarningThreshold => "warning",
            _ => "critical"
        };

        if (indicators.Count == 0)
        {
            indicators.Add("Operación normal");
        }

        return (status, score, indicators);
    }

    /// <summary>
    /// Obtiene los movimientos de stock diarios del mes actual comparando todas las tiendas
    /// </summary>
    /// <param name="businessId">ID del negocio</param>
    /// <returns>Movimientos de stock diarios por tienda del mes actual</returns>
    [HttpGet("monthly-stock-chart")]
    public async Task<ActionResult<MonthlyStockChartResponse>> GetMonthlyStockChart(
        [FromQuery] int businessId)
    {
        try
        {
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();
            _logger.LogInformation("🔄 Obteniendo gráfico de movimientos de stock mensuales para negocio: {businessId}", businessId);

            // Verificar que el negocio existe
            var businessExists = await _context.Database.SqlQueryRaw<int>(
                "SELECT COUNT(*) as Value FROM business WHERE id = {0}", businessId)
                .FirstOrDefaultAsync() > 0;

            if (!businessExists)
            {
                return NotFound(new { message = "Negocio no encontrado" });
            }

            // Obtener primer y último día del mes actual
            var today = DateTime.Today;
            var firstDayOfMonth = new DateTime(today.Year, today.Month, 1);
            var lastDayOfMonth = firstDayOfMonth.AddMonths(1).AddDays(-1);

            // Query para obtener movimientos de stock diarios por tienda
            // Incluye tanto entradas (amount > 0) como salidas (amount < 0)
            var dailyStockQuery = @"
                SELECT 
                    DATE(s.date) as MovementDate,
                    st.id as StoreId,
                    st.name as StoreName,
                    -- Entradas de stock (compras, producciones)
                    COALESCE(SUM(CASE WHEN s.amount > 0 THEN s.amount ELSE 0 END), 0) as DailyEntries,
                    -- Salidas de stock (ventas, pérdidas)
                    COALESCE(ABS(SUM(CASE WHEN s.amount < 0 THEN s.amount ELSE 0 END)), 0) as DailyExits,
                    -- Movimiento neto
                    COALESCE(SUM(s.amount), 0) as NetMovement,
                    -- Conteo de movimientos
                    COUNT(s.id) as MovementCount
                FROM store st
                LEFT JOIN stock s ON st.id = s.id_store 
                    AND DATE(s.date) >= {1}
                    AND DATE(s.date) <= {2}
                INNER JOIN product p ON s.product = p.id
                WHERE st.id_business = {0}
                    AND st.active = 1
                    AND p.business = {0}
                GROUP BY DATE(s.date), st.id, st.name
                ORDER BY DATE(s.date), st.name";

            var stockData = await _context.Database
                .SqlQueryRaw<DailyStockData>(
                    dailyStockQuery,
                    businessId,
                    firstDayOfMonth.ToString("yyyy-MM-dd"),
                    today.ToString("yyyy-MM-dd")
                )
                .ToListAsync();

            // Obtener todas las tiendas activas
            var stores = await _context.Database
                .SqlQueryRaw<StoreBasicInfo>(
                    "SELECT id as StoreId, name as StoreName FROM store WHERE id_business = {0} AND active = 1 ORDER BY name",
                    businessId
                )
                .ToListAsync();

            if (!stores.Any())
            {
                return Ok(new MonthlyStockChartResponse
                {
                    BusinessId = businessId,
                    Month = today.ToString("MMMM yyyy"),
                    StartDate = firstDayOfMonth,
                    EndDate = today,
                    Stores = new List<StoreStockChartInfo>(),
                    DailyData = new List<DailyStockChartData>(),
                    ExecutionTimeMs = stopwatch.ElapsedMilliseconds
                });
            }

            // Asignar colores únicos a cada tienda
            var storeColors = new[] { "#3b82f6", "#ef4444", "#10b981", "#f59e0b", "#8b5cf6", "#ec4899", "#14b8a6", "#f97316" };
            var storesInfo = stores.Select((store, index) => new StoreStockChartInfo
            {
                StoreId = store.StoreId,
                StoreName = store.StoreName,
                Color = storeColors[index % storeColors.Length],
                TotalEntries = stockData.Where(s => s.StoreId == store.StoreId).Sum(s => s.DailyEntries),
                TotalExits = stockData.Where(s => s.StoreId == store.StoreId).Sum(s => s.DailyExits),
                NetMovement = stockData.Where(s => s.StoreId == store.StoreId).Sum(s => s.NetMovement),
                TotalMovements = stockData.Where(s => s.StoreId == store.StoreId).Sum(s => s.MovementCount)
            }).ToList();

            // Construir datos diarios - incluir TODOS los días del mes hasta hoy
            var dailyData = new List<DailyStockChartData>();

            for (var date = firstDayOfMonth; date <= today; date = date.AddDays(1))
            {
                var dayData = new DailyStockChartData
                {
                    Date = date,
                    Day = date.Day,
                    DayName = date.ToString("ddd", new System.Globalization.CultureInfo("es-ES")),
                    Stores = new Dictionary<string, StockMovementData>()
                };

                // Agregar movimientos de cada tienda para este día
                foreach (var store in stores)
                {
                    var storeStock = stockData.FirstOrDefault(s => 
                        s.MovementDate.Date == date.Date && s.StoreId == store.StoreId);

                    dayData.Stores[$"store_{store.StoreId}"] = new StockMovementData
                    {
                        Entries = storeStock?.DailyEntries ?? 0,
                        Exits = storeStock?.DailyExits ?? 0,
                        Net = storeStock?.NetMovement ?? 0
                    };
                }

                dailyData.Add(dayData);
            }

            var response = new MonthlyStockChartResponse
            {
                BusinessId = businessId,
                Month = today.ToString("MMMM yyyy", new System.Globalization.CultureInfo("es-ES")),
                StartDate = firstDayOfMonth,
                EndDate = today,
                Stores = storesInfo,
                DailyData = dailyData,
                ExecutionTimeMs = stopwatch.ElapsedMilliseconds
            };

            stopwatch.Stop();
            _logger.LogInformation("✅ Gráfico de movimientos de stock mensuales obtenido en {elapsed}ms", stopwatch.ElapsedMilliseconds);

            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Error obteniendo gráfico de movimientos de stock mensuales para negocio: {businessId}", businessId);
            return StatusCode(500, new { message = "Error interno del servidor", error = ex.Message });
        }
    }

    private static string GetOrderByClause(string criteria)
    {
        return criteria.ToLower() switch
        {
            "volume" => "TotalUnits DESC",
            "revenue" => "TotalRevenue DESC",
            "margin" => "TotalMargin DESC",
            _ => "TotalUnits DESC"
        };
    }

    private static decimal? CalculateChangePercent(decimal current, decimal previous)
    {
        if (previous <= 0) return null;
        return Math.Round(((current - previous) / previous) * 100, 2);
    }

    private static string GetTrend(decimal current, decimal previous)
    {
        if (current > previous) return "up";
        if (current < previous) return "down";
        return "neutral";
    }

    private static string FormatCurrency(decimal value)
    {
        return $"${value:N0}";
    }
}

// DTOs para las respuestas

public class DailyKPIsResponse
{
    public int BusinessId { get; set; }
    public int? StoreId { get; set; }
    public DateTime Date { get; set; }
    public List<DailyKPI> Kpis { get; set; } = new(); // Cambio: KPIs -> Kpis
    public long ExecutionTimeMs { get; set; }
}

public class DailyKPI
{
    public string Name { get; set; } = string.Empty;
    public decimal Value { get; set; }
    public string FormattedValue { get; set; } = string.Empty;
    public decimal? ChangePercentage { get; set; }
    public decimal? ChangeAmount { get; set; }
    public decimal? PreviousValue { get; set; }
    public string Trend { get; set; } = "neutral"; // up, down, neutral
    public bool IsAlert { get; set; }
}

// Clases para mapear resultados de queries SQL

public class KPIsRawData
{
    public decimal TodayRevenue { get; set; }
    public decimal YesterdayRevenue { get; set; }
    public int TodaySalesCount { get; set; }
    public int YesterdaySalesCount { get; set; }
}

public class StockCapitalData
{
    public decimal TodayStockCapital { get; set; }
}

public class StockRevenueData
{
    public decimal StockRevenuePotential { get; set; }
}

public class AlertsData
{
    public int TotalAlerts { get; set; }
}

// DTOs para productos top

public class TopProductsResponse
{
    public int BusinessId { get; set; }
    public int? StoreId { get; set; }
    public DateTime Date { get; set; }
    public string Criteria { get; set; } = string.Empty;
    public List<TopProduct> Products { get; set; } = new();
    public long ExecutionTimeMs { get; set; }
}

public class TopProduct
{
    public int Rank { get; set; }
    public int ProductId { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public string ProductCode { get; set; } = string.Empty;
    public decimal TotalUnits { get; set; }
    public decimal TotalRevenue { get; set; }
    public decimal TotalMargin { get; set; }
    public decimal MarginPercentage { get; set; }
    public decimal AverageCost { get; set; }
    public decimal AveragePrice { get; set; }
}

public class TopProductData
{
    public int ProductId { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public string ProductCode { get; set; } = string.Empty;
    public decimal TotalUnits { get; set; }
    public decimal TotalRevenue { get; set; }
    public decimal TotalMargin { get; set; }
    public decimal AverageCost { get; set; }
    public decimal AveragePrice { get; set; }
}

// DTOs para comparativa de tiendas

public class StoresComparisonResponse
{
    public int BusinessId { get; set; }
    public DateTime Date { get; set; }
    public List<StoreComparison> Stores { get; set; } = new();
    public BusinessTotals Totals { get; set; } = new();
    public long ExecutionTimeMs { get; set; }
}

public class StoreComparison
{
    public int StoreId { get; set; }
    public string StoreName { get; set; } = string.Empty;
    public decimal TodayRevenue { get; set; }
    public decimal YesterdayRevenue { get; set; }
    public decimal? RevenueChangePercent { get; set; }
    public int TodaySalesCount { get; set; }
    public int YesterdaySalesCount { get; set; }
    public decimal TodayTicketAverage { get; set; }
    public decimal YesterdayTicketAverage { get; set; }
    public decimal? TicketChangePercent { get; set; }
    public decimal MonthRevenue { get; set; }
    public decimal LastMonthRevenue { get; set; }
    public decimal? MonthChangePercent { get; set; }
    public decimal StockCapital { get; set; }
    public decimal StockValue { get; set; }
    public int LowStockProducts { get; set; }
    public int OutOfStockProducts { get; set; }
    public int TotalUnits { get; set; }
    public string HealthStatus { get; set; } = "healthy"; // healthy, warning, critical
    public int HealthScore { get; set; }
    public List<string> HealthIndicators { get; set; } = new();
}

public class BusinessTotals
{
    public decimal TotalRevenue { get; set; }
    public int TotalSalesCount { get; set; }
    public decimal TotalStockCapital { get; set; }
    public decimal TotalStockValue { get; set; }
    public decimal AverageTicket { get; set; }
    public int ActiveStores { get; set; }
    public int HealthyStores { get; set; }
    public int WarningStores { get; set; }
    public int CriticalStores { get; set; }
}

public class StoreMetricsData
{
    public int StoreId { get; set; }
    public string StoreName { get; set; } = string.Empty;
    public decimal TodayRevenue { get; set; }
    public decimal YesterdayRevenue { get; set; }
    public int TodaySalesCount { get; set; }
    public int YesterdaySalesCount { get; set; }
    public decimal MonthRevenue { get; set; }
    public decimal LastMonthRevenue { get; set; }
}

public class StoreStockData
{
    public int StoreId { get; set; }
    public decimal StockCapital { get; set; }
    public decimal StockValue { get; set; }
    public int LowStockProducts { get; set; }
    public int OutOfStockProducts { get; set; }
    public int TotalUnits { get; set; }
}

// DTOs para gráfico de movimientos de stock mensuales

public class MonthlyStockChartResponse
{
    public int BusinessId { get; set; }
    public string Month { get; set; } = string.Empty;
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public List<StoreStockChartInfo> Stores { get; set; } = new();
    public List<DailyStockChartData> DailyData { get; set; } = new();
    public long ExecutionTimeMs { get; set; }
}

public class StoreStockChartInfo
{
    public int StoreId { get; set; }
    public string StoreName { get; set; } = string.Empty;
    public string Color { get; set; } = string.Empty;
    public decimal TotalEntries { get; set; }
    public decimal TotalExits { get; set; }
    public decimal NetMovement { get; set; }
    public int TotalMovements { get; set; }
}

public class DailyStockChartData
{
    public DateTime Date { get; set; }
    public int Day { get; set; }
    public string DayName { get; set; } = string.Empty;
    public Dictionary<string, StockMovementData> Stores { get; set; } = new();
}

public class StockMovementData
{
    public decimal Entries { get; set; }
    public decimal Exits { get; set; }
    public decimal Net { get; set; }
}

public class DailyStockData
{
    public DateTime MovementDate { get; set; }
    public int StoreId { get; set; }
    public string StoreName { get; set; } = string.Empty;
    public decimal DailyEntries { get; set; }
    public decimal DailyExits { get; set; }
    public decimal NetMovement { get; set; }
    public int MovementCount { get; set; }
}

public class StorBasicInfo
{
    public int StoreId { get; set; }
    public string StoreName { get; set; } = string.Empty;
}

// DTOs obsoletos de ventas mensuales (mantener por compatibilidad)

public class MonthlySalesChartResponse
{
    public int BusinessId { get; set; }
    public string Month { get; set; } = string.Empty;
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public List<StoreChartInfo> Stores { get; set; } = new();
    public List<DailyChartData> DailyData { get; set; } = new();
    public long ExecutionTimeMs { get; set; }
}

public class StoreChartInfo
{
    public int StoreId { get; set; }
    public string StoreName { get; set; } = string.Empty;
    public string Color { get; set; } = string.Empty;
    public decimal TotalRevenue { get; set; }
    public int TotalTransactions { get; set; }
}

public class DailyChartData
{
    public DateTime Date { get; set; }
    public int Day { get; set; }
    public string DayName { get; set; } = string.Empty;
    public Dictionary<string, decimal> Stores { get; set; } = new();
}

public class DailySalesData
{
    public DateTime SaleDate { get; set; }
    public int StoreId { get; set; }
    public string StoreName { get; set; } = string.Empty;
    public decimal DailyRevenue { get; set; }
    public int TransactionCount { get; set; }
}

public class StoreBasicInfo
{
    public int StoreId { get; set; }
    public string StoreName { get; set; } = string.Empty;
}
