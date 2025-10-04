using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using GPInventory.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Cors;
using GPInventory.Infrastructure.Services;

namespace GPInventory.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[EnableCors("AllowFrontend")]
public class StockController : ControllerBase
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<StockController> _logger;
    private readonly IProductAuditService _auditService;

    public StockController(ApplicationDbContext context, ILogger<StockController> logger, IProductAuditService auditService)
    {
        _context = context;
        _logger = logger;
        _auditService = auditService;
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
                .Include(s => s.Store)
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
                    store = new { id = s.Store.Id, name = s.Store.Name, location = s.Store.Location },
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

            // Calcular stock actual sumando todos los movimientos de todos los stores del business
            var businessStores = await _context.Stores
                .Where(s => s.BusinessId == product.BusinessId && s.Active)
                .Select(s => s.Id)
                .ToListAsync();

            if (!businessStores.Any())
            {
                var defaultStore = await GetOrCreateDefaultStore(product.BusinessId);
                businessStores.Add(defaultStore.Id);
            }

            var currentStock = await _context.Stocks
                .Where(s => s.ProductId == productId && 
                           businessStores.Contains(s.StoreId) &&
                           s.StockId == null && 
                           s.Amount > 0 && 
                           s.IsActive == true)
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

            if (request.StoreId <= 0)
            {
                return BadRequest(new { message = "ID de store inválido" });
            }

            if (request.Amount == 0)
            {
                return BadRequest(new { message = "La cantidad no puede ser cero" });
            }

            if (request.FlowTypeId <= 0)
            {
                return BadRequest(new { message = "Tipo de flujo requerido" });
            }

            _logger.LogInformation("Creando movimiento de stock para producto: {productId} en store: {storeId}", request.ProductId, request.StoreId);

            // Verificar que el producto existe
            var product = await _context.Products.FindAsync(request.ProductId);
            if (product == null)
            {
                return BadRequest(new { message = "El producto especificado no existe" });
            }

            // Verificar que el store existe y está activo
            var store = await _context.Stores.FindAsync(request.StoreId);
            if (store == null)
            {
                return BadRequest(new { message = "El store especificado no existe" });
            }

            if (!store.Active)
            {
                return BadRequest(new { message = "El store especificado no está activo" });
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
                providerId = await GetOrCreateProviderForStore(request.ProviderName.Trim(), request.StoreId);
            }

            // Usar SQL directo para evitar problemas con Entity Framework y valores NULL
            var date = request.Date ?? DateTime.UtcNow;
            var notes = request.Notes?.Trim();
            var cost = request.Cost;
            
            // Debug: Log valores antes de guardar
            _logger.LogInformation("🔍 Debug - Valores del stock antes de insertar:");
            _logger.LogInformation("  ProductId: {ProductId}", request.ProductId);
            _logger.LogInformation("  StoreId: {StoreId}", request.StoreId);
            _logger.LogInformation("  FlowTypeId: {FlowTypeId}", request.FlowTypeId);
            _logger.LogInformation("  Amount: {Amount}", request.Amount);
            _logger.LogInformation("  Cost: {Cost}", cost);
            _logger.LogInformation("  ProviderId: {ProviderId}", providerId);
            _logger.LogInformation("  Notes: '{Notes}'", notes);
            _logger.LogInformation("  Date: {Date}", date);

            // Construir SQL con valores explícitos para manejar NULL correctamente
            var costValue = cost?.ToString() ?? "NULL";
            var providerValue = providerId?.ToString() ?? "NULL";
            var notesValue = notes != null ? $"'{notes.Replace("'", "''")}'" : "NULL";
            var dateString = date.ToString("yyyy-MM-dd HH:mm:ss");

            // Usar transacción y conexión directa para obtener el LAST_INSERT_ID() correctamente
            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                // Ejecutar INSERT 
                var insertSql = $@"
                    INSERT INTO stock (product, date, flow, amount, cost, provider, notes, id_store, active, created_at, updated_at)
                    VALUES ({request.ProductId}, '{dateString}', {request.FlowTypeId}, {request.Amount}, {costValue}, {providerValue}, {notesValue}, {request.StoreId}, 1, NOW(), NOW())";

                var affectedRows = await _context.Database.ExecuteSqlRawAsync(insertSql);
                _logger.LogInformation("🔍 Rows afectadas por INSERT: {affectedRows}", affectedRows);

                // En MySQL, obtener el último ID insertado usando una variable de sesión
                var lastIdQuery = await _context.Database.SqlQueryRaw<LastInsertIdResult>("SELECT @@IDENTITY as Id").FirstAsync();
                var lastInsertId = lastIdQuery.Id;
                
                _logger.LogInformation("🔍 ID obtenido con @@IDENTITY: {lastInsertId}", lastInsertId);
                
                await transaction.CommitAsync();

                _logger.LogInformation("Movimiento de stock creado exitosamente: {stockId}", lastInsertId);
                
                // Retornar respuesta simple sin consulta adicional para evitar problemas de NULL
                var simpleResponse = new
                {
                    id = lastInsertId,
                    productId = request.ProductId,
                    storeId = request.StoreId,
                    amount = request.Amount,
                    cost = cost,
                    date = date,
                    message = "Movimiento de stock creado exitosamente"
                };
                
                return CreatedAtAction(nameof(GetStockMovements), simpleResponse);
            }
            catch (Exception transactionEx)
            {
                await transaction.RollbackAsync();
                _logger.LogError(transactionEx, "Error en la transacción al crear movimiento de stock");
                throw; // Re-lanzar para que sea capturado por el catch principal
            }
        }
        catch (Microsoft.EntityFrameworkCore.DbUpdateException dbEx)
        {
            _logger.LogError(dbEx, "Error de base de datos al crear movimiento de stock");
            _logger.LogError("Inner exception: {InnerException}", dbEx.InnerException?.Message);
            
            if (dbEx.InnerException is InvalidCastException castEx)
            {
                _logger.LogError("Error de conversión de tipos: {CastException}", castEx.Message);
            }
            
            return StatusCode(500, new { 
                message = "Error al guardar en la base de datos", 
                details = dbEx.InnerException?.Message ?? dbEx.Message 
            });
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
                .Include(s => s.Store)
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
                    store = new { id = s.Store.Id, name = s.Store.Name, location = s.Store.Location },
                    notes = s.Notes
                })
                .ToListAsync();

            // Calcular stock actual solo con movimientos activos usando lógica FIFO
            var currentStock = await _context.Stocks
                .Where(s => s.ProductId == productId && 
                           s.StockId == null && 
                           s.Amount > 0 && 
                           s.IsActive == true)
                .SumAsync(s => s.Amount);

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
    /// Obtiene o crea un proveedor para un store específico
    /// </summary>
    /// <param name="providerName">Nombre del proveedor</param>
    /// <param name="storeId">ID del store</param>
    /// <returns>ID del proveedor</returns>
    private async Task<int> GetOrCreateProviderForStore(string providerName, int storeId)
    {
        // Buscar proveedor existente en el store
        var existingProvider = await _context.Providers
            .FirstOrDefaultAsync(p => p.Name.ToLower() == providerName.ToLower() && p.StoreId == storeId);

        if (existingProvider != null)
        {
            return existingProvider.Id;
        }

        // Crear nuevo proveedor asociado al store
        var newProvider = new GPInventory.Domain.Entities.Provider
        {
            Name = providerName,
            StoreId = storeId
        };

        _context.Providers.Add(newProvider);
        await _context.SaveChangesAsync();

        _logger.LogInformation("Proveedor creado: {providerName} para store: {storeId}", providerName, storeId);
        return newProvider.Id;
    }

    /// <summary>
    /// Obtiene o crea un proveedor
    /// </summary>
    /// <param name="providerName">Nombre del proveedor</param>
    /// <param name="businessId">ID del negocio</param>
    /// <returns>ID del proveedor</returns>
    private async Task<int> GetOrCreateProvider(string providerName, int businessId)
    {
        // Primero obtener o crear un store por defecto para el business
        var defaultStore = await GetOrCreateDefaultStore(businessId);
        
        // Buscar proveedor existente en esa store
        var existingProvider = await _context.Providers
            .FirstOrDefaultAsync(p => p.Name.ToLower() == providerName.ToLower() && p.StoreId == defaultStore.Id);

        if (existingProvider != null)
        {
            return existingProvider.Id;
        }

        // Crear nuevo proveedor asociado al store
        var newProvider = new GPInventory.Domain.Entities.Provider
        {
            Name = providerName,
            StoreId = defaultStore.Id
        };

        _context.Providers.Add(newProvider);
        await _context.SaveChangesAsync();

        _logger.LogInformation("Proveedor creado: {providerName} para store: {storeId} (business: {businessId})", providerName, defaultStore.Id, businessId);
        return newProvider.Id;
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

    /// <summary>
    /// Obtiene el inventario completo de un negocio dividido por stores
    /// </summary>
    /// <param name="businessId">ID del negocio</param>
    /// <returns>Inventario dividido por stores con stock actual de cada store</returns>
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
    public async Task<ActionResult<object>> GetBusinessInventory(int businessId)
    {
        try
        {
            _logger.LogInformation("Obteniendo inventario dividido por stores para negocio: {businessId}", businessId);

            // Verificar que el negocio existe
            var business = await _context.Businesses.FindAsync(businessId);
            if (business == null)
            {
                return NotFound(new { message = "Negocio no encontrado" });
            }

            // Obtener todos los productos del negocio
            var products = await _context.Products
                .Include(p => p.ProductType)
                .Include(p => p.Business)
                .Where(p => p.BusinessId == businessId)
                .ToListAsync();

            // Obtener todos los stores activos del negocio
            var stores = await _context.Stores
                .Where(s => s.BusinessId == businessId && s.Active)
                .OrderBy(s => s.Name)
                .ToListAsync();

            // Si no hay stores, crear uno por defecto
            if (!stores.Any())
            {
                var defaultStore = await GetOrCreateDefaultStore(businessId);
                stores = await _context.Stores
                    .Where(s => s.Id == defaultStore.Id)
                    .ToListAsync();
            }

            var storeIds = stores.Select(s => s.Id).ToList();

            // Obtener todos los stocks y ventas de una vez para optimizar
            // Solo stocks activos con lógica FIFO: stock_id es null, amount > 0 y activo = 1
            var allStocks = await _context.Stocks
                .Where(s => storeIds.Contains(s.StoreId) && 
                           s.StockId == null && 
                           s.Amount > 0 && 
                           s.IsActive == true)
                .ToListAsync();

            var allSalesDetails = await _context.SaleDetails
                .Include(sd => sd.Sale)
                    .ThenInclude(s => s.Store)
                .Where(sd => storeIds.Contains(sd.Sale.StoreId))
                .ToListAsync();

            // Calcular fechas para métricas de ventas
            var today = DateTime.Today;
            var startOfMonth = new DateTime(today.Year, today.Month, 1);
            var lastMonth = startOfMonth.AddMonths(-1);
            var endOfLastMonth = startOfMonth.AddDays(-1);

            // Separar ventas por períodos
            var todaySales = allSalesDetails.Where(sd => sd.Sale.Date.Date == today).ToList();
            var monthSales = allSalesDetails.Where(sd => sd.Sale.Date >= startOfMonth).ToList();
            var lastMonthSales = allSalesDetails.Where(sd => sd.Sale.Date >= lastMonth && sd.Sale.Date <= endOfLastMonth).ToList();

            // Calcular summary total del negocio
            var totalProducts = products.Count;
            var totalStock = allStocks.GroupBy(s => s.ProductId).Sum(g => g.Sum(s => s.Amount));
            
            var businessTodaySalesAmount = todaySales.Sum(sd => (decimal)sd.Price * decimal.Parse(sd.Amount));
            var businessTodayTransactions = todaySales.GroupBy(sd => new { sd.Sale.Id }).Count();
            var businessMonthSalesAmount = monthSales.Sum(sd => (decimal)sd.Price * decimal.Parse(sd.Amount));
            var businessMonthTransactions = monthSales.GroupBy(sd => new { sd.Sale.Id }).Count();
            var businessLastMonthSalesAmount = lastMonthSales.Sum(sd => (decimal)sd.Price * decimal.Parse(sd.Amount));

            // Calcular porcentaje de cambio mensual
            decimal? businessMonthChangePercent = null;
            if (businessLastMonthSalesAmount > 0)
            {
                businessMonthChangePercent = ((businessMonthSalesAmount - businessLastMonthSalesAmount) / businessLastMonthSalesAmount) * 100;
            }

            var totalSummary = new
            {
                totalProducts = totalProducts,
                totalStock = totalStock,
                todaySales = new
                {
                    amount = businessTodaySalesAmount,
                    transactions = businessTodayTransactions,
                    changePercent = (decimal?)null // Necesitaríamos datos históricos para calcular esto
                },
                monthSales = new
                {
                    amount = businessMonthSalesAmount,
                    transactions = businessMonthTransactions,
                    changePercent = businessMonthChangePercent
                }
            };

            // Procesar cada store individualmente
            var storesSummary = new List<object>();
            var storesData = new List<object>();

            foreach (var store in stores)
            {
                var storeStocks = allStocks.Where(s => s.StoreId == store.Id).ToList();
                var storeSalesDetails = allSalesDetails.Where(sd => sd.Sale.StoreId == store.Id).ToList();
                var storeTodaySales = storeSalesDetails.Where(sd => sd.Sale.Date.Date == today).ToList();
                var storeMonthSales = storeSalesDetails.Where(sd => sd.Sale.Date >= startOfMonth).ToList();
                var storeLastMonthSales = storeSalesDetails.Where(sd => sd.Sale.Date >= lastMonth && sd.Sale.Date <= endOfLastMonth).ToList();

                var storeStock = storeStocks.GroupBy(s => s.ProductId).Sum(g => g.Sum(s => s.Amount));
                var storeTodaySalesAmount = storeTodaySales.Sum(sd => (decimal)sd.Price * decimal.Parse(sd.Amount));
                var storeTodayTransactions = storeTodaySales.GroupBy(sd => sd.Sale.Id).Count();
                var storeMonthSalesAmount = storeMonthSales.Sum(sd => (decimal)sd.Price * decimal.Parse(sd.Amount));
                var storeMonthTransactions = storeMonthSales.GroupBy(sd => sd.Sale.Id).Count();
                var storeLastMonthSalesAmount = storeLastMonthSales.Sum(sd => (decimal)sd.Price * decimal.Parse(sd.Amount));

                // Calcular porcentaje de cambio mensual del store
                decimal? storeMonthChangePercent = null;
                if (storeLastMonthSalesAmount > 0)
                {
                    storeMonthChangePercent = ((storeMonthSalesAmount - storeLastMonthSalesAmount) / storeLastMonthSalesAmount) * 100;
                }

                // Summary del store
                storesSummary.Add(new
                {
                    storeId = store.Id,
                    storeName = store.Name,
                    location = store.Location,
                    totalProducts = products.Count,
                    totalStock = storeStock,
                    todaySales = new
                    {
                        amount = storeTodaySalesAmount,
                        transactions = storeTodayTransactions,
                        changePercent = (decimal?)null
                    },
                    monthSales = new
                    {
                        amount = storeMonthSalesAmount,
                        transactions = storeMonthTransactions,
                        changePercent = storeMonthChangePercent
                    }
                });

                // Productos del store
                var storeProducts = new List<object>();

                foreach (var product in products)
                {
                    var productStocks = storeStocks.Where(s => s.ProductId == product.Id).ToList();
                    var currentStock = productStocks.Sum(s => s.Amount);

                    // Calcular costo promedio ponderado para este store
                    var stockMovements = productStocks.Where(s => s.Amount > 0).ToList();
                    decimal? averageCost = null;

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

                    // Calcular ventas del producto en este store
                    var productSalesDetails = storeSalesDetails.Where(sd => sd.ProductId == product.Id).ToList();
                    var productTodaySales = productSalesDetails.Where(sd => sd.Sale.Date.Date == today).ToList();
                    var productMonthSales = productSalesDetails.Where(sd => sd.Sale.Date >= startOfMonth).ToList();
                    var productLastMonthSales = productSalesDetails.Where(sd => sd.Sale.Date >= lastMonth && sd.Sale.Date <= endOfLastMonth).ToList();

                    // Calcular precio promedio de ventas para este store
                    decimal? averagePrice = null;
                    if (productSalesDetails.Any())
                    {
                        var totalValue = productSalesDetails.Sum(sd => (decimal)sd.Price * decimal.Parse(sd.Amount));
                        var totalQuantity = productSalesDetails.Sum(sd => decimal.Parse(sd.Amount));
                        averagePrice = totalQuantity > 0 ? totalValue / totalQuantity : null;
                    }

                    var todayQuantity = productTodaySales.Sum(sd => decimal.Parse(sd.Amount));
                    var todaySalesAmount = productTodaySales.Sum(sd => (decimal)sd.Price * decimal.Parse(sd.Amount));
                    var monthQuantity = productMonthSales.Sum(sd => decimal.Parse(sd.Amount));
                    var monthSalesAmount = productMonthSales.Sum(sd => (decimal)sd.Price * decimal.Parse(sd.Amount));
                    var lastMonthSalesAmount = productLastMonthSales.Sum(sd => (decimal)sd.Price * decimal.Parse(sd.Amount));

                    // Calcular porcentaje de cambio mensual del producto en este store
                    decimal? productMonthChangePercent = null;
                    if (lastMonthSalesAmount > 0)
                    {
                        productMonthChangePercent = ((monthSalesAmount - lastMonthSalesAmount) / lastMonthSalesAmount) * 100;
                    }

                    storeProducts.Add(new
                    {
                        id = product.Id,
                        name = product.Name,
                        sku = product.Sku,
                        price = product.Price,
                        cost = product.Cost,
                        minimumStock = product.MinimumStock,
                        image = product.Image,
                        productType = product.ProductType != null ? new { id = product.ProductType.Id, name = product.ProductType.Name } : null,
                        business = new { id = product.Business.Id, companyName = product.Business.CompanyName },
                        currentStock = currentStock,
                        averageCost = averageCost.HasValue ? Math.Round(averageCost.Value, 2) : (decimal?)null,
                        averagePrice = averagePrice.HasValue ? Math.Round(averagePrice.Value, 2) : (decimal?)null,
                        totalMovements = stockMovements.Count,
                        lastMovementDate = stockMovements.OrderByDescending(s => s.Date).FirstOrDefault()?.Date,
                        todaySales = new
                        {
                            amount = todaySalesAmount,
                            quantity = (int)todayQuantity,
                            changePercent = (decimal?)null
                        },
                        monthSales = new
                        {
                            amount = monthSalesAmount,
                            quantity = (int)monthQuantity,
                            changePercent = productMonthChangePercent
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
                businessId = businessId,
                summary = totalSummary,
                storesSummary = storesSummary,
                stores = storesData
            };

            _logger.LogInformation($"Inventario obtenido para {stores.Count} stores con {products.Count} productos del negocio {businessId}");
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
    /// ID del store donde se realiza el movimiento
    /// </summary>
    public int StoreId { get; set; }

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

/// <summary>
/// Clase para mapear el resultado de LAST_INSERT_ID()
/// </summary>
public class LastInsertIdResult
{
    public int Id { get; set; }
}
