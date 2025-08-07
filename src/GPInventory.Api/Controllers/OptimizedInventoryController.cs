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

            // 1. CONSULTA OPTIMIZADA: Resumen usando LINQ optimizado con una sola consulta
            var storesWithData = await _context.Stores
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
                    TodaySalesAmount = _context.SaleDetails
                        .Where(sd => sd.Sale.StoreId == store.Id && sd.Sale.Date.Date == today)
                        .Sum(sd => (decimal?)(sd.Price * decimal.Parse(sd.Amount))) ?? 0,
                    TodayTransactions = _context.Sales
                        .Where(s => s.StoreId == store.Id && s.Date.Date == today)
                        .Count(),
                    MonthSalesAmount = _context.SaleDetails
                        .Where(sd => sd.Sale.StoreId == store.Id && sd.Sale.Date >= startOfMonth)
                        .Sum(sd => (decimal?)(sd.Price * decimal.Parse(sd.Amount))) ?? 0,
                    MonthTransactions = _context.Sales
                        .Where(s => s.StoreId == store.Id && s.Date >= startOfMonth)
                        .Count(),
                    LastMonthSalesAmount = _context.SaleDetails
                        .Where(sd => sd.Sale.StoreId == store.Id && sd.Sale.Date >= lastMonth && sd.Sale.Date <= endOfLastMonth)
                        .Sum(sd => (decimal?)(sd.Price * decimal.Parse(sd.Amount))) ?? 0
                })
                .ToListAsync();

            // 2. CONSULTA OPTIMIZADA: Top productos del negocio
            var topProducts = await _context.Products
                .Where(p => p.BusinessId == businessId)
                .Select(p => new
                {
                    Id = p.Id,
                    Name = p.Name,
                    Sku = p.Sku,
                    Price = p.Price,
                    CurrentStock = _context.Stocks
                        .Where(st => st.ProductId == p.Id)
                        .Sum(st => (int?)st.Amount) ?? 0,
                    MonthSalesAmount = _context.SaleDetails
                        .Where(sd => sd.ProductId == p.Id && sd.Sale.Date >= startOfMonth)
                        .Sum(sd => (decimal?)(sd.Price * decimal.Parse(sd.Amount))) ?? 0,
                    MonthQuantitySold = _context.SaleDetails
                        .Where(sd => sd.ProductId == p.Id && sd.Sale.Date >= startOfMonth)
                        .Sum(sd => (decimal?)decimal.Parse(sd.Amount)) ?? 0
                })
                .OrderByDescending(p => p.MonthSalesAmount)
                .Take(10)
                .ToListAsync();

            // 3. Agregar datos del negocio
            var businessSummary = new
            {
                totalProducts = await _context.Products.CountAsync(p => p.BusinessId == businessId),
                totalStock = storesWithData.Sum(s => s.TotalStock),
                todaySales = new
                {
                    amount = storesWithData.Sum(s => s.TodaySalesAmount),
                    transactions = storesWithData.Sum(s => s.TodayTransactions)
                },
                monthSales = new
                {
                    amount = storesWithData.Sum(s => s.MonthSalesAmount),
                    transactions = storesWithData.Sum(s => s.MonthTransactions),
                    changePercent = CalculateChangePercent(
                        storesWithData.Sum(s => s.MonthSalesAmount),
                        storesWithData.Sum(s => s.LastMonthSalesAmount)
                    )
                }
            };

            var storesData = storesWithData.Select(store => new
            {
                storeId = store.StoreId,
                storeName = store.StoreName,
                location = store.Location,
                totalProducts = store.ProductCount,
                totalStock = store.TotalStock,
                todaySales = new
                {
                    amount = store.TodaySalesAmount,
                    transactions = store.TodayTransactions
                },
                monthSales = new
                {
                    amount = store.MonthSalesAmount,
                    transactions = store.MonthTransactions,
                    changePercent = CalculateChangePercent(
                        store.MonthSalesAmount,
                        store.LastMonthSalesAmount
                    )
                }
            }).ToList();

            stopwatch.Stop();
            _logger.LogInformation($"Inventario optimizado completado en {stopwatch.ElapsedMilliseconds}ms para negocio {businessId}");

            var result = new
            {
                businessId = businessId,
                executionTimeMs = stopwatch.ElapsedMilliseconds,
                summary = businessSummary,
                storesSummary = storesData,
                topProducts = topProducts.ToList(),
                optimization = new
                {
                    version = "v2_optimized_linq",
                    description = "Optimizado con consultas LINQ agrupadas",
                    originalEndpoint = "/api/stock/inventory/{businessId}",
                    newEndpoint = "/api/optimizedinventory/inventory/{businessId}",
                    improvements = new string[]
                    {
                        "Eliminados bucles anidados",
                        "Consultas LINQ optimizadas",
                        "Reducción de N+1 queries",
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
