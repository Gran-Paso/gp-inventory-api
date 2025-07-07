using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using GPInventory.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Cors;

namespace GPInventory.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[EnableCors("AllowFrontend")]
public class AnalyticsController : ControllerBase
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<AnalyticsController> _logger;

    public AnalyticsController(ApplicationDbContext context, ILogger<AnalyticsController> logger)
    {
        _context = context;
        _logger = logger;
    }

    /// <summary>
    /// Obtiene un dashboard completo de analytics del negocio
    /// </summary>
    /// <param name="businessId">ID del negocio</param>
    /// <param name="days">Número de días para análisis (opcional, por defecto 30)</param>
    /// <returns>Métricas completas de analytics</returns>
    [HttpGet("dashboard/{businessId}")]
    [Authorize]
    [ProducesResponseType(200)]
    [ProducesResponseType(401)]
    [ProducesResponseType(404)]
    [ProducesResponseType(500)]
    public async Task<ActionResult<object>> GetAnalyticsDashboard(int businessId, [FromQuery] int days = 30)
    {
        try
        {
            _logger.LogInformation("Obteniendo analytics para negocio: {businessId}", businessId);

            // Verificar que el negocio existe
            var business = await _context.Businesses.FindAsync(businessId);
            if (business == null)
            {
                return NotFound(new { message = "Negocio no encontrado" });
            }

            // Fechas de análisis
            var today = DateTime.Today;
            var startDate = today.AddDays(-days);
            var yesterday = today.AddDays(-1);
            var lastWeek = today.AddDays(-7);
            var lastMonth = today.AddMonths(-1);
            var startOfMonth = new DateTime(today.Year, today.Month, 1);
            var lastMonthStart = startOfMonth.AddMonths(-1);
            var lastMonthEnd = startOfMonth.AddDays(-1);

            // Obtener datos base de ventas
            var salesData = await _context.SaleDetails
                .Include(sd => sd.Sale)
                .Where(sd => sd.Sale.BusinessId == businessId && sd.Sale.Date >= startDate)
                .Select(sd => new {
                    sd.ProductId,
                    sd.Price,
                    sd.Amount,
                    sd.Sale.Date
                })
                .ToListAsync();

            // Obtener datos de stock
            var stockData = await _context.Stocks
                .Include(s => s.Product)
                .Where(s => s.Product.BusinessId == businessId)
                .Select(s => new {
                    s.ProductId,
                    s.Amount,
                    s.Cost,
                    s.Date
                })
                .ToListAsync();

            // Obtener productos
            var products = await _context.Products
                .Where(p => p.BusinessId == businessId)
                .Select(p => new {
                    p.Id,
                    p.Name,
                    p.Sku,
                    p.Price,
                    p.Cost
                })
                .ToListAsync();

            // === CÁLCULOS PRINCIPALES ===
            
            // Métricas de ventas
            var totalRevenue = salesData.Sum(sd => sd.Price * decimal.Parse(sd.Amount.ToString()));
            var totalTransactions = salesData.GroupBy(sd => new { sd.Date, sd.ProductId }).Count();
            var totalItemsSold = salesData.Sum(sd => int.Parse(sd.Amount.ToString()));

            // COGS y margen
            var cogs = CalculateCOGS(salesData, stockData);
            var grossProfit = totalRevenue - cogs;
            var grossMargin = totalRevenue > 0 ? (grossProfit / totalRevenue) * 100 : 0;

            // Métricas por período
            var todaySales = GetSalesMetrics(salesData, today, today);
            var yesterdaySales = GetSalesMetrics(salesData, yesterday, yesterday);
            var weekSales = GetSalesMetrics(salesData, lastWeek, today);
            var monthSales = GetSalesMetrics(salesData, startOfMonth, today);
            var lastMonthSales = GetSalesMetrics(salesData, lastMonthStart, lastMonthEnd);

            // Análisis de productos
            var productAnalytics = GetProductAnalytics(products, salesData, stockData);

            // Análisis de inventario
            var inventoryMetrics = GetInventoryMetrics(products, stockData);

            // KPIs financieros
            var inventoryTurnover = inventoryMetrics.totalValue > 0 ? cogs / inventoryMetrics.totalValue : 0;
            var daysInventoryOutstanding = inventoryTurnover > 0 ? 365 / inventoryTurnover : 0;
            var returnOnInventory = inventoryMetrics.totalValue > 0 ? (grossProfit / inventoryMetrics.totalValue) * 100 : 0;

            // Métricas de crecimiento
            var dailyGrowth = yesterdaySales.revenue > 0 ? 
                ((todaySales.revenue - yesterdaySales.revenue) / yesterdaySales.revenue) * 100 : 0;
            var monthlyGrowth = lastMonthSales.revenue > 0 ? 
                ((monthSales.revenue - lastMonthSales.revenue) / lastMonthSales.revenue) * 100 : 0;

            var result = new
            {
                businessId = businessId,
                period = new { 
                    days = days, 
                    startDate = startDate, 
                    endDate = today 
                },
                
                // Resumen ejecutivo
                summary = new
                {
                    totalRevenue = Math.Round(totalRevenue, 2),
                    totalTransactions = totalTransactions,
                    totalItemsSold = totalItemsSold,
                    averageOrderValue = totalTransactions > 0 ? Math.Round(totalRevenue / totalTransactions, 2) : 0m,
                    grossProfit = Math.Round(grossProfit, 2),
                    grossMargin = Math.Round(grossMargin, 2),
                    totalProducts = products.Count,
                    activeProducts = productAnalytics.Count(p => p.unitsSold > 0)
                },

                // Métricas de ventas por período
                sales = new
                {
                    today = todaySales,
                    yesterday = yesterdaySales,
                    week = weekSales,
                    month = monthSales,
                    lastMonth = lastMonthSales
                },

                // Análisis de crecimiento
                growth = new
                {
                    dailyGrowth = Math.Round(dailyGrowth, 2),
                    monthlyGrowth = Math.Round(monthlyGrowth, 2),
                    revenueGrowthTrend = GetGrowthTrend(dailyGrowth, monthlyGrowth)
                },

                // Top productos por ingresos
                topProducts = productAnalytics
                    .OrderByDescending(p => p.revenue)
                    .Take(10)
                    .Select(p => new {
                        productId = p.productId,
                        name = p.name,
                        revenue = Math.Round(p.revenue, 2),
                        unitsSold = p.unitsSold,
                        margin = Math.Round(p.margin, 2),
                        currentStock = p.currentStock
                    })
                    .ToList(),

                // Productos con bajo rendimiento
                underperformingProducts = productAnalytics
                    .Where(p => p.unitsSold > 0 && p.margin < 20)
                    .OrderBy(p => p.margin)
                    .Take(5)
                    .Select(p => new {
                        productId = p.productId,
                        name = p.name,
                        revenue = Math.Round(p.revenue, 2),
                        margin = Math.Round(p.margin, 2),
                        currentStock = p.currentStock
                    })
                    .ToList(),

                // Análisis de inventario
                inventory = new
                {
                    totalProducts = inventoryMetrics.totalProducts,
                    totalStock = inventoryMetrics.totalStock,
                    totalValue = Math.Round(inventoryMetrics.totalValue, 2),
                    outOfStockProducts = inventoryMetrics.outOfStock,
                    lowStockProducts = inventoryMetrics.lowStock,
                    averageStockPerProduct = inventoryMetrics.averageStock
                },

                // KPIs financieros avanzados
                financialKPIs = new
                {
                    // Rotación de inventario (veces por año)
                    inventoryTurnover = Math.Round(inventoryTurnover, 2),
                    
                    // Días de inventario pendiente
                    daysInventoryOutstanding = Math.Round(daysInventoryOutstanding, 2),
                    
                    // Retorno sobre inventario (%)
                    returnOnInventory = Math.Round(returnOnInventory, 2),
                    
                    // Margen bruto (%)
                    grossMarginPercentage = Math.Round(grossMargin, 2),
                    
                    // Costo de productos vendidos
                    costOfGoodsSold = Math.Round(cogs, 2),
                    
                    // Velocidad de ventas (items/día)
                    salesVelocity = days > 0 ? Math.Round((decimal)totalItemsSold / days, 2) : 0m,
                    
                    // Ingresos promedio por día
                    avgDailyRevenue = days > 0 ? Math.Round(totalRevenue / days, 2) : 0m,
                    
                    // Tasa de conversión de stock (%)
                    stockConversionRate = inventoryMetrics.totalStock > 0 ? 
                        Math.Round(((decimal)totalItemsSold / inventoryMetrics.totalStock) * 100, 2) : 0m
                },

                // DATOS PARA GRÁFICOS
                charts = new
                {
                    // Gráfico de ingresos diarios (últimos 30 días)
                    dailyRevenue = GetDailyRevenueChart(salesData, days),
                    
                    // Gráfico de productos más vendidos (top 10)
                    topProductsSales = GetTopProductsChart(productAnalytics),
                    
                    // Gráfico de distribución de márgenes
                    marginDistribution = GetMarginDistributionChart(productAnalytics),
                    
                    // Gráfico de ventas por hora del día
                    hourlyDistribution = GetHourlyDistributionChart(salesData),
                    
                    // Gráfico de ventas por día de la semana
                    weeklyDistribution = GetWeeklyDistributionChart(salesData),
                    
                    // Gráfico de evolución mensual
                    monthlyTrend = GetMonthlyTrendChart(salesData),
                    
                    // Gráfico de inventario vs ventas
                    inventoryVsSales = GetInventoryVsSalesChart(productAnalytics),
                    
                    // Gráfico de KPIs principales (gauge/indicadores)
                    kpiGauges = GetKPIGaugesChart(grossMargin, inventoryTurnover, returnOnInventory),
                    
                    // Gráfico de crecimiento (comparativo)
                    growthComparison = GetGrowthComparisonChart(todaySales, yesterdaySales, monthSales, lastMonthSales)
                },

                // Análisis temporal
                timeAnalysis = GetTimeAnalysis(salesData),

                // Tendencias semanales
                weeklyTrends = GetWeeklyTrends(salesData)
            };

            _logger.LogInformation($"Analytics generados para negocio {businessId} - {days} días");
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al obtener analytics del negocio: {businessId}", businessId);
            return StatusCode(500, new { message = "Error interno del servidor" });
        }
    }

    /// <summary>
    /// Obtiene análisis de rentabilidad detallado por producto
    /// </summary>
    [HttpGet("profitability/{businessId}")]
    [Authorize]
    [ProducesResponseType(200)]
    [ProducesResponseType(401)]
    [ProducesResponseType(404)]
    [ProducesResponseType(500)]
    public async Task<ActionResult<object>> GetProfitabilityAnalysis(int businessId, [FromQuery] int days = 30)
    {
        try
        {
            var business = await _context.Businesses.FindAsync(businessId);
            if (business == null)
            {
                return NotFound(new { message = "Negocio no encontrado" });
            }

            var startDate = DateTime.Today.AddDays(-days);
            
            var salesData = await _context.SaleDetails
                .Include(sd => sd.Sale)
                .Where(sd => sd.Sale.BusinessId == businessId && sd.Sale.Date >= startDate)
                .Select(sd => new {
                    sd.ProductId,
                    sd.Price,
                    sd.Amount,
                    sd.Sale.Date
                })
                .ToListAsync();

            var stockData = await _context.Stocks
                .Include(s => s.Product)
                .Where(s => s.Product.BusinessId == businessId)
                .Select(s => new {
                    s.ProductId,
                    s.Amount,
                    s.Cost,
                    s.Date
                })
                .ToListAsync();

            var products = await _context.Products
                .Where(p => p.BusinessId == businessId)
                .Select(p => new {
                    p.Id,
                    p.Name,
                    p.Sku,
                    p.Price,
                    p.Cost
                })
                .ToListAsync();

            var profitabilityData = GetDetailedProfitability(products, salesData, stockData);

            var result = new
            {
                businessId,
                period = new { days, startDate, endDate = DateTime.Today },
                totalRevenue = Math.Round(profitabilityData.Sum(p => p.revenue), 2),
                totalCost = Math.Round(profitabilityData.Sum(p => p.totalCost), 2),
                totalProfit = Math.Round(profitabilityData.Sum(p => p.profit), 2),
                averageMargin = profitabilityData.Any() ? 
                    Math.Round(profitabilityData.Average(p => p.margin), 2) : 0m,
                products = profitabilityData
                    .OrderByDescending(p => p.profit)
                    .Select(p => new {
                        productId = p.productId,
                        name = p.name,
                        sku = p.sku,
                        revenue = Math.Round(p.revenue, 2),
                        cost = Math.Round(p.totalCost, 2),
                        profit = Math.Round(p.profit, 2),
                        margin = Math.Round(p.margin, 2),
                        unitsSold = p.unitsSold,
                        avgSellingPrice = Math.Round(p.avgSellingPrice, 2)
                    })
                    .ToList()
            };

            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al obtener análisis de rentabilidad");
            return StatusCode(500, new { message = "Error interno del servidor" });
        }
    }

    #region Métodos Privados de Cálculo

    private decimal CalculateCOGS(IEnumerable<dynamic> salesData, IEnumerable<dynamic> stockData)
    {
        decimal totalCOGS = 0;

        var salesByProduct = salesData.GroupBy(sd => sd.ProductId);

        foreach (var productSales in salesByProduct)
        {
            var productStocks = stockData
                .Where(s => s.ProductId == productSales.Key && s.Cost != null && s.Cost > 0)
                .ToList();

            if (productStocks.Any())
            {
                var avgCost = productStocks.Average(s => (decimal)s.Cost);
                var totalSold = productSales.Sum(sd => int.Parse(sd.Amount.ToString()));
                totalCOGS += avgCost * totalSold;
            }
        }

        return totalCOGS;
    }

    private (decimal revenue, int transactions, int items) GetSalesMetrics(IEnumerable<dynamic> salesData, DateTime start, DateTime end)
    {
        var periodSales = salesData.Where(sd => sd.Date.Date >= start && sd.Date.Date <= end).ToList();
        
        var revenue = periodSales.Sum(sd => sd.Price * int.Parse(sd.Amount.ToString()));
        var transactions = periodSales.GroupBy(sd => new { sd.Date, sd.ProductId }).Count();
        var items = periodSales.Sum(sd => int.Parse(sd.Amount.ToString()));

        return (revenue, transactions, items);
    }

    private List<(int productId, string name, string sku, decimal revenue, int unitsSold, decimal margin, int currentStock)> 
        GetProductAnalytics(IEnumerable<dynamic> products, IEnumerable<dynamic> salesData, IEnumerable<dynamic> stockData)
    {
        var result = new List<(int, string, string, decimal, int, decimal, int)>();

        foreach (var product in products)
        {
            var productSales = salesData.Where(sd => sd.ProductId == product.Id).ToList();
            var productStocks = stockData.Where(s => s.ProductId == product.Id).ToList();

            var revenue = productSales.Sum(sd => sd.Price * int.Parse(sd.Amount.ToString()));
            var unitsSold = productSales.Sum(sd => int.Parse(sd.Amount.ToString()));
            var currentStock = productStocks.Sum(s => s.Amount);

            var avgCost = productStocks.Where(s => s.Cost != null && s.Cost > 0).Any() ?
                productStocks.Where(s => s.Cost != null && s.Cost > 0).Average(s => (decimal)s.Cost) : 
                (decimal)product.Cost;

            var totalCost = avgCost * unitsSold;
            var profit = revenue - totalCost;
            var margin = revenue > 0 ? (profit / revenue) * 100 : 0;

            result.Add((product.Id, product.Name, product.Sku, revenue, unitsSold, margin, currentStock));
        }

        return result;
    }

    private (int totalProducts, int totalStock, decimal totalValue, int outOfStock, int lowStock, decimal averageStock) 
        GetInventoryMetrics(IEnumerable<dynamic> products, IEnumerable<dynamic> stockData)
    {
        var inventoryByProduct = products.Select(p =>
        {
            var productStocks = stockData.Where(s => s.ProductId == p.Id).ToList();
            var currentStock = productStocks.Sum(s => s.Amount);
            var avgCost = productStocks.Where(s => s.Cost != null && s.Cost > 0).Any() ?
                productStocks.Where(s => s.Cost != null && s.Cost > 0).Average(s => (decimal)s.Cost) :
                (decimal)p.Cost;

            return new
            {
                currentStock = currentStock,
                value = currentStock * avgCost,
                status = currentStock <= 0 ? "OutOfStock" : 
                        currentStock <= 5 ? "LowStock" : "InStock"
            };
        }).ToList();

        var totalProducts = products.Count();
        var totalStock = inventoryByProduct.Sum(i => i.currentStock);
        var totalValue = inventoryByProduct.Sum(i => i.value);
        var outOfStock = inventoryByProduct.Count(i => i.status == "OutOfStock");
        var lowStock = inventoryByProduct.Count(i => i.status == "LowStock");
        var averageStock = totalProducts > 0 ? (decimal)totalStock / totalProducts : 0;

        return (totalProducts, totalStock, totalValue, outOfStock, lowStock, averageStock);
    }

    private string GetGrowthTrend(decimal dailyGrowth, decimal monthlyGrowth)
    {
        if (dailyGrowth > 0 && monthlyGrowth > 0) return "Positive";
        if (dailyGrowth < 0 && monthlyGrowth < 0) return "Negative";
        if (dailyGrowth > 0 && monthlyGrowth < 0) return "Short-term-positive";
        if (dailyGrowth < 0 && monthlyGrowth > 0) return "Long-term-positive";
        return "Stable";
    }

    private object GetTimeAnalysis(IEnumerable<dynamic> salesData)
    {
        var hourlyStats = salesData
            .GroupBy(sd => sd.Date.Hour)
            .Select(g => new
            {
                hour = g.Key,
                transactions = g.Count(),
                revenue = g.Sum(sd => sd.Price * int.Parse(sd.Amount.ToString()))
            })
            .OrderBy(h => h.hour)
            .ToList();

        var dailyStats = salesData
            .GroupBy(sd => sd.Date.DayOfWeek)
            .Select(g => new
            {
                dayOfWeek = g.Key.ToString(),
                transactions = g.Count(),
                revenue = g.Sum(sd => sd.Price * int.Parse(sd.Amount.ToString()))
            })
            .ToList();

        var peakHour = hourlyStats.OrderByDescending(h => h.revenue).FirstOrDefault();
        var peakDay = dailyStats.OrderByDescending(d => d.revenue).FirstOrDefault();

        return new
        {
            hourlyDistribution = hourlyStats,
            weeklyDistribution = dailyStats,
            peakHour = peakHour?.hour,
            peakDayOfWeek = peakDay?.dayOfWeek
        };
    }

    private object GetWeeklyTrends(IEnumerable<dynamic> salesData)
    {
        var weeklyData = salesData
            .GroupBy(sd => new { 
                Year = sd.Date.Year, 
                Week = GetWeekOfYear(sd.Date) 
            })
            .Select(g => new
            {
                year = g.Key.Year,
                week = g.Key.Week,
                transactions = g.Count(),
                revenue = g.Sum(sd => sd.Price * int.Parse(sd.Amount.ToString())),
                items = g.Sum(sd => int.Parse(sd.Amount.ToString()))
            })
            .OrderBy(w => w.year)
            .ThenBy(w => w.week)
            .Take(12)
            .ToList();

        return weeklyData;
    }

    private List<(int productId, string name, string sku, decimal revenue, decimal totalCost, decimal profit, decimal margin, int unitsSold, decimal avgSellingPrice)> 
        GetDetailedProfitability(IEnumerable<dynamic> products, IEnumerable<dynamic> salesData, IEnumerable<dynamic> stockData)
    {
        var result = new List<(int, string, string, decimal, decimal, decimal, decimal, int, decimal)>();

        foreach (var product in products)
        {
            var productSales = salesData.Where(sd => sd.ProductId == product.Id).ToList();
            var productStocks = stockData.Where(s => s.ProductId == product.Id).ToList();

            var revenue = productSales.Sum(sd => sd.Price * int.Parse(sd.Amount.ToString()));
            var unitsSold = productSales.Sum(sd => int.Parse(sd.Amount.ToString()));

            var avgCost = productStocks.Where(s => s.Cost != null && s.Cost > 0).Any() ?
                productStocks.Where(s => s.Cost != null && s.Cost > 0).Average(s => (decimal)s.Cost) :
                (decimal)product.Cost;

            var totalCost = avgCost * unitsSold;
            var profit = revenue - totalCost;
            var margin = revenue > 0 ? (profit / revenue) * 100 : 0;
            var avgSellingPrice = unitsSold > 0 ? revenue / unitsSold : 0;

            result.Add((product.Id, product.Name, product.Sku, revenue, totalCost, profit, margin, unitsSold, avgSellingPrice));
        }

        return result;
    }

    private int GetWeekOfYear(DateTime date)
    {
        var cal = System.Globalization.CultureInfo.CurrentCulture.Calendar;
        return cal.GetWeekOfYear(date, System.Globalization.CalendarWeekRule.FirstDay, DayOfWeek.Monday);
    }

    #region Métodos para Gráficos

    /// <summary>
    /// Datos para gráfico de ingresos diarios mejorado (Chart.js Line Chart)
    /// </summary>
    private object GetDailyRevenueChart(IEnumerable<dynamic> salesData, int days)
    {
        var dailyData = new List<object>();
        var today = DateTime.Today;
        
        for (int i = days - 1; i >= 0; i--)
        {
            var date = today.AddDays(-i);
            var dayData = salesData.Where(sd => sd.Date.Date == date).ToList();
            var revenue = dayData.Sum(sd => sd.Price * int.Parse(sd.Amount.ToString()));
            var transactions = dayData.GroupBy(sd => new { sd.Date, sd.ProductId }).Count();
            var items = dayData.Sum(sd => int.Parse(sd.Amount.ToString()));
            
            dailyData.Add(new 
            {
                date = date.ToString("yyyy-MM-dd"),
                dateLabel = date.ToString("dd MMM"),
                dayName = date.ToString("ddd"),
                revenue = Math.Round((decimal)revenue, 2),
                transactions = transactions,
                items = items,
                avgTicket = transactions > 0 ? Math.Round((decimal)revenue / transactions, 2) : 0m
            });
        }

        return new
        {
            type = "line",
            data = new
            {
                labels = dailyData.Select(d => ((dynamic)d).dateLabel).ToArray(),
                datasets = new object[]
                {
                    new
                    {
                        label = "💰 Ingresos ($)",
                        data = dailyData.Select(d => ((dynamic)d).revenue).ToArray(),
                        borderColor = "#667eea",
                        backgroundColor = "rgba(102, 126, 234, 0.3)",
                        borderWidth = 4,
                        fill = true,
                        tension = 0.4,
                        pointBackgroundColor = "#667eea",
                        pointBorderColor = "#ffffff",
                        pointBorderWidth = 3,
                        pointRadius = 8,
                        pointHoverRadius = 12,
                        pointHoverBackgroundColor = "#764ba2",
                        pointHoverBorderColor = "#ffffff",
                        pointHoverBorderWidth = 4,
                        pointStyle = "circle",
                        yAxisID = "y",
                        // Datos para gradientes personalizados (se aplicarán en frontend)
                        gradientColors = new
                        {
                            line = new[] { "#667eea", "#764ba2", "#f093fb" },
                            fill = new[] { "rgba(102, 126, 234, 0.8)", "rgba(118, 75, 162, 0.4)", "rgba(240, 147, 251, 0.1)" }
                        }
                    },
                    new
                    {
                        label = "� Transacciones",
                        data = dailyData.Select(d => ((dynamic)d).transactions).ToArray(),
                        borderColor = "#4facfe",
                        backgroundColor = "rgba(79, 172, 254, 0.2)",
                        borderWidth = 3,
                        fill = false,
                        tension = 0.3,
                        pointBackgroundColor = "#4facfe",
                        pointBorderColor = "#ffffff",
                        pointBorderWidth = 2,
                        pointRadius = 6,
                        pointHoverRadius = 10,
                        pointHoverBackgroundColor = "#00d4ff",
                        pointHoverBorderColor = "#ffffff",
                        pointHoverBorderWidth = 3,
                        yAxisID = "y1"
                    },
                    new
                    {
                        label = "� Items Vendidos",
                        data = dailyData.Select(d => ((dynamic)d).items).ToArray(),
                        borderColor = "#f093fb",
                        backgroundColor = "rgba(240, 147, 251, 0.15)",
                        borderWidth = 2,
                        fill = false,
                        tension = 0.3,
                        pointBackgroundColor = "#f093fb",
                        pointBorderColor = "#ffffff",
                        pointBorderWidth = 2,
                        pointRadius = 5,
                        pointHoverRadius = 8,
                        pointHoverBackgroundColor = "#ff8cc8",
                        pointHoverBorderColor = "#ffffff",
                        pointHoverBorderWidth = 3,
                        yAxisID = "y2",
                        borderDash = new[] { 5, 5 },
                        pointStyle = "rectRot"
                    }
                }
            },
            options = new
            {
                responsive = true,
                maintainAspectRatio = false,
                interaction = new
                {
                    mode = "index",
                    intersect = false,
                    includeInvisible = false
                },
                plugins = new
                {
                    title = new
                    {
                        display = true,
                        text = "📈 Dashboard de Ventas Diarias",
                        font = new 
                        { 
                            size = 22, 
                            weight = "bold",
                            family = "'Segoe UI', -apple-system, BlinkMacSystemFont, sans-serif"
                        },
                        color = "#1a202c",
                        padding = new { top = 15, bottom = 25 }
                    },
                    subtitle = new
                    {
                        display = true,
                        text = "Evolución de ingresos, transacciones e items vendidos",
                        font = new { size = 14, style = "italic" },
                        color = "#4a5568",
                        padding = new { bottom = 20 }
                    },
                    legend = new
                    {
                        display = true,
                        position = "top",
                        align = "end",
                        labels = new
                        {
                            usePointStyle = true,
                            pointStyle = "circle",
                            pointStyleWidth = 15,
                            padding = 25,
                            font = new 
                            { 
                                size = 13,
                                weight = "500",
                                family = "'Segoe UI', sans-serif"
                            },
                            color = "#2d3748",
                            boxWidth = 15,
                            boxHeight = 15
                        }
                    },
                    tooltip = new
                    {
                        enabled = true,
                        backgroundColor = "rgba(26, 32, 44, 0.95)",
                        titleColor = "#ffffff",
                        bodyColor = "#e2e8f0",
                        borderColor = "#667eea",
                        borderWidth = 2,
                        cornerRadius = 12,
                        displayColors = true,
                        boxPadding = 8,
                        titleFont = new
                        {
                            size = 14,
                            weight = "bold"
                        },
                        bodyFont = new
                        {
                            size = 13,
                            weight = "normal"
                        },
                        footerFont = new
                        {
                            size = 12,
                            style = "italic"
                        },
                        padding = 15,
                        caretSize = 8,
                        multiKeyBackground = "rgba(102, 126, 234, 0.8)"
                    }
                },
                scales = new
                {
                    x = new
                    {
                        display = true,
                        title = new
                        {
                            display = true,
                            text = "📅 Período de Análisis",
                            font = new 
                            { 
                                size = 15, 
                                weight = "600",
                                family = "'Segoe UI', sans-serif"
                            },
                            color = "#4a5568"
                        },
                        grid = new
                        {
                            display = true,
                            color = "rgba(160, 174, 192, 0.3)",
                            borderDash = new[] { 3, 3 },
                            lineWidth = 1
                        },
                        ticks = new
                        {
                            font = new 
                            { 
                                size = 12,
                                weight = "500"
                            },
                            color = "#718096",
                            maxRotation = 0,
                            padding = 10
                        },
                        border = new
                        {
                            display = true,
                            color = "#cbd5e0",
                            width = 2
                        }
                    },
                    y = new
                    {
                        type = "linear",
                        display = true,
                        position = "left",
                        beginAtZero = true,
                        title = new
                        {
                            display = true,
                            text = "💰 Ingresos ($)",
                            font = new 
                            { 
                                size = 15, 
                                weight = "600",
                                family = "'Segoe UI', sans-serif"
                            },
                            color = "#667eea"
                        },
                        grid = new
                        {
                            display = true,
                            color = "rgba(102, 126, 234, 0.15)",
                            lineWidth = 1
                        },
                        ticks = new
                        {
                            font = new { size = 12, weight = "500" },
                            color = "#667eea",
                            padding = 8,
                            count = 6
                        },
                        border = new
                        {
                            display = true,
                            color = "#667eea",
                            width = 2
                        }
                    },
                    y1 = new
                    {
                        type = "linear",
                        display = true,
                        position = "right",
                        beginAtZero = true,
                        title = new
                        {
                            display = true,
                            text = "� Transacciones",
                            font = new 
                            { 
                                size = 15, 
                                weight = "600",
                                family = "'Segoe UI', sans-serif"
                            },
                            color = "#4facfe"
                        },
                        grid = new
                        {
                            drawOnChartArea = false
                        },
                        ticks = new
                        {
                            font = new { size = 12, weight = "500" },
                            color = "#4facfe",
                            padding = 8
                        },
                        border = new
                        {
                            display = true,
                            color = "#4facfe",
                            width = 2
                        }
                    },
                    y2 = new
                    {
                        type = "linear",
                        display = false,
                        position = "right",
                        beginAtZero = true
                    }
                },
                elements = new
                {
                    point = new
                    {
                        hoverBorderWidth = 4,
                        hoverRadius = 10
                    },
                    line = new
                    {
                        borderCapStyle = "round",
                        borderJoinStyle = "round"
                    }
                },
                animation = new
                {
                    duration = 2500,
                    easing = "easeInOutQuart",
                    animateRotate = true,
                    animateScale = true
                }
            },
            // Datos adicionales para tooltips enriquecidos y configuraciones avanzadas
            tooltipData = dailyData.Select(d => new {
                date = ((dynamic)d).date,
                dateLabel = ((dynamic)d).dateLabel,
                dayName = ((dynamic)d).dayName,
                revenue = ((dynamic)d).revenue,
                transactions = ((dynamic)d).transactions,
                items = ((dynamic)d).items,
                avgTicket = ((dynamic)d).avgTicket
            }).ToArray(),
            chartConfig = new
            {
                responsive = true,
                maintainAspectRatio = false,
                devicePixelRatio = 2, // Para mejor calidad en pantallas HD
                plugins = new[] { "tooltip", "legend", "title" },
                theme = "modern-gradient",
                preferredSize = new { width = 800, height = 400 }
            }
        };
    }

    /// <summary>
    /// Datos para gráfico de productos más vendidos mejorado (Chart.js Bar Chart)
    /// </summary>
    private object GetTopProductsChart(IEnumerable<(int productId, string name, string sku, decimal revenue, int unitsSold, decimal margin, int currentStock)> productAnalytics)
    {
        var topProducts = productAnalytics
            .OrderByDescending(p => p.unitsSold)
            .Take(10)
            .ToList();

        // Generar colores degradados basados en performance
        var backgroundColors = topProducts.Select((product, index) => 
        {
            // Gradiente de colores de mejor a peor performance
            var hue = 120 - (index * 15); // Verde a amarillo/rojo
            return $"hsla({Math.Max(hue, 0)}, 70%, 60%, 0.8)";
        }).ToArray();

        var borderColors = topProducts.Select((product, index) => 
        {
            var hue = 120 - (index * 15);
            return $"hsla({Math.Max(hue, 0)}, 70%, 50%, 1)";
        }).ToArray();

        return new
        {
            type = "bar",
            data = new
            {
                labels = topProducts.Select(p => p.name.Length > 15 ? p.name.Substring(0, 15) + "..." : p.name).ToArray(),
                datasets = new object[]
                {
                    new
                    {
                        label = "🏆 Unidades Vendidas",
                        data = topProducts.Select(p => p.unitsSold).ToArray(),
                        backgroundColor = backgroundColors,
                        borderColor = borderColors,
                        borderWidth = 2,
                        borderRadius = 8,
                        borderSkipped = false,
                        hoverBackgroundColor = backgroundColors.Select(c => c.Replace("0.8", "0.9")).ToArray(),
                        hoverBorderWidth = 3
                    }
                }
            },
            options = new
            {
                responsive = true,
                maintainAspectRatio = false,
                indexAxis = "y", // Barras horizontales para mejor legibilidad
                plugins = new
                {
                    title = new
                    {
                        display = true,
                        text = "🏆 Top Productos Más Vendidos",
                        font = new { size = 18, weight = "bold" },
                        color = "#1F2937",
                        padding = 20
                    },
                    legend = new
                    {
                        display = false // No necesario para un solo dataset
                    },
                    tooltip = new
                    {
                        backgroundColor = "rgba(0, 0, 0, 0.8)",
                        titleColor = "#ffffff",
                        bodyColor = "#ffffff",
                        borderColor = "#374151",
                        borderWidth = 1,
                        cornerRadius = 8,
                        callbacks = new
                        {
                            title = new[] { "function(context) { return context[0].label; }" },
                            label = new[] { 
                                @"function(context) { 
                                    const product = context.label;
                                    const units = context.formattedValue;
                                    return ['🛍️ Unidades: ' + units, '📊 Ranking: #' + (context.dataIndex + 1)];
                                }" 
                            }
                        }
                    }
                },
                scales = new
                {
                    x = new
                    {
                        beginAtZero = true,
                        title = new
                        {
                            display = true,
                            text = "🔢 Unidades Vendidas",
                            font = new { size = 14, weight = "bold" },
                            color = "#6B7280"
                        },
                        grid = new
                        {
                            display = true,
                            color = "rgba(0, 0, 0, 0.05)"
                        },
                        ticks = new
                        {
                            font = new { size = 11 },
                            color = "#6B7280"
                        }
                    },
                    y = new
                    {
                        title = new
                        {
                            display = true,
                            text = "🛍️ Productos",
                            font = new { size = 14, weight = "bold" },
                            color = "#6B7280"
                        },
                        grid = new
                        {
                            display = false
                        },
                        ticks = new
                        {
                            font = new { size = 12, weight = "bold" },
                            color = "#374151"
                        }
                    }
                },
                animation = new
                {
                    duration = 2000,
                    easing = "easeOutQuart"
                }
            }
        };
    }

    /// <summary>
    /// Datos para gráfico de distribución de márgenes (Chart.js Doughnut Chart)
    /// </summary>
    private object GetMarginDistributionChart(IEnumerable<(int productId, string name, string sku, decimal revenue, int unitsSold, decimal margin, int currentStock)> productAnalytics)
    {
        var marginRanges = new[]
        {
            new { label = "Bajo (0-20%)", min = 0m, max = 20m, color = "rgba(255, 99, 132, 0.8)" },
            new { label = "Medio (20-40%)", min = 20m, max = 40m, color = "rgba(255, 205, 86, 0.8)" },
            new { label = "Alto (40-60%)", min = 40m, max = 60m, color = "rgba(75, 192, 192, 0.8)" },
            new { label = "Excelente (60%+)", min = 60m, max = 100m, color = "rgba(153, 102, 255, 0.8)" }
        };

        var distribution = marginRanges.Select(range => new
        {
            label = range.label,
            count = productAnalytics.Count(p => p.margin >= range.min && p.margin < range.max),
            color = range.color
        }).ToList();

        return new
        {
            type = "doughnut",
            data = new
            {
                labels = distribution.Select(d => d.label).ToArray(),
                datasets = new object[]
                {
                    new
                    {
                        data = distribution.Select(d => d.count).ToArray(),
                        backgroundColor = distribution.Select(d => d.color).ToArray(),
                        borderWidth = 2
                    }
                }
            },
            options = new
            {
                responsive = true,
                plugins = new
                {
                    legend = new { position = "right" },
                    title = new { display = true, text = "Distribución de Márgenes por Producto" }
                }
            }
        };
    }

    /// <summary>
    /// Datos para gráfico de ventas por hora (Chart.js Radar Chart)
    /// </summary>
    private object GetHourlyDistributionChart(IEnumerable<dynamic> salesData)
    {
        var hourlyStats = new List<object>();
        
        for (int hour = 0; hour < 24; hour++)
        {
            var hourData = salesData.Where(sd => sd.Date.Hour == hour).ToList();
            var revenue = hourData.Sum(sd => sd.Price * int.Parse(sd.Amount.ToString()));
            var transactions = hourData.Count();
            
            hourlyStats.Add(new
            {
                hour = hour,
                label = $"{hour:00}:00",
                revenue = Math.Round((decimal)revenue, 2),
                transactions = transactions
            });
        }

        return new
        {
            type = "radar",
            data = new
            {
                labels = hourlyStats.Select(h => ((dynamic)h).label).ToArray(),
                datasets = new object[]
                {
                    new
                    {
                        label = "Ingresos por Hora",
                        data = hourlyStats.Select(h => ((dynamic)h).revenue).ToArray(),
                        backgroundColor = "rgba(255, 99, 132, 0.2)",
                        borderColor = "rgba(255, 99, 132, 1)",
                        pointBackgroundColor = "rgba(255, 99, 132, 1)"
                    }
                }
            },
            options = new
            {
                responsive = true,
                plugins = new
                {
                    title = new { display = true, text = "Distribución de Ventas por Hora del Día" }
                }
            }
        };
    }

    /// <summary>
    /// Datos para gráfico de ventas por día de semana (Chart.js Polar Area Chart)
    /// </summary>
    private object GetWeeklyDistributionChart(IEnumerable<dynamic> salesData)
    {
        var daysOfWeek = new[] { "Lunes", "Martes", "Miércoles", "Jueves", "Viernes", "Sábado", "Domingo" };
        var weeklyStats = new List<object>();

        for (int day = 1; day <= 7; day++)
        {
            var dayOfWeek = (DayOfWeek)(day % 7);
            var dayData = salesData.Where(sd => sd.Date.DayOfWeek == dayOfWeek).ToList();
            var revenue = dayData.Sum(sd => sd.Price * int.Parse(sd.Amount.ToString()));
            
            weeklyStats.Add(new
            {
                day = daysOfWeek[day - 1],
                revenue = Math.Round((decimal)revenue, 2)
            });
        }

        return new
        {
            type = "polarArea",
            data = new
            {
                labels = weeklyStats.Select(w => ((dynamic)w).day).ToArray(),
                datasets = new object[]
                {
                    new
                    {
                        label = "Ingresos por Día",
                        data = weeklyStats.Select(w => ((dynamic)w).revenue).ToArray(),
                        backgroundColor = new[]
                        {
                            "rgba(255, 99, 132, 0.8)",
                            "rgba(54, 162, 235, 0.8)",
                            "rgba(255, 205, 86, 0.8)",
                            "rgba(75, 192, 192, 0.8)",
                            "rgba(153, 102, 255, 0.8)",
                            "rgba(255, 159, 64, 0.8)",
                            "rgba(201, 203, 207, 0.8)"
                        }
                    }
                }
            },
            options = new
            {
                responsive = true,
                plugins = new
                {
                    title = new { display = true, text = "Distribución de Ventas por Día de la Semana" }
                }
            }
        };
    }

    /// <summary>
    /// Datos para gráfico de evolución mensual (Chart.js Line Chart)
    /// </summary>
    private object GetMonthlyTrendChart(IEnumerable<dynamic> salesData)
    {
        var monthlyData = salesData
            .GroupBy(sd => new { sd.Date.Year, sd.Date.Month })
            .Select(g => new
            {
                year = g.Key.Year,
                month = g.Key.Month,
                monthLabel = new DateTime(g.Key.Year, g.Key.Month, 1).ToString("MMM yyyy"),
                revenue = Math.Round((decimal)g.Sum(sd => sd.Price * int.Parse(sd.Amount.ToString())), 2),
                transactions = g.GroupBy(sd => new { sd.Date, sd.ProductId }).Count(),
                items = g.Sum(sd => int.Parse(sd.Amount.ToString()))
            })
            .OrderBy(m => m.year)
            .ThenBy(m => m.month)
            .Take(12)
            .ToList();

        return new
        {
            type = "line",
            data = new
            {
                labels = monthlyData.Select(m => m.monthLabel).ToArray(),
                datasets = new object[]
                {
                    new
                    {
                        label = "Ingresos Mensuales",
                        data = monthlyData.Select(m => m.revenue).ToArray(),
                        borderColor = "rgb(75, 192, 192)",
                        backgroundColor = "rgba(75, 192, 192, 0.2)",
                        tension = 0.4
                    }
                }
            },
            options = new
            {
                responsive = true,
                plugins = new
                {
                    title = new { display = true, text = "Evolución de Ingresos Mensuales" }
                },
                scales = new
                {
                    y = new { beginAtZero = true, title = new { display = true, text = "Ingresos ($)" } }
                }
            }
        };
    }

    /// <summary>
    /// Datos para gráfico de inventario vs ventas (Chart.js Scatter Chart)
    /// </summary>
    private object GetInventoryVsSalesChart(IEnumerable<(int productId, string name, string sku, decimal revenue, int unitsSold, decimal margin, int currentStock)> productAnalytics)
    {
        var scatterData = productAnalytics
            .Where(p => p.unitsSold > 0)
            .Select(p => new
            {
                x = p.currentStock,
                y = p.unitsSold,
                label = p.name,
                margin = p.margin
            })
            .ToList();

        return new
        {
            type = "scatter",
            data = new
            {
                datasets = new object[]
                {
                    new
                    {
                        label = "Productos",
                        data = scatterData.Select(s => new { x = s.x, y = s.y }).ToArray(),
                        backgroundColor = "rgba(255, 99, 132, 0.6)",
                        borderColor = "rgba(255, 99, 132, 1)"
                    }
                }
            },
            options = new
            {
                responsive = true,
                plugins = new
                {
                    title = new { display = true, text = "Inventario vs Ventas por Producto" },
                    legend = new { display = false }
                },
                scales = new
                {
                    x = new { title = new { display = true, text = "Stock Actual" } },
                    y = new { title = new { display = true, text = "Unidades Vendidas" } }
                }
            }
        };
    }

    /// <summary>
    /// Datos para gráficos tipo gauge/indicadores de KPIs
    /// </summary>
    private object GetKPIGaugesChart(decimal grossMargin, decimal inventoryTurnover, decimal returnOnInventory)
    {
        return new
        {
            grossMarginGauge = new
            {
                type = "gauge",
                value = Math.Round(grossMargin, 1),
                min = 0,
                max = 100,
                title = "Margen Bruto (%)",
                color = grossMargin >= 30 ? "green" : grossMargin >= 15 ? "orange" : "red",
                ranges = new[]
                {
                    new { from = 0, to = 15, color = "red" },
                    new { from = 15, to = 30, color = "orange" },
                    new { from = 30, to = 100, color = "green" }
                }
            },
            inventoryTurnoverGauge = new
            {
                type = "gauge",
                value = Math.Round(inventoryTurnover, 1),
                min = 0,
                max = 12,
                title = "Rotación de Inventario",
                color = inventoryTurnover >= 4 ? "green" : inventoryTurnover >= 2 ? "orange" : "red",
                ranges = new[]
                {
                    new { from = 0, to = 2, color = "red" },
                    new { from = 2, to = 4, color = "orange" },
                    new { from = 4, to = 12, color = "green" }
                }
            },
            roiGauge = new
            {
                type = "gauge",
                value = Math.Round(returnOnInventory, 1),
                min = 0,
                max = 100,
                title = "ROI Inventario (%)",
                color = returnOnInventory >= 25 ? "green" : returnOnInventory >= 10 ? "orange" : "red",
                ranges = new[]
                {
                    new { from = 0, to = 10, color = "red" },
                    new { from = 10, to = 25, color = "orange" },
                    new { from = 25, to = 100, color = "green" }
                }
            }
        };
    }

    /// <summary>
    /// Datos para gráfico de comparación de crecimiento
    /// </summary>
    private object GetGrowthComparisonChart(
        (decimal revenue, int transactions, int items) todaySales,
        (decimal revenue, int transactions, int items) yesterdaySales,
        (decimal revenue, int transactions, int items) monthSales,
        (decimal revenue, int transactions, int items) lastMonthSales)
    {
        return new
        {
            type = "bar",
            data = new
            {
                labels = new[] { "Hoy", "Ayer", "Este Mes", "Mes Anterior" },
                datasets = new object[]
                {
                    new
                    {
                        label = "Ingresos",
                        data = new[] { todaySales.revenue, yesterdaySales.revenue, monthSales.revenue, lastMonthSales.revenue },
                        backgroundColor = new[]
                        {
                            "rgba(75, 192, 192, 0.8)",
                            "rgba(54, 162, 235, 0.8)",
                            "rgba(255, 205, 86, 0.8)",
                            "rgba(153, 102, 255, 0.8)"
                        }
                    }
                }
            },
            options = new
            {
                responsive = true,
                plugins = new
                {
                    title = new { display = true, text = "Comparación de Ingresos por Período" }
                },
                scales = new
                {
                    y = new { beginAtZero = true, title = new { display = true, text = "Ingresos ($)" } }
                }
            }
        };
    }

    #endregion

    #endregion
}
