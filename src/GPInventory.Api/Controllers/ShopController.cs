using GPInventory.Application.Interfaces;
using GPInventory.Domain.Entities;
using GPInventory.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Cors;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace GPInventory.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[EnableCors("AllowFrontend")]
[Authorize]
public class ShopController : ControllerBase
{
    private readonly ApplicationDbContext _db;
    private readonly ITokenService _tokenService;
    private readonly ILogger<ShopController> _logger;

    public ShopController(ApplicationDbContext db, ITokenService tokenService, ILogger<ShopController> logger)
    {
        _db = db;
        _tokenService = tokenService;
        _logger = logger;
    }

    private bool HasBusinessAccess(int businessId)
    {
        var token = Request.Headers["Authorization"].ToString().Replace("Bearer ", "");
        var systemRole = User.Claims.FirstOrDefault(c => c.Type == "systemRole")?.Value;
        if (systemRole == "super_admin") return true;
        return _tokenService.HasAccessToBusiness(token, businessId);
    }

    // ───────────────────────────────────────────────
    // BANNERS
    // ───────────────────────────────────────────────

    [HttpGet("{businessId}/banners")]
    public async Task<IActionResult> GetBanners(int businessId, [FromQuery] string? channel = null)
    {
        if (!HasBusinessAccess(businessId)) return Forbid();

        var query = _db.ShopBanners.Where(b => b.BusinessId == businessId);
        if (!string.IsNullOrWhiteSpace(channel))
            query = query.Where(b => b.Channel == channel || b.Channel == null);

        var banners = await query
            .OrderBy(b => b.DisplayOrder)
            .Select(b => new
            {
                b.Id, b.BusinessId, b.Channel, b.Slot, b.Title, b.Subtitle,
                b.ImageUrl, b.RedirectUrl, b.DisplayOrder, b.Active,
                b.StartsAt, b.EndsAt, b.SeasonId, b.CreatedAt, b.UpdatedAt
            })
            .ToListAsync();

        return Ok(banners);
    }

