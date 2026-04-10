#pragma warning disable CS8601
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Cors;
using Microsoft.AspNetCore.Mvc;
using MySqlConnector;
using System.Security.Claims;
using System.Text.Json;
using GPInventory.Api.Services;

namespace GPInventory.Api.Controllers;

/// <summary>
/// GP Assets — Módulo de Activos Fijos.
/// Gestión de activos empresariales con depreciación SII Chile (línea recta).
/// </summary>
[ApiController]
[Route("api/assets")]
[EnableCors("AllowFrontend")]
[Authorize]
public class AssetController : ControllerBase
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<AssetController> _logger;
    private readonly AssetSseService _sse;
    private readonly IWebHostEnvironment _env;

    public AssetController(
        IConfiguration configuration,
        ILogger<AssetController> logger,
        AssetSseService sse,
        IWebHostEnvironment env)
    {
        _configuration = configuration;
        _logger        = logger;
        _sse           = sse;
        _env           = env;
    }

    private MySqlConnection GetConnection()
        => new(_configuration.GetConnectionString("DefaultConnection")!);

    private static bool IsNull(MySqlDataReader r, string col) => r.IsDBNull(r.GetOrdinal(col));

    private int? GetCurrentUserId()
    {
        var claim = User.FindFirst("sub")
                 ?? User.FindFirst("userId")
                 ?? User.FindFirst("id")
                 ?? User.FindFirst(ClaimTypes.NameIdentifier);
        return int.TryParse(claim?.Value, out int id) ? id : null;
    }

    // ── Depreciación SII Chile (línea recta) ─────────────────────────────
    // Resolución Exenta SII N°3/2021. Método único permitido: línea recta.
    //   Cuota anual   = (Costo − Valor residual) / Vida útil
    //   Cuota mensual = cuota anual / 12
    //   Valor libro   = Costo − Depreciación acumulada
    private static object CalculateDepreciation(
        decimal cost, decimal residualValue, int usefulLifeYears, DateTime acquisitionDate)
    {
        var today         = DateTime.Today;
        var monthsElapsed = Math.Max(0,
            (today.Year - acquisitionDate.Year) * 12 + (today.Month - acquisitionDate.Month));

        var depreciableAmount = cost - residualValue;
        var annualDepr        = usefulLifeYears > 0 ? depreciableAmount / usefulLifeYears : 0;
        var monthlyDepr       = annualDepr / 12;
        var accumulated       = Math.Min(monthlyDepr * monthsElapsed, depreciableAmount);
        var bookValue         = cost - accumulated;
        var deprPercent       = depreciableAmount > 0 ? accumulated / depreciableAmount * 100 : 0;
        var fullyDepreciated  = accumulated >= depreciableAmount;

        return new
        {
            annualDepreciation     = Math.Round(annualDepr,  2),
            monthlyDepreciation    = Math.Round(monthlyDepr, 2),
            accumulatedDepreciation= Math.Round(accumulated, 2),
            bookValue              = Math.Round(bookValue,   2),
            depreciationPercent    = Math.Round(deprPercent, 2),
            monthsElapsed,
            yearsElapsed           = Math.Round(monthsElapsed / 12.0, 2),
            fullyDepreciated,
        };
    }

    // ====================================================================
    // SSE — real-time stream
    // EventSource no puede enviar Authorization header, por eso el token
    // se pasa como query string y se valida manualmente.
    // ====================================================================

    /// GET /api/assets/events?businessId=X&token=JWT
    [HttpGet("events")]
    [AllowAnonymous]
    public async Task StreamEvents([FromQuery] int businessId, [FromQuery] string token, CancellationToken ct)
    {
        // Validate JWT manually
        if (string.IsNullOrWhiteSpace(token))
        {
            Response.StatusCode = 401;
            return;
        }
        try
        {
            var jwtSettings = _configuration.GetSection("JwtSettings");
            var secretKey   = jwtSettings["SecretKey"];
            if (string.IsNullOrEmpty(secretKey)) { Response.StatusCode = 401; return; }

            var tokenHandler = new System.IdentityModel.Tokens.Jwt.JwtSecurityTokenHandler();
            tokenHandler.ValidateToken(token,
                new Microsoft.IdentityModel.Tokens.TokenValidationParameters
                {
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey         = new Microsoft.IdentityModel.Tokens.SymmetricSecurityKey(
                        System.Text.Encoding.UTF8.GetBytes(secretKey)),
                    ValidateIssuer   = true,
                    ValidIssuer      = jwtSettings["Issuer"],
                    ValidateAudience = true,
                    ValidAudience    = jwtSettings["Audience"],
                    ClockSkew        = TimeSpan.Zero,
                },
                out _);
        }
        catch
        {
            Response.StatusCode = 401;
            return;
        }

        Response.ContentType = "text/event-stream";
        Response.Headers["Cache-Control"]   = "no-cache";
        Response.Headers["X-Accel-Buffering"] = "no";

        var ch = _sse.Subscribe(businessId);
        try
        {
            // Send initial heartbeat
            await Response.WriteAsync(": connected\n\n", ct);
            await Response.Body.FlushAsync(ct);

            while (!ct.IsCancellationRequested)
            {
                while (ch.Reader.TryRead(out var msg))
                {
                    await Response.WriteAsync(msg, ct);
                    await Response.Body.FlushAsync(ct);
                }
                // Heartbeat every 25 s
                var read = await ch.Reader.WaitToReadAsync(ct).AsTask()
                    .WaitAsync(TimeSpan.FromSeconds(25), ct)
                    .ContinueWith(t => !t.IsFaulted && !t.IsCanceled && t.Result, ct);
                if (!read)
                {
                    await Response.WriteAsync(": heartbeat\n\n", ct);
                    await Response.Body.FlushAsync(ct);
                }
            }
        }
        catch (OperationCanceledException) { /* client disconnected */ }
        finally { _sse.Unsubscribe(businessId, ch); }
    }

    // ====================================================================
    // CATEGORIES
    // ====================================================================

    /// GET /api/assets/categories
    [HttpGet("categories")]
    public async Task<IActionResult> GetCategories()
    {
        try
        {
            using var conn = GetConnection();
            await conn.OpenAsync();
            using var cmd = new MySqlCommand(
                "SELECT id, name, sii_useful_years, description FROM asset_category WHERE active=1 ORDER BY name",
                conn);
            using var r   = await cmd.ExecuteReaderAsync();
            var list      = new List<object>();
            while (await r.ReadAsync())
                list.Add(new {
                    id            = r.GetInt32("id"),
                    name          = r.GetString("name"),
                    siiUsefulYears= r.GetInt32("sii_useful_years"),
                    description   = IsNull(r, "description") ? null : r.GetString("description"),
                });
            return Ok(list);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error obteniendo categorías de activos");
            return StatusCode(500, new { message = "Error interno del servidor" });
        }
    }

    // ====================================================================
    // ASSETS LIST
    // ====================================================================

    /// GET /api/assets?businessId=&storeId=&categoryId=&status=
    [HttpGet]
    public async Task<IActionResult> GetAssets(
        [FromQuery] int  businessId,
        [FromQuery] int? storeId,
        [FromQuery] int? categoryId,
        [FromQuery] string? status)
    {
        try
        {
            using var conn = GetConnection();
            await conn.OpenAsync();

            var where = new List<string> { "a.business_id=@Biz", "a.parent_asset_id IS NULL" };
            if (storeId.HasValue)    where.Add("a.store_id=@StoreId");
            if (categoryId.HasValue) where.Add("a.category_id=@CatId");
            if (!string.IsNullOrEmpty(status)) where.Add("a.status=@Status");
            else where.Add("a.status != 'disposed'");   // default: exclude disposed

            using var cmd = new MySqlCommand($@"
                SELECT
                    a.id, a.name, a.description, a.serial_number,
                    a.acquisition_date, a.acquisition_cost, a.residual_value,
                    a.useful_life_years, a.depreciation_method, a.status,
                    a.photo_url, a.expense_id, a.invoice_number, a.notes,
                    a.is_pack, a.cost_percentage,
                    a.store_id,  s.name  AS store_name,
                    a.category_id, c.name AS category_name, c.sii_useful_years,
                    a.created_at, a.created_by_user_id
                FROM asset a
                LEFT JOIN store          s ON s.id = a.store_id
                LEFT JOIN asset_category c ON c.id = a.category_id
                WHERE {string.Join(" AND ", where)}
                ORDER BY a.acquisition_date DESC", conn);

            cmd.Parameters.AddWithValue("@Biz", businessId);
            if (storeId.HasValue)    cmd.Parameters.AddWithValue("@StoreId", storeId.Value);
            if (categoryId.HasValue) cmd.Parameters.AddWithValue("@CatId",   categoryId.Value);
            if (!string.IsNullOrEmpty(status)) cmd.Parameters.AddWithValue("@Status", status);

            using var r  = await cmd.ExecuteReaderAsync();
            // Collect raw rows first so we can close reader before running child queries
            var rows = new List<(int id, string name, string? desc, string? sn,
                DateTime acqDt, decimal cost, decimal residual, int lifeYears,
                string method, string status2, string? photo, int? expId,
                string? invoice, string? notes2, bool isPack, decimal? costPct,
                int? storeId2, string? storeName, int catId, string catName,
                int siiYears, DateTime createdAt)>();

            while (await r.ReadAsync())
                rows.Add((
                    r.GetInt32("id"), r.GetString("name"),
                    IsNull(r,"description")   ? null : r.GetString("description"),
                    IsNull(r,"serial_number") ? null : r.GetString("serial_number"),
                    r.GetDateTime("acquisition_date"),
                    r.GetDecimal("acquisition_cost"), r.GetDecimal("residual_value"),
                    r.GetInt32("useful_life_years"), r.GetString("depreciation_method"),
                    r.GetString("status"),
                    IsNull(r,"photo_url")      ? null : r.GetString("photo_url"),
                    IsNull(r,"expense_id")     ? (int?)null : r.GetInt32("expense_id"),
                    IsNull(r,"invoice_number") ? null : r.GetString("invoice_number"),
                    IsNull(r,"notes")          ? null : r.GetString("notes"),
                    r.GetBoolean("is_pack"),
                    IsNull(r,"cost_percentage") ? (decimal?)null : r.GetDecimal("cost_percentage"),
                    IsNull(r,"store_id")  ? (int?)null : r.GetInt32("store_id"),
                    IsNull(r,"store_name")? null : r.GetString("store_name"),
                    r.GetInt32("category_id"), r.GetString("category_name"),
                    r.GetInt32("sii_useful_years"), r.GetDateTime("created_at")));
            await r.CloseAsync();

            var list = new List<object>();
            foreach (var row in rows)
            {
                object deprObj;
                if (row.isPack)
                {
                    // Aggregate children depreciation
                    using var childCmd = new MySqlCommand(@"
                        SELECT acquisition_cost, residual_value, useful_life_years, acquisition_date
                        FROM asset WHERE parent_asset_id=@PId", conn);
                    childCmd.Parameters.AddWithValue("@PId", row.id);
                    using var cr = await childCmd.ExecuteReaderAsync();
                    decimal totalAnnual=0, totalMonthly=0, totalAccumulated=0, totalBook=0;
                    while (await cr.ReadAsync())
                    {
                        var cd = (dynamic)CalculateDepreciation(
                            cr.GetDecimal(0), cr.GetDecimal(1), cr.GetInt32(2), cr.GetDateTime(3));
                        totalAnnual      += (decimal)cd.annualDepreciation;
                        totalMonthly     += (decimal)cd.monthlyDepreciation;
                        totalAccumulated += (decimal)cd.accumulatedDepreciation;
                        totalBook        += (decimal)cd.bookValue;
                    }
                    await cr.CloseAsync();
                    var totalCost = row.cost;
                    var totalDeprPct = totalCost > 0 ? totalAccumulated / totalCost * 100 : 0;
                    deprObj = new {
                        annualDepreciation      = Math.Round(totalAnnual, 2),
                        monthlyDepreciation     = Math.Round(totalMonthly, 2),
                        accumulatedDepreciation = Math.Round(totalAccumulated, 2),
                        bookValue               = Math.Round(totalBook, 2),
                        depreciationPercent     = Math.Round(totalDeprPct, 2),
                        monthsElapsed = 0, yearsElapsed = 0.0, fullyDepreciated = false,
                    };
                }
                else
                {
                    deprObj = CalculateDepreciation(row.cost, row.residual, row.lifeYears, row.acqDt);
                }

                list.Add(new {
                    id               = row.id,
                    name             = row.name,
                    description      = row.desc,
                    serialNumber     = row.sn,
                    acquisitionDate  = row.acqDt.ToString("yyyy-MM-dd"),
                    acquisitionCost  = row.cost,
                    residualValue    = row.residual,
                    usefulLifeYears  = row.lifeYears,
                    depreciationMethod = row.method,
                    status           = row.status2,
                    photoUrl         = row.photo,
                    expenseId        = row.expId,
                    invoiceNumber    = row.invoice,
                    notes            = row.notes2,
                    isPack           = row.isPack,
                    costPercentage   = row.costPct,
                    storeId          = row.storeId2,
                    storeName        = row.storeName,
                    categoryId       = row.catId,
                    categoryName     = row.catName,
                    siiUsefulYears   = row.siiYears,
                    createdAt        = row.createdAt.ToString("o"),
                    depreciation     = deprObj,
                });
            }
            return Ok(list);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error listando activos del negocio {Id}", businessId);
            return StatusCode(500, new { message = "Error interno del servidor" });
        }
    }

    // ====================================================================
    // SINGLE ASSET
    // ====================================================================

    /// GET /api/assets/{id}
    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetAsset(int id)
    {
        try
        {
            using var conn = GetConnection();
            await conn.OpenAsync();
            using var cmd = new MySqlCommand(@"
                SELECT
                    a.id, a.business_id, a.name, a.description, a.serial_number,
                    a.acquisition_date, a.acquisition_cost, a.residual_value,
                    a.useful_life_years, a.depreciation_method, a.status,
                    a.photo_url, a.expense_id, a.invoice_number, a.notes,
                    a.is_pack, a.parent_asset_id, a.cost_percentage,
                    a.store_id,  s.name  AS store_name,
                    a.category_id, c.name AS category_name, c.sii_useful_years, c.description AS cat_description,
                    a.created_at, a.created_by_user_id
                FROM asset a
                LEFT JOIN store          s ON s.id = a.store_id
                LEFT JOIN asset_category c ON c.id = a.category_id
                WHERE a.id=@Id", conn);
            cmd.Parameters.AddWithValue("@Id", id);
            using var r = await cmd.ExecuteReaderAsync();
            if (!await r.ReadAsync()) return NotFound();

            var cost         = r.GetDecimal("acquisition_cost");
            var residual     = r.GetDecimal("residual_value");
            var lifeYears    = r.GetInt32("useful_life_years");
            var acquisitionDt= r.GetDateTime("acquisition_date");
            var isPack       = r.GetBoolean("is_pack");
            var parentId     = IsNull(r, "parent_asset_id") ? (int?)null : r.GetInt32("parent_asset_id");
            var costPct      = IsNull(r, "cost_percentage")  ? (decimal?)null : r.GetDecimal("cost_percentage");

            var assetData = new {
                id               = r.GetInt32("id"),
                businessId       = r.GetInt32("business_id"),
                name             = r.GetString("name"),
                description      = IsNull(r, "description")    ? null : r.GetString("description"),
                serialNumber     = IsNull(r, "serial_number")  ? null : r.GetString("serial_number"),
                acquisitionDate  = acquisitionDt.ToString("yyyy-MM-dd"),
                acquisitionCost  = cost,
                residualValue    = residual,
                usefulLifeYears  = lifeYears,
                depreciationMethod = r.GetString("depreciation_method"),
                status           = r.GetString("status"),
                photoUrl         = IsNull(r, "photo_url")      ? null : r.GetString("photo_url"),
                expenseId        = IsNull(r, "expense_id")     ? (int?)null : r.GetInt32("expense_id"),
                invoiceNumber    = IsNull(r, "invoice_number") ? null : r.GetString("invoice_number"),
                notes            = IsNull(r, "notes")          ? null : r.GetString("notes"),
                isPack,
                parentAssetId    = parentId,
                costPercentage   = costPct,
                storeId          = IsNull(r, "store_id")       ? (int?)null : r.GetInt32("store_id"),
                storeName        = IsNull(r, "store_name")     ? null : r.GetString("store_name"),
                categoryId       = r.GetInt32("category_id"),
                categoryName     = r.GetString("category_name"),
                siiUsefulYears   = r.GetInt32("sii_useful_years"),
                categoryDescription = IsNull(r, "cat_description") ? null : r.GetString("cat_description"),
                createdAt        = r.GetDateTime("created_at").ToString("o"),
            };
            await r.CloseAsync();

            // Load children if it's a pack
            var components = new List<object>();
            if (isPack)
            {
                using var childCmd = new MySqlCommand(@"
                    SELECT a.id, a.name, a.description, a.serial_number,
                           a.acquisition_date, a.acquisition_cost, a.residual_value,
                           a.useful_life_years, a.status, a.cost_percentage,
                           a.category_id, c.name AS category_name, c.sii_useful_years
                    FROM asset a
                    LEFT JOIN asset_category c ON c.id = a.category_id
                    WHERE a.parent_asset_id=@PId
                    ORDER BY a.cost_percentage DESC", conn);
                childCmd.Parameters.AddWithValue("@PId", id);
                using var cr = await childCmd.ExecuteReaderAsync();
                decimal totalAnnual=0, totalMonthly=0, totalAccumulated=0, totalBook=0;
                while (await cr.ReadAsync())
                {
                    var cc   = cr.GetDecimal("acquisition_cost");
                    var cr2  = cr.GetDecimal("residual_value");
                    var cl   = cr.GetInt32("useful_life_years");
                    var cDt  = cr.GetDateTime("acquisition_date");
                    var cd   = (dynamic)CalculateDepreciation(cc, cr2, cl, cDt);
                    totalAnnual      += (decimal)cd.annualDepreciation;
                    totalMonthly     += (decimal)cd.monthlyDepreciation;
                    totalAccumulated += (decimal)cd.accumulatedDepreciation;
                    totalBook        += (decimal)cd.bookValue;
                    components.Add(new {
                        id              = cr.GetInt32("id"),
                        name            = cr.GetString("name"),
                        description     = IsNull(cr,"description")    ? null : cr.GetString("description"),
                        serialNumber    = IsNull(cr,"serial_number")  ? null : cr.GetString("serial_number"),
                        acquisitionDate = cDt.ToString("yyyy-MM-dd"),
                        acquisitionCost = cc,
                        residualValue   = cr2,
                        usefulLifeYears = cl,
                        status          = cr.GetString("status"),
                        costPercentage  = IsNull(cr,"cost_percentage") ? (decimal?)null : cr.GetDecimal("cost_percentage"),
                        categoryId      = cr.GetInt32("category_id"),
                        categoryName    = cr.GetString("category_name"),
                        siiUsefulYears  = cr.GetInt32("sii_useful_years"),
                        depreciation    = (object)cd,
                    });
                }
                await cr.CloseAsync();

                var deprAgg = new {
                    annualDepreciation      = Math.Round(totalAnnual, 2),
                    monthlyDepreciation     = Math.Round(totalMonthly, 2),
                    accumulatedDepreciation = Math.Round(totalAccumulated, 2),
                    bookValue               = Math.Round(totalBook, 2),
                    depreciationPercent     = cost > 0 ? Math.Round(totalAccumulated / cost * 100, 2) : 0m,
                    monthsElapsed = 0, yearsElapsed = 0.0, fullyDepreciated = false,
                };
                return Ok(new { assetData.id, assetData.businessId, assetData.name, assetData.description,
                    assetData.serialNumber, assetData.acquisitionDate, assetData.acquisitionCost,
                    assetData.residualValue, assetData.usefulLifeYears, assetData.depreciationMethod,
                    assetData.status, assetData.photoUrl, assetData.expenseId, assetData.invoiceNumber,
                    assetData.notes, assetData.isPack, assetData.parentAssetId, assetData.costPercentage,
                    assetData.storeId, assetData.storeName, assetData.categoryId, assetData.categoryName,
                    assetData.siiUsefulYears, assetData.categoryDescription, assetData.createdAt,
                    depreciation = deprAgg, components });
            }

            return Ok(new { assetData.id, assetData.businessId, assetData.name, assetData.description,
                assetData.serialNumber, assetData.acquisitionDate, assetData.acquisitionCost,
                assetData.residualValue, assetData.usefulLifeYears, assetData.depreciationMethod,
                assetData.status, assetData.photoUrl, assetData.expenseId, assetData.invoiceNumber,
                assetData.notes, assetData.isPack, assetData.parentAssetId, assetData.costPercentage,
                assetData.storeId, assetData.storeName, assetData.categoryId, assetData.categoryName,
                assetData.siiUsefulYears, assetData.categoryDescription, assetData.createdAt,
                depreciation = CalculateDepreciation(cost, residual, lifeYears, acquisitionDt),
                components });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error obteniendo activo {Id}", id);
            return StatusCode(500, new { message = "Error interno del servidor" });
        }
    }

    // ====================================================================
    // CREATE ASSET
    // ====================================================================

    /// POST /api/assets
    [HttpPost]
    public async Task<IActionResult> CreateAsset([FromBody] JsonElement body)
    {
        var userId = GetCurrentUserId();
        if (userId == null) return Unauthorized();

        try
        {
            var businessId      = body.GetProperty("businessId").GetInt32();
            var categoryId      = body.GetProperty("categoryId").GetInt32();
            var name            = body.GetProperty("name").GetString()!;
            var acquisitionDate = DateTime.Parse(body.GetProperty("acquisitionDate").GetString()!);
            var acquisitionCost = body.GetProperty("acquisitionCost").GetDecimal();
            var usefulLifeYears = body.GetProperty("usefulLifeYears").GetInt32();
            int? storeId        = body.TryGetProperty("storeId",       out var si)  && si.ValueKind  != JsonValueKind.Null ? si.GetInt32()     : (int?)null;
            var description     = body.TryGetProperty("description",    out var d)   ? d.GetString()  : null;
            var serialNumber    = body.TryGetProperty("serialNumber",   out var sn)  ? sn.GetString() : null;
            var invoiceNumber   = body.TryGetProperty("invoiceNumber",  out var inv) ? inv.GetString(): null;
            var notes           = body.TryGetProperty("notes",          out var no)  ? no.GetString() : null;
            var residualValue   = body.TryGetProperty("residualValue",  out var rv)  && rv.ValueKind != JsonValueKind.Null ? rv.GetDecimal() : 0m;
            var registerExpense = body.TryGetProperty("registerExpense", out var re)  && re.GetBoolean();
            var expenseDate     = body.TryGetProperty("expenseDate",    out var ed)  ? ed.GetString() : acquisitionDate.ToString("yyyy-MM-dd");
            var receiptTypeId   = body.TryGetProperty("receiptTypeId",  out var rt)  && rt.ValueKind != JsonValueKind.Null ? rt.GetInt32() : 1;

            using var conn = GetConnection();
            await conn.OpenAsync();
            using var tx   = await conn.BeginTransactionAsync();

            int? expenseId = null;

            // ── Registrar egreso en GP Outflow si se solicitó ──────────────
            if (registerExpense && acquisitionCost > 0)
            {
                // Find-or-create expense category "Activos Fijos"
                int catId;
                using (var catCmd = new MySqlCommand(
                    "SELECT id FROM expense_category WHERE name='Activos Fijos' LIMIT 1", conn, tx))
                {
                    var catRes = await catCmd.ExecuteScalarAsync();
                    if (catRes != null && catRes != DBNull.Value)
                    {
                        catId = Convert.ToInt32(catRes);
                    }
                    else
                    {
                        using var insertCat = new MySqlCommand(
                            "INSERT INTO expense_category (name) VALUES ('Activos Fijos'); SELECT LAST_INSERT_ID();",
                            conn, tx);
                        catId = Convert.ToInt32(await insertCat.ExecuteScalarAsync());
                    }
                }

                // Find-or-create expense subcategory for the asset category name
                int subId;
                using (var subCmd = new MySqlCommand(
                    "SELECT id FROM expense_subcategory WHERE expense_category_id=@C AND name=@Cat LIMIT 1", conn, tx))
                {
                    subCmd.Parameters.AddWithValue("@C",   catId);
                    subCmd.Parameters.AddWithValue("@Cat", "Activos Fijos");
                    var subRes = await subCmd.ExecuteScalarAsync();
                    if (subRes != null && subRes != DBNull.Value)
                    {
                        subId = Convert.ToInt32(subRes);
                    }
                    else
                    {
                        using var insertSub = new MySqlCommand(
                            "INSERT INTO expense_subcategory (expense_category_id, name) VALUES (@C, @Cat); SELECT LAST_INSERT_ID();",
                            conn, tx);
                        insertSub.Parameters.AddWithValue("@C",   catId);
                        insertSub.Parameters.AddWithValue("@Cat", "Activos Fijos");
                        subId = Convert.ToInt32(await insertSub.ExecuteScalarAsync());
                    }
                }

                // Cálculo de neto / IVA / total según tipo de comprobante
                // receipt_type_id: 1=Boleta, 2=Factura exenta, 3=Factura afecta, 4=Sin documento
                // El acquisitionCost se trata como monto NETO (SII: base depreciable = costo neto)
                decimal amtNet   = acquisitionCost;
                decimal amtIva   = receiptTypeId == 3 ? Math.Round(acquisitionCost * 0.19m, 2) : 0m;
                decimal amtTotal = amtNet + amtIva;

                using var expCmd = new MySqlCommand(@"
                    INSERT INTO expenses
                        (subcategory_id, amount, description, date, business_id,
                         receipt_type_id, expense_type_id, is_paid,
                         amount_net, amount_iva, amount_total, notes)
                    VALUES
                        (@Sub, @Amt, @Desc, @Dt, @Biz,
                         @Rct, 3, 1,
                         @Net, @Iva, @Total, @Notes);
                    SELECT LAST_INSERT_ID();", conn, tx);
                expCmd.Parameters.AddWithValue("@Sub",   subId);
                expCmd.Parameters.AddWithValue("@Amt",   amtTotal);
                expCmd.Parameters.AddWithValue("@Desc",  $"Adquisición de activo: {name}");
                expCmd.Parameters.AddWithValue("@Dt",    expenseDate);
                expCmd.Parameters.AddWithValue("@Biz",   businessId);
                expCmd.Parameters.AddWithValue("@Rct",   receiptTypeId);
                expCmd.Parameters.AddWithValue("@Net",   amtNet);
                expCmd.Parameters.AddWithValue("@Iva",   amtIva);
                expCmd.Parameters.AddWithValue("@Total", amtTotal);
                expCmd.Parameters.AddWithValue("@Notes", notes ?? $"Activo registrado en GP Assets. Factura: {invoiceNumber ?? "N/A"}");
                expenseId = Convert.ToInt32(await expCmd.ExecuteScalarAsync());
            }

            // ── Parse pack components if any ─────────────────────────────
            var isPack   = body.TryGetProperty("isPack", out var ip) && ip.GetBoolean();
            var compJson = body.TryGetProperty("components", out var cj) ? cj : (JsonElement?)null;

            // Validate pack: components % must sum to ~100
            if (isPack && compJson.HasValue && compJson.Value.ValueKind == JsonValueKind.Array)
            {
                var totalPct = 0m;
                foreach (var comp in compJson.Value.EnumerateArray())
                    totalPct += comp.TryGetProperty("costPercentage", out var cpv) ? cpv.GetDecimal() : 0;
                if (Math.Abs(totalPct - 100m) > 0.5m)
                {
                    await tx.RollbackAsync();
                    return BadRequest(new { message = $"Los porcentajes suman {totalPct}%, deben sumar 100%" });
                }
            }

            // ── Insert parent asset ───────────────────────────────────────
            using var assetCmd = new MySqlCommand(@"
                INSERT INTO asset
                    (business_id, store_id, category_id, name, description, serial_number,
                     acquisition_date, acquisition_cost, residual_value, useful_life_years,
                     depreciation_method, status, is_pack, invoice_number, expense_id, notes, created_by_user_id)
                VALUES
                    (@Biz, @Store, @Cat, @Name, @Desc, @SN,
                     @AcqDate, @Cost, @Residual, @Life,
                     'linear', 'active', @IsPack, @Inv, @ExpId, @Notes, @Uid);
                SELECT LAST_INSERT_ID();", conn, tx);

            assetCmd.Parameters.AddWithValue("@Biz",      businessId);
            assetCmd.Parameters.AddWithValue("@Store",    (object?)storeId ?? DBNull.Value);
            assetCmd.Parameters.AddWithValue("@Cat",      categoryId);
            assetCmd.Parameters.AddWithValue("@Name",     name);
            assetCmd.Parameters.AddWithValue("@Desc",     (object?)description ?? DBNull.Value);
            assetCmd.Parameters.AddWithValue("@SN",       (object?)serialNumber ?? DBNull.Value);
            assetCmd.Parameters.AddWithValue("@AcqDate",  acquisitionDate.ToString("yyyy-MM-dd"));
            assetCmd.Parameters.AddWithValue("@Cost",     acquisitionCost);
            assetCmd.Parameters.AddWithValue("@Residual", residualValue);
            assetCmd.Parameters.AddWithValue("@Life",     usefulLifeYears);
            assetCmd.Parameters.AddWithValue("@IsPack",   isPack ? 1 : 0);
            assetCmd.Parameters.AddWithValue("@Inv",      (object?)invoiceNumber ?? DBNull.Value);
            assetCmd.Parameters.AddWithValue("@ExpId",    (object?)expenseId ?? DBNull.Value);
            assetCmd.Parameters.AddWithValue("@Notes",    (object?)notes ?? DBNull.Value);
            assetCmd.Parameters.AddWithValue("@Uid",      userId);

            var newId = Convert.ToInt32(await assetCmd.ExecuteScalarAsync());

            // ── Insert components if pack ─────────────────────────────────
            if (isPack && compJson.HasValue && compJson.Value.ValueKind == JsonValueKind.Array)
            {
                foreach (var comp in compJson.Value.EnumerateArray())
                {
                    var cName    = comp.GetProperty("name").GetString()!;
                    var cCatId   = comp.GetProperty("categoryId").GetInt32();
                    var cPct     = comp.GetProperty("costPercentage").GetDecimal();
                    var cLife    = comp.GetProperty("usefulLifeYears").GetInt32();
                    var cResidual= comp.TryGetProperty("residualValue", out var crv) && crv.ValueKind != JsonValueKind.Null
                                   ? crv.GetDecimal() : 0m;
                    var cDesc    = comp.TryGetProperty("description",  out var cd)  ? cd.GetString()  : null;
                    var cSN      = comp.TryGetProperty("serialNumber", out var csn) ? csn.GetString() : null;
                    var cCost    = Math.Round(acquisitionCost * cPct / 100m, 2);

                    using var compCmd = new MySqlCommand(@"
                        INSERT INTO asset
                            (business_id, store_id, category_id, name, description, serial_number,
                             acquisition_date, acquisition_cost, residual_value, useful_life_years,
                             depreciation_method, status, is_pack, parent_asset_id, cost_percentage,
                             created_by_user_id)
                        VALUES
                            (@Biz, @Store, @Cat, @Name, @Desc, @SN,
                             @AcqDate, @Cost, @Residual, @Life,
                             'linear', 'active', 0, @ParentId, @Pct, @Uid);", conn, tx);
                    compCmd.Parameters.AddWithValue("@Biz",      businessId);
                    compCmd.Parameters.AddWithValue("@Store",    (object?)storeId ?? DBNull.Value);
                    compCmd.Parameters.AddWithValue("@Cat",      cCatId);
                    compCmd.Parameters.AddWithValue("@Name",     cName);
                    compCmd.Parameters.AddWithValue("@Desc",     (object?)cDesc ?? DBNull.Value);
                    compCmd.Parameters.AddWithValue("@SN",       (object?)cSN ?? DBNull.Value);
                    compCmd.Parameters.AddWithValue("@AcqDate",  acquisitionDate.ToString("yyyy-MM-dd"));
                    compCmd.Parameters.AddWithValue("@Cost",     cCost);
                    compCmd.Parameters.AddWithValue("@Residual", cResidual);
                    compCmd.Parameters.AddWithValue("@Life",     cLife);
                    compCmd.Parameters.AddWithValue("@ParentId", newId);
                    compCmd.Parameters.AddWithValue("@Pct",      cPct);
                    compCmd.Parameters.AddWithValue("@Uid",      userId);
                    await compCmd.ExecuteNonQueryAsync();
                }
            }

            await tx.CommitAsync();

            _sse.Notify(businessId, "asset.created", new { id = newId });
            return CreatedAtAction(nameof(GetAsset), new { id = newId }, new { id = newId, businessId, name });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creando activo");
            return StatusCode(500, new { message = "Error interno del servidor" });
        }
    }

    // ====================================================================
    // UPDATE ASSET
    // ====================================================================

    /// PATCH /api/assets/{id}
    [HttpPatch("{id:int}")]
    public async Task<IActionResult> UpdateAsset(int id, [FromBody] JsonElement body)
    {
        try
        {
            using var conn = GetConnection();
            await conn.OpenAsync();

            var setClauses = new List<string>();
            using var cmd  = new MySqlCommand("", conn);

            if (body.TryGetProperty("name",            out var n))   { setClauses.Add("name=@Name");            cmd.Parameters.AddWithValue("@Name",    n.GetString()); }
            if (body.TryGetProperty("description",     out var d))   { setClauses.Add("description=@Desc");     cmd.Parameters.AddWithValue("@Desc",    d.ValueKind == JsonValueKind.Null ? DBNull.Value : d.GetString()); }
            if (body.TryGetProperty("serialNumber",    out var sn))  { setClauses.Add("serial_number=@SN");     cmd.Parameters.AddWithValue("@SN",      sn.ValueKind == JsonValueKind.Null ? DBNull.Value : sn.GetString()); }
            if (body.TryGetProperty("storeId",         out var st))  { setClauses.Add("store_id=@StoreId");     cmd.Parameters.AddWithValue("@StoreId", st.ValueKind == JsonValueKind.Null ? DBNull.Value : st.GetInt32()); }
            if (body.TryGetProperty("categoryId",      out var c))   { setClauses.Add("category_id=@CatId");   cmd.Parameters.AddWithValue("@CatId",   c.GetInt32()); }
            if (body.TryGetProperty("acquisitionDate", out var ad))  { setClauses.Add("acquisition_date=@AcqD"); cmd.Parameters.AddWithValue("@AcqD",  ad.GetString()); }
            if (body.TryGetProperty("acquisitionCost", out var ac))  { setClauses.Add("acquisition_cost=@Cost"); cmd.Parameters.AddWithValue("@Cost",  ac.GetDecimal()); }
            if (body.TryGetProperty("residualValue",   out var rv))  { setClauses.Add("residual_value=@Res");   cmd.Parameters.AddWithValue("@Res",    rv.ValueKind == JsonValueKind.Null ? DBNull.Value : rv.GetDecimal()); }
            if (body.TryGetProperty("usefulLifeYears", out var ul))  { setClauses.Add("useful_life_years=@Life"); cmd.Parameters.AddWithValue("@Life", ul.GetInt32()); }
            if (body.TryGetProperty("invoiceNumber",   out var inv)) { setClauses.Add("invoice_number=@Inv");   cmd.Parameters.AddWithValue("@Inv",    inv.ValueKind == JsonValueKind.Null ? DBNull.Value : inv.GetString()); }
            if (body.TryGetProperty("notes",           out var no))  { setClauses.Add("notes=@Notes");          cmd.Parameters.AddWithValue("@Notes",  no.ValueKind == JsonValueKind.Null ? DBNull.Value : no.GetString()); }
            if (body.TryGetProperty("status",          out var s))   { setClauses.Add("status=@Status");        cmd.Parameters.AddWithValue("@Status", s.GetString()); }

            if (setClauses.Count == 0) return BadRequest(new { message = "Nada que actualizar" });

            cmd.CommandText = $"UPDATE asset SET {string.Join(",", setClauses)} WHERE id=@Id";
            cmd.Parameters.AddWithValue("@Id", id);
            await cmd.ExecuteNonQueryAsync();

            // Get businessId to notify SSE
            using var bizCmd = new MySqlCommand("SELECT business_id FROM asset WHERE id=@Id", conn);
            bizCmd.Parameters.AddWithValue("@Id", id);
            var bizRes = await bizCmd.ExecuteScalarAsync();
            if (bizRes != null && bizRes != DBNull.Value)
                _sse.Notify(Convert.ToInt32(bizRes), "asset.updated", new { id });

            return Ok(new { id });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error actualizando activo {Id}", id);
            return StatusCode(500, new { message = "Error interno del servidor" });
        }
    }

    // ====================================================================
    // DELETE (soft delete → status='disposed')
    // ====================================================================

    /// DELETE /api/assets/{id}
    [HttpDelete("{id:int}")]
    public async Task<IActionResult> DeleteAsset(int id)
    {
        try
        {
            using var conn = GetConnection();
            await conn.OpenAsync();

            using var bizCmd = new MySqlCommand("SELECT business_id FROM asset WHERE id=@Id", conn);
            bizCmd.Parameters.AddWithValue("@Id", id);
            var bizRes = await bizCmd.ExecuteScalarAsync();
            int? notifyBiz = (bizRes != null && bizRes != DBNull.Value) ? Convert.ToInt32(bizRes) : (int?)null;

            using var cmd = new MySqlCommand("UPDATE asset SET status='disposed' WHERE id=@Id", conn);
            cmd.Parameters.AddWithValue("@Id", id);
            await cmd.ExecuteNonQueryAsync();

            if (notifyBiz.HasValue)
                _sse.Notify(notifyBiz.Value, "asset.deleted", new { id });

            return NoContent();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error dando de baja activo {Id}", id);
            return StatusCode(500, new { message = "Error interno del servidor" });
        }
    }

    // ====================================================================
    // PHOTO UPLOAD
    // ====================================================================

    /// POST /api/assets/{id}/photo  (multipart/form-data, field: "photo")
    [HttpPost("{id:int}/photo")]
    [RequestSizeLimit(10 * 1024 * 1024)] // 10 MB
    public async Task<IActionResult> UploadPhoto(int id, IFormFile? photo)
    {
        if (photo == null || photo.Length == 0)
            return BadRequest(new { message = "No se recibió ninguna imagen." });

        var ext = Path.GetExtension(photo.FileName).ToLowerInvariant();
        if (ext is not (".jpg" or ".jpeg" or ".png" or ".webp"))
            return BadRequest(new { message = "Solo se permiten imágenes JPG, PNG o WebP." });

        try
        {
            using var conn = GetConnection();
            await conn.OpenAsync();

            // Determine base URL
            var baseUrl   = $"{Request.Scheme}://{Request.Host}";
            var uploadDir = Path.Combine(_env.WebRootPath, "asset-photos", id.ToString());
            Directory.CreateDirectory(uploadDir);

            // Delete old photo files for this asset
            foreach (var old in Directory.GetFiles(uploadDir))
                System.IO.File.Delete(old);

            var fileName = $"{Guid.NewGuid()}{ext}";
            var filePath = Path.Combine(uploadDir, fileName);
            await using (var stream = System.IO.File.Create(filePath))
                await photo.CopyToAsync(stream);

            var photoUrl = $"{baseUrl}/asset-photos/{id}/{fileName}";

            using var cmd = new MySqlCommand(
                "UPDATE asset SET photo_url=@Url WHERE id=@Id", conn);
            cmd.Parameters.AddWithValue("@Url", photoUrl);
            cmd.Parameters.AddWithValue("@Id",  id);
            await cmd.ExecuteNonQueryAsync();

            using var bizCmd = new MySqlCommand("SELECT business_id FROM asset WHERE id=@Id", conn);
            bizCmd.Parameters.AddWithValue("@Id", id);
            var bizRes = await bizCmd.ExecuteScalarAsync();
            if (bizRes != null && bizRes != DBNull.Value)
                _sse.Notify(Convert.ToInt32(bizRes), "asset.updated", new { id });

            return Ok(new { id, photoUrl });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error subiendo foto del activo {Id}", id);
            return StatusCode(500, new { message = "Error interno del servidor" });
        }
    }

    // ====================================================================
    // STORES (helper para el selector de tienda)
    // ====================================================================

    /// GET /api/assets/stores?businessId=
    [HttpGet("stores")]
    public async Task<IActionResult> GetStores([FromQuery] int businessId)
    {
        try
        {
            using var conn = GetConnection();
            await conn.OpenAsync();
            using var cmd = new MySqlCommand(
                "SELECT id, name, location FROM store WHERE id_business=@Biz AND active=1 ORDER BY name",
                conn);
            cmd.Parameters.AddWithValue("@Biz", businessId);
            using var r = await cmd.ExecuteReaderAsync();
            var list    = new List<object>();
            while (await r.ReadAsync())
                list.Add(new {
                    id       = r.GetInt32("id"),
                    name     = r.GetString("name"),
                    location = IsNull(r, "location") ? null : r.GetString("location"),
                });
            return Ok(list);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error obteniendo tiendas del negocio {Id}", businessId);
            return StatusCode(500, new { message = "Error interno del servidor" });
        }
    }

    // ====================================================================
    // SUMMARY (totales para dashboard)
    // ====================================================================

    /// GET /api/assets/summary?businessId=
    [HttpGet("summary")]
    public async Task<IActionResult> GetSummary([FromQuery] int businessId)
    {
        try
        {
            using var conn = GetConnection();
            await conn.OpenAsync();
            using var cmd = new MySqlCommand(@"
                SELECT a.id, a.acquisition_cost, a.residual_value, a.useful_life_years,
                       a.acquisition_date, a.status
                FROM asset a
                WHERE a.business_id=@Biz AND a.status='active'", conn);
            cmd.Parameters.AddWithValue("@Biz", businessId);
            using var r = await cmd.ExecuteReaderAsync();

            decimal totalCost        = 0;
            decimal totalBookValue   = 0;
            decimal totalAccumulated = 0;
            int     count            = 0;

            while (await r.ReadAsync())
            {
                var cost          = r.GetDecimal("acquisition_cost");
                var residual      = r.GetDecimal("residual_value");
                var life          = r.GetInt32("useful_life_years");
                var acqDate       = r.GetDateTime("acquisition_date");
                var d             = CalculateDepreciation(cost, residual, life, acqDate);
                var deprObj       = (dynamic)d;

                totalCost        += cost;
                totalBookValue   += (decimal)deprObj.bookValue;
                totalAccumulated += (decimal)deprObj.accumulatedDepreciation;
                count++;
            }

            return Ok(new {
                activeCount            = count,
                totalAcquisitionCost   = Math.Round(totalCost,        2),
                totalBookValue         = Math.Round(totalBookValue,   2),
                totalAccumulatedDepr   = Math.Round(totalAccumulated, 2),
                totalDepreciationPct   = totalCost > 0 ? Math.Round(totalAccumulated / totalCost * 100, 2) : 0,
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error calculando resumen de activos del negocio {Id}", businessId);
            return StatusCode(500, new { message = "Error interno del servidor" });
        }
    }
}
