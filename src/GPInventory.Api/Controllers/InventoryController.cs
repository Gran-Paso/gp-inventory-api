using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using GPInventory.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Cors;

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
    /// Obtiene el inventario con stock real calculado
    /// </summary>
    /// <param name="businessId">ID del negocio (opcional)</param>
    /// <param name="category">Categoría de productos (opcional)</param>
    /// <param name="search">Búsqueda por nombre (opcional)</param>
    /// <param name="status">Estado del stock (opcional)</param>
    /// <returns>Lista de productos con stock real</returns>
    [HttpGet]
    [Authorize]
    [ProducesResponseType(200)]
    [ProducesResponseType(401)]
    [ProducesResponseType(500)]
    public async Task<ActionResult<IEnumerable<object>>> GetInventory(
        [FromQuery] int? businessId = null,
        [FromQuery] string? category = null,
        [FromQuery] string? search = null,
        [FromQuery] string? status = null)
    {
        try
        {
            _logger.LogInformation("Obteniendo inventario con stock real");

            var productsQuery = _context.Products
                .Include(p => p.ProductType)
                .Include(p => p.Business)
                .AsQueryable();

            // Aplicar filtros
            if (businessId.HasValue)
            {
                productsQuery = productsQuery.Where(p => p.BusinessId == businessId.Value);
            }

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

            // Calcular stock real para cada producto
            var inventoryItems = new List<object>();

            foreach (var product in products)
            {
                // Calcular stock actual sumando movimientos
                var currentStock = await _context.Stocks
                    .Where(s => s.ProductId == product.Id)
                    .SumAsync(s => s.Amount);

                // Calcular precio y costo promedio basado en movimientos de stock
                var stockMovements = await _context.Stocks
                    .Where(s => s.ProductId == product.Id && s.Amount > 0) // Solo entradas (compras)
                    .ToListAsync();

                decimal averagePrice = product.Price; // Precio por defecto del producto
                decimal averageCost = product.Cost;   // Costo por defecto del producto

                if (stockMovements.Any())
                {
                    // Calcular costo promedio ponderado solo con movimientos que tienen costo
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

                    // El precio puede ser un markup sobre el costo promedio
                    // O mantener el precio original del producto
                    averagePrice = product.Price; // Mantener precio original o calcular markup
                }

                // Determinar estado del stock
                var stockStatus = GetStockStatus(currentStock, 0); // minStock no existe en la tabla actual

                // Aplicar filtro de estado si se especifica
                if (!string.IsNullOrEmpty(status) && !IsStatusMatch(stockStatus, status))
                {
                    continue;
                }

                inventoryItems.Add(new
                {
                    id = product.Id,
                    name = product.Name,
                    image = product.Image,
                    price = Math.Round(averagePrice, 2),
                    cost = Math.Round(averageCost, 2),
                    sku = product.Sku,
                    currentStock = currentStock,
                    minStock = 0, // No disponible en el esquema actual
                    productType = product.ProductType != null ? new { id = product.ProductType.Id, name = product.ProductType.Name } : null,
                    business = new { id = product.Business.Id, companyName = product.Business.CompanyName },
                    lastUpdated = product.Date,
                    status = stockStatus,
                    // Información adicional para debug
                    totalMovements = stockMovements.Count,
                    lastMovementDate = stockMovements.OrderByDescending(s => s.Date).FirstOrDefault()?.Date
                });
            }

            _logger.LogInformation($"Se encontraron {inventoryItems.Count} productos en inventario");
            return Ok(inventoryItems);
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

            var summary = new
            {
                totalProducts = products.Count,
                inStock = 0,
                lowStock = 0,
                outOfStock = 0,
                totalValue = 0.0
            };

            int inStock = 0, lowStock = 0, outOfStock = 0;
            long totalValue = 0;

            foreach (var product in products)
            {
                var currentStock = await _context.Stocks
                    .Where(s => s.ProductId == product.Id)
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
            "out-of-stock" or "out_of_stock" => actualStatus == "out-of-stock",
            _ => true
        };
    }
}
