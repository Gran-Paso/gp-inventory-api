using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using GPInventory.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Cors;
using GPInventory.Application.Interfaces;
using GPInventory.Api.Extensions;
using System.Security.Claims;

namespace GPInventory.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[EnableCors("AllowFrontend")]
public class SalesController : ControllerBase
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<SalesController> _logger;
    private readonly INotificationService _notificationService;

    public SalesController(ApplicationDbContext context, ILogger<SalesController> logger, INotificationService notificationService)
    {
        _context = context;
        _logger = logger;
        _notificationService = notificationService;
    }

    /// <summary>
    /// Obtiene todos los métodos de pago disponibles
    /// </summary>
    /// <returns>Lista de métodos de pago</returns>
    [HttpGet("payment-methods")]
    [Authorize]
    [ProducesResponseType(200)]
    [ProducesResponseType(401)]
    [ProducesResponseType(500)]
    public async Task<ActionResult<IEnumerable<object>>> GetPaymentMethods()
    {
        try
        {
            _logger.LogInformation("Obteniendo métodos de pago");

            var paymentMethods = await _context.PaymentMethods
                .OrderBy(pm => pm.Name)
                .Select(pm => new { id = pm.Id, name = pm.Name })
                .ToListAsync();

            return Ok(paymentMethods);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al obtener métodos de pago");
            return StatusCode(500, new { message = "Error interno del servidor" });
        }
    }

    /// <summary>
    /// Busca productos disponibles por nombre o SKU en un store específico
    /// </summary>
    /// <param name="storeId">ID del store</param>
    /// <param name="searchTerm">Término de búsqueda (nombre o SKU) - opcional</param>
    /// <returns>Lista de productos que coinciden con la búsqueda y tienen stock disponible</returns>
    [HttpGet("search-available-products/{storeId}")]
    [Authorize]
    [ProducesResponseType(200)]
    [ProducesResponseType(401)]
    [ProducesResponseType(404)]
    [ProducesResponseType(500)]
    public async Task<ActionResult<object>> SearchAvailableProductsByStore(int storeId, [FromQuery] string? searchTerm = null)
    {
        try
        {
            _logger.LogInformation("Buscando productos disponibles en store {storeId} con término: {searchTerm}", storeId, searchTerm ?? "todos");

            // Verificar que el store existe y está activo
            var store = await _context.Stores
                .Include(s => s.Business)
                .FirstOrDefaultAsync(s => s.Id == storeId && s.Active);

            if (store == null)
            {
                return NotFound(new { message = "Store no encontrado o no está activo" });
            }

            // Buscar productos por nombre o SKU, o todos si no hay término de búsqueda
            var query = _context.Products
                .Include(p => p.ProductType)
                .Include(p => p.Business)
                .Where(p => p.BusinessId == store.BusinessId);

            if (!string.IsNullOrWhiteSpace(searchTerm))
            {
                var searchTermLower = searchTerm.ToLower();
                query = query.Where(p => p.Name.ToLower().Contains(searchTermLower) || 
                                        (p.Sku != null && p.Sku.ToLower().Contains(searchTermLower)));
            }

            var matchingProducts = await query.ToListAsync();

            var availableProducts = new List<object>();

            foreach (var product in matchingProducts)
            {
                // Obtener todos los stocks del producto en el store
                var stockLots = await _context.Stocks
                    .Where(s => s.ProductId == product.Id && s.StoreId == storeId && s.Amount > 0)
                    .ToListAsync();

                // Calcular stock real disponible considerando las ventas
                var currentStock = 0;
                foreach (var stock in stockLots)
                {
                    var saleDetailsForStock = await _context.SaleDetails
                        .Where(sd => sd.StockId == stock.Id)
                        .Select(sd => sd.Amount)
                        .ToListAsync();
                        
                    var stockUsedInSales = saleDetailsForStock.Sum(amount => int.Parse(amount));

                    var availableInLot = stock.Amount - stockUsedInSales;
                    if (availableInLot > 0)
                    {
                        currentStock += availableInLot;
                    }
                }

                // Solo incluir productos con stock disponible
                if (currentStock > 0)
                {
                    availableProducts.Add(new
                    {
                        id = product.Id,
                        name = product.Name,
                        sku = product.Sku,
                        price = product.Price,
                        cost = product.Cost,
                        image = product.Image,
                        currentStock = currentStock,
                        productType = product.ProductType != null ? new { id = product.ProductType.Id, name = product.ProductType.Name } : null,
                        canSell = true
                    });
                }
            }

            var result = new
            {
                storeId = store.Id,
                storeName = store.Name,
                searchTerm = searchTerm ?? "todos",
                isFiltered = !string.IsNullOrWhiteSpace(searchTerm),
                totalFound = availableProducts.Count,
                products = availableProducts.OrderBy(p => ((dynamic)p).name)
            };

            var logMessage = string.IsNullOrWhiteSpace(searchTerm) 
                ? $"Se encontraron {availableProducts.Count} productos disponibles en store {storeId}"
                : $"Se encontraron {availableProducts.Count} productos disponibles que coinciden con '{searchTerm}' en store {storeId}";
            
            _logger.LogInformation(logMessage);
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al buscar productos disponibles en store: {storeId}", storeId);
            return StatusCode(500, new { message = "Error interno del servidor" });
        }
    }

    /// <summary>
    /// Obtiene todos los productos disponibles (con stock > 0) en un store específico
    /// </summary>
    /// <param name="storeId">ID del store</param>
    /// <returns>Lista de productos disponibles con su stock en el store</returns>
    [HttpGet("available-products/{storeId}")]
    [Authorize]
    [ProducesResponseType(200)]
    [ProducesResponseType(401)]
    [ProducesResponseType(404)]
    [ProducesResponseType(500)]
    public async Task<ActionResult<object>> GetAvailableProductsByStore(int storeId)
    {
        try
        {
            _logger.LogInformation("Obteniendo productos disponibles para store: {storeId}", storeId);

            // Verificar que el store existe y está activo usando SQL directo
            var storeQuery = @"
                SELECT 
                    s.id AS Id,
                    s.name AS Name,
                    s.location AS Location,
                    s.id_business AS BusinessId,
                    COALESCE(s.active, 0) AS Active,
                    b.company_name AS BusinessCompanyName
                FROM store s
                LEFT JOIN business b ON s.id_business = b.id
                WHERE s.id = {0} AND COALESCE(s.active, 0) = 1";

            var storeResult = await _context.Database
                .SqlQueryRaw<StoreWithBusinessResult>(storeQuery, storeId)
                .FirstOrDefaultAsync();

            if (storeResult == null)
            {
                return NotFound(new { message = "Store no encontrado o no está activo" });
            }

            // Obtener productos con stock disponible usando SQL directo con cálculo FIFO
            var productsQuery = @"
                SELECT 
                    p.id AS Id,
                    p.name AS Name,
                    p.sku AS Sku,
                    COALESCE(p.price, 0) AS Price,
                    COALESCE(p.cost, 0) AS Cost,
                    p.image AS Image,
                    p.product_type AS ProductTypeId,
                    pt.name AS ProductTypeName,
                    p.business AS BusinessId,
                    b.company_name AS BusinessCompanyName,
                    COALESCE(p.minimumStock, 0) AS MinimumStock,
                    COALESCE(
                        (SELECT SUM(s.amount)
                         FROM stock s
                         LEFT JOIN stock sp ON s.stock_id = sp.id
                         WHERE s.product = p.id 
                         AND s.id_store = {0}
                         AND ((s.amount > 0 AND COALESCE(s.active, 0) = 1) OR (s.amount < 0 AND COALESCE(sp.active, 0) = 1))),
                        0
                    ) AS CurrentStock,
                    -- Calcular costo promedio ponderado (FIFO) basado en lotes de entrada con stock disponible
                    COALESCE(
                        (SELECT 
                            CASE 
                                WHEN SUM(GREATEST(
                                    s.amount - COALESCE((
                                        SELECT SUM(CAST(sd.amount AS SIGNED))
                                        FROM sales_detail sd
                                        WHERE sd.stock_id = s.id
                                    ), 0), 
                                    0
                                )) > 0
                                THEN SUM(
                                    GREATEST(
                                        s.amount - COALESCE((
                                            SELECT SUM(CAST(sd.amount AS SIGNED))
                                            FROM sales_detail sd
                                            WHERE sd.stock_id = s.id
                                        ), 0), 
                                        0
                                    ) * COALESCE(s.cost, 0)
                                ) / SUM(GREATEST(
                                    s.amount - COALESCE((
                                        SELECT SUM(CAST(sd.amount AS SIGNED))
                                        FROM sales_detail sd
                                        WHERE sd.stock_id = s.id
                                    ), 0), 
                                    0
                                ))
                                ELSE NULL
                            END
                         FROM stock s
                         WHERE s.product = p.id 
                         AND s.id_store = {0}
                         AND s.amount > 0
                         AND COALESCE(s.active, 0) = 1
                         AND s.cost IS NOT NULL 
                         AND s.cost > 0),
                        NULL
                    ) AS AverageCost
                FROM product p
                LEFT JOIN product_type pt ON p.product_type = pt.id
                LEFT JOIN business b ON p.business = b.id
                WHERE p.business = {1}
                HAVING CurrentStock > 0
                ORDER BY p.name ASC";

            var productsResult = await _context.Database
                .SqlQueryRaw<ProductWithStockResult>(productsQuery, storeId, storeResult.BusinessId)
                .ToListAsync();

            var availableProducts = productsResult.Select(p => new
            {
                id = p.Id,
                name = p.Name,
                sku = p.Sku,
                price = p.Price,
                cost = p.Cost,
                image = p.Image,
                currentStock = p.CurrentStock,
                minimumStock = p.MinimumStock,
                averageCost = p.AverageCost.HasValue ? Math.Round(p.AverageCost.Value, 2) : (decimal?)null,
                productType = p.ProductTypeId.HasValue ? new { 
                    id = p.ProductTypeId.Value, 
                    name = p.ProductTypeName 
                } : null,
                business = new { 
                    id = p.BusinessId, 
                    companyName = p.BusinessCompanyName 
                }
            }).ToList();

            var result = new
            {
                storeId = storeResult.Id,
                storeName = storeResult.Name,
                storeLocation = storeResult.Location,
                businessId = storeResult.BusinessId,
                businessName = storeResult.BusinessCompanyName,
                totalAvailableProducts = availableProducts.Count,
                products = availableProducts
            };

            _logger.LogInformation($"Se encontraron {availableProducts.Count} productos disponibles en store {storeId}");
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al obtener productos disponibles del store: {storeId}", storeId);
            return StatusCode(500, new { message = "Error interno del servidor", details = ex.Message });
        }
    }

    /// <summary>
    /// Busca un producto por ID para venta rápida
    /// </summary>
    /// <param name="productId">ID del producto</param>
    /// <param name="storeId">ID del store</param>
    /// <returns>Información del producto y stock disponible en el store</returns>
    [HttpGet("products/{productId}")]
    [Authorize]
    [ProducesResponseType(200)]
    [ProducesResponseType(401)]
    [ProducesResponseType(404)]
    [ProducesResponseType(500)]
    public async Task<ActionResult<object>> GetProductForSale(int productId, [FromQuery] int storeId)
    {
        try
        {
            _logger.LogInformation("Buscando producto {productId} para venta en store {storeId}", productId, storeId);

            // Verificar que el store existe y está activo
            var store = await _context.Stores
                .Include(s => s.Business)
                .FirstOrDefaultAsync(s => s.Id == storeId && s.Active);

            if (store == null)
            {
                return BadRequest(new { message = "Store no encontrado o no está activo" });
            }

            // Buscar el producto que pertenezca al business del store
            var product = await _context.Products
                .Include(p => p.ProductType)
                .Include(p => p.Business)
                .Where(p => p.Id == productId && p.BusinessId == store.BusinessId)
                .FirstOrDefaultAsync();

            if (product == null)
            {
                return NotFound(new { message = "Producto no encontrado o no pertenece al negocio del store" });
            }

            // Calcular stock actual en el store específico
            var currentStock = await _context.Stocks
                .Where(s => s.ProductId == productId && s.StoreId == storeId)
                .SumAsync(s => s.Amount);

            // Calcular precio promedio y costo promedio basado en las ventas del store
            var salesData = await _context.SaleDetails
                .Include(sd => sd.Sale)
                .Where(sd => sd.ProductId == productId && sd.Sale.StoreId == storeId)
                .Select(sd => new { 
                    sd.Price, 
                    Cost = _context.Stocks
                        .Where(s => s.ProductId == productId && s.Date <= sd.Sale.Date && s.StoreId == storeId)
                        .OrderByDescending(s => s.Date)
                        .Select(s => s.Cost)
                        .FirstOrDefault() 
                })
                .ToListAsync();

            var averagePrice = salesData.Any() ? (decimal?)salesData.Average(s => s.Price) : null;
            var averageCost = salesData.Any() && salesData.Any(s => s.Cost > 0) 
                ? (decimal?)salesData.Where(s => s.Cost > 0).Average(s => s.Cost) 
                : null;

            var result = new
            {
                id = product.Id,
                name = product.Name,
                price = product.Price,
                cost = product.Cost,
                sku = product.Sku,
                image = product.Image,
                currentStock = currentStock,
                averagePrice = averagePrice,
                averageCost = averageCost,
                productType = product.ProductType != null ? new { id = product.ProductType.Id, name = product.ProductType.Name } : null,
                store = new { id = store.Id, name = store.Name, location = store.Location },
                business = new { id = product.Business.Id, companyName = product.Business.CompanyName },
                canSell = currentStock > 0
            };

            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al buscar producto para venta: {productId}", productId);
            return StatusCode(500, new { message = "Error interno del servidor" });
        }
    }

    /// <summary>
    /// Obtiene los stocks disponibles de un producto específico en un store
    /// </summary>
    /// <param name="productId">ID del producto</param>
    /// <param name="storeId">ID del store</param>
    /// <returns>Lista de stocks disponibles del producto en el store</returns>
    [HttpGet("product-stocks/{productId}")]
    [Authorize]
    [ProducesResponseType(200)]
    [ProducesResponseType(401)]
    [ProducesResponseType(404)]
    [ProducesResponseType(500)]
    public async Task<ActionResult<object>> GetProductStocks(int productId, [FromQuery] int storeId)
    {
        try
        {
            _logger.LogInformation("Obteniendo stocks del producto {productId} en store {storeId}", productId, storeId);

            // Verificar que el store existe y está activo
            var store = await _context.Stores
                .Include(s => s.Business)
                .FirstOrDefaultAsync(s => s.Id == storeId && s.Active);

            if (store == null)
            {
                return BadRequest(new { message = "Store no encontrado o no está activo" });
            }

            // Verificar que el producto existe y pertenece al business del store
            var product = await _context.Products
                .Include(p => p.ProductType)
                .Where(p => p.Id == productId && p.BusinessId == store.BusinessId)
                .FirstOrDefaultAsync();

            if (product == null)
            {
                return NotFound(new { message = "Producto no encontrado o no pertenece al negocio del store" });
            }

            // Obtener todos los stocks del producto en el store con cantidad > 0
            _logger.LogInformation("Buscando stocks con filtros: ProductId={productId}, StoreId={storeId}, Amount > 0", productId, storeId);
            
            var stocksQuery = await _context.Stocks
                .Include(s => s.FlowType)
                .Where(s => s.ProductId == productId && s.StoreId == storeId && s.Amount > 0)
                .OrderByDescending(s => s.Date)
                .ToListAsync();

            // Calcular el stock real disponible considerando las ventas
            var stocks = new List<object>();
            var totalAvailableStock = 0;

            foreach (var stock in stocksQuery)
            {
                // Calcular cuánto stock se ha usado de este lote en ventas
                var saleDetailsForStock = await _context.SaleDetails
                    .Where(sd => sd.StockId == stock.Id)
                    .Select(sd => sd.Amount)
                    .ToListAsync();
                    
                var stockUsedInSales = saleDetailsForStock.Sum(amount => int.Parse(amount));

                var availableAmount = stock.Amount - stockUsedInSales;

                // Solo incluir si hay stock disponible después de restar las ventas
                if (availableAmount > 0)
                {
                    stocks.Add(new
                    {
                        id = stock.Id,
                        amount = availableAmount, // Stock disponible real
                        originalAmount = stock.Amount, // Stock original del lote
                        usedInSales = stockUsedInSales, // Stock usado en ventas
                        cost = stock.Cost,
                        date = stock.Date,
                        flowType = stock.FlowType != null ? new { id = stock.FlowType.Id, name = stock.FlowType.Name } : null,
                        notes = stock.Notes
                    });

                    totalAvailableStock += availableAmount;
                }

                _logger.LogInformation("Lote {stockId}: Original={original}, Usado={used}, Disponible={available}", 
                    stock.Id, stock.Amount, stockUsedInSales, availableAmount);
            }

            _logger.LogInformation("Encontrados {stockCount} lotes de stock disponibles para producto {productId} en store {storeId}, total disponible: {totalStock}", 
                stocks.Count, productId, storeId, totalAvailableStock);

            var result = new
            {
                productId = product.Id,
                productName = product.Name,
                productSku = product.Sku,
                storeId = store.Id,
                storeName = store.Name,
                totalAvailableStock,
                stockLots = stocks
            };

            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al obtener stocks del producto: {productId} en store: {storeId}", productId, storeId);
            return StatusCode(500, new { message = "Error al obtener stocks del producto", details = ex.Message });
        }
    }

    /// <summary>
    /// Procesa una venta rápida
    /// </summary>
    /// <param name="request">Datos de la venta</param>
    /// <returns>Venta procesada</returns>
    [HttpPost("quick-sale")]
    [Authorize]
    [ProducesResponseType(201)]
    [ProducesResponseType(400)]
    [ProducesResponseType(401)]
    [ProducesResponseType(500)]
    public async Task<ActionResult<object>> ProcessQuickSale([FromBody] QuickSaleRequest request)
    {
        using var transaction = await _context.Database.BeginTransactionAsync();
        
        try
        {
            // Validaciones básicas
            if (request.StoreId <= 0)
            {
                return BadRequest(new { message = "ID de store inválido" });
            }

            if (request.Items == null || !request.Items.Any())
            {
                return BadRequest(new { message = "La venta debe tener al menos un producto" });
            }

            _logger.LogInformation("Procesando venta rápida para store: {storeId}", request.StoreId);

            // Verificar que el store existe y está activo
            var store = await _context.Stores
                .Include(s => s.Business)
                .FirstOrDefaultAsync(s => s.Id == request.StoreId);
            
            if (store == null)
            {
                return BadRequest(new { message = "El store especificado no existe" });
            }

            if (!store.Active)
            {
                return BadRequest(new { message = "El store especificado no está activo" });
            }

            // Verificar que todos los productos existen y pertenecen al mismo business del store
            var productIds = request.Items.Select(i => i.ProductId).Distinct().ToList();
            var products = await _context.Products
                .Where(p => productIds.Contains(p.Id) && p.BusinessId == store.BusinessId)
                .ToListAsync();

            if (products.Count != productIds.Count)
            {
                return BadRequest(new { message = "Uno o más productos no existen o no pertenecen al negocio del store" });
            }

            // Verificar stock disponible en el store específico y validar StockId si se proporciona
            foreach (var item in request.Items)
            {
                // Si se especifica un StockId, validar que existe y pertenece al producto y store
                if (item.StockId.HasValue)
                {
                    var specificStock = await _context.Stocks
                        .Where(s => s.Id == item.StockId.Value && 
                                   s.ProductId == item.ProductId && 
                                   s.StoreId == request.StoreId)
                        .FirstOrDefaultAsync();

                    if (specificStock == null)
                    {
                        var product = products.First(p => p.Id == item.ProductId);
                        return BadRequest(new { message = $"El stock específico {item.StockId.Value} no existe para el producto {product.Name} en este store" });
                    }

                    // Para un lote específico, necesitamos calcular cuánto stock queda disponible
                    // considerando el stock original del lote menos todas las salidas por ventas de ese lote
                    var saleDetailsForStock = await _context.SaleDetails
                        .Where(sd => sd.StockId == item.StockId.Value)
                        .Select(sd => sd.Amount)
                        .ToListAsync();
                    
                    var stockUsedInSales = saleDetailsForStock.Sum(amount => int.Parse(amount));

                    var availableInLot = specificStock.Amount - stockUsedInSales;

                    if (availableInLot < item.Quantity)
                    {
                        var product = products.First(p => p.Id == item.ProductId);
                        return BadRequest(new { message = $"Stock insuficiente en el lote específico {item.StockId.Value} para {product.Name}. Disponible: {availableInLot}, Solicitado: {item.Quantity}" });
                    }
                    
                    _logger.LogInformation("Validación de stock para lote {stockId}: Original={originalAmount}, Usado={usedAmount}, Disponible={availableAmount}, Solicitado={requestedAmount}", 
                        item.StockId.Value, specificStock.Amount, stockUsedInSales, availableInLot, item.Quantity);
                }
                else
                {
                    // Validación de stock total cuando no se especifica StockId específico
                    var currentStock = await _context.Stocks
                        .Where(s => s.ProductId == item.ProductId && s.StoreId == request.StoreId)
                        .SumAsync(s => s.Amount);

                    if (currentStock < item.Quantity)
                    {
                        var product = products.First(p => p.Id == item.ProductId);
                        return BadRequest(new { message = $"Stock insuficiente para {product.Name} en este store. Disponible: {currentStock}, Solicitado: {item.Quantity}" });
                    }
                }
            }

            // Crear la venta
            var sale = new GPInventory.Domain.Entities.Sale
            {
                StoreId = request.StoreId,
                Date = DateTime.UtcNow,
                CustomerName = request.CustomerName?.Trim(),
                CustomerRut = request.CustomerRut?.Trim(),
                PaymentMethodId = request.PaymentMethodId,
                Notes = request.Notes?.Trim(),
                Total = 0 // Se calculará después
            };

            _context.Sales.Add(sale);
            await _context.SaveChangesAsync(); // Para obtener el ID de la venta

            // Crear detalles de venta y calcular total
            int totalAmount = 0;
            var saleDetails = new List<object>();

            foreach (var item in request.Items)
            {
                var product = products.First(p => p.Id == item.ProductId);
                var unitPrice = item.UnitPrice ?? product.Price;
                var subtotal = unitPrice * item.Quantity;
                
                totalAmount += subtotal;

                var saleDetail = new GPInventory.Domain.Entities.SaleDetail
                {
                    ProductId = item.ProductId,
                    Amount = item.Quantity.ToString(),
                    Price = unitPrice,
                    Discount = item.Discount,
                    SaleId = sale.Id,
                    StockId = item.StockId
                };

                _context.SaleDetails.Add(saleDetail);

                // Crear movimiento de stock (salida por venta)
                if (item.StockId.HasValue)
                {
                    // Obtener el stock específico para usar su costo, pero NO restarlo directamente
                    var specificStock = await _context.Stocks
                        .FirstAsync(s => s.Id == item.StockId.Value);
                    
                    // Crear un registro de movimiento de stock para registrar la salida por venta
                    // Este registro negativo es lo que efectivamente reduce el stock
                    var saleStockMovement = new GPInventory.Domain.Entities.Stock
                    {
                        ProductId = item.ProductId,
                        Date = DateTime.UtcNow,
                        FlowTypeId = 11, // FlowType "Venta"
                        Amount = -item.Quantity, // Cantidad negativa para salida
                        Cost = specificStock.Cost, // Usar el costo del stock específico
                        StoreId = request.StoreId,
                        SaleId = sale.Id, // Vincular con la venta
                        Notes = $"Venta #{sale.Id} - Stock lote #{item.StockId.Value}",
                        IsActive = true // ✅ Establecer como activo
                    };

                    _context.Stocks.Add(saleStockMovement);
                    
                    _logger.LogInformation("Creado movimiento de stock (venta) para producto {productId}, cantidad: {quantity}, desde stock lote {stockId}, venta {saleId}", 
                        item.ProductId, -item.Quantity, item.StockId.Value, sale.Id);
                }
                else
                {
                    // Crear movimiento de stock general (método tradicional)
                    var stockMovement = new GPInventory.Domain.Entities.Stock
                    {
                        ProductId = item.ProductId,
                        Date = DateTime.UtcNow,
                        FlowTypeId = 11, // FlowType "Venta"
                        Amount = -item.Quantity, // Cantidad negativa para salida
                        Cost = null, // No se especifica costo en las ventas
                        StoreId = request.StoreId,
                        SaleId = sale.Id, // Vincular con la venta
                        Notes = $"Venta #{sale.Id}",
                        IsActive = true // ✅ Establecer como activo
                    };

                    _context.Stocks.Add(stockMovement);
                }

                // Obtener información del stock para incluir en la respuesta
                object? stockInfo = null;
                if (item.StockId.HasValue)
                {
                    var stockUsed = await _context.Stocks
                        .Include(s => s.FlowType)
                        .Where(s => s.Id == item.StockId.Value)
                        .FirstOrDefaultAsync();
                    
                    if (stockUsed != null)
                    {
                        stockInfo = new
                        {
                            id = stockUsed.Id,
                            originalAmount = stockUsed.Amount,
                            cost = stockUsed.Cost,
                            date = stockUsed.Date,
                            flowType = stockUsed.FlowType != null ? new { id = stockUsed.FlowType.Id, name = stockUsed.FlowType.Name } : null,
                            notes = stockUsed.Notes
                        };
                    }
                }

                saleDetails.Add(new
                {
                    productId = item.ProductId,
                    productName = product.Name,
                    quantity = item.Quantity,
                    unitPrice = unitPrice,
                    subtotal = subtotal,
                    discount = item.Discount,
                    stockId = item.StockId,
                    stockInfo = stockInfo,
                    stockSource = item.StockId.HasValue ? "Lote específico" : "Stock general"
                });
            }

            // Actualizar total de la venta
            sale.Total = totalAmount;
            await _context.SaveChangesAsync();

            await transaction.CommitAsync();

            // 🔔 ENVIAR NOTIFICACIONES DESPUÉS DE COMPLETAR LA VENTA
            try
            {
                var userId = GetCurrentUserId();
                var businessName = store.Business?.CompanyName ?? "Negocio";
                
                // Verificar si se alcanzó el hito de $100,000
                await _notificationService.CheckAndSendQuickSaleNotificationAsync(
                    userId, 
                    totalAmount, 
                    businessName
                );

                // Verificar stock bajo en los productos vendidos
                foreach (var item in request.Items)
                {
                    var currentStock = await _context.Stocks
                        .Where(s => s.ProductId == item.ProductId && s.StoreId == request.StoreId)
                        .SumAsync(s => s.Amount);

                    var product = products.First(p => p.Id == item.ProductId);
                    
                    if (currentStock <= 0)
                    {
                        // Producto agotado
                        await _notificationService.CheckAndSendOutOfStockNotificationAsync(
                            userId,
                            product.Name,
                            businessName
                        );
                    }
                    else if (currentStock <= 5)
                    {
                        // Stock bajo
                        await _notificationService.CheckAndSendLowStockNotificationAsync(
                            userId,
                            product.Name,
                            currentStock,
                            businessName
                        );
                    }
                }

                // 🎯 VERIFICAR PUNTO DE EQUILIBRIO (BREAKEVEN)
                if (store.BusinessId.HasValue)
                {
                    await CheckAndSendBreakevenNotificationAsync(store.BusinessId.Value, businessName);
                }
            }
            catch (Exception ex)
            {
                // Las notificaciones no deben afectar el proceso principal
                _logger.LogWarning(ex, "Error enviando notificaciones para venta {saleId}", sale.Id);
            }

            var result = new
            {
                saleId = sale.Id,
                storeId = sale.StoreId,
                storeName = store.Name,
                businessId = store.BusinessId,
                businessName = store.Business?.CompanyName,
                date = sale.Date,
                customerName = sale.CustomerName,
                customerRut = sale.CustomerRut,
                total = sale.Total,
                paymentMethodId = sale.PaymentMethodId,
                items = saleDetails,
                notes = sale.Notes
            };

            _logger.LogInformation("Venta rápida procesada exitosamente: {saleId}", sale.Id);
            return CreatedAtAction(nameof(GetSale), new { id = sale.Id }, result);
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync();
            _logger.LogError(ex, "Error al procesar venta rápida");
            return StatusCode(500, new { message = "Error al procesar la venta", details = ex.Message });
        }
    }

    /// <summary>
    /// Obtiene una venta específica
    /// </summary>
    /// <param name="id">ID de la venta</param>
    /// <returns>Venta con detalles</returns>
    [HttpGet("{id}")]
    [Authorize]
    [ProducesResponseType(200)]
    [ProducesResponseType(401)]
    [ProducesResponseType(404)]
    [ProducesResponseType(500)]
    public async Task<ActionResult<object>> GetSale(int id)
    {
        try
        {
            var sale = await _context.Sales
                .Include(s => s.Store)
                    .ThenInclude(st => st.Business)
                .Include(s => s.PaymentMethod)
                .Include(s => s.SaleDetails)
                    .ThenInclude(sd => sd.Product)
                .Include(s => s.SaleDetails)
                    .ThenInclude(sd => sd.Stock!)
                        .ThenInclude(st => st.FlowType)
                .Where(s => s.Id == id)
                .FirstOrDefaultAsync();

            if (sale == null)
            {
                return NotFound(new { message = "Venta no encontrada" });
            }

            // Obtener movimientos de stock asociados a esta venta
            var stockMovements = await _context.Stocks
                .Include(s => s.Product)
                .Include(s => s.FlowType)
                .Where(s => s.SaleId == id)
                .OrderBy(s => s.Date)
                .Select(s => new
                {
                    id = s.Id,
                    productId = s.ProductId,
                    productName = s.Product.Name,
                    amount = s.Amount,
                    cost = s.Cost,
                    date = s.Date,
                    flowType = new { id = s.FlowType.Id, name = s.FlowType.Name },
                    notes = s.Notes,
                    isOutbound = s.Amount < 0,
                    movementType = s.Amount < 0 ? "Salida por venta" : "Entrada"
                })
                .ToListAsync();

            var result = new
            {
                id = sale.Id,
                storeId = sale.StoreId,
                storeName = sale.Store?.Name,
                storeLocation = sale.Store?.Location,
                businessId = sale.Store?.BusinessId,
                businessName = sale.Store?.Business?.CompanyName,
                date = sale.Date,
                customerName = sale.CustomerName,
                customerRut = sale.CustomerRut,
                total = sale.Total,
                paymentMethod = sale.PaymentMethod != null ? new { id = sale.PaymentMethod.Id, name = sale.PaymentMethod.Name } : null,
                notes = sale.Notes,
                items = sale.SaleDetails.Select(sd => new
                {
                    productId = sd.ProductId,
                    productName = sd.Product.Name,
                    quantity = sd.AmountAsInt,
                    unitPrice = sd.Price,
                    subtotal = sd.Subtotal,
                    discount = sd.Discount,
                    stockId = sd.StockId,
                    stockSource = sd.StockId.HasValue ? "Lote específico" : "Stock general",
                    stockInfo = sd.Stock != null ? new
                    {
                        id = sd.Stock.Id,
                        originalAmount = sd.Stock.Amount,
                        cost = sd.Stock.Cost,
                        date = sd.Stock.Date,
                        notes = sd.Stock.Notes,
                        flowType = sd.Stock.FlowType != null ? new { id = sd.Stock.FlowType.Id, name = sd.Stock.FlowType.Name } : null
                    } : null
                }),
                stockMovements = stockMovements
            };

            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al obtener venta: {id}", id);
            return StatusCode(500, new { message = "Error al obtener la venta", details = ex.Message });
        }
    }

    /// <summary>
    /// Obtiene los movimientos de stock asociados a una venta específica
    /// </summary>
    /// <param name="saleId">ID de la venta</param>
    /// <returns>Lista de movimientos de stock de la venta</returns>
    [HttpGet("{saleId}/stock-movements")]
    [Authorize]
    [ProducesResponseType(200)]
    [ProducesResponseType(401)]
    [ProducesResponseType(404)]
    [ProducesResponseType(500)]
    public async Task<ActionResult<object>> GetSaleStockMovements(int saleId)
    {
        try
        {
            _logger.LogInformation("Obteniendo movimientos de stock para venta: {saleId}", saleId);

            // Verificar que la venta existe
            var sale = await _context.Sales
                .Include(s => s.Store)
                    .ThenInclude(st => st.Business)
                .FirstOrDefaultAsync(s => s.Id == saleId);

            if (sale == null)
            {
                return NotFound(new { message = "Venta no encontrada" });
            }

            // Obtener movimientos de stock asociados a esta venta
            var stockMovements = await _context.Stocks
                .Include(s => s.Product)
                .Include(s => s.FlowType)
                .Include(s => s.Store)
                .Where(s => s.SaleId == saleId)
                .OrderBy(s => s.Date)
                .ThenBy(s => s.ProductId)
                .Select(s => new
                {
                    id = s.Id,
                    productId = s.ProductId,
                    productName = s.Product.Name,
                    productSku = s.Product.Sku,
                    amount = s.Amount,
                    cost = s.Cost,
                    date = s.Date,
                    flowType = new { id = s.FlowType.Id, name = s.FlowType.Name },
                    storeId = s.StoreId,
                    storeName = s.Store.Name,
                    notes = s.Notes
                })
                .ToListAsync();

            var result = new
            {
                saleId = sale.Id,
                saleDate = sale.Date,
                storeId = sale.StoreId,
                storeName = sale.Store?.Name,
                businessId = sale.Store?.BusinessId,
                businessName = sale.Store?.Business?.CompanyName,
                totalMovements = stockMovements.Count,
                stockMovements = stockMovements
            };

            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al obtener movimientos de stock de la venta: {saleId}", saleId);
            return StatusCode(500, new { message = "Error interno del servidor" });
        }
    }

    /// <summary>
    /// Obtiene todos los stores activos de un business para selección
    /// </summary>
    /// <param name="businessId">ID del negocio</param>
    /// <returns>Lista de stores activos</returns>
    [HttpGet("stores/{businessId}")]
    [Authorize]
    [ProducesResponseType(200)]
    [ProducesResponseType(401)]
    [ProducesResponseType(404)]
    [ProducesResponseType(500)]
    public async Task<ActionResult<IEnumerable<object>>> GetStoresByBusiness(int businessId)
    {
        try
        {
            _logger.LogInformation("Obteniendo stores para business: {businessId}", businessId);

            var business = await _context.Businesses.FindAsync(businessId);
            if (business == null)
            {
                return NotFound(new { message = "Negocio no encontrado" });
            }

            var stores = await _context.Stores
                .Where(s => s.BusinessId == businessId && s.Active)
                .OrderBy(s => s.Name)
                .Select(s => new
                {
                    id = s.Id,
                    name = s.Name,
                    location = s.Location,
                    businessId = s.BusinessId
                })
                .ToListAsync();

            // Si no hay stores, crear uno por defecto
            if (!stores.Any())
            {
                var defaultStore = await GetOrCreateDefaultStore(businessId);
                return Ok(new[]
                {
                    new
                    {
                        id = defaultStore.Id,
                        name = defaultStore.Name,
                        location = defaultStore.Location,
                        businessId = defaultStore.BusinessId
                    }
                });
            }

            return Ok(stores);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al obtener stores del business: {businessId}", businessId);
            return StatusCode(500, new { message = "Error interno del servidor" });
        }
    }

    /// <summary>
    /// Obtiene todas las ventas de un store específico con paginación
    /// </summary>
    /// <param name="storeId">ID del store</param>
    /// <param name="dateFrom">Fecha desde (opcional)</param>
    /// <param name="dateTo">Fecha hasta (opcional)</param>
    /// <param name="page">Número de página (por defecto 1)</param>
    /// <param name="pageSize">Tamaño de página (por defecto 10, máximo 50)</param>
    /// <returns>Lista paginada de ventas del store</returns>
    [HttpGet("store/{storeId}")]
    [Authorize]
    [ProducesResponseType(200)]
    [ProducesResponseType(401)]
    [ProducesResponseType(404)]
    [ProducesResponseType(500)]
    public async Task<ActionResult<object>> GetSalesByStore(
        int storeId, 
        [FromQuery] DateTime? dateFrom = null, 
        [FromQuery] DateTime? dateTo = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10)
    {
        try
        {
            _logger.LogInformation("Obteniendo ventas para store: {storeId}, página: {page}, tamaño: {pageSize}", storeId, page, pageSize);

            // Validar parámetros de paginación
            if (page < 1) page = 1;
            if (pageSize < 1) pageSize = 10;
            if (pageSize > 50) pageSize = 50; // Limitar tamaño máximo

            // Verificar que el store existe
            var store = await _context.Stores
                .Include(s => s.Business)
                .FirstOrDefaultAsync(s => s.Id == storeId);

            if (store == null)
            {
                return NotFound(new { message = "Store no encontrado" });
            }

            var query = _context.Sales
                .Include(s => s.PaymentMethod)
                .Include(s => s.SaleDetails)
                    .ThenInclude(sd => sd.Product)
                .Where(s => s.StoreId == storeId);

            if (dateFrom.HasValue)
            {
                query = query.Where(s => s.Date >= dateFrom.Value);
            }

            if (dateTo.HasValue)
            {
                query = query.Where(s => s.Date <= dateTo.Value);
            }

            // Contar total de registros para paginación
            var totalCount = await query.CountAsync();
            var totalPages = (int)Math.Ceiling((double)totalCount / pageSize);

            // Aplicar paginación
            var salesData = await query
                .OrderByDescending(s => s.Date)
                .ThenByDescending(s => s.Id)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Include(s => s.SaleDetails)
                    .ThenInclude(sd => sd.Product)
                .Include(s => s.PaymentMethod)
                .ToListAsync();

            var sales = salesData.Select(s => new
            {
                id = s.Id,
                date = s.Date,
                customerName = s.CustomerName,
                customerRut = s.CustomerRut,
                total = s.Total,
                paymentMethod = s.PaymentMethod != null ? new { id = s.PaymentMethod.Id, name = s.PaymentMethod.Name } : null,
                itemsCount = s.SaleDetails.Count,
                totalQuantity = s.SaleDetails.Sum(sd => int.TryParse(sd.Amount, out var amount) ? amount : 0),
                notes = s.Notes,
                details = s.SaleDetails.Select(sd => new
                {
                    productId = sd.ProductId,
                    productName = sd.Product.Name,
                    productSku = sd.Product.Sku,
                    quantity = int.TryParse(sd.Amount, out var amount) ? amount : 0,
                    unitPrice = sd.Price,
                    discount = sd.Discount ?? 0,
                    subtotal = (int.TryParse(sd.Amount, out var qty) ? qty : 0) * sd.Price - (sd.Discount ?? 0)
                }).OrderBy(d => d.productName).ToList()
            }).ToList();

            // Calcular totales (solo para la información general, no paginada)
            var totalAmount = await query.SumAsync(s => s.Total);

            var result = new
            {
                storeId = store.Id,
                storeName = store.Name,
                storeLocation = store.Location,
                businessId = store.BusinessId,
                businessName = store.Business?.CompanyName,
                totalSales = totalCount,
                totalAmount = totalAmount,
                dateFrom = dateFrom,
                dateTo = dateTo,
                pagination = new
                {
                    currentPage = page,
                    pageSize = pageSize,
                    totalPages = totalPages,
                    totalCount = totalCount,
                    hasNextPage = page < totalPages,
                    hasPreviousPage = page > 1
                },
                sales = sales
            };

            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al obtener ventas del store: {storeId}", storeId);
            return StatusCode(500, new { message = "Error interno del servidor" });
        }
    }

    /// <summary>
    /// Obtiene o crea un store por defecto para un business
    /// </summary>
    /// <param name="businessId">ID del negocio</param>
    /// <returns>Store por defecto</returns>
    private async Task<GPInventory.Domain.Entities.Store> GetOrCreateDefaultStore(int businessId)
    {
        // Buscar store existente para el business
        var existingStore = await _context.Stores
            .FirstOrDefaultAsync(s => s.BusinessId == businessId);

        if (existingStore != null)
        {
            return existingStore;
        }

        // Si no existe, crear uno por defecto
        var business = await _context.Businesses.FindAsync(businessId);
        var storeName = business?.CompanyName ?? "Store Principal";

        var newStore = new GPInventory.Domain.Entities.Store
        {
            Name = storeName,
            BusinessId = businessId,
            Active = true
        };

        _context.Stores.Add(newStore);
        await _context.SaveChangesAsync();

        _logger.LogInformation("Store por defecto creado: {storeName} para business: {businessId}", storeName, businessId);
        return newStore;
    }

    #region Helper Methods

    private int GetCurrentUserId()
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);
        if (userIdClaim == null || !int.TryParse(userIdClaim.Value, out int userId))
        {
            throw new UnauthorizedAccessException("User ID not found in token");
        }
        return userId;
    }

    /// <summary>
    /// Verifica si el negocio ha alcanzado el punto de equilibrio y envía notificación
    /// </summary>
    /// <param name="businessId">ID del negocio</param>
    /// <param name="businessName">Nombre del negocio</param>
    private async Task CheckAndSendBreakevenNotificationAsync(int businessId, string businessName)
    {
        try
        {
            var today = DateTime.UtcNow.Date;
            var tomorrow = today.AddDays(1);

            // Calcular ingresos del día (sum de todas las ventas del día)
            var dailyRevenue = await _context.Sales
                .Where(s => s.Store.BusinessId == businessId && 
                           s.Date >= today && s.Date < tomorrow)
                .SumAsync(s => s.Total);

            // Calcular costos del día (sum de todos los stocks con costo del día)
            // Solo considerar movimientos de entrada (compras/reposición) del día
            var dailyCosts = await _context.Stocks
                .Where(s => s.Product.BusinessId == businessId && 
                           s.Cost.HasValue && 
                           s.Amount > 0 && 
                           s.Date >= today && s.Date < tomorrow)
                .SumAsync(s => s.Cost!.Value * s.Amount);

            // Verificar si se alcanzó el punto de equilibrio del día
            if (dailyRevenue >= dailyCosts && dailyCosts > 0)
            {
                // Verificar si ya se envió esta notificación para el día de hoy
                var existingNotification = await _context.UserNotifications
                    .Include(un => un.Notification)
                    .Where(un => un.Notification.Type == "breakeven_achievement" && 
                                (un.RenderedMessage ?? "").Contains($"Negocio: {businessName}") &&
                                un.CreatedAt >= today && un.CreatedAt < tomorrow)
                    .AnyAsync();

                if (!existingNotification)
                {
                    await _notificationService.SendBreakevenNotificationAsync(
                        businessId, 
                        businessName, 
                        dailyRevenue, 
                        dailyCosts
                    );
                    
                    _logger.LogInformation("Daily breakeven notification sent for business {businessId}: Daily Revenue={revenue}, Daily Costs={costs}", 
                        businessId, dailyRevenue, dailyCosts);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error checking daily breakeven for business {businessId}", businessId);
        }
    }

    /// <summary>
    /// Procesa una venta aplicando FIFO automáticamente (First In, First Out)
    /// </summary>
    /// <param name="request">Datos de la venta</param>
    /// <returns>Venta procesada con detalles de los lotes consumidos</returns>
    [HttpPost("fifo-sale")]
    [Authorize]
    [ProducesResponseType(201)]
    [ProducesResponseType(400)]
    [ProducesResponseType(401)]
    [ProducesResponseType(500)]
    public async Task<ActionResult<object>> ProcessFifoSale([FromBody] FifoSaleRequest request)
    {
        using var transaction = await _context.Database.BeginTransactionAsync();
        
        try
        {
            // Validación del request
            if (request == null)
            {
                return BadRequest(new { message = "Request no puede ser null" });
            }

            // Validaciones básicas
            if (request.StoreId <= 0)
            {
                return BadRequest(new { message = "ID de store inválido" });
            }

            if (request.Items == null || !request.Items.Any())
            {
                return BadRequest(new { message = "La venta debe tener al menos un producto" });
            }

            // Validar que no haya elementos null en la lista
            if (request.Items.Any(i => i == null))
            {
                return BadRequest(new { message = "La lista de productos contiene elementos inválidos" });
            }

            _logger.LogInformation("🔄 Procesando venta FIFO para store: {storeId}", request.StoreId);

            // Verificar que el store existe y está activo
            var store = await _context.Stores
                .Include(s => s.Business)
                .FirstOrDefaultAsync(s => s.Id == request.StoreId);
            
            if (store == null)
            {
                return BadRequest(new { message = "El store especificado no existe" });
            }

            if (!store.Active)
            {
                return BadRequest(new { message = "El store especificado no está activo" });
            }

            // Verificar que todos los productos existen y pertenecen al mismo business del store
            // Usar una lista temporal para evitar problemas con LINQ
            var tempProductIds = new List<int>();
            foreach (var item in request.Items)
            {
                if (item != null && item.ProductId > 0)
                {
                    tempProductIds.Add(item.ProductId);
                }
            }
            var productIds = tempProductIds.Distinct().ToList();
            
            var products = await _context.Products
                .Where(p => productIds.Contains(p.Id) && p.BusinessId == store.BusinessId)
                .ToListAsync();

            if (products.Count != productIds.Count)
            {
                return BadRequest(new { message = "Uno o más productos no existen o no pertenecen al negocio del store" });
            }

            // Validar stock disponible y aplicar FIFO para cada producto
            var fifoAllocations = new List<object>();
            
            foreach (var item in request.Items.ToList())
            {
                // Validaciones defensivas para evitar null reference
                if (item == null)
                {
                    _logger.LogError("❌ Item is null in request.Items during FIFO validation");
                    continue;
                }

                if (item.ProductId <= 0)
                {
                    _logger.LogError("❌ Invalid ProductId: {productId}", item.ProductId);
                    continue;
                }

                var product = products.FirstOrDefault(p => p.Id == item.ProductId);
                if (product == null)
                {
                    _logger.LogError("❌ Product not found for ProductId: {productId}", item.ProductId);
                    continue;
                }
                
                // Usar consulta SQL directa para evitar problemas con valores NULL
                List<GPInventory.Domain.Entities.Stock> availableStocks;
                
                _logger.LogInformation("🔍 Obteniendo stocks FIFO para producto {productId} en store {storeId}", item.ProductId, request.StoreId);
                
                // Obtener SOLO lotes de ENTRADA (amount > 0) activos para aplicar FIFO correctamente
                // NO incluir movimientos negativos en la selección FIFO
                var stocksRawQuery = await _context.Database.SqlQueryRaw<StockRawResult>(
                    @"SELECT
                        s.id                          AS Id,
                        COALESCE(s.product, 0)        AS ProductId,
                        COALESCE(s.`date`, NOW())     AS `Date`,
                        COALESCE(s.`flow`, 1)         AS FlowTypeId,
                        s.amount                      AS Amount,
                        s.cost                        AS Cost,
                        s.provider                    AS ProviderId,
                        s.notes                       AS Notes,
                        s.id_store                    AS StoreId,
                        s.sale_id                     AS SaleId,
                        s.stock_id                    AS StockId,
                        COALESCE(s.active, 0)         AS IsActive,
                        COALESCE(s.created_at, NOW()) AS CreatedAt,
                        COALESCE(s.updated_at, NOW()) AS UpdatedAt
                    FROM stock s
                    WHERE COALESCE(s.product, 0) = {0}
                    AND s.id_store = {1}
                    AND s.amount > 0
                    AND COALESCE(s.active, 0) = 1
                    ORDER BY COALESCE(s.`date`, NOW()) ASC, s.id ASC;",
                    item.ProductId, request.StoreId).ToListAsync();

                // Convertir los resultados SQL a objetos Stock
                availableStocks = stocksRawQuery.Select(sr => new GPInventory.Domain.Entities.Stock
                {
                    Id = sr.Id,
                    ProductId = sr.ProductId ?? 0, // Usar 0 como fallback si es NULL
                    Date = sr.Date ?? DateTime.UtcNow, // Usar fecha actual como fallback
                    FlowTypeId = sr.FlowTypeId ?? 1, // Usar FlowType por defecto
                    Amount = sr.Amount,
                    Cost = sr.Cost.HasValue && sr.Cost.Value > 0 ? sr.Cost.Value : null,
                    ProviderId = sr.ProviderId.HasValue && sr.ProviderId.Value > 0 ? sr.ProviderId.Value : null,
                    Notes = string.IsNullOrEmpty(sr.Notes) ? null : sr.Notes,
                    StoreId = sr.StoreId,
                    SaleId = sr.SaleId.HasValue && sr.SaleId.Value > 0 ? sr.SaleId.Value : null,
                    StockId = sr.StockId,
                    IsActive = sr.IsActive.HasValue ? sr.IsActive.Value == 1 : true,
                    CreatedAt = sr.CreatedAt ?? DateTime.UtcNow,
                    UpdatedAt = sr.UpdatedAt ?? DateTime.UtcNow
                }).ToList();

                _logger.LogInformation("✅ Encontrados {stockCount} lotes disponibles para producto {productId}", availableStocks.Count, item.ProductId);

                // Calcular stock real disponible considerando las ventas previas
                var stocksWithAvailable = new List<(GPInventory.Domain.Entities.Stock Stock, int Available)>();
                
                foreach (var stock in availableStocks)
                {
                    var saleDetailsForStock = await _context.SaleDetails
                        .Where(sd => sd.StockId == stock.Id)
                        .Select(sd => sd.Amount)
                        .ToListAsync();
                        
                    var stockUsedInSales = saleDetailsForStock.Sum(amount => int.Parse(amount));
                    var availableInLot = stock.Amount - stockUsedInSales;
                    
                    if (availableInLot > 0)
                    {
                        stocksWithAvailable.Add((stock, availableInLot));
                    }
                }

                var totalAvailable = stocksWithAvailable.Sum(s => s.Available);
                
                if (totalAvailable < item.Quantity)
                {
                    return BadRequest(new { 
                        message = $"Stock insuficiente para {product.Name}. Disponible: {totalAvailable}, Solicitado: {item.Quantity}",
                        productId = item.ProductId,
                        available = totalAvailable,
                        requested = item.Quantity
                    });
                }

                // Aplicar FIFO: asignar cantidades desde los lotes más antiguos
                var remainingToAllocate = item.Quantity;
                var allocations = new List<object>();
                
                foreach (var (stock, available) in stocksWithAvailable)
                {
                    if (remainingToAllocate <= 0) break;
                    
                    var toAllocate = Math.Min(remainingToAllocate, available);
                    
                    allocations.Add(new
                    {
                        stockId = stock.Id,
                        stockDate = stock.Date,
                        originalAmount = stock.Amount,
                        availableBeforeSale = available,
                        allocatedQuantity = toAllocate,
                        cost = stock.Cost,
                        notes = stock.Notes
                    });
                    
                    remainingToAllocate -= toAllocate;
                    
                    _logger.LogInformation("📦 FIFO - Producto {productId}: Lote {stockId} ({stockDate:yyyy-MM-dd}) - Asignando {allocated} de {available} disponibles",
                        item.ProductId, stock.Id, stock.Date, toAllocate, available);
                }
                
                if (remainingToAllocate > 0)
                {
                    return BadRequest(new { 
                        message = $"Error en asignación FIFO para {product.Name}. No se pudo asignar completamente la cantidad solicitada.",
                        productId = item.ProductId,
                        remainingToAllocate = remainingToAllocate
                    });
                }
                
                fifoAllocations.Add(new
                {
                    productId = item.ProductId,
                    productName = product.Name,
                    requestedQuantity = item.Quantity,
                    totalAllocated = allocations.Sum(a => ((dynamic)a).allocatedQuantity),
                    allocations = allocations
                });
            }

            _logger.LogInformation("✅ FIFO validation passed. Creating sale...");

            // Crear la venta
            var sale = new GPInventory.Domain.Entities.Sale
            {
                StoreId = request.StoreId,
                Date = DateTime.UtcNow,
                CustomerName = request.CustomerName?.Trim(),
                CustomerRut = request.CustomerRut?.Trim(),
                PaymentMethodId = request.PaymentMethodId,
                Notes = request.Notes?.Trim(),
                Total = 0 // Se calculará después
            };

            _context.Sales.Add(sale);
            await _context.SaveChangesAsync(); // Para obtener el ID de la venta

            // Crear detalles de venta basados en las asignaciones FIFO
            int totalAmount = 0;
            var saleDetails = new List<object>();

            // Filtrar items válidos antes del loop para evitar problemas
            var validItems = request.Items?.Where(i => i != null && i.ProductId > 0).ToList() ?? new List<FifoSaleItem>();
            
            if (!validItems.Any())
            {
                _logger.LogWarning("⚠️ No hay items válidos para procesar");
                return BadRequest(new { message = "No hay productos válidos para procesar en la venta" });
            }
            
            foreach (var item in validItems)
            {
                try
                {
                    // Logging detallado para debugging
                    _logger.LogInformation("🔍 Procesando item: {item}", item?.ToString() ?? "NULL");
                    
                    // Validaciones defensivas para evitar null reference (aunque ya fueron filtrados)
                    if (item == null)
                    {
                        _logger.LogError("❌ Item is null in request.Items");
                        continue;
                    }

                    _logger.LogInformation("🔍 ProductId: {productId}", item.ProductId);
                    
                    if (item.ProductId <= 0)
                    {
                        _logger.LogError("❌ Invalid ProductId: {productId}", item.ProductId);
                        continue;
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "❌ Error accediendo a las propiedades del item");
                    continue;
                }

                var product = products.FirstOrDefault(p => p.Id == item.ProductId);
                if (product == null)
                {
                    _logger.LogError("❌ Product not found for ProductId: {productId}", item.ProductId);
                    continue;
                }

                var unitPrice = item.UnitPrice ?? product.Price;
                var subtotal = unitPrice * item.Quantity;
                totalAmount += subtotal;
                
                // Guardar referencias locales para evitar problemas de contexto
                var currentItemProductId = item.ProductId;
                var currentItemQuantity = item.Quantity;
                var currentItemDiscount = item.Discount;
                
                var fifoAllocation = fifoAllocations.FirstOrDefault(fa => ((dynamic)fa).productId == currentItemProductId);
                if (fifoAllocation == null)
                {
                    _logger.LogError("❌ No FIFO allocation found for ProductId: {productId}", currentItemProductId);
                    continue;
                }
                var allocations = ((dynamic)fifoAllocation).allocations;
                
                // Validar que existan asignaciones
                if (allocations == null)
                {
                    _logger.LogError("❌ No allocations found for ProductId: {productId}", currentItemProductId);
                    continue;
                }
                
                // Crear un SaleDetail por cada lote asignado
                var itemDetails = new List<object>();
                
                foreach (dynamic allocation in allocations)
                {
                    // Validación defensiva para allocation
                    if (allocation == null)
                    {
                        _logger.LogError("❌ Allocation is null for ProductId: {productId}", currentItemProductId);
                        continue;
                    }
                    
                    var allocatedQty = (int)allocation.allocatedQuantity;
                    var stockId = (int)allocation.stockId;
                    var stockCost = allocation.cost;
                    
                    // Convertir stockCost de manera segura
                    decimal? costValue = null;
                    if (stockCost != null)
                    {
                        if (stockCost is decimal decimalCost)
                        {
                            costValue = decimalCost;
                        }
                        else if (decimal.TryParse(stockCost.ToString(), out decimal parsedCost))
                        {
                            costValue = parsedCost;
                        }
                    }
                    
                    var proportionalSubtotal = (int)Math.Round((decimal)subtotal * allocatedQty / currentItemQuantity);
                    var proportionalDiscount = currentItemDiscount.HasValue ? 
                        (int)Math.Round((decimal)currentItemDiscount.Value * allocatedQty / currentItemQuantity) : 0;
                    
                    // Crear SaleDetail usando SQL directo
                    await _context.Database.ExecuteSqlRawAsync(
                        @"INSERT INTO sales_detail (product, amount, price, discount, sale, stock_id) 
                          VALUES ({0}, {1}, {2}, {3}, {4}, {5})",
                        currentItemProductId,
                        allocatedQty.ToString(),
                        unitPrice,
                        proportionalDiscount > 0 ? proportionalDiscount : 0,
                        sale.Id,
                        stockId);

                    // Crear movimiento de stock negativo (salida por venta FIFO) usando SQL directo
                    // Este movimiento DEBE tener stock_id apuntando al lote padre del cual se saca el stock
                    var stockNotes = $"Venta FIFO #{sale.Id} - Desde lote #{stockId}";
                    
                    _logger.LogInformation("📦 Creando movimiento de stock NEGATIVO: Producto={productId}, Cantidad={qty}, StockPadre={stockId}, Venta={saleId}",
                        currentItemProductId, -allocatedQty, stockId, sale.Id);
                    
                    await _context.Database.ExecuteSqlRawAsync(
                        @"INSERT INTO stock (product, `date`, `flow`, amount, cost, id_store, sale_id, stock_id, notes, active, created_at, updated_at) 
                          VALUES ({0}, {1}, {2}, {3}, {4}, {5}, {6}, {7}, {8}, {9}, {10}, {11})",
                        currentItemProductId,
                        DateTime.UtcNow,
                        11, // FlowType "Venta"
                        -allocatedQty, // Cantidad negativa para salida
                        costValue.HasValue ? (object)costValue.Value : DBNull.Value,
                        request.StoreId,
                        sale.Id,
                        stockId, // ✅ stock_id apunta al lote padre del cual se saca el inventario
                        stockNotes,
                        1, // active = 1 (activo)
                        DateTime.UtcNow,
                        DateTime.UtcNow);
                    
                    _logger.LogInformation("✅ Movimiento de stock negativo creado exitosamente para lote #{stockId}", stockId);

                    // Verificar si el stock padre se agotó completamente y marcarlo como inactivo
                    // Usar SQL directo para evitar problemas con valores NULL
                    var parentStockRaw = await _context.Database.SqlQueryRaw<StockRawResult>(
                        @"SELECT
                            s.id                          AS Id,
                            COALESCE(s.product, 0)        AS ProductId,
                            COALESCE(s.`date`, NOW())     AS `Date`,
                            COALESCE(s.`flow`, 1)         AS FlowTypeId,
                            s.amount                      AS Amount,
                            s.cost                        AS Cost,
                            s.provider                    AS ProviderId,
                            s.notes                       AS Notes,
                            s.id_store                    AS StoreId,
                            s.sale_id                     AS SaleId,
                            s.stock_id                    AS StockId,
                            COALESCE(s.active, 0)         AS IsActive,
                            COALESCE(s.created_at, NOW()) AS CreatedAt,
                            COALESCE(s.updated_at, NOW()) AS UpdatedAt
                        FROM stock s
                        WHERE s.id = {0}", stockId).ToListAsync();
                    
                    if (parentStockRaw.Any())
                    {
                        var parentStockData = parentStockRaw.First();
                        // Calcular el total usado de este lote incluyendo esta venta
                        var saleDetailsForLot = await _context.SaleDetails
                            .Where(sd => sd.StockId == stockId)
                            .Select(sd => sd.Amount)
                            .ToListAsync();
                        
                        var totalUsedFromLot = saleDetailsForLot.Sum(amount => int.Parse(amount));
                        
                        // Sumar la cantidad que se está vendiendo ahora
                        totalUsedFromLot += allocatedQty;
                        
                        // Si se agotó el lote completamente, marcarlo como inactivo usando SQL directo
                        if (totalUsedFromLot >= parentStockData.Amount)
                        {
                            await _context.Database.ExecuteSqlRawAsync(
                                "UPDATE stock SET active = 0 WHERE id = {0}", stockId);
                            _logger.LogInformation("🔄 Stock lote #{stockId} agotado completamente, marcado como inactivo", stockId);
                        }
                    }
                    
                    itemDetails.Add(new
                    {
                        stockId = stockId,
                        stockDate = allocation.stockDate,
                        quantity = allocatedQty,
                        unitPrice = unitPrice,
                        subtotal = proportionalSubtotal,
                        discount = proportionalDiscount > 0 ? proportionalDiscount : (int?)null,
                        cost = costValue
                    });
                    
                    _logger.LogInformation("💰 Sale detail created: Product {productId}, Stock {stockId}, Qty {qty}, Price {price}",
                        currentItemProductId, stockId, allocatedQty, unitPrice);
                }
                
                saleDetails.Add(new
                {
                    productId = currentItemProductId,
                    productName = product.Name,
                    totalQuantity = currentItemQuantity,
                    unitPrice = unitPrice,
                    totalSubtotal = subtotal,
                    totalDiscount = currentItemDiscount ?? 0,
                    lotsUsed = itemDetails.Count,
                    details = itemDetails
                });
            }

            // Actualizar total de la venta usando SQL directo con parámetros seguros
            await _context.Database.ExecuteSqlRawAsync(
                "UPDATE sales SET total = {0} WHERE id = {1}", totalAmount, sale.Id);

            // Actualizar el objeto sale en memoria para que refleje el total correcto
            sale.Total = totalAmount;

            await transaction.CommitAsync();

            _logger.LogInformation("🎉 Venta FIFO procesada exitosamente: {saleId}, Total: ${total}", sale.Id, totalAmount);

            var result = new
            {
                saleId = sale.Id,
                storeId = sale.StoreId,
                storeName = store.Name,
                businessId = store.BusinessId,
                businessName = store.Business?.CompanyName,
                date = sale.Date,
                customerName = sale.CustomerName,
                customerRut = sale.CustomerRut,
                total = totalAmount, // Usar totalAmount directamente en lugar de sale.Total
                paymentMethodId = sale.PaymentMethodId,
                notes = sale.Notes,
                fifoDetails = fifoAllocations,
                items = saleDetails,
                message = "Venta procesada exitosamente aplicando FIFO automático"
            };

            return CreatedAtAction(nameof(GetSale), new { id = sale.Id }, result);
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync();
            _logger.LogError(ex, "❌ Error al procesar venta FIFO");
            return StatusCode(500, new { message = "Error al procesar la venta FIFO", details = ex.Message });
        }
    }

    #endregion
}

/// <summary>
/// Modelo para venta rápida
/// </summary>
public class QuickSaleRequest
{
    /// <summary>
    /// ID del store donde se realiza la venta
    /// </summary>
    public int StoreId { get; set; }

    /// <summary>
    /// Nombre del cliente (opcional)
    /// </summary>
    public string? CustomerName { get; set; }

    /// <summary>
    /// RUT del cliente (opcional)
    /// </summary>
    public string? CustomerRut { get; set; }

    /// <summary>
    /// ID del método de pago (opcional)
    /// </summary>
    public int? PaymentMethodId { get; set; }

    /// <summary>
    /// Notas adicionales (opcional)
    /// </summary>
    public string? Notes { get; set; }

    /// <summary>
    /// Productos vendidos
    /// </summary>
    public List<QuickSaleItem> Items { get; set; } = new List<QuickSaleItem>();
}

/// <summary>
/// Modelo para item de venta rápida
/// </summary>
public class QuickSaleItem
{
    /// <summary>
    /// ID del producto
    /// </summary>
    public int ProductId { get; set; }

    /// <summary>
    /// Cantidad vendida
    /// </summary>
    public int Quantity { get; set; }

    /// <summary>
    /// Precio unitario (opcional, usa el del producto si no se especifica)
    /// </summary>
    public int? UnitPrice { get; set; }

    /// <summary>
    /// Descuento aplicado (opcional)
    /// </summary>
    public int? Discount { get; set; }

    /// <summary>
    /// ID del stock específico del cual se está vendiendo el producto (opcional)
    /// </summary>
    public int? StockId { get; set; }
}

/// <summary>
/// Modelo para venta con FIFO automático
/// </summary>
public class FifoSaleRequest
{
    /// <summary>
    /// ID del store donde se realiza la venta
    /// </summary>
    public int StoreId { get; set; }

    /// <summary>
    /// Nombre del cliente (opcional)
    /// </summary>
    public string? CustomerName { get; set; }

    /// <summary>
    /// RUT del cliente (opcional)
    /// </summary>
    public string? CustomerRut { get; set; }

    /// <summary>
    /// ID del método de pago
    /// </summary>
    public int PaymentMethodId { get; set; }

    /// <summary>
    /// Lista de productos a vender (sin especificar StockId)
    /// </summary>
    public List<FifoSaleItem> Items { get; set; } = new();

    /// <summary>
    /// Notas adicionales de la venta (opcional)
    /// </summary>
    public string? Notes { get; set; }
}

/// <summary>
/// Item de venta con FIFO automático
/// </summary>
public class FifoSaleItem
{
    /// <summary>
    /// ID del producto
    /// </summary>
    public int ProductId { get; set; }

    /// <summary>
    /// Cantidad a vender
    /// </summary>
    public int Quantity { get; set; }

    /// <summary>
    /// Precio unitario (opcional, se usará el precio del producto si no se especifica)
    /// </summary>
    public int? UnitPrice { get; set; }

    /// <summary>
    /// Descuento aplicado (opcional)
    /// </summary>
    public int? Discount { get; set; }
}

/// <summary>
/// Clase para mapear IDs de consultas SQL
/// </summary>
public class IdResult
{
    public int Id { get; set; }
}

/// <summary>
/// Clase para mapear resultados SQL directos de la tabla stock
/// </summary>
public class StockRawResult
{
    public int Id { get; set; }
    public int? ProductId { get; set; }
    public DateTime? Date { get; set; }
    public int? FlowTypeId { get; set; }
    public int Amount { get; set; }
    public int? Cost { get; set; }
    public int? ProviderId { get; set; }
    public string? Notes { get; set; }
    public int StoreId { get; set; }
    public int? SaleId { get; set; }
    public int? StockId { get; set; }
    public int? IsActive { get; set; }
    public DateTime? CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}

/// <summary>
/// Clase para mapear resultados de consulta SQL de producto con stock
/// </summary>
public class ProductWithStockResult
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Sku { get; set; }
    public int Price { get; set; }
    public int Cost { get; set; }
    public string? Image { get; set; }
    public int? ProductTypeId { get; set; }
    public string? ProductTypeName { get; set; }
    public int BusinessId { get; set; }
    public string? BusinessCompanyName { get; set; }
    public int MinimumStock { get; set; }
    public int CurrentStock { get; set; }
    public decimal? AverageCost { get; set; }
}

/// <summary>
/// Clase extendida para mapear resultados de consulta SQL de store con info del business
/// </summary>
public class StoreWithBusinessResult
{
    public int Id { get; set; }
    public string? Name { get; set; }
    public string? Location { get; set; }
    public int BusinessId { get; set; }
    public bool Active { get; set; }
    public string? BusinessCompanyName { get; set; }
}

public class Item
{

/*     Discount[int ?] =
0
ProductId[int] =
5
Quantity[int] =
11
UnitPrice[int ?] =
3000 */

    public int? Discount { get; set; }
    public int ProductId { get; set; }
    public int Quantity { get; set; }
    public int? UnitPrice { get; set; }
}