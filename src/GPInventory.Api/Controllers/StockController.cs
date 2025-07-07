using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using GPInventory.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Cors;

namespace GPInventory.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[EnableCors("AllowFrontend")]
public class StockController : ControllerBase
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<StockController> _logger;

    public StockController(ApplicationDbContext context, ILogger<StockController> logger)
    {
        _context = context;
        _logger = logger;
    }

    /// <summary>
    /// Obtiene todos los movimientos de stock con filtros opcionales
    /// </summary>
    /// <param name="productId">ID del producto (opcional)</param>
    /// <param name="businessId">ID del negocio (opcional)</param>
    /// <param name="flowTypeId">ID del tipo de flujo (opcional)</param>
    /// <param name="dateFrom">Fecha desde (opcional)</param>
    /// <param name="dateTo">Fecha hasta (opcional)</param>
    /// <returns>Lista de movimientos de stock</returns>
    [HttpGet]
    [Authorize]
    [ProducesResponseType(200)]
    [ProducesResponseType(401)]
    [ProducesResponseType(500)]
    public async Task<ActionResult<IEnumerable<object>>> GetStockMovements(
        [FromQuery] int? productId = null,
        [FromQuery] int? businessId = null,
        [FromQuery] int? flowTypeId = null,
        [FromQuery] DateTime? dateFrom = null,
        [FromQuery] DateTime? dateTo = null)
    {
        try
        {
            _logger.LogInformation("Obteniendo movimientos de stock con filtros");

            var query = _context.Stocks
                .Include(s => s.Product)
                .Include(s => s.FlowType)
                .Include(s => s.Provider)
                .AsQueryable();

            if (productId.HasValue)
            {
                query = query.Where(s => s.ProductId == productId.Value);
            }

            if (businessId.HasValue)
            {
                query = query.Where(s => s.Product.BusinessId == businessId.Value);
            }

            if (flowTypeId.HasValue)
            {
                query = query.Where(s => s.FlowTypeId == flowTypeId.Value);
            }

            if (dateFrom.HasValue)
            {
                query = query.Where(s => s.Date >= dateFrom.Value);
            }

            if (dateTo.HasValue)
            {
                query = query.Where(s => s.Date <= dateTo.Value);
            }

            var stockMovements = await query
                .OrderByDescending(s => s.Date)
                .Select(s => new
                {
                    id = s.Id,
                    productId = s.ProductId,
                    productName = s.Product.Name,
                    date = s.Date,
                    flowType = new { id = s.FlowType.Id, name = s.FlowType.Name },
                    amount = s.Amount,
                    cost = s.Cost,
                    provider = s.Provider != null ? new { id = s.Provider.Id, name = s.Provider.Name } : null,
                    notes = s.Notes
                })
                .ToListAsync();

            _logger.LogInformation($"Se encontraron {stockMovements.Count} movimientos de stock");
            return Ok(stockMovements);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al obtener movimientos de stock");
            return StatusCode(500, new { message = "Error interno del servidor" });
        }
    }

    /// <summary>
    /// Obtiene el stock actual de un producto específico
    /// </summary>
    /// <param name="productId">ID del producto</param>
    /// <returns>Stock actual del producto</returns>
    [HttpGet("product/{productId}/current")]
    [Authorize]
    [ProducesResponseType(200)]
    [ProducesResponseType(401)]
    [ProducesResponseType(404)]
    [ProducesResponseType(500)]
    public async Task<ActionResult<object>> GetCurrentStock(int productId)
    {
        try
        {
            _logger.LogInformation("Obteniendo stock actual para producto: {productId}", productId);

            var product = await _context.Products.FindAsync(productId);
            if (product == null)
            {
                return NotFound(new { message = "Producto no encontrado" });
            }

            // Calcular stock actual sumando todos los movimientos
            var currentStock = await _context.Stocks
                .Where(s => s.ProductId == productId)
                .SumAsync(s => s.Amount);

            var result = new
            {
                productId = productId,
                productName = product.Name,
                currentStock = currentStock,
                calculatedAt = DateTime.UtcNow
            };

            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al obtener stock actual para producto: {productId}", productId);
            return StatusCode(500, new { message = "Error interno del servidor" });
        }
    }

    /// <summary>
    /// Crea un nuevo movimiento de stock (entrada o salida)
    /// </summary>
    /// <param name="request">Datos del movimiento de stock</param>
    /// <returns>Movimiento de stock creado</returns>
    [HttpPost]
    [Authorize]
    [ProducesResponseType(201)]
    [ProducesResponseType(400)]
    [ProducesResponseType(401)]
    [ProducesResponseType(500)]
    public async Task<ActionResult<object>> CreateStockMovement([FromBody] CreateStockMovementRequest request)
    {
        try
        {
            // Validaciones básicas
            if (request.ProductId <= 0)
            {
                return BadRequest(new { message = "ID de producto inválido" });
            }

            if (request.Amount == 0)
            {
                return BadRequest(new { message = "La cantidad no puede ser cero" });
            }

            if (request.FlowTypeId <= 0)
            {
                return BadRequest(new { message = "Tipo de flujo requerido" });
            }

            _logger.LogInformation("Creando movimiento de stock para producto: {productId}", request.ProductId);

            // Verificar que el producto existe
            var product = await _context.Products.FindAsync(request.ProductId);
            if (product == null)
            {
                return BadRequest(new { message = "El producto especificado no existe" });
            }

            // Verificar que el tipo de flujo existe
            var flowType = await _context.FlowTypes.FindAsync(request.FlowTypeId);
            if (flowType == null)
            {
                return BadRequest(new { message = "El tipo de flujo especificado no existe" });
            }

            // Manejar proveedor
            int? providerId = null;
            if (!string.IsNullOrEmpty(request.ProviderName))
            {
                providerId = await GetOrCreateProvider(request.ProviderName.Trim(), product.BusinessId);
            }

            // Crear movimiento de stock
            var stockMovement = new GPInventory.Domain.Entities.Stock
            {
                ProductId = request.ProductId,
                Date = request.Date ?? DateTime.UtcNow,
                FlowTypeId = request.FlowTypeId,
                Amount = request.Amount,
                Cost = request.Cost,
                ProviderId = providerId,
                Notes = request.Notes?.Trim()
            };

            _context.Stocks.Add(stockMovement);
            await _context.SaveChangesAsync();

            // Obtener el movimiento creado con sus relaciones
            var createdMovement = await _context.Stocks
                .Include(s => s.Product)
                .Include(s => s.FlowType)
                .Include(s => s.Provider)
                .Where(s => s.Id == stockMovement.Id)
                .Select(s => new
                {
                    id = s.Id,
                    productId = s.ProductId,
                    productName = s.Product.Name,
                    date = s.Date,
                    flowType = new { id = s.FlowType.Id, name = s.FlowType.Name },
                    amount = s.Amount,
                    cost = s.Cost,
                    provider = s.Provider != null ? new { id = s.Provider.Id, name = s.Provider.Name } : null,
                    notes = s.Notes
                })
                .FirstOrDefaultAsync();

            _logger.LogInformation("Movimiento de stock creado exitosamente: {stockId}", stockMovement.Id);
            return CreatedAtAction(nameof(GetStockMovements), createdMovement);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al crear movimiento de stock");
            return StatusCode(500, new { message = "Error interno del servidor" });
        }
    }

    /// <summary>
    /// Obtiene el historial de stock de un producto
    /// </summary>
    /// <param name="productId">ID del producto</param>
    /// <returns>Historial de movimientos del producto</returns>
    [HttpGet("product/{productId}/history")]
    [Authorize]
    [ProducesResponseType(200)]
    [ProducesResponseType(401)]
    [ProducesResponseType(404)]
    [ProducesResponseType(500)]
    public async Task<ActionResult<object>> GetProductStockHistory(int productId)
    {
        try
        {
            _logger.LogInformation("Obteniendo historial de stock para producto: {productId}", productId);

            var product = await _context.Products.FindAsync(productId);
            if (product == null)
            {
                return NotFound(new { message = "Producto no encontrado" });
            }

            var movements = await _context.Stocks
                .Include(s => s.FlowType)
                .Include(s => s.Provider)
                .Where(s => s.ProductId == productId)
                .OrderByDescending(s => s.Date)
                .Select(s => new
                {
                    id = s.Id,
                    date = s.Date,
                    flowType = new { id = s.FlowType.Id, name = s.FlowType.Name },
                    amount = s.Amount,
                    cost = s.Cost,
                    provider = s.Provider != null ? new { id = s.Provider.Id, name = s.Provider.Name } : null,
                    notes = s.Notes
                })
                .ToListAsync();

            var currentStock = movements.Sum(m => m.amount);

            var result = new
            {
                productId = productId,
                productName = product.Name,
                currentStock = currentStock,
                movements = movements
            };

            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al obtener historial de stock para producto: {productId}", productId);
            return StatusCode(500, new { message = "Error interno del servidor" });
        }
    }

    /// <summary>
    /// Obtiene todos los tipos de flujo disponibles
    /// </summary>
    /// <returns>Lista de tipos de flujo</returns>
    /// <response code="200">Lista de tipos de flujo obtenida exitosamente</response>
    /// <response code="401">No autorizado - Token JWT requerido</response>
    /// <response code="500">Error interno del servidor</response>
    [HttpGet("flow-types")]
    [Authorize]
    [ProducesResponseType(200)]
    [ProducesResponseType(401)]
    [ProducesResponseType(500)]
    public async Task<ActionResult<IEnumerable<object>>> GetFlowTypes()
    {
        try
        {
            _logger.LogInformation("Obteniendo tipos de flujo disponibles");

            var flowTypes = await _context.FlowTypes
                .OrderBy(ft => ft.Name)
                .Select(ft => new 
                { 
                    id = ft.Id, 
                    name = ft.Name
                })
                .ToListAsync();

            _logger.LogInformation($"Se encontraron {flowTypes.Count} tipos de flujo");
            return Ok(flowTypes);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al obtener tipos de flujo");
            return StatusCode(500, new { message = "Error interno del servidor" });
        }
    }

    /// <summary>
    /// Obtiene o crea un proveedor
    /// </summary>
    /// <param name="providerName">Nombre del proveedor</param>
    /// <param name="businessId">ID del negocio</param>
    /// <returns>ID del proveedor</returns>
    private async Task<int> GetOrCreateProvider(string providerName, int businessId)
    {
        // Buscar proveedor existente
        var existingProvider = await _context.Providers
            .FirstOrDefaultAsync(p => p.Name.ToLower() == providerName.ToLower() && p.BusinessId == businessId);

        if (existingProvider != null)
        {
            return existingProvider.Id;
        }

        // Crear nuevo proveedor
        var newProvider = new GPInventory.Domain.Entities.Provider
        {
            Name = providerName,
            BusinessId = businessId
        };

        _context.Providers.Add(newProvider);
        await _context.SaveChangesAsync();

        _logger.LogInformation("Proveedor creado: {providerName} para negocio: {businessId}", providerName, businessId);
        return newProvider.Id;
    }

    /// <summary>
    /// Obtiene el inventario completo de un negocio con stock actual
    /// </summary>
    /// <param name="businessId">ID del negocio</param>
    /// <returns>Lista de productos con su stock actual</returns>
    /// <response code="200">Inventario obtenido exitosamente</response>
    /// <response code="401">No autorizado - Token JWT requerido</response>
    /// <response code="404">Negocio no encontrado</response>
    /// <response code="500">Error interno del servidor</response>
    [HttpGet("inventory/{businessId}")]
    [Authorize]
    [ProducesResponseType(200)]
    [ProducesResponseType(401)]
    [ProducesResponseType(404)]
    [ProducesResponseType(500)]
    public async Task<ActionResult<IEnumerable<object>>> GetBusinessInventory(int businessId)
    {
        try
        {
            _logger.LogInformation("Obteniendo inventario para negocio: {businessId}", businessId);

            // Verificar que el negocio existe
            var business = await _context.Businesses.FindAsync(businessId);
            if (business == null)
            {
                return NotFound(new { message = "Negocio no encontrado" });
            }

            // Optimización: Una sola consulta con todas las relaciones necesarias
            // Separar el cálculo de averagePrice para evitar problemas de traducción SQL
            var inventoryBase = await _context.Products
                .Include(p => p.ProductType)
                .Include(p => p.Business)
                .Where(p => p.BusinessId == businessId)
                .Select(p => new
                {
                    id = p.Id,
                    name = p.Name,
                    sku = p.Sku,
                    price = p.Price,
                    cost = p.Cost,
                    image = p.Image,
                    productType = new { id = p.ProductType.Id, name = p.ProductType.Name },
                    business = new { id = p.Business.Id, companyName = p.Business.CompanyName },
                    
                    // Stock actual - suma directa en SQL
                    currentStock = _context.Stocks
                        .Where(s => s.ProductId == p.Id)
                        .Sum(s => s.Amount),
                    
                    // Costo promedio ponderado - solo entradas con costo
                    averageCost = _context.Stocks
                        .Where(s => s.ProductId == p.Id && s.Cost.HasValue && s.Cost > 0 && s.Amount > 0)
                        .Any() 
                        ? _context.Stocks
                            .Where(s => s.ProductId == p.Id && s.Cost.HasValue && s.Cost > 0 && s.Amount > 0)
                            .Sum(s => s.Cost!.Value * s.Amount) / 
                          _context.Stocks
                            .Where(s => s.ProductId == p.Id && s.Cost.HasValue && s.Cost > 0 && s.Amount > 0)
                            .Sum(s => s.Amount)
                        : (decimal?)null,
                    
                    // Total de movimientos
                    totalMovements = _context.Stocks
                        .Where(s => s.ProductId == p.Id)
                        .Count(),
                    
                    // Último movimiento
                    lastMovementDate = _context.Stocks
                        .Where(s => s.ProductId == p.Id)
                        .Max(s => (DateTime?)s.Date)
                })
                .OrderBy(p => p.name)
                .ToListAsync();

            // Paso 2: Obtener datos de ventas para el cálculo de averagePrice y métricas
            var productIds = inventoryBase.Select(p => p.id).ToList();
            var salesData = await _context.SaleDetails
                .Include(sd => sd.Sale)
                .Where(sd => productIds.Contains(sd.ProductId))
                .Select(sd => new { 
                    sd.ProductId, 
                    sd.Price, 
                    sd.Amount,
                    SaleDate = sd.Sale.Date
                })
                .ToListAsync();

            // Calcular fechas para filtros y comparaciones
            var today = DateTime.Today;
            var startOfMonth = new DateTime(today.Year, today.Month, 1);
            var lastMonth = startOfMonth.AddMonths(-1);
            var endOfLastMonth = startOfMonth.AddDays(-1);
            var sameDayLastMonth = today.AddMonths(-1);
            
            // Filtrar ventas del día, mes actual, mes anterior y mismo día mes anterior
            var todaySales = salesData.Where(sd => sd.SaleDate.Date == today).ToList();
            var monthSales = salesData.Where(sd => sd.SaleDate >= startOfMonth).ToList();
            var lastMonthSales = salesData.Where(sd => sd.SaleDate >= lastMonth && sd.SaleDate <= endOfLastMonth).ToList();
            var sameDayLastMonthSales = salesData.Where(sd => sd.SaleDate.Date == sameDayLastMonth).ToList();

            // Paso 3: Calcular métricas y construir resultado final
            var inventory = inventoryBase.Select(item =>
            {
                var productSales = salesData.Where(sd => sd.ProductId == item.id).ToList();
                var productTodaySales = todaySales.Where(sd => sd.ProductId == item.id).ToList();
                var productMonthSales = monthSales.Where(sd => sd.ProductId == item.id).ToList();
                var productLastMonthSales = lastMonthSales.Where(sd => sd.ProductId == item.id).ToList();
                var productSameDayLastMonthSales = sameDayLastMonthSales.Where(sd => sd.ProductId == item.id).ToList();
                
                decimal? averagePrice = null;
                decimal todaySalesAmount = 0;
                int todayQuantitySold = 0;
                decimal monthSalesAmount = 0;
                int monthQuantitySold = 0;
                decimal lastMonthSalesAmount = 0;
                decimal sameDayLastMonthSalesAmount = 0;

                // Calcular precio promedio general
                if (productSales.Any())
                {
                    try
                    {
                        var totalValue = productSales.Sum(sd => sd.Price * int.Parse(sd.Amount));
                        var totalQuantity = productSales.Sum(sd => int.Parse(sd.Amount));
                        averagePrice = totalQuantity > 0 ? totalValue / totalQuantity : null;
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning("Error calculando averagePrice para producto {productId}: {error}", item.id, ex.Message);
                        averagePrice = null;
                    }
                }

                // Calcular ventas del día
                if (productTodaySales.Any())
                {
                    try
                    {
                        todaySalesAmount = productTodaySales.Sum(sd => sd.Price * int.Parse(sd.Amount));
                        todayQuantitySold = productTodaySales.Sum(sd => int.Parse(sd.Amount));
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning("Error calculando ventas del día para producto {productId}: {error}", item.id, ex.Message);
                    }
                }

                // Calcular ventas del mes
                if (productMonthSales.Any())
                {
                    try
                    {
                        monthSalesAmount = productMonthSales.Sum(sd => sd.Price * int.Parse(sd.Amount));
                        monthQuantitySold = productMonthSales.Sum(sd => int.Parse(sd.Amount));
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning("Error calculando ventas del mes para producto {productId}: {error}", item.id, ex.Message);
                    }
                }

                // Calcular ventas del mes anterior (para comparación)
                if (productLastMonthSales.Any())
                {
                    try
                    {
                        lastMonthSalesAmount = productLastMonthSales.Sum(sd => sd.Price * int.Parse(sd.Amount));
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning("Error calculando ventas del mes anterior para producto {productId}: {error}", item.id, ex.Message);
                    }
                }

                // Calcular ventas del mismo día mes anterior (para comparación)
                if (productSameDayLastMonthSales.Any())
                {
                    try
                    {
                        sameDayLastMonthSalesAmount = productSameDayLastMonthSales.Sum(sd => sd.Price * int.Parse(sd.Amount));
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning("Error calculando ventas del mismo día mes anterior para producto {productId}: {error}", item.id, ex.Message);
                    }
                }

                // Calcular porcentajes de cambio
                decimal? todayChangePercent = null;
                decimal? monthChangePercent = null;

                if (sameDayLastMonthSalesAmount > 0)
                {
                    todayChangePercent = ((todaySalesAmount - sameDayLastMonthSalesAmount) / sameDayLastMonthSalesAmount) * 100;
                }

                if (lastMonthSalesAmount > 0)
                {
                    monthChangePercent = ((monthSalesAmount - lastMonthSalesAmount) / lastMonthSalesAmount) * 100;
                }

                return new
                {
                    id = item.id,
                    name = item.name,
                    sku = item.sku,
                    price = item.price,
                    cost = item.cost,
                    image = item.image,
                    productType = item.productType,
                    business = item.business,
                    currentStock = item.currentStock,
                    averageCost = item.averageCost,
                    averagePrice = averagePrice,
                    totalMovements = item.totalMovements,
                    lastMovementDate = item.lastMovementDate,
                    // Métricas de ventas con porcentajes de cambio
                    todaySales = new
                    {
                        amount = todaySalesAmount,
                        quantity = todayQuantitySold,
                        changePercent = todayChangePercent
                    },
                    monthSales = new
                    {
                        amount = monthSalesAmount,
                        quantity = monthQuantitySold,
                        changePercent = monthChangePercent
                    }
                };
            }).ToList();

            // Calcular resumen de ventas del negocio con porcentajes de cambio
            decimal businessTodaySales = 0;
            int businessTodayTransactions = 0;
            decimal businessMonthSales = 0;
            int businessMonthTransactions = 0;
            decimal businessLastMonthSales = 0;
            decimal businessSameDayLastMonthSales = 0;

            try
            {
                businessTodaySales = todaySales.Sum(sd => sd.Price * int.Parse(sd.Amount));
                businessTodayTransactions = todaySales.GroupBy(sd => sd.SaleDate).Count();
                
                businessMonthSales = monthSales.Sum(sd => sd.Price * int.Parse(sd.Amount));
                businessMonthTransactions = monthSales.GroupBy(sd => sd.SaleDate).Count();

                businessLastMonthSales = lastMonthSales.Sum(sd => sd.Price * int.Parse(sd.Amount));
                businessSameDayLastMonthSales = sameDayLastMonthSales.Sum(sd => sd.Price * int.Parse(sd.Amount));
            }
            catch (Exception ex)
            {
                _logger.LogWarning("Error calculando resumen de ventas del negocio {businessId}: {error}", businessId, ex.Message);
            }

            // Calcular porcentajes de cambio del negocio
            decimal? businessTodayChangePercent = null;
            decimal? businessMonthChangePercent = null;

            if (businessSameDayLastMonthSales > 0)
            {
                businessTodayChangePercent = ((businessTodaySales - businessSameDayLastMonthSales) / businessSameDayLastMonthSales) * 100;
            }

            if (businessLastMonthSales > 0)
            {
                businessMonthChangePercent = ((businessMonthSales - businessLastMonthSales) / businessLastMonthSales) * 100;
            }

            var result = new
            {
                businessId = businessId,
                summary = new
                {
                    totalProducts = inventory.Count,
                    totalStock = inventory.Sum(i => i.currentStock),
                    todaySales = new
                    {
                        amount = businessTodaySales,
                        transactions = businessTodayTransactions,
                        changePercent = businessTodayChangePercent
                    },
                    monthSales = new
                    {
                        amount = businessMonthSales,
                        transactions = businessMonthTransactions,
                        changePercent = businessMonthChangePercent
                    }
                },
                products = inventory
            };

            _logger.LogInformation($"Se encontraron {inventory.Count} productos en el inventario del negocio {businessId}");
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al obtener inventario del negocio: {businessId}", businessId);
            return StatusCode(500, new { message = "Error interno del servidor" });
        }
    }
}

/// <summary>
/// Modelo para crear un movimiento de stock
/// </summary>
public class CreateStockMovementRequest
{
    /// <summary>
    /// ID del producto
    /// </summary>
    public int ProductId { get; set; }

    /// <summary>
    /// Fecha del movimiento (opcional, por defecto fecha actual)
    /// </summary>
    public DateTime? Date { get; set; }

    /// <summary>
    /// ID del tipo de flujo (entrada/salida)
    /// </summary>
    public int FlowTypeId { get; set; }

    /// <summary>
    /// Cantidad del movimiento (positivo para entrada, negativo para salida)
    /// </summary>
    public int Amount { get; set; }

    /// <summary>
    /// Costo (opcional)
    /// </summary>
    public int? Cost { get; set; }

    /// <summary>
    /// Nombre del proveedor (opcional, se creará automáticamente si no existe)
    /// </summary>
    public string? ProviderName { get; set; }

    /// <summary>
    /// Notas adicionales (opcional)
    /// </summary>
    public string? Notes { get; set; }
}
