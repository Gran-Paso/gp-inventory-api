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

            // Verificar que el store existe y está activo
            var store = await _context.Stores
                .Include(s => s.Business)
                .FirstOrDefaultAsync(s => s.Id == storeId && s.Active);

            if (store == null)
            {
                return NotFound(new { message = "Store no encontrado o no está activo" });
            }

            // Obtener todos los productos del business del store
            var businessProducts = await _context.Products
                .Include(p => p.ProductType)
                .Include(p => p.Business)
                .Where(p => p.BusinessId == store.BusinessId)
                .ToListAsync();

            // Calcular stock para cada producto en el store específico
            var availableProducts = new List<object>();

            foreach (var product in businessProducts)
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
                    // Calcular precio promedio basado en ventas del store
                    var salesData = await _context.SaleDetails
                        .Include(sd => sd.Sale)
                        .Where(sd => sd.ProductId == product.Id && sd.Sale.StoreId == storeId)
                        .ToListAsync();

                    var averagePrice = salesData.Any() ? (decimal?)salesData.Average(s => s.Price) : null;

                    // Calcular costo promedio basado en movimientos de stock del store
                    var stockMovements = await _context.Stocks
                        .Where(s => s.ProductId == product.Id && s.StoreId == storeId && s.Cost.HasValue && s.Cost.Value > 0)
                        .ToListAsync();

                    decimal? averageCost = null;
                    if (stockMovements.Any())
                    {
                        var totalCostValue = stockMovements.Sum(s => (decimal)s.Amount * (decimal)s.Cost!.Value);
                        var totalQuantity = stockMovements.Sum(s => (decimal)s.Amount);
                        
                        if (totalQuantity > 0)
                        {
                            averageCost = totalCostValue / totalQuantity;
                        }
                    }

                    availableProducts.Add(new
                    {
                        id = product.Id,
                        name = product.Name,
                        sku = product.Sku,
                        price = product.Price,
                        cost = product.Cost,
                        image = product.Image,
                        currentStock = currentStock,
                        averagePrice = averagePrice.HasValue ? Math.Round(averagePrice.Value, 2) : (decimal?)null,
                        averageCost = averageCost.HasValue ? Math.Round(averageCost.Value, 2) : (decimal?)null,
                        productType = product.ProductType != null ? new { id = product.ProductType.Id, name = product.ProductType.Name } : null,
                        business = new { id = product.Business.Id, companyName = product.Business.CompanyName }
                    });
                }
            }

            var result = new
            {
                storeId = store.Id,
                storeName = store.Name,
                storeLocation = store.Location,
                businessId = store.BusinessId,
                businessName = store.Business?.CompanyName,
                totalAvailableProducts = availableProducts.Count,
                products = availableProducts.OrderBy(p => ((dynamic)p).name)
            };

            _logger.LogInformation($"Se encontraron {availableProducts.Count} productos disponibles en store {storeId}");
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al obtener productos disponibles del store: {storeId}", storeId);
            return StatusCode(500, new { message = "Error interno del servidor" });
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
                        Notes = $"Venta #{sale.Id} - Stock lote #{item.StockId.Value}"
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
                        Notes = $"Venta #{sale.Id}"
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
