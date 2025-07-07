using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using GPInventory.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Cors;

namespace GPInventory.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[EnableCors("AllowFrontend")]
public class SalesController : ControllerBase
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<SalesController> _logger;

    public SalesController(ApplicationDbContext context, ILogger<SalesController> logger)
    {
        _context = context;
        _logger = logger;
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
    /// Busca un producto por ID para venta rápida
    /// </summary>
    /// <param name="productId">ID del producto</param>
    /// <param name="businessId">ID del negocio</param>
    /// <returns>Información del producto y stock disponible</returns>
    [HttpGet("products/{productId}")]
    [Authorize]
    [ProducesResponseType(200)]
    [ProducesResponseType(401)]
    [ProducesResponseType(404)]
    [ProducesResponseType(500)]
    public async Task<ActionResult<object>> GetProductForSale(int productId, [FromQuery] int businessId)
    {
        try
        {
            _logger.LogInformation("Buscando producto {productId} para venta en negocio {businessId}", productId, businessId);

            var product = await _context.Products
                .Include(p => p.ProductType)
                .Include(p => p.Business)
                .Where(p => p.Id == productId && p.BusinessId == businessId)
                .FirstOrDefaultAsync();

            if (product == null)
            {
                return NotFound(new { message = "Producto no encontrado o no pertenece al negocio" });
            }

            // Calcular stock actual
            var currentStock = await _context.Stocks
                .Where(s => s.ProductId == productId)
                .SumAsync(s => s.Amount);

            // Calcular precio promedio y costo promedio basado en las ventas
            var salesData = await _context.SaleDetails
                .Include(sd => sd.Sale)
                .Where(sd => sd.ProductId == productId)
                .Select(sd => new { sd.Price, Cost = _context.Stocks
                    .Where(s => s.ProductId == productId && s.Date <= sd.Sale.Date)
                    .OrderByDescending(s => s.Date)
                    .Select(s => s.Cost)
                    .FirstOrDefault() })
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
            if (request.BusinessId <= 0)
            {
                return BadRequest(new { message = "ID de negocio inválido" });
            }

            if (request.Items == null || !request.Items.Any())
            {
                return BadRequest(new { message = "La venta debe tener al menos un producto" });
            }

            _logger.LogInformation("Procesando venta rápida para negocio: {businessId}", request.BusinessId);

            // Verificar que el negocio existe
            var business = await _context.Businesses.FindAsync(request.BusinessId);
            if (business == null)
            {
                return BadRequest(new { message = "El negocio especificado no existe" });
            }

            // Verificar que todos los productos existen y tienen stock
            var productIds = request.Items.Select(i => i.ProductId).ToList();
            var products = await _context.Products
                .Where(p => productIds.Contains(p.Id) && p.BusinessId == request.BusinessId)
                .ToListAsync();

            if (products.Count != productIds.Count)
            {
                return BadRequest(new { message = "Uno o más productos no existen o no pertenecen al negocio" });
            }

            // Verificar stock disponible
            foreach (var item in request.Items)
            {
                var currentStock = await _context.Stocks
                    .Where(s => s.ProductId == item.ProductId)
                    .SumAsync(s => s.Amount);

                if (currentStock < item.Quantity)
                {
                    var product = products.First(p => p.Id == item.ProductId);
                    return BadRequest(new { message = $"Stock insuficiente para {product.Name}. Disponible: {currentStock}, Solicitado: {item.Quantity}" });
                }
            }

            // Crear la venta
            var sale = new GPInventory.Domain.Entities.Sale
            {
                BusinessId = request.BusinessId,
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
                    SaleId = sale.Id
                };

                _context.SaleDetails.Add(saleDetail);

                // Crear movimiento de stock (salida por venta)
                var stockMovement = new GPInventory.Domain.Entities.Stock
                {
                    ProductId = item.ProductId,
                    Date = DateTime.UtcNow,
                    FlowTypeId = 11, // FlowType "Venta"
                    Amount = -item.Quantity, // Cantidad negativa para salida
                    Cost = null, // No se especifica costo en las ventas
                    Notes = $"Venta rápida #{sale.Id}"
                };

                _context.Stocks.Add(stockMovement);

                saleDetails.Add(new
                {
                    productId = item.ProductId,
                    productName = product.Name,
                    quantity = item.Quantity,
                    unitPrice = unitPrice,
                    subtotal = subtotal,
                    discount = item.Discount
                });
            }

            // Actualizar total de la venta
            sale.Total = totalAmount;
            await _context.SaveChangesAsync();

            await transaction.CommitAsync();

            var result = new
            {
                saleId = sale.Id,
                businessId = sale.BusinessId,
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
            return StatusCode(500, new { message = "Error interno del servidor" });
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
                .Include(s => s.Business)
                .Include(s => s.PaymentMethod)
                .Include(s => s.SaleDetails)
                    .ThenInclude(sd => sd.Product)
                .Where(s => s.Id == id)
                .FirstOrDefaultAsync();

            if (sale == null)
            {
                return NotFound(new { message = "Venta no encontrada" });
            }

            var result = new
            {
                id = sale.Id,
                businessId = sale.BusinessId,
                businessName = sale.Business.CompanyName,
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
                    discount = sd.Discount
                })
            };

            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al obtener venta: {id}", id);
            return StatusCode(500, new { message = "Error interno del servidor" });
        }
    }
}

/// <summary>
/// Modelo para venta rápida
/// </summary>
public class QuickSaleRequest
{
    /// <summary>
    /// ID del negocio
    /// </summary>
    public int BusinessId { get; set; }

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
}