    [HttpPost("{businessId}/banners")]
    public async Task<IActionResult> CreateBanner(int businessId, [FromBody] BannerRequest dto)
    {
        if (!HasBusinessAccess(businessId)) return Forbid();

        var banner = new ShopBanner
        {
            BusinessId = businessId,
            Channel = dto.Channel,
            Slot = dto.Slot ?? "hero",
            Title = dto.Title,
            Subtitle = dto.Subtitle,
            ImageUrl = dto.ImageUrl ?? string.Empty,
            RedirectUrl = dto.RedirectUrl,
            DisplayOrder = dto.DisplayOrder,
            Active = dto.Active,
            StartsAt = dto.StartsAt,
            EndsAt = dto.EndsAt,
            SeasonId = dto.SeasonId,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _db.ShopBanners.Add(banner);
        await _db.SaveChangesAsync();
        return Ok(new { banner.Id });
    }

    [HttpPut("{businessId}/banners/{id}")]
    public async Task<IActionResult> UpdateBanner(int businessId, int id, [FromBody] BannerRequest dto)
    {
        if (!HasBusinessAccess(businessId)) return Forbid();

        var banner = await _db.ShopBanners.FirstOrDefaultAsync(b => b.Id == id && b.BusinessId == businessId);
        if (banner is null) return NotFound();

        banner.Channel = dto.Channel;
        banner.Slot = dto.Slot ?? banner.Slot;
        banner.Title = dto.Title;
        banner.Subtitle = dto.Subtitle;
        banner.ImageUrl = dto.ImageUrl;
        banner.RedirectUrl = dto.RedirectUrl;
        banner.DisplayOrder = dto.DisplayOrder;
        banner.Active = dto.Active;
        banner.StartsAt = dto.StartsAt;
        banner.EndsAt = dto.EndsAt;
        banner.SeasonId = dto.SeasonId;
        banner.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync();
        return Ok(new { banner.Id });
    }

    [HttpDelete("{businessId}/banners/{id}")]
    public async Task<IActionResult> DeleteBanner(int businessId, int id)
    {
        if (!HasBusinessAccess(businessId)) return Forbid();

        var banner = await _db.ShopBanners.FirstOrDefaultAsync(b => b.Id == id && b.BusinessId == businessId);
        if (banner is null) return NotFound();

        _db.ShopBanners.Remove(banner);
        await _db.SaveChangesAsync();
        return NoContent();
    }

    [HttpPost("{businessId}/banners/upload-image")]
    [Consumes("multipart/form-data")]
    public async Task<IActionResult> UploadBannerImage(int businessId, IFormFile file)
    {
        if (!HasBusinessAccess(businessId)) return Forbid();
        if (file == null || file.Length == 0) return BadRequest(new { message = "Archivo requerido" });

        var allowed = new[] { ".jpg", ".jpeg", ".png", ".webp" };
        var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (!allowed.Contains(ext)) return BadRequest(new { message = "Tipo no permitido. Use jpg, png o webp" });
        if (file.Length > 5 * 1024 * 1024) return BadRequest(new { message = "Máximo 5 MB" });

        var dir = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads", "banners");
        Directory.CreateDirectory(dir);

        var fileName = $"{Guid.NewGuid()}{ext}";
        var path = Path.Combine(dir, fileName);
        await using (var stream = new FileStream(path, FileMode.Create))
            await file.CopyToAsync(stream);

        return Ok(new { imageUrl = $"/uploads/banners/{fileName}" });
    }

    [HttpPatch("{businessId}/banners/reorder")]
    public async Task<IActionResult> ReorderBanners(int businessId, [FromBody] List<ReorderItem> items)
    {
        if (!HasBusinessAccess(businessId)) return Forbid();

        var ids = items.Select(i => i.Id).ToList();
        var banners = await _db.ShopBanners.Where(b => b.BusinessId == businessId && ids.Contains(b.Id)).ToListAsync();

        foreach (var item in items)
        {
            var banner = banners.FirstOrDefault(b => b.Id == item.Id);
            if (banner is not null)
            {
                banner.DisplayOrder = item.DisplayOrder;
                banner.UpdatedAt = DateTime.UtcNow;
            }
        }

        await _db.SaveChangesAsync();
        return NoContent();
    }

    // ───────────────────────────────────────────────
    // COLLECTIONS
    // ───────────────────────────────────────────────

    [HttpGet("{businessId}/collections")]
    public async Task<IActionResult> GetCollections(int businessId, [FromQuery] string? channel = null)
    {
        if (!HasBusinessAccess(businessId)) return Forbid();

        var query = _db.ShopCollections.Where(c => c.BusinessId == businessId);
        if (!string.IsNullOrWhiteSpace(channel))
            query = query.Where(c => c.Channel == channel);

        var collections = await query
            .OrderBy(c => c.DisplayOrder)
            .Select(c => new
            {
                c.Id, c.BusinessId, c.Channel, c.Name, c.Slug, c.Description,
                c.CoverImageUrl, c.HeaderColor, c.DisplayAs, c.SortRule, c.DisplayOrder, c.Active,
                c.SeasonId, c.CreatedAt, c.UpdatedAt,
                ItemCount = c.Items.Count
            })
            .ToListAsync();

        return Ok(collections);
    }

    [HttpGet("{businessId}/collections/{id}")]
    public async Task<IActionResult> GetCollection(int businessId, int id)
    {
        if (!HasBusinessAccess(businessId)) return Forbid();

        var collection = await _db.ShopCollections
            .Include(c => c.Items)
                .ThenInclude(i => i.Product)
            .FirstOrDefaultAsync(c => c.Id == id && c.BusinessId == businessId);

        if (collection is null) return NotFound();

        return Ok(new
        {
            collection.Id,
            collection.BusinessId,
            collection.SeasonId,
            collection.Channel,
            collection.Name,
            collection.Slug,
            collection.Description,
            collection.CoverImageUrl,
            collection.HeaderColor,
            collection.DisplayAs,
            collection.SortRule,
            collection.DisplayOrder,
            collection.Active,
            items = collection.Items.Select(i => new
            {
                i.Id,
                i.ProductId,
                i.DisplayOrder,
                i.Pinned,
                productName  = i.Product.Name,
                productSku   = i.Product.Sku,
                productImage = i.Product.Image,
            }).OrderBy(i => i.DisplayOrder).ThenBy(i => i.Pinned ? 0 : 1),
        });
    }

    [HttpPost("{businessId}/collections")]
    public async Task<IActionResult> CreateCollection(int businessId, [FromBody] CollectionRequest dto)
    {
        if (!HasBusinessAccess(businessId)) return Forbid();

        var slugExists = await _db.ShopCollections.AnyAsync(c => c.BusinessId == businessId && c.Slug == dto.Slug);
        if (slugExists) return BadRequest(new { message = "El slug ya existe para este negocio." });

        var collection = new ShopCollection
        {
            BusinessId = businessId,
            Channel = dto.Channel,
            Name = dto.Name,
            Slug = dto.Slug,
            Description = dto.Description,
            CoverImageUrl = dto.CoverImageUrl,
            HeaderColor = dto.HeaderColor,
            DisplayAs = dto.DisplayAs ?? "catalog",
            SortRule = dto.SortRule ?? "manual",
            DisplayOrder = dto.DisplayOrder,
            Active = dto.Active,
            SeasonId = dto.SeasonId,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _db.ShopCollections.Add(collection);
        await _db.SaveChangesAsync();
        return Ok(new { collection.Id });
    }

    [HttpPut("{businessId}/collections/{id}")]
    public async Task<IActionResult> UpdateCollection(int businessId, int id, [FromBody] CollectionRequest dto)
    {
        if (!HasBusinessAccess(businessId)) return Forbid();

        var collection = await _db.ShopCollections.FirstOrDefaultAsync(c => c.Id == id && c.BusinessId == businessId);
        if (collection is null) return NotFound();

        var slugExists = await _db.ShopCollections.AnyAsync(c => c.BusinessId == businessId && c.Slug == dto.Slug && c.Id != id);
        if (slugExists) return BadRequest(new { message = "El slug ya existe para este negocio." });

        collection.Channel = dto.Channel;
        collection.Name = dto.Name;
        collection.Slug = dto.Slug;
        collection.Description = dto.Description;
        collection.CoverImageUrl = dto.CoverImageUrl;
        collection.HeaderColor = dto.HeaderColor;
        collection.DisplayAs = dto.DisplayAs ?? collection.DisplayAs;
        collection.SortRule = dto.SortRule ?? collection.SortRule;
        collection.DisplayOrder = dto.DisplayOrder;
        collection.Active = dto.Active;
        collection.SeasonId = dto.SeasonId;
        collection.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync();
        return Ok(new { collection.Id });
    }

    [HttpDelete("{businessId}/collections/{id}")]
    public async Task<IActionResult> DeleteCollection(int businessId, int id)
    {
        if (!HasBusinessAccess(businessId)) return Forbid();

        var collection = await _db.ShopCollections.FirstOrDefaultAsync(c => c.Id == id && c.BusinessId == businessId);
        if (collection is null) return NotFound();

        _db.ShopCollections.Remove(collection);
        await _db.SaveChangesAsync();
        return NoContent();
    }

    [HttpPost("{businessId}/collections/{id}/items")]
    public async Task<IActionResult> AddCollectionItem(int businessId, int id, [FromBody] CollectionItemRequest dto)
    {
        if (!HasBusinessAccess(businessId)) return Forbid();

        var collection = await _db.ShopCollections.FirstOrDefaultAsync(c => c.Id == id && c.BusinessId == businessId);
        if (collection is null) return NotFound();

        var exists = await _db.ShopCollectionItems.AnyAsync(i => i.CollectionId == id && i.ProductId == dto.ProductId);
        if (exists) return BadRequest(new { message = "El producto ya está en esta colección." });

        var item = new ShopCollectionItem
        {
            CollectionId = id,
            ProductId = dto.ProductId,
            DisplayOrder = dto.DisplayOrder,
            Pinned = dto.Pinned,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _db.ShopCollectionItems.Add(item);
        await _db.SaveChangesAsync();
        return Ok(new { item.Id });
    }

    [HttpDelete("{businessId}/collections/{id}/items/{productId}")]
    public async Task<IActionResult> RemoveCollectionItem(int businessId, int id, int productId)
    {
        if (!HasBusinessAccess(businessId)) return Forbid();

        var item = await _db.ShopCollectionItems.FirstOrDefaultAsync(i => i.CollectionId == id && i.ProductId == productId);
        if (item is null) return NotFound();

        _db.ShopCollectionItems.Remove(item);
        await _db.SaveChangesAsync();
        return NoContent();
    }

    [HttpPatch("{businessId}/collections/{id}/items/reorder")]
    public async Task<IActionResult> ReorderCollectionItems(int businessId, int id, [FromBody] List<ReorderItem> items)
    {
        if (!HasBusinessAccess(businessId)) return Forbid();

        var ids = items.Select(i => i.Id).ToList();
        var existing = await _db.ShopCollectionItems.Where(i => i.CollectionId == id && ids.Contains(i.Id)).ToListAsync();

        foreach (var item in items)
        {
            var row = existing.FirstOrDefault(e => e.Id == item.Id);
            if (row is not null)
            {
                row.DisplayOrder = item.DisplayOrder;
                if (item.Pinned.HasValue) row.Pinned = item.Pinned.Value;
                row.UpdatedAt = DateTime.UtcNow;
            }
        }

        await _db.SaveChangesAsync();
        return NoContent();
    }

    // ───────────────────────────────────────────────
    // CAMPAIGNS
    // ───────────────────────────────────────────────

    [HttpGet("{businessId}/campaigns")]
    public async Task<IActionResult> GetCampaigns(int businessId, [FromQuery] string? channel = null)
    {
        if (!HasBusinessAccess(businessId)) return Forbid();

        var query = _db.ShopCampaigns.Where(c => c.BusinessId == businessId);
        if (!string.IsNullOrWhiteSpace(channel))
            query = query.Where(c => c.Channel == channel);

        var campaigns = await query
            .OrderByDescending(c => c.StartsAt)
            .Select(c => new
            {
                c.Id, c.BusinessId, c.Channel, c.Name, c.Description,
                c.StartsAt, c.EndsAt, c.Active, c.CreatedAt, c.UpdatedAt,
                PromotionCount = c.CampaignPromotions.Count,
                BannerCount = c.CampaignBanners.Count
            })
            .ToListAsync();

        return Ok(campaigns);
    }

    [HttpGet("{businessId}/campaigns/{id}")]
    public async Task<IActionResult> GetCampaign(int businessId, int id)
    {
        if (!HasBusinessAccess(businessId)) return Forbid();

        var campaign = await _db.ShopCampaigns
            .Include(c => c.CampaignPromotions).ThenInclude(cp => cp.Promotion)
            .Include(c => c.CampaignBanners).ThenInclude(cb => cb.Banner)
            .FirstOrDefaultAsync(c => c.Id == id && c.BusinessId == businessId);

        if (campaign is null) return NotFound();
        return Ok(campaign);
    }

    [HttpPost("{businessId}/campaigns")]
    public async Task<IActionResult> CreateCampaign(int businessId, [FromBody] CampaignRequest dto)
    {
        if (!HasBusinessAccess(businessId)) return Forbid();

        var campaign = new ShopCampaign
        {
            BusinessId = businessId,
            Channel = dto.Channel,
            Name = dto.Name,
            Description = dto.Description,
            StartsAt = dto.StartsAt,
            EndsAt = dto.EndsAt,
            Active = dto.Active,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _db.ShopCampaigns.Add(campaign);
        await _db.SaveChangesAsync();
        return Ok(new { campaign.Id });
    }

    [HttpPut("{businessId}/campaigns/{id}")]
    public async Task<IActionResult> UpdateCampaign(int businessId, int id, [FromBody] CampaignRequest dto)
    {
        if (!HasBusinessAccess(businessId)) return Forbid();

        var campaign = await _db.ShopCampaigns.FirstOrDefaultAsync(c => c.Id == id && c.BusinessId == businessId);
        if (campaign is null) return NotFound();

        campaign.Channel = dto.Channel;
        campaign.Name = dto.Name;
        campaign.Description = dto.Description;
        campaign.StartsAt = dto.StartsAt;
        campaign.EndsAt = dto.EndsAt;
        campaign.Active = dto.Active;
        campaign.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync();
        return Ok(new { campaign.Id });
    }

    [HttpDelete("{businessId}/campaigns/{id}")]
    public async Task<IActionResult> DeleteCampaign(int businessId, int id)
    {
        if (!HasBusinessAccess(businessId)) return Forbid();

        var campaign = await _db.ShopCampaigns.FirstOrDefaultAsync(c => c.Id == id && c.BusinessId == businessId);
        if (campaign is null) return NotFound();

        _db.ShopCampaigns.Remove(campaign);
        await _db.SaveChangesAsync();
        return NoContent();
    }

    [HttpPost("{businessId}/campaigns/{id}/promotions")]
    public async Task<IActionResult> AddCampaignPromotion(int businessId, int id, [FromBody] CampaignLinkRequest dto)
    {
        if (!HasBusinessAccess(businessId)) return Forbid();

        var exists = await _db.ShopCampaignPromotions.AnyAsync(cp => cp.CampaignId == id && cp.PromotionId == dto.TargetId);
        if (exists) return BadRequest(new { message = "La promoción ya está en la campaña." });

        _db.ShopCampaignPromotions.Add(new ShopCampaignPromotion { CampaignId = id, PromotionId = dto.TargetId });
        await _db.SaveChangesAsync();
        return NoContent();
    }

    [HttpDelete("{businessId}/campaigns/{id}/promotions/{promotionId}")]
    public async Task<IActionResult> RemoveCampaignPromotion(int businessId, int id, int promotionId)
    {
        if (!HasBusinessAccess(businessId)) return Forbid();

        var link = await _db.ShopCampaignPromotions.FirstOrDefaultAsync(cp => cp.CampaignId == id && cp.PromotionId == promotionId);
        if (link is null) return NotFound();

        _db.ShopCampaignPromotions.Remove(link);
        await _db.SaveChangesAsync();
        return NoContent();
    }

    [HttpPost("{businessId}/campaigns/{id}/banners")]
    public async Task<IActionResult> AddCampaignBanner(int businessId, int id, [FromBody] CampaignLinkRequest dto)
    {
        if (!HasBusinessAccess(businessId)) return Forbid();

        var exists = await _db.ShopCampaignBanners.AnyAsync(cb => cb.CampaignId == id && cb.BannerId == dto.TargetId);
        if (exists) return BadRequest(new { message = "El banner ya está en la campaña." });

        _db.ShopCampaignBanners.Add(new ShopCampaignBanner { CampaignId = id, BannerId = dto.TargetId });
        await _db.SaveChangesAsync();
        return NoContent();
    }

    [HttpDelete("{businessId}/campaigns/{id}/banners/{bannerId}")]
    public async Task<IActionResult> RemoveCampaignBanner(int businessId, int id, int bannerId)
    {
        if (!HasBusinessAccess(businessId)) return Forbid();

        var link = await _db.ShopCampaignBanners.FirstOrDefaultAsync(cb => cb.CampaignId == id && cb.BannerId == bannerId);
        if (link is null) return NotFound();

        _db.ShopCampaignBanners.Remove(link);
        await _db.SaveChangesAsync();
        return NoContent();
    }

    // ───────────────────────────────────────────────
    // SEASONS (Temporadas de catálogo)
    // ───────────────────────────────────────────────

    [HttpGet("{businessId}/seasons")]
    public async Task<IActionResult> GetSeasons(int businessId, [FromQuery] string? channel = null)
    {
        if (!HasBusinessAccess(businessId)) return Forbid();

        var query = _db.ShopSeasons.Where(s => s.BusinessId == businessId);
        if (!string.IsNullOrWhiteSpace(channel))
            query = query.Where(s => s.Channel == channel || s.Channel == null);

        var seasons = await query
            .OrderByDescending(s => s.IsActive)
            .ThenByDescending(s => s.StartsAt)
            .Select(s => new
            {
                s.Id, s.BusinessId, s.Channel, s.Name, s.Description,
                s.CoverImageUrl, s.IsActive, s.StartsAt, s.EndsAt,
                s.CreatedAt, s.UpdatedAt,
                BannerCount = s.Banners.Count,
                CollectionCount = s.Collections.Count,
                CampaignCount = s.Campaigns.Count
            })
            .ToListAsync();

        return Ok(seasons);
    }

    [HttpGet("{businessId}/seasons/{id}")]
    public async Task<IActionResult> GetSeason(int businessId, int id)
    {
        if (!HasBusinessAccess(businessId)) return Forbid();

        var season = await _db.ShopSeasons
            .Include(s => s.Banners)
            .Include(s => s.Collections).ThenInclude(c => c.Items)
            .Include(s => s.Campaigns).ThenInclude(c => c.CampaignPromotions).ThenInclude(cp => cp.Promotion)
            .FirstOrDefaultAsync(s => s.Id == id && s.BusinessId == businessId);

        if (season is null) return NotFound();
        return Ok(season);
    }

    [HttpPost("{businessId}/seasons")]
    public async Task<IActionResult> CreateSeason(int businessId, [FromBody] SeasonRequest dto)
    {
        if (!HasBusinessAccess(businessId)) return Forbid();

        var season = new ShopSeason
        {
            BusinessId = businessId,
            Channel = dto.Channel,
            Name = dto.Name,
            Description = dto.Description,
            CoverImageUrl = dto.CoverImageUrl,
            IsActive = false,
            StartsAt = dto.StartsAt,
            EndsAt = dto.EndsAt,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _db.ShopSeasons.Add(season);
        await _db.SaveChangesAsync();
        return Ok(new { season.Id });
    }

    [HttpPut("{businessId}/seasons/{id}")]
    public async Task<IActionResult> UpdateSeason(int businessId, int id, [FromBody] SeasonRequest dto)
    {
        if (!HasBusinessAccess(businessId)) return Forbid();

        var season = await _db.ShopSeasons.FirstOrDefaultAsync(s => s.Id == id && s.BusinessId == businessId);
        if (season is null) return NotFound();

        season.Channel = dto.Channel;
        season.Name = dto.Name;
        season.Description = dto.Description;
        season.CoverImageUrl = dto.CoverImageUrl;
        season.StartsAt = dto.StartsAt;
        season.EndsAt = dto.EndsAt;
        season.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync();
        return Ok(new { season.Id });
    }

    [HttpDelete("{businessId}/seasons/{id}")]
    public async Task<IActionResult> DeleteSeason(int businessId, int id)
    {
        if (!HasBusinessAccess(businessId)) return Forbid();

        var season = await _db.ShopSeasons.FirstOrDefaultAsync(s => s.Id == id && s.BusinessId == businessId);
        if (season is null) return NotFound();
        if (season.IsActive) return BadRequest(new { message = "No se puede eliminar la temporada activa. Desactívala primero." });

        _db.ShopSeasons.Remove(season);
        await _db.SaveChangesAsync();
        return NoContent();
    }

    /// <summary>
    /// Activa una temporada: desactiva todas las demás del mismo canal y marca ésta como activa.
    /// Todo el catálogo visible en webadas cambia inmediatamente a los contenidos de esta temporada.
    /// </summary>
    [HttpPost("{businessId}/seasons/{id}/activate")]
    public async Task<IActionResult> ActivateSeason(int businessId, int id)
    {
        if (!HasBusinessAccess(businessId)) return Forbid();

        var season = await _db.ShopSeasons.FirstOrDefaultAsync(s => s.Id == id && s.BusinessId == businessId);
        if (season is null) return NotFound();
        if (season.IsActive) return Ok(new { message = "La temporada ya está activa." });

        // Desactivar todas las temporadas del mismo canal para este negocio
        var othersToDeactivate = await _db.ShopSeasons
            .Where(s => s.BusinessId == businessId && s.IsActive
                && (s.Channel == season.Channel || s.Channel == null || season.Channel == null))
            .ToListAsync();

        foreach (var other in othersToDeactivate)
        {
            other.IsActive = false;
            other.UpdatedAt = DateTime.UtcNow;
        }

        season.IsActive = true;
        season.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        return Ok(new
        {
            message = $"Temporada '{season.Name}' activada. El catálogo de {season.Channel ?? "todos los canales"} fue actualizado.",
            seasonId = season.Id,
            seasonName = season.Name
        });
    }

    /// <summary>Desactiva la temporada activa, volviendo al catálogo base (contenido sin temporada asignada).</summary>
    [HttpPost("{businessId}/seasons/{id}/deactivate")]
    public async Task<IActionResult> DeactivateSeason(int businessId, int id)
    {
        if (!HasBusinessAccess(businessId)) return Forbid();

        var season = await _db.ShopSeasons.FirstOrDefaultAsync(s => s.Id == id && s.BusinessId == businessId);
        if (season is null) return NotFound();

        season.IsActive = false;
        season.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        return Ok(new { message = "Temporada desactivada. Volviendo al catálogo base.", seasonId = season.Id });
    }

    // ───────────────────────────────────────────────
    // ORDERS (Sales con canal e-commerce)
    // ───────────────────────────────────────────────

    [HttpGet("{businessId}/orders")]
    public async Task<IActionResult> GetOrders(int businessId, [FromQuery] string? channel = null, [FromQuery] string? shippingStatus = null, [FromQuery] int page = 1, [FromQuery] int pageSize = 20)
    {
        if (!HasBusinessAccess(businessId)) return Forbid();

        var query = _db.Sales
            .Include(s => s.Store)
            .Where(s => s.Store != null && s.Store.BusinessId == businessId && s.Channel != null);

        if (!string.IsNullOrWhiteSpace(channel))
            query = query.Where(s => s.Channel == channel);

        if (!string.IsNullOrWhiteSpace(shippingStatus))
            query = query.Where(s => s.ShippingStatus == shippingStatus);

        var total = await query.CountAsync();

        var orders = await query
            .OrderByDescending(s => s.Date)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(s => new
            {
                s.Id, s.Date, s.Total, s.Channel,
                StoreName = s.Store != null ? s.Store.Name : null,
                s.ShippingName, s.ShippingPhone, s.ShippingAddress,
                s.ShippingCity, s.ShippingRegion, s.ShippingStatus,
                s.TrackingNumber, s.ShippingCarrier
            })
            .ToListAsync();

        return Ok(new { total, page, pageSize, data = orders });
    }

    [HttpGet("{businessId}/orders/{saleId}")]
    public async Task<IActionResult> GetOrder(int businessId, int saleId)
    {
        if (!HasBusinessAccess(businessId)) return Forbid();

        var order = await _db.Sales
            .Include(s => s.Store)
            .Include(s => s.SaleDetails).ThenInclude(d => d.Product)
            .Include(s => s.PaymentMethod)
            .FirstOrDefaultAsync(s => s.Id == saleId && s.Store != null && s.Store.BusinessId == businessId);

        if (order is null) return NotFound();
        return Ok(order);
    }

    [HttpPatch("{businessId}/orders/{saleId}/shipping")]
    public async Task<IActionResult> UpdateOrderShipping(int businessId, int saleId, [FromBody] ShippingUpdateRequest dto)
    {
        if (!HasBusinessAccess(businessId)) return Forbid();

        var order = await _db.Sales
            .Include(s => s.Store)
            .FirstOrDefaultAsync(s => s.Id == saleId && s.Store != null && s.Store.BusinessId == businessId);

        if (order is null) return NotFound();

        if (dto.ShippingStatus is not null) order.ShippingStatus = dto.ShippingStatus;
        if (dto.TrackingNumber is not null) order.TrackingNumber = dto.TrackingNumber;
        if (dto.ShippingCarrier is not null) order.ShippingCarrier = dto.ShippingCarrier;
        if (dto.ShippingNotes is not null) order.ShippingNotes = dto.ShippingNotes;

        await _db.SaveChangesAsync();
        return Ok(new
        {
            order.Id, order.ShippingStatus, order.TrackingNumber,
            order.ShippingCarrier, order.ShippingNotes
        });
    }
}

// ───────────────────────────────────────────────
// DTOs
// ───────────────────────────────────────────────

public record BannerRequest(
    string? Channel,
    string? Slot,
    string Title,
    string? Subtitle,
    string? ImageUrl,
    string? RedirectUrl,
    int DisplayOrder,
    bool Active,
    DateTime? StartsAt,
    DateTime? EndsAt,
    int? SeasonId
);

public record CollectionRequest(
    string? Channel,
    string Name,
    string Slug,
    string? Description,
    string? CoverImageUrl,
    string? HeaderColor,
    string? DisplayAs,
    string? SortRule,
    int DisplayOrder,
    bool Active,
    int? SeasonId
);

public record CollectionItemRequest(
    int ProductId,
    int DisplayOrder,
    bool Pinned
);

public record CampaignRequest(
    string? Channel,
    string Name,
    string? Description,
    DateTime? StartsAt,
    DateTime? EndsAt,
    bool Active
);

public record CampaignLinkRequest(int TargetId);

public record ReorderItem(int Id, int DisplayOrder, bool? Pinned = null);

public record SeasonRequest(
    string? Channel,
    string Name,
    string? Description,
    string? CoverImageUrl,
    DateTime? StartsAt,
    DateTime? EndsAt
);

public record ShippingUpdateRequest(
    string? ShippingStatus,
    string? TrackingNumber,
    string? ShippingCarrier,
    string? ShippingNotes
);
