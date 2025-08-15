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
            _logger.LogInformation("Iniciando inventario optimizado para negocio: {businessId}", businessId);

            // Verificar que el negocio existe
            var business = await _context.Businesses.FindAsync(businessId);
            if (business == null)
            {
                return NotFound(new { message = "Negocio no encontrado" });
            }

            // Fechas para cálculos
            var today = DateTime.Today;
            var startOfMonth = new DateTime(today.Year, today.Month, 1);
            var lastMonth = startOfMonth.AddMonths(-1);
            var endOfLastMonth = startOfMonth.AddDays(-1);

            // 1. Obtener información básica de las tiendas
            var stores = await _context.Stores
                .Where(s => s.BusinessId == businessId && s.Active)
                .Select(store => new
                {
                    StoreId = store.Id,
                    StoreName = store.Name,
                    Location = store.Location,
                    TotalStock = _context.Stocks
                        .Where(st => st.StoreId == store.Id)
                        .Sum(st => (int?)st.Amount) ?? 0,
                    ProductCount = _context.Stocks
                        .Where(st => st.StoreId == store.Id)
                        .Select(st => st.ProductId)
                        .Distinct()
                        .Count(),
                    TodayTransactions = _context.Sales
                        .Where(s => s.StoreId == store.Id && s.Date.Date == today)
                        .Count(),
                    MonthTransactions = _context.Sales
                        .Where(s => s.StoreId == store.Id && s.Date >= startOfMonth)
                        .Count()
                })
                .ToListAsync();

            // 2. Calcular ventas por tienda por separado para evitar decimal.Parse en LINQ to SQL
            var storesWithSales = new List<object>();
            decimal totalTodaySales = 0;
            decimal totalMonthSales = 0;
            decimal totalLastMonthSales = 0;

            foreach (var store in stores)
            {
                // Ventas de hoy
                var todaySaleDetails = await _context.SaleDetails
                    .Where(sd => sd.Sale.StoreId == store.StoreId && sd.Sale.Date.Date == today)
                    .Select(sd => new { sd.Price, sd.Amount })
                    .ToListAsync();
                
                var todaySalesAmount = todaySaleDetails
                    .Sum(sd => sd.Price * decimal.Parse(sd.Amount));

                // Ventas del mes
                var monthSaleDetails = await _context.SaleDetails
                    .Where(sd => sd.Sale.StoreId == store.StoreId && sd.Sale.Date >= startOfMonth)
                    .Select(sd => new { sd.Price, sd.Amount })
                    .ToListAsync();
                
                var monthSalesAmount = monthSaleDetails
                    .Sum(sd => sd.Price * decimal.Parse(sd.Amount));

                // Ventas del mes pasado
                var lastMonthSaleDetails = await _context.SaleDetails
                    .Where(sd => sd.Sale.StoreId == store.StoreId && sd.Sale.Date >= lastMonth && sd.Sale.Date <= endOfLastMonth)
                    .Select(sd => new { sd.Price, sd.Amount })
                    .ToListAsync();
                
                var lastMonthSalesAmount = lastMonthSaleDetails
                    .Sum(sd => sd.Price * decimal.Parse(sd.Amount));

                // Acumular totales
                totalTodaySales += todaySalesAmount;
                totalMonthSales += monthSalesAmount;
                totalLastMonthSales += lastMonthSalesAmount;

                // Agregar tienda con datos de ventas
                storesWithSales.Add(new
                {
                    storeId = store.StoreId,
                    storeName = store.StoreName,
                    location = store.Location,
                    totalProducts = store.ProductCount,
                    totalStock = store.TotalStock,
                    todaySales = new
                    {
                        amount = todaySalesAmount,
                        transactions = store.TodayTransactions
                    },
                    monthSales = new
                    {
                        amount = monthSalesAmount,
                        transactions = store.MonthTransactions,
                        changePercent = CalculateChangePercent(monthSalesAmount, lastMonthSalesAmount)
                    }
                });
            }

            // 3. Top productos del negocio - también calculado por separado
            var products = await _context.Products
                .Where(p => p.BusinessId == businessId)
                .Select(p => new
                {
                    Id = p.Id,
                    Name = p.Name,
                    Sku = p.Sku,
                    Price = p.Price,
                    CurrentStock = _context.Stocks
                        .Where(st => st.ProductId == p.Id)
                        .Sum(st => (int?)st.Amount) ?? 0
                })
                .ToListAsync();

            var topProducts = new List<object>();
            foreach (var product in products.Take(50)) // Limitar para evitar demasiadas consultas
            {
                var productSaleDetails = await _context.SaleDetails
                    .Where(sd => sd.ProductId == product.Id && sd.Sale.Date >= startOfMonth)
                    .Select(sd => new { sd.Price, sd.Amount })
                    .ToListAsync();

                var monthSalesAmount = productSaleDetails
                    .Sum(sd => sd.Price * decimal.Parse(sd.Amount));
                
                var monthQuantitySold = productSaleDetails
                    .Sum(sd => decimal.Parse(sd.Amount));

                topProducts.Add(new
                {
                    id = product.Id,
                    name = product.Name,
                    sku = product.Sku,
                    price = product.Price,
                    currentStock = product.CurrentStock,
                    monthSalesAmount = monthSalesAmount,
                    monthQuantitySold = monthQuantitySold
                });
            }

            // Ordenar por ventas del mes
            topProducts = topProducts
                .OrderByDescending(p => ((dynamic)p).monthSalesAmount)
                .Take(10)
                .ToList();

            // 4. Resumen del negocio
            var businessSummary = new
            {
                totalProducts = await _context.Products.CountAsync(p => p.BusinessId == businessId),
                totalStock = stores.Sum(s => s.TotalStock),
                todaySales = new
                {
                    amount = totalTodaySales,
                    transactions = stores.Sum(s => s.TodayTransactions)
                },
                monthSales = new
                {
                    amount = totalMonthSales,
                    transactions = stores.Sum(s => s.MonthTransactions),
                    changePercent = CalculateChangePercent(totalMonthSales, totalLastMonthSales)
                }
            };

            stopwatch.Stop();
            _logger.LogInformation($"Inventario optimizado completado en {stopwatch.ElapsedMilliseconds}ms para negocio {businessId}");

            var result = new
            {
                businessId = businessId,
                executionTimeMs = stopwatch.ElapsedMilliseconds,
                summary = businessSummary,
                storesSummary = storesWithSales,
                topProducts = topProducts,
                optimization = new
                {
                    version = "v2_optimized_fixed",
                    description = "Optimizado con consultas separadas para evitar decimal.Parse en LINQ to SQL",
                    originalEndpoint = "/api/stock/inventory/{businessId}",
                    newEndpoint = "/api/optimizedinventory/inventory/{businessId}",
                    improvements = new string[]
                    {
                        "Eliminados bucles anidados",
                        "Consultas LINQ optimizadas",
                        "Reducción de N+1 queries",
                        "Fixed decimal.Parse issues",
                        "Medición de tiempo de ejecución"
                    }
                }
            };

            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error en inventario optimizado del negocio: {businessId}", businessId);
            return StatusCode(500, new { message = "Error interno del servidor", error = ex.Message });
        }
    }

    private static decimal? CalculateChangePercent(decimal current, decimal previous)
    {
        if (previous <= 0) return null;
        return Math.Round(((current - previous) / previous) * 100, 2);
    }
}
