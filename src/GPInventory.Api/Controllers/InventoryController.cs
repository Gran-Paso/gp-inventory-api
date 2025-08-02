using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using GPInventory.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Cors;
using GPInventory.Domain.Entities;

namespace GPInventory.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[EnableCors("AllowFrontend")]
public class InventoryController : ControllerBase
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<InventoryController> _logger;

    public InventoryController(ApplicationDbContext context, ILogger<InventoryController> logger)
    {
        _context = context;
        _logger = logger;
    }

    /// <summary>
    /// Obtiene el inventario con stock real calculado por store
    /// </summary>
    /// <param name="businessId">ID del negocio (requerido)</param>
    /// <param name="category">Categoría de productos (opcional)</param>
    /// <param name="search">Búsqueda por nombre (opcional)</param>
    /// <param name="status">Estado del stock (opcional)</param>
    /// <returns>Inventario completo con summary total, por store y productos por store</returns>
    [HttpGet]
    [Authorize]
    [ProducesResponseType(200)]
    [ProducesResponseType(401)]
    [ProducesResponseType(500)]
    public async Task<ActionResult<object>> GetInventory(
        [FromQuery] int? businessId = null,
        [FromQuery] string? category = null,
        [FromQuery] string? search = null,
        [FromQuery] string? status = null)
    {
        try
        {
            if (!businessId.HasValue)
            {
                return BadRequest(new { message = "businessId es requerido" });
            }

            _logger.LogInformation($"Obteniendo inventario por stores para businessId: {businessId}");

            // Obtener productos del negocio
            var productsQuery = _context.Products
                .Include(p => p.ProductType)
                .Include(p => p.Business)
                .Where(p => p.BusinessId == businessId.Value);

            // Aplicar filtros
            if (!string.IsNullOrEmpty(category))
            {
                productsQuery = productsQuery.Where(p => p.ProductType.Name.Contains(category));
            }

            if (!string.IsNullOrEmpty(search))
            {
                productsQuery = productsQuery.Where(p => p.Name.Contains(search) || 
                                                       (p.Sku != null && p.Sku.Contains(search)));
            }

            var products = await productsQuery.ToListAsync();

            // Obtener todos los stores activos del negocio
            var stores = await _context.Stores
                .Where(s => s.BusinessId == businessId.Value && s.Active)
                .Include(s => s.Business)
                .ToListAsync();

            // Si no hay stores, crear uno por defecto
            if (!stores.Any())
            {
                var defaultStore = await GetOrCreateDefaultStore(businessId.Value);
                stores = await _context.Stores
                    .Where(s => s.Id == defaultStore.Id)
                    .Include(s => s.Business)
                    .ToListAsync();
            }

            var storeIds = stores.Select(s => s.Id).ToList();

            // Obtener todos los stocks para optimizar consultas
            var allStocks = await _context.Stocks
                .Where(s => storeIds.Contains(s.StoreId))
                .ToListAsync();

            // Obtener todas las ventas del día y del mes para optimizar consultas
            var today = DateTime.Today;
            var startOfMonth = new DateTime(today.Year, today.Month, 1);
            
            var allSales = await _context.Sales
                .Include(s => s.SaleDetails)
                .Where(s => storeIds.Contains(s.StoreId))
                .ToListAsync();

            // Calcular summary total
            var totalProducts = products.Count;
            var totalStock = allStocks.GroupBy(s => s.ProductId).Sum(g => g.Sum(s => s.Amount));
            
            var todaySales = allSales.Where(s => s.Date.Date == today).ToList();
            var monthSales = allSales.Where(s => s.Date >= startOfMonth).ToList();

            var totalSummary = new
            {
                totalProducts = totalProducts,
                totalStock = totalStock,
                todaySales = new
                {
                    amount = todaySales.Sum(s => s.Total),
                    transactions = todaySales.Count,
                    changePercent = (double?)null // Necesitaríamos datos históricos para calcular esto
                },
                monthSales = new
                {
                    amount = monthSales.Sum(s => s.Total),
                    transactions = monthSales.Count,
                    changePercent = -100.0 // Placeholder, necesitaríamos datos del mes anterior
                }
            };

            // Calcular summary por store
            var storesSummary = new List<object>();
            var storesData = new List<object>();

            foreach (var store in stores)
            {
                var storeStocks = allStocks.Where(s => s.StoreId == store.Id).ToList();
                var storeSales = allSales.Where(s => s.StoreId == store.Id).ToList();
                var storeTodaySales = storeSales.Where(s => s.Date.Date == today).ToList();
                var storeMonthSales = storeSales.Where(s => s.Date >= startOfMonth).ToList();

                var storeStock = storeStocks.GroupBy(s => s.ProductId).Sum(g => g.Sum(s => s.Amount));

                storesSummary.Add(new
                {
                    storeId = store.Id,
                    storeName = store.Name,
                    location = store.Location,
                    totalProducts = products.Count, // Todos los productos están disponibles en todas las tiendas
                    totalStock = storeStock,
                    todaySales = new
                    {
                        amount = storeTodaySales.Sum(s => s.Total),
                        transactions = storeTodaySales.Count,
                        changePercent = (double?)null
                    },
                    monthSales = new
                    {
                        amount = storeMonthSales.Sum(s => s.Total),
                        transactions = storeMonthSales.Count,
                        changePercent = -100.0
                    }
                });

                // Productos por store
                var storeProducts = new List<object>();

                foreach (var product in products)
                {
                    var productStocks = storeStocks.Where(s => s.ProductId == product.Id).ToList();
                    var currentStock = productStocks.Sum(s => s.Amount);

                    // Calcular costos promedio
                    var stockMovements = productStocks.Where(s => s.Amount > 0).ToList();
                    decimal averagePrice = product.Price;
                    decimal averageCost = product.Cost;

                    if (stockMovements.Any())
                    {
                        var movementsWithCost = stockMovements.Where(s => s.Cost.HasValue && s.Cost.Value > 0).ToList();
                        
                        if (movementsWithCost.Any())
                        {
                            var totalCostValue = movementsWithCost.Sum(s => (decimal)s.Amount * (decimal)s.Cost!.Value);
                            var totalQuantity = movementsWithCost.Sum(s => (decimal)s.Amount);
                            
                            if (totalQuantity > 0)
                            {
                                averageCost = totalCostValue / totalQuantity;
                            }
                        }
                    }

                    // Determinar estado del stock
                    var stockStatus = GetStockStatus(currentStock, 0);

                    // Aplicar filtro de estado si se especifica
                    if (!string.IsNullOrEmpty(status) && !IsStatusMatch(stockStatus, status))
                    {
                        continue;
                    }

                    // Calcular ventas del producto en esta tienda
                    var productSales = storeSales.Where(s => s.SaleDetails.Any(sd => sd.ProductId == product.Id)).ToList();
                    var productTodaySales = productSales.Where(s => s.Date.Date == today).ToList();
                    var productMonthSales = productSales.Where(s => s.Date >= startOfMonth).ToList();

                    var todayQuantity = productTodaySales.SelectMany(s => s.SaleDetails)
                        .Where(sd => sd.ProductId == product.Id)
                        .Sum(sd => int.Parse(sd.Amount));
                    
                    var monthQuantity = productMonthSales.SelectMany(s => s.SaleDetails)
                        .Where(sd => sd.ProductId == product.Id)
                        .Sum(sd => int.Parse(sd.Amount));

                    storeProducts.Add(new
                    {
                        id = product.Id,
                        name = product.Name,
                        sku = product.Sku,
                        price = product.Price,
                        cost = product.Cost,
                        image = product.Image,
                        productType = product.ProductType != null ? new { id = product.ProductType.Id, name = product.ProductType.Name } : null,
                        business = new { id = product.Business.Id, companyName = product.Business.CompanyName },
                        currentStock = currentStock,
                        averageCost = Math.Round(averageCost, 2),
                        averagePrice = Math.Round(averagePrice, 2),
                        status = stockStatus,
                        totalMovements = stockMovements.Count,
                        lastMovementDate = stockMovements.OrderByDescending(s => s.Date).FirstOrDefault()?.Date,
                        todaySales = new
                        {
                            amount = productTodaySales.SelectMany(s => s.SaleDetails)
                                .Where(sd => sd.ProductId == product.Id)
                                .Sum(sd => sd.Price * int.Parse(sd.Amount)),
                            quantity = todayQuantity,
                            changePercent = (double?)null
                        },
                        monthSales = new
                        {
                            amount = productMonthSales.SelectMany(s => s.SaleDetails)
                                .Where(sd => sd.ProductId == product.Id)
                                .Sum(sd => sd.Price * int.Parse(sd.Amount)),
                            quantity = monthQuantity,
                            changePercent = -100.0
                        }
                    });
                }

                storesData.Add(new
                {
                    storeId = store.Id,
                    storeName = store.Name,
                    location = store.Location,
                    products = storeProducts
                });
            }

            var result = new
            {
                businessId = businessId.Value,
                summary = totalSummary,
                storesSummary = storesSummary,
                stores = storesData
            };

            _logger.LogInformation($"Inventario obtenido para {stores.Count} stores con {products.Count} productos");
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al obtener inventario");
            return StatusCode(500, new { message = "Error interno del servidor" });
        }
    }

    /// <summary>
    /// Obtiene el resumen del inventario
    /// </summary>
    /// <param name="businessId">ID del negocio (opcional)</param>
    /// <returns>Resumen del inventario</returns>
    [HttpGet("summary")]
    [Authorize]
    [ProducesResponseType(200)]
    [ProducesResponseType(401)]
    [ProducesResponseType(500)]
    public async Task<ActionResult<object>> GetInventorySummary([FromQuery] int? businessId = null)
    {
        try
        {
            _logger.LogInformation("Obteniendo resumen del inventario");

            var productsQuery = _context.Products.AsQueryable();

            if (businessId.HasValue)
            {
                productsQuery = productsQuery.Where(p => p.BusinessId == businessId.Value);
            }

            var products = await productsQuery.ToListAsync();

            // Obtener todos los stores del negocio si se especifica businessId
            List<int> relevantStoreIds = new List<int>();
            if (businessId.HasValue)
            {
                relevantStoreIds = await _context.Stores
                    .Where(s => s.BusinessId == businessId.Value && s.Active)
                    .Select(s => s.Id)
                    .ToListAsync();

                // Si no hay stores, crear uno por defecto
                if (!relevantStoreIds.Any())
                {
                    var defaultStore = await GetOrCreateDefaultStore(businessId.Value);
                    relevantStoreIds.Add(defaultStore.Id);
                }
            }

            int inStock = 0, lowStock = 0, outOfStock = 0;
            long totalValue = 0;

            foreach (var product in products)
            {
                List<int> businessStores;
                
                if (businessId.HasValue && relevantStoreIds.Any())
                {
                    // Usar los stores ya obtenidos para el businessId filtrado
                    businessStores = relevantStoreIds;
                }
                else
                {
                    // Obtener todos los stores del negocio del producto
                    businessStores = await _context.Stores
                        .Where(s => s.BusinessId == product.BusinessId && s.Active)
                        .Select(s => s.Id)
                        .ToListAsync();

                    // Si no hay stores, crear uno por defecto
                    if (!businessStores.Any())
                    {
                        var defaultStore = await GetOrCreateDefaultStore(product.BusinessId);
                        businessStores.Add(defaultStore.Id);
                    }
                }
                
                // Calcular stock actual sumando movimientos de TODOS los stores del negocio
                var currentStock = await _context.Stocks
                    .Where(s => s.ProductId == product.Id && businessStores.Contains(s.StoreId))
                    .SumAsync(s => s.Amount);

                var status = GetStockStatus(currentStock, 0);
                switch (status)
                {
                    case "in-stock":
                        inStock++;
                        break;
                    case "low-stock":
                        lowStock++;
                        break;
                    case "out-of-stock":
                        outOfStock++;
                        break;
                }

                // Calcular valor total
                totalValue += product.Price * currentStock;
            }

            var result = new
            {
                totalProducts = products.Count,
                inStock = inStock,
                lowStock = lowStock,
                outOfStock = outOfStock,
                totalValue = totalValue
            };

            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al obtener resumen del inventario");
            return StatusCode(500, new { message = "Error interno del servidor" });
        }
    }

    private string GetStockStatus(int currentStock, int minStock)
    {
        if (currentStock == 0) return "out-of-stock";
        if (currentStock < minStock) return "low-stock";
        return "in-stock";
    }

    private bool IsStatusMatch(string actualStatus, string requestedStatus)
    {
        return requestedStatus.ToLower() switch
        {
            "in-stock" or "in_stock" => actualStatus == "in-stock",
            "low-stock" or "low_stock" => actualStatus == "low-stock",
            "out-of-stock" or "out_stock" => actualStatus == "out-of-stock",
            _ => true
        };
    }

    /// <summary>
    /// Obtiene o crea una tienda por defecto para un negocio
    /// </summary>
    /// <param name="businessId">ID del negocio</param>
    /// <returns>Store por defecto</returns>
    private async Task<Store> GetOrCreateDefaultStore(int businessId)
    {
        var store = await _context.Stores.FirstOrDefaultAsync(s => s.BusinessId == businessId);
        
        if (store == null)
        {
            store = new Store
            {
                Name = "Tienda Principal",
                BusinessId = businessId,
                Active = true
            };
            
            _context.Stores.Add(store);
            await _context.SaveChangesAsync();
        }
        
        return store;
    }
}
