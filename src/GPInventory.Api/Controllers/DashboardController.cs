using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using GPInventory.Infrastructure.Data;
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
            var kpisQuery = @"
                SELECT 
                    -- Ingresos del día actual
                    COALESCE(SUM(CASE 
                        WHEN DATE(s.date) = CURDATE() 
                        THEN s.total 
                        ELSE 0 
                    END), 0) as TodayRevenue,
                    
                    -- Ingresos del día anterior
                    COALESCE(SUM(CASE 
                        WHEN DATE(s.date) = DATE_SUB(CURDATE(), INTERVAL 1 DAY) 
                        THEN s.total 
                        ELSE 0 
                    END), 0) as YesterdayRevenue,
                    
                    -- Número de ventas del día actual
                    COUNT(DISTINCT CASE 
                        WHEN DATE(s.date) = CURDATE() 
                        THEN s.id 
                    END) as TodaySalesCount,
                    
                    -- Número de ventas del día anterior
                    COUNT(DISTINCT CASE 
                        WHEN DATE(s.date) = DATE_SUB(CURDATE(), INTERVAL 1 DAY) 
                        THEN s.id 
                    END) as YesterdaySalesCount
                FROM sales s
                INNER JOIN store st ON s.id_store = st.id
                WHERE st.id_business = {0}
                    AND DATE(s.date) >= DATE_SUB(CURDATE(), INTERVAL 1 DAY)
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

            // Query para obtener capital en stock (valor total del inventario)
            var stockCapitalQuery = @"
                SELECT 
                    COALESCE(SUM(s.amount * COALESCE(s.cost, 0)), 0) as TodayStockCapital
                FROM stock s
                INNER JOIN store st ON s.id_store = st.id
                WHERE st.id_business = {0}
                    AND COALESCE(s.active, 0) = 1
                    AND s.amount > 0
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

            // Query para obtener total de alertas (productos con stock bajo o agotados)
            var alertsQuery = @"
                SELECT 
                    COUNT(DISTINCT p.id) as TotalAlerts
                FROM product p
                LEFT JOIN (
                    SELECT 
                        s.product,
                        SUM(s.amount) as total_stock
                    FROM stock s
                    INNER JOIN store st ON s.id_store = st.id
                    WHERE st.id_business = {0}
                        AND COALESCE(s.active, 0) = 1
                        " + (storeId.HasValue ? "AND s.id_store = {1}" : "") + @"
                    GROUP BY s.product
                ) stock_data ON p.id = stock_data.product
                WHERE p.business = {0}
                    AND (
                        COALESCE(stock_data.total_stock, 0) = 0 
                        OR COALESCE(stock_data.total_stock, 0) <= COALESCE(p.minimumStock, 0)
                    )";

            var alertsResult = storeId.HasValue
                ? await _context.Database.SqlQueryRaw<AlertsData>(alertsQuery, businessId, storeId.Value)
                    .FirstOrDefaultAsync()
                : await _context.Database.SqlQueryRaw<AlertsData>(alertsQuery, businessId)
                    .FirstOrDefaultAsync();

            int totalAlerts = alertsResult?.TotalAlerts ?? 0;

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
                    // 3. Capital en stock
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
                    // 4. Total de alertas
                    new DailyKPI
                    {
                        Name = "Total de alertas",
                        Value = totalAlerts,
                        FormattedValue = totalAlerts.ToString(),
                        ChangePercentage = null,
                        ChangeAmount = null,
                        PreviousValue = null,
                        Trend = "neutral",
                        IsAlert = totalAlerts > 0
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

public class AlertsData
{
    public int TotalAlerts { get; set; }
}
