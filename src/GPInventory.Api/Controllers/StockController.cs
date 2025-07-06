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

            // Obtener todos los productos del negocio con sus movimientos de stock
            var inventory = await _context.Products
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
                    // Calcular stock actual sumando todos los movimientos
                    currentStock = _context.Stocks
                        .Where(s => s.ProductId == p.Id)
                        .Sum(s => s.Amount),
                    // Calcular costo promedio ponderado
                    averageCost = _context.Stocks
                        .Where(s => s.ProductId == p.Id && s.Cost.HasValue && s.Amount > 0)
                        .Average(s => s.Cost) ?? 0,
                    // Calcular precio promedio ponderado
                    averagePrice = _context.Stocks
                        .Where(s => s.ProductId == p.Id && s.Cost.HasValue && s.Amount > 0)
                        .Average(s => s.Cost) ?? p.Cost,
                    // Contar total de movimientos
                    totalMovements = _context.Stocks
                        .Where(s => s.ProductId == p.Id)
                        .Count(),
                    // Fecha del último movimiento
                    lastMovementDate = _context.Stocks
                        .Where(s => s.ProductId == p.Id)
                        .OrderByDescending(s => s.Date)
                        .Select(s => s.Date)
                        .FirstOrDefault()
                })
                .OrderBy(p => p.name)
                .ToListAsync();

            _logger.LogInformation($"Se encontraron {inventory.Count} productos en el inventario del negocio {businessId}");
            return Ok(inventory);
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
