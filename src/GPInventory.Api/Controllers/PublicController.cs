using GPInventory.Application.Common;
using GPInventory.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Cors;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace GPInventory.Api.Controllers;

/// <summary>
/// Endpoints públicos para canales externos (Webadas, integraciones B2B, etc.).
/// Autenticados con API Key (header X-Api-Key) en lugar de JWT de usuario.
/// </summary>
[ApiController]
[Route("api/public")]
[EnableCors("AllowFrontend")]
public class PublicController : ControllerBase
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<PublicController> _logger;

    public PublicController(ApplicationDbContext context, ILogger<PublicController> logger)
    {
        _context = context;
        _logger = logger;
    }

    // ─────────────────────────────────────────────────────────────────────
    // IDENTITY — info del canal desde la clave (sin hardcodear businessId)
    // ─────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Devuelve el businessId, nombre del negocio y canal asociados a la API Key.
    /// El BFF lo llama una vez al arrancar para resolver su contexto sin hardcodear IDs.
    /// </summary>
    [HttpGet("me")]
    [Authorize(AuthenticationSchemes = "ApiKey")]
    [ProducesResponseType(200)]
    [ProducesResponseType(401)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> Me()
    {
        var businessId = GetBusinessIdFromKey();
        var channel    = GetChannel();
        var scopes     = User.FindAll("scope").Select(c => c.Value).ToList();

        var business = await _context.Businesses
            .Where(b => b.Id == businessId)
            .Select(b => new { b.Id, b.CompanyName })
            .FirstOrDefaultAsync();

        if (business == null)
            return NotFound(new { message = "Negocio no encontrado" });

        // Obtener la tienda activa del negocio (primera disponible)
        var store = await _context.Stores
            .Where(s => s.BusinessId == businessId && s.Active)
            .OrderBy(s => s.Name)
            .Select(s => new { s.Id, s.Name })
            .FirstOrDefaultAsync();

        return Ok(new
        {
            businessId,
            businessName = business.CompanyName,
            channel,
            scopes,
            defaultStoreId = store?.Id,
            defaultStoreName = store?.Name,
        });
    }

    // ─────────────────────────────────────────────────────────────────────
    // CATÁLOGO
    // ─────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Lista todos los productos disponibles (stock > 0) de un negocio.
    /// Se aplican los descuentos del canal si existen en webstore_promotion.
    /// </summary>
    [HttpGet("{businessId:int}/products")]
    [Authorize(AuthenticationSchemes = "ApiKey")]
    [ProducesResponseType(200)]
    [ProducesResponseType(401)]
    [ProducesResponseType(404)]
    [ProducesResponseType(500)]
    public async Task<IActionResult> GetProducts(int businessId, [FromQuery] string? search = null)
    {
        try
        {
            if (!ValidateBusinessAccess(businessId, out var errorResult))
                return errorResult!;

            var business = await _context.Businesses.FindAsync(businessId);
            if (business == null)
                return NotFound(new { message = "Negocio no encontrado" });

            // Obtener canal de la API Key
            var channel = GetChannel();

            // Obtener descuentos vigentes del canal
            var now = DateTime.UtcNow;
            var promotions = await _context.WebstorePromotions
                .Where(p => p.BusinessId == businessId
                         && p.Channel == channel
                         && p.Active
                         && (p.StartsAt == null || p.StartsAt <= now)
                         && (p.EndsAt == null || p.EndsAt >= now))
                .ToDictionaryAsync(p => p.ProductId, p => p.DiscountPct);

            // Obtener productos con stock disponible
            var sql = @"
                SELECT
                    p.id          AS Id,
                    p.name        AS Name,
                    p.sku         AS Sku,
                    COALESCE(p.price, 0) AS Price,
                    p.image       AS Image,
                    pt.name       AS ProductTypeName,
                    COALESCE((
                        SELECT SUM(s.amount)
                        FROM stock s
                        INNER JOIN store st ON s.id_store = st.id
                        WHERE s.product = p.id
                          AND st.id_business = {0}
                          AND COALESCE(s.active, 0) = 1
                    ), 0) AS CurrentStock
                FROM product p
                LEFT JOIN product_type pt ON p.product_type = pt.id
                WHERE p.business = {0}
                HAVING CurrentStock > 0
                ORDER BY
                    COALESCE((
                        SELECT MIN(sci.display_order)
                        FROM shop_collection_item sci
                        INNER JOIN shop_collection sc ON sc.id = sci.collection_id
                        WHERE sci.product_id = p.id
                          AND sc.business_id = {0}
                          AND sc.active = 1
                    ), 9999) ASC,
                    p.name ASC";

            var rawProducts = await _context.Database
                .SqlQueryRaw<PublicProductResult>(sql, businessId)
                .ToListAsync();

            if (!string.IsNullOrWhiteSpace(search))
            {
                var s = search.ToLowerInvariant();
                rawProducts = rawProducts
                    .Where(p => (p.Name ?? "").ToLower().Contains(s) || (p.Sku ?? "").ToLower().Contains(s))
                    .ToList();
            }

            var products = rawProducts.Select(p =>
            {
                var discount = promotions.GetValueOrDefault(p.Id, 0);
                var finalPrice = discount > 0
                    ? Math.Round(p.Price * (1 - discount / 100m), 0)
                    : p.Price;

                return new
                {
                    id = p.Id,
                    name = p.Name,
                    sku = p.Sku,
                    price = (int)p.Price,
                    finalPrice = (int)finalPrice,
                    discountPct = discount,
                    stock = p.CurrentStock,
                    image = p.Image,
                    productType = p.ProductTypeName,
                };
            }).ToList();

            return Ok(new
            {
                businessId,
                channel,
                totalProducts = products.Count,
                products
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error en GetProducts (businessId={BusinessId})", businessId);
            return StatusCode(500, new { message = "Error interno del servidor" });
        }
    }

    /// <summary>
    /// Detalle de un producto específico con su stock actual.
    /// </summary>
    [HttpGet("{businessId:int}/products/{productId:int}")]
    [Authorize(AuthenticationSchemes = "ApiKey")]
    [ProducesResponseType(200)]
    [ProducesResponseType(401)]
    [ProducesResponseType(404)]
    [ProducesResponseType(500)]
    public async Task<IActionResult> GetProduct(int businessId, int productId)
    {
        try
        {
            if (!ValidateBusinessAccess(businessId, out var errorResult))
                return errorResult!;

            var product = await _context.Products
                .Include(p => p.ProductType)
                .Where(p => p.Id == productId && p.BusinessId == businessId)
                .FirstOrDefaultAsync();

            if (product == null)
                return NotFound(new { message = "Producto no encontrado" });

            var currentStock = await _context.Stocks
                .Where(s => s.ProductId == productId && s.Store.BusinessId == businessId && s.IsActive)
                .SumAsync(s => s.Amount);

            var channel = GetChannel();
            var now = DateTime.UtcNow;
            var promotion = await _context.WebstorePromotions
                .Where(p => p.BusinessId == businessId
                         && p.Channel == channel
                         && p.ProductId == productId
                         && p.Active
                         && (p.StartsAt == null || p.StartsAt <= now)
                         && (p.EndsAt == null || p.EndsAt >= now))
                .FirstOrDefaultAsync();

            var discountPct = promotion?.DiscountPct ?? 0;
            var finalPrice = discountPct > 0
                ? (int)Math.Round(product.Price * (1 - discountPct / 100m), 0)
                : product.Price;

            return Ok(new
            {
                id = product.Id,
                name = product.Name,
                sku = product.Sku,
                price = product.Price,
                finalPrice,
                discountPct,
                stock = currentStock,
                image = product.Image,
                productType = product.ProductType?.Name,
                canSell = currentStock > 0
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error en GetProduct (businessId={BusinessId}, productId={ProductId})", businessId, productId);
            return StatusCode(500, new { message = "Error interno del servidor" });
        }
    }

    /// <summary>
    /// Devuelve el mix promocional activo del canal para el negocio.
    /// </summary>
    [HttpGet("{businessId:int}/promotions")]
    [Authorize(AuthenticationSchemes = "ApiKey")]
    [ProducesResponseType(200)]
    [ProducesResponseType(401)]
    [ProducesResponseType(404)]
    [ProducesResponseType(500)]
    public async Task<IActionResult> GetPromotions(int businessId)
    {
        try
        {
            if (!ValidateBusinessAccess(businessId, out var errorResult))
                return errorResult!;

            var channel = GetChannel();
            var now = DateTime.UtcNow;

            var promotions = await _context.WebstorePromotions
                .Include(p => p.Product).ThenInclude(p => p.ProductType)
                .Where(p => p.BusinessId == businessId
                         && p.Channel == channel
                         && p.Active
                         && (p.StartsAt == null || p.StartsAt <= now)
                         && (p.EndsAt == null || p.EndsAt >= now))
                .OrderBy(p => p.DisplayOrder)
                .ToListAsync();

            // Calcular stock actual de cada producto promocionado
            var productIds = promotions.Select(p => p.ProductId).ToList();
            var stockByProduct = await _context.Stocks
                .Where(s => productIds.Contains(s.ProductId) && s.Store.BusinessId == businessId && s.IsActive)
                .GroupBy(s => s.ProductId)
                .Select(g => new { ProductId = g.Key, Stock = g.Sum(s => s.Amount) })
                .ToDictionaryAsync(x => x.ProductId, x => x.Stock);

            var result = promotions.Select(p =>
            {
                var stock = stockByProduct.GetValueOrDefault(p.ProductId, 0);
                var finalPrice = p.DiscountPct > 0
                    ? (int)Math.Round(p.Product.Price * (1 - p.DiscountPct / 100m), 0)
                    : p.Product.Price;

                return new
                {
                    promotionId = p.Id,
                    displayOrder = p.DisplayOrder,
                    discountPct = p.DiscountPct,
                    endsAt = p.EndsAt,
                    product = new
                    {
                        id = p.Product.Id,
                        name = p.Product.Name,
                        sku = p.Product.Sku,
                        price = p.Product.Price,
                        finalPrice,
                        image = p.Product.Image,
                        productType = p.Product.ProductType?.Name,
                        stock,
                        canSell = stock > 0
                    }
                };
            }).ToList();

            return Ok(new { businessId, channel, totalPromotions = result.Count, promotions = result });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error en GetPromotions (businessId={BusinessId})", businessId);
            return StatusCode(500, new { message = "Error interno del servidor" });
        }
    }

    // ─────────────────────────────────────────────────────────────────────
    // CHECKOUT
    // ─────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Procesa una venta FIFO desde un canal externo (Webadas, etc.).
    /// Registra la venta en la misma tabla 'sales' con el canal de origen.
    /// </summary>
    [HttpPost("checkout")]
    [Authorize(AuthenticationSchemes = "ApiKey")]
    [ProducesResponseType(201)]
    [ProducesResponseType(400)]
    [ProducesResponseType(401)]
    [ProducesResponseType(500)]
    public async Task<IActionResult> Checkout([FromBody] PublicCheckoutRequest request)
    {
        if (request == null || request.Items == null || !request.Items.Any())
            return BadRequest(new { message = "Request inválido: se requiere al menos un producto" });

        using var tx = await _context.Database.BeginTransactionAsync();
        try
        {
            // Validar que el businessId del request coincide con el de la API Key
            var keyBusinessId = GetBusinessIdFromKey();
            if (keyBusinessId != request.BusinessId)
                return Forbid();

            var store = await _context.Stores
                .Include(s => s.Business)
                .FirstOrDefaultAsync(s => s.Id == request.StoreId && s.BusinessId == request.BusinessId);

            if (store == null)
                return BadRequest(new { message = "Store no encontrado o no pertenece al negocio" });

            if (!store.Active)
                return BadRequest(new { message = "El store no está activo" });

            var productIds = request.Items.Select(i => i.ProductId).Distinct().ToList();
            var products = await _context.Products
                .Where(p => productIds.Contains(p.Id) && p.BusinessId == request.BusinessId)
                .ToListAsync();

            if (products.Count != productIds.Count)
                return BadRequest(new { message = "Uno o más productos no pertenecen a este negocio" });

            // ── FIFO pre-validación ──────────────────────────────────────
            var allocations = new Dictionary<int, List<(GPInventory.Domain.Entities.Stock Stock, int Qty)>>();

            foreach (var item in request.Items)
            {
                var product = products.First(p => p.Id == item.ProductId);

                var availableStocks = await _context.Stocks
                    .Where(s => s.ProductId == item.ProductId
                             && s.StoreId == request.StoreId
                             && s.Amount > 0
                             && s.IsActive)
                    .OrderBy(s => s.Date).ThenBy(s => s.Id)
                    .ToListAsync();

                var stocksWithAvail = new List<(GPInventory.Domain.Entities.Stock, int Available)>();
                foreach (var lot in availableStocks)
                {
                    var used = await _context.SaleDetails
                        .Where(sd => sd.StockId == lot.Id)
                        .SumAsync(sd => (int?)int.Parse(sd.Amount)) ?? 0;

                    var avail = lot.Amount - used;
                    if (avail > 0) stocksWithAvail.Add((lot, avail));
                }

                var totalAvail = stocksWithAvail.Sum(x => x.Item2);
                if (totalAvail < item.Quantity)
                    return BadRequest(new
                    {
                        message = $"Stock insuficiente para '{product.Name}'. Disponible: {totalAvail}, Solicitado: {item.Quantity}",
                        productId = item.ProductId,
                        available = totalAvail,
                        requested = item.Quantity
                    });

                // Armar asignaciones FIFO
                var itemAllocs = new List<(GPInventory.Domain.Entities.Stock, int)>();
                var remaining = item.Quantity;
                foreach (var (lot, avail) in stocksWithAvail)
                {
                    if (remaining <= 0) break;
                    var toAlloc = Math.Min(remaining, avail);
                    itemAllocs.Add((lot, toAlloc));
                    remaining -= toAlloc;
                }
                allocations[item.ProductId] = itemAllocs;
            }

            // ── Crear venta ──────────────────────────────────────────────
            var channel = request.Channel ?? GetChannel();

            var sale = new GPInventory.Domain.Entities.Sale
            {
                StoreId = request.StoreId,
                Date = DateTimeHelper.GetChileNow(),
                CustomerName = request.CustomerName?.Trim(),
                CustomerRut = request.CustomerRut?.Trim(),
                PaymentMethodId = request.PaymentMethodId,
                Notes = $"[{channel.ToUpper()}] {request.Notes}".Trim().TrimEnd(']').TrimEnd('['),
                Channel = channel,
                Total = 0
            };

            _context.Sales.Add(sale);
            await _context.SaveChangesAsync();

            decimal total = 0;
            var saleItems = new List<object>();

            foreach (var item in request.Items)
            {
                var product = products.First(p => p.Id == item.ProductId);

                // Aplicar descuento del canal si existe
                var now = DateTime.UtcNow;
                var promo = await _context.WebstorePromotions
                    .Where(p => p.BusinessId == request.BusinessId
                             && p.Channel == channel
                             && p.ProductId == item.ProductId
                             && p.Active
                             && (p.StartsAt == null || p.StartsAt <= now)
                             && (p.EndsAt == null || p.EndsAt >= now))
                    .FirstOrDefaultAsync();

                var unitPrice = item.UnitPrice > 0
                    ? item.UnitPrice
                    : (promo != null
                        ? (decimal)Math.Round(product.Price * (1 - promo.DiscountPct / 100m), 0)
                        : product.Price);

                var subtotal = unitPrice * item.Quantity;
                total += subtotal;

                var detail = new GPInventory.Domain.Entities.SaleDetail
                {
                    ProductId = item.ProductId,
                    SaleId = sale.Id,
                    Amount = item.Quantity.ToString(),
                    Price = (int)Math.Round(unitPrice, 0),
                    StockId = allocations[item.ProductId].FirstOrDefault().Stock?.Id
                };
                _context.SaleDetails.Add(detail);

                // Movimientos de stock FIFO
                foreach (var (lot, qty) in allocations[item.ProductId])
                {
                    _context.Stocks.Add(new GPInventory.Domain.Entities.Stock
                    {
                        ProductId = item.ProductId,
                        Date = DateTimeHelper.GetChileNow(),
                        FlowTypeId = 11, // Venta
                        Amount = -qty,
                        Cost = lot.Cost,
                        StoreId = request.StoreId,
                        SaleId = sale.Id,
                        Notes = $"[{channel}] Venta #{sale.Id} - lote #{lot.Id}",
                        IsActive = true
                    });
                }

                saleItems.Add(new
                {
                    productId = item.ProductId,
                    productName = product.Name,
                    quantity = item.Quantity,
                    unitPrice = (int)unitPrice,
                    subtotal = (int)subtotal,
                    discountApplied = promo?.DiscountPct ?? 0
                });
            }

            sale.Total = (int)Math.Round(total, 0);
            await _context.SaveChangesAsync();
            await tx.CommitAsync();

            _logger.LogInformation("✅ Checkout [{Channel}] - venta #{SaleId} por ${Total}", channel, sale.Id, sale.Total);

            return CreatedAtAction(null, null, new
            {
                orderId = sale.Id,
                channel,
                storeId = request.StoreId,
                businessId = request.BusinessId,
                date = sale.Date,
                customerName = sale.CustomerName,
                total = sale.Total,
                paymentMethodId = sale.PaymentMethodId,
                items = saleItems
            });
        }
        catch (Exception ex)
        {
            await tx.RollbackAsync();
            _logger.LogError(ex, "Error en Checkout público");
            return StatusCode(500, new { message = "Error al procesar el checkout", details = ex.Message });
        }
    }

    // ─────────────────────────────────────────────────────────────────────
    // HELPERS
    // ─────────────────────────────────────────────────────────────────────

    private int GetBusinessIdFromKey() =>
        int.TryParse(User.FindFirstValue("business_id"), out var id) ? id : 0;

    private string GetChannel() =>
        User.FindFirstValue("api_key_label") ?? "external";

    /// <summary>
    /// Verifica que el business_id del request corresponda al negocio de la API Key.
    /// </summary>
    private bool ValidateBusinessAccess(int businessId, out IActionResult? error)
    {
        var keyBusiness = GetBusinessIdFromKey();
        if (keyBusiness != businessId)
        {
            error = Forbid();
            return false;
        }
        error = null;
        return true;
    }

    // ─────────────────────────────────────────────────────────────────────
    // SHOP — Banners y Colecciones públicas (filtradas por canal y fecha)
    // ─────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Devuelve los banners activos y vigentes para el canal del negocio,
    /// filtrados por la temporada activa (si existe). El contenido sin temporada es siempre visible.
    /// </summary>
    [HttpGet("{businessId}/banners")]
    [Authorize(AuthenticationSchemes = "ApiKey")]
    public async Task<IActionResult> GetPublicBanners(int businessId, [FromQuery] string? channel = null, [FromQuery] string? slot = null)
    {
        if (!ValidateBusinessAccess(businessId, out var err)) return err!;

        var now = DateTime.UtcNow;
        var effectiveChannel = channel ?? GetChannel();

        // Buscar temporada activa para este canal
        var activeSeason = await _context.ShopSeasons
            .Where(s => s.BusinessId == businessId && s.IsActive
                && (s.Channel == null || s.Channel == effectiveChannel))
            .FirstOrDefaultAsync();

        var query = _context.ShopBanners
            .Where(b => b.BusinessId == businessId && b.Active
                && (b.Channel == null || b.Channel == effectiveChannel)
                && (b.StartsAt == null || b.StartsAt <= now)
                && (b.EndsAt == null || b.EndsAt >= now)
                && (activeSeason == null ? b.SeasonId == null : (b.SeasonId == null || b.SeasonId == activeSeason.Id)));

        if (!string.IsNullOrWhiteSpace(slot))
            query = query.Where(b => b.Slot == slot);

        var banners = await query
            .OrderBy(b => b.DisplayOrder)
            .Select(b => new
            {
                b.Id, b.Channel, b.Slot, b.Title, b.Subtitle,
                b.ImageUrl, b.RedirectUrl, b.DisplayOrder,
                b.StartsAt, b.EndsAt, b.SeasonId
            })
            .ToListAsync();

        return Ok(banners);
    }

    /// <summary>
    /// Devuelve las colecciones activas del negocio, filtradas por temporada activa.
    /// </summary>
    [HttpGet("{businessId}/collections")]
    [Authorize(AuthenticationSchemes = "ApiKey")]
    public async Task<IActionResult> GetPublicCollections(int businessId, [FromQuery] string? channel = null)
    {
        if (!ValidateBusinessAccess(businessId, out var err)) return err!;

        var effectiveChannel = channel ?? GetChannel();

        var activeSeason = await _context.ShopSeasons
            .Where(s => s.BusinessId == businessId && s.IsActive
                && (s.Channel == null || s.Channel == effectiveChannel))
            .FirstOrDefaultAsync();

        var collections = await _context.ShopCollections
            .Where(c => c.BusinessId == businessId && c.Active
                && (c.Channel == null || c.Channel == effectiveChannel)
                && (activeSeason == null ? c.SeasonId == null : (c.SeasonId == null || c.SeasonId == activeSeason.Id)))
            .OrderBy(c => c.DisplayOrder)
            .Select(c => new
            {
                c.Id, c.Channel, c.Name, c.Slug, c.Description,
                c.CoverImageUrl, c.HeaderColor, c.DisplayAs, c.SortRule, c.DisplayOrder, c.SeasonId,
                ItemCount = c.Items.Count
            })
            .ToListAsync();

        return Ok(collections);
    }

    /// <summary>
    /// Devuelve el detalle de una colección con sus productos, respetando sort_rule.
    /// </summary>
    [HttpGet("{businessId}/collections/{slug}")]
    [Authorize(AuthenticationSchemes = "ApiKey")]
    public async Task<IActionResult> GetPublicCollection(int businessId, string slug)
    {
        if (!ValidateBusinessAccess(businessId, out var err)) return err!;

        var effectiveChannel = GetChannel();
        var now = DateTime.UtcNow;

        var activeSeason = await _context.ShopSeasons
            .Where(s => s.BusinessId == businessId && s.IsActive
                && (s.Channel == null || s.Channel == effectiveChannel))
            .FirstOrDefaultAsync();

        var collection = await _context.ShopCollections
            .Include(c => c.Items)
                .ThenInclude(i => i.Product)
            .FirstOrDefaultAsync(c => c.BusinessId == businessId && c.Slug == slug && c.Active
                && (activeSeason == null ? c.SeasonId == null : (c.SeasonId == null || c.SeasonId == activeSeason.Id)));

        if (collection is null) return NotFound();

        // Cargar stock y descuentos para los productos de la colección
        var productIds = collection.Items
            .Where(i => i.Product != null)
            .Select(i => i.ProductId)
            .Distinct()
            .ToList();

        var stockByProduct = await _context.Stocks
            .Where(s => productIds.Contains(s.ProductId) && s.Store.BusinessId == businessId && s.IsActive)
            .GroupBy(s => s.ProductId)
            .ToDictionaryAsync(g => g.Key, g => g.Sum(s => s.Amount));

        var promotions = await _context.WebstorePromotions
            .Where(p => p.BusinessId == businessId
                     && p.Channel == effectiveChannel
                     && productIds.Contains(p.ProductId)
                     && p.Active
                     && (p.StartsAt == null || p.StartsAt <= now)
                     && (p.EndsAt == null || p.EndsAt >= now))
            .ToDictionaryAsync(p => p.ProductId, p => p.DiscountPct);

        // Sort items: pinned first, then by display_order
        var items = collection.Items
            .OrderByDescending(i => i.Pinned)
            .ThenBy(i => i.DisplayOrder)
            .Select(i =>
            {
                if (i.Product is null) return new
                {
                    i.Id, i.ProductId, i.DisplayOrder, i.Pinned,
                    Product = (object?)null
                };

                var stock = stockByProduct.GetValueOrDefault(i.ProductId, 0);
                var discountPct = promotions.GetValueOrDefault(i.ProductId, 0);
                var finalPrice = discountPct > 0
                    ? (int)Math.Round(i.Product.Price * (1 - discountPct / 100m), 0)
                    : (int)i.Product.Price;

                return new
                {
                    i.Id, i.ProductId, i.DisplayOrder, i.Pinned,
                    Product = (object?)new
                    {
                        i.Product.Id,
                        i.Product.Name,
                        Price = (int)i.Product.Price,
                        FinalPrice = finalPrice,
                        DiscountPct = discountPct,
                        Stock = stock,
                        Image = i.Product.Image,
                        i.Product.Sku
                    }
                };
            })
            .ToList();

        return Ok(new
        {
            collection.Id, collection.Name, collection.Slug, collection.Description,
            collection.CoverImageUrl, collection.HeaderColor, collection.SortRule,
            collection.Channel, collection.SeasonId,
            ActiveSeason = activeSeason is null ? null : new { activeSeason.Id, activeSeason.Name },
            Items = items
        });
    }
}


// ─────────────────────────────────────────────────────────────────────────────
// DTOs
// ─────────────────────────────────────────────────────────────────────────────

public class PublicCheckoutRequest
{
    public int BusinessId { get; set; }
    public int StoreId { get; set; }
    public string? Channel { get; set; }
    public string? CustomerName { get; set; }
    public string? CustomerRut { get; set; }
    public string? CustomerEmail { get; set; }
    public int PaymentMethodId { get; set; }
    public string? Notes { get; set; }
    public List<PublicCheckoutItem> Items { get; set; } = new();
}

public class PublicCheckoutItem
{
    public int ProductId { get; set; }
    public int Quantity { get; set; }
    /// <summary>Precio unitario personalizado (opcional). Si es 0, se usa el precio del catálogo con descuento de canal.</summary>
    public decimal UnitPrice { get; set; }
}

/// <summary>
/// Resultado interno para la query SQL de productos públicos.
/// </summary>
public class PublicProductResult
{
    public int Id { get; set; }
    public string? Name { get; set; }
    public string? Sku { get; set; }
    public decimal Price { get; set; }
    public string? Image { get; set; }
    public string? ProductTypeName { get; set; }
    public int CurrentStock { get; set; }
}
