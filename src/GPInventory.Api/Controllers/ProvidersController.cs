using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Cors;
using GPInventory.Application.DTOs.Production;
using GPInventory.Application.Interfaces;
using GPInventory.Infrastructure.Data;
using GPInventory.Api.Services;
using Microsoft.EntityFrameworkCore;

namespace GPInventory.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[EnableCors("AllowFrontend")]
public class ProvidersController : ControllerBase
{
    private readonly IProviderService _providerService;
    private readonly ILogger<ProvidersController> _logger;
    private readonly ApplicationDbContext _context;
    private readonly ProviderSseService _sse;

    public ProvidersController(IProviderService providerService, ILogger<ProvidersController> logger, ApplicationDbContext context, ProviderSseService sse)
    {
        _providerService = providerService;
        _logger = logger;
        _context = context;
        _sse = sse;
    }

    /// <summary>
    /// SSE stream — clients subscribe here to receive real-time provider change events.
    /// No auth required; events only carry id + event type.
    /// </summary>
    [HttpGet("events")]
    [EnableCors("AllowFrontend")]
    public async Task StreamEvents(CancellationToken ct)
    {
        Response.Headers.Append("Content-Type", "text/event-stream");
        Response.Headers.Append("Cache-Control", "no-cache");
        Response.Headers.Append("X-Accel-Buffering", "no");
        Response.Headers.Append("Connection", "keep-alive");

        var (clientId, reader) = _sse.Subscribe();
        try
        {
            // Send a heartbeat comment every 25 s to keep the connection alive through proxies
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            var heartbeatTask = Task.Run(async () =>
            {
                using var timer = new PeriodicTimer(TimeSpan.FromSeconds(25));
                while (!cts.Token.IsCancellationRequested)
                {
                    try
                    {
                        await timer.WaitForNextTickAsync(cts.Token);
                        await Response.WriteAsync(": heartbeat\n\n", cts.Token);
                        await Response.Body.FlushAsync(cts.Token);
                    }
                    catch { break; }
                }
            }, cts.Token);

            await foreach (var message in reader.ReadAllAsync(ct))
            {
                await Response.WriteAsync(message, ct);
                await Response.Body.FlushAsync(ct);
            }

            cts.Cancel();
        }
        catch (OperationCanceledException) { }
        finally
        {
            _sse.Unsubscribe(clientId);
        }
    }

    /// <summary>
    /// Get all providers or filter by business
    /// </summary>
    [HttpGet]
    [Authorize]
    [ProducesResponseType(200)]
    [ProducesResponseType(401)]
    [ProducesResponseType(500)]
    public async Task<ActionResult<IEnumerable<ProviderDto>>> GetProviders([FromQuery] int? businessId = null)
    {
        try
        {
            _logger.LogInformation("Getting providers with filters");

            IEnumerable<ProviderDto> providers;

            if (businessId.HasValue)
            {
                providers = await _providerService.GetProvidersByBusinessIdAsync(businessId.Value);
            }
            else
            {
                providers = await _providerService.GetAllProvidersAsync();
            }

            _logger.LogInformation($"Found {providers.Count()} providers");
            return Ok(providers);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving providers");
            return StatusCode(500, new { message = "Error retrieving providers", error = ex.Message });
        }
    }

    /// <summary>
    /// Get provider by ID
    /// </summary>
    [HttpGet("{id}")]
    [Authorize]
    [ProducesResponseType(200)]
    [ProducesResponseType(401)]
    [ProducesResponseType(404)]
    [ProducesResponseType(500)]
    public async Task<ActionResult<ProviderDto>> GetProvider(int id)
    {
        try
        {
            _logger.LogInformation("Getting provider with ID: {id}", id);

            var provider = await _providerService.GetProviderByIdAsync(id);
            return Ok(provider);
        }
        catch (ArgumentException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving provider with ID: {id}", id);
            return StatusCode(500, new { message = "Error retrieving provider", error = ex.Message });
        }
    }

    /// <summary>
    /// Get providers by business ID
    /// </summary>
    [HttpGet("business/{businessId}")]
    [Authorize]
    [ProducesResponseType(200)]
    [ProducesResponseType(401)]
    [ProducesResponseType(500)]
    public async Task<ActionResult<IEnumerable<ProviderDto>>> GetProvidersByBusiness(int businessId)
    {
        try
        {
            _logger.LogInformation("Getting providers for business ID: {businessId}", businessId);

            var providers = await _providerService.GetProvidersByBusinessIdAsync(businessId);

            _logger.LogInformation($"Found {providers.Count()} providers for business {businessId}");
            return Ok(providers);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving providers for business ID: {businessId}", businessId);
            return StatusCode(500, new { message = "Error retrieving providers", error = ex.Message });
        }
    }

    /// <summary>
    /// Create a new provider
    /// </summary>
    [HttpPost]
    [Authorize]
    [ProducesResponseType(201)]
    [ProducesResponseType(400)]
    [ProducesResponseType(401)]
    [ProducesResponseType(409)]
    [ProducesResponseType(500)]
    public async Task<ActionResult<ProviderDto>> CreateProvider([FromBody] CreateProviderDto createProviderDto)
    {
        try
        {
            _logger.LogInformation("Creating new provider: {providerName}", createProviderDto.Name);

            var provider = await _providerService.CreateProviderAsync(createProviderDto);

            _logger.LogInformation("Provider created successfully: {providerName} with ID: {providerId}", provider.Name, provider.Id);
            _ = _sse.BroadcastAsync("provider-created", provider.Id);
            return CreatedAtAction(nameof(GetProvider), new { id = provider.Id }, provider);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating provider");
            return StatusCode(500, new { message = "Error creating provider", error = ex.Message });
        }
    }

    /// <summary>
    /// Update an existing provider
    /// </summary>
    [HttpPut("{id}")]
    [Authorize]
    [ProducesResponseType(200)]
    [ProducesResponseType(400)]
    [ProducesResponseType(401)]
    [ProducesResponseType(404)]
    [ProducesResponseType(409)]
    [ProducesResponseType(500)]
    public async Task<ActionResult<ProviderDto>> UpdateProvider(int id, [FromBody] UpdateProviderDto updateProviderDto)
    {
        try
        {
            if (updateProviderDto == null)
                return BadRequest(new { message = "Request body is required" });

            _logger.LogInformation("Updating provider with ID: {id}", id);
            _logger.LogInformation("[DEBUG] UpdateProviderDto IsSelf value: {isSelf}", updateProviderDto.IsSelf);

            var provider = await _providerService.UpdateProviderAsync(id, updateProviderDto);

            _logger.LogInformation("Provider updated successfully: {providerName} with ID: {id}", provider.Name, id);
            _logger.LogInformation("[DEBUG] Updated provider IsSelf value: {isSelf}", provider.IsSelf);
            _ = _sse.BroadcastAsync("provider-updated", id);
            return Ok(provider);
        }
        catch (ArgumentException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating provider with ID: {id}", id);
            return StatusCode(500, new { message = "Error updating provider", error = ex.Message });
        }
    }

    /// <summary>
    /// Delete a provider
    /// </summary>
    [HttpDelete("{id}")]
    [Authorize]
    [ProducesResponseType(200)]
    [ProducesResponseType(401)]
    [ProducesResponseType(404)]
    [ProducesResponseType(500)]
    public async Task<ActionResult> DeleteProvider(int id)
    {
        try
        {
            _logger.LogInformation("Deleting provider with ID: {id}", id);

            await _providerService.DeleteProviderAsync(id);

            _logger.LogInformation("Provider deleted successfully with ID: {id}", id);
            _ = _sse.BroadcastAsync("provider-deleted", id);
            return NoContent();
        }
        catch (ArgumentException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting provider with ID: {id}", id);
            return StatusCode(500, new { message = "Error deleting provider", error = ex.Message });
        }
    }

    /// <summary>
    /// Get supply entry transactions for a provider (purchase history)
    /// </summary>
    [HttpGet("{id}/supply-entries")]
    [Authorize]
    [ProducesResponseType(200)]
    [ProducesResponseType(401)]
    [ProducesResponseType(500)]
    public async Task<ActionResult<IEnumerable<object>>> GetProviderSupplyEntries(int id)
    {
        try
        {
            _logger.LogInformation("Getting supply entries for provider ID: {id}", id);

            await _context.Database.OpenConnectionAsync();
            try
            {
                using var connection = _context.Database.GetDbConnection();
                var sql = @"
                    -- Insumos (gp-factory: supply_entry)
                    SELECT
                        se.id,
                        se.provider_id,
                        se.amount          AS quantity,
                        se.unit_cost,
                        COALESCE(se.total_cost, ABS(se.amount * se.unit_cost)) AS total_cost,
                        s.name             AS supply_name,
                        COALESCE(um.symbol, um.name, '') AS supply_unit,
                        se.created_at,
                        se.tag             AS notes
                    FROM supply_entry se
                    INNER JOIN supplies s ON se.supply_id = s.id
                    LEFT JOIN unit_measures um ON s.unit_measure_id = um.id
                    WHERE se.provider_id = @providerId
                      AND se.amount > 0
                      AND se.active = 1
                      AND se.supply_entry_id IS NULL

                    UNION ALL

                    -- Productos (gp-inventory: stock)
                    SELECT
                        sk.id,
                        sk.provider        AS provider_id,
                        sk.amount          AS quantity,
                        COALESCE(sk.cost, 0) AS unit_cost,
                        ABS(sk.amount * COALESCE(sk.cost, 0)) AS total_cost,
                        prod.name          AS supply_name,
                        ''                 AS supply_unit,
                        sk.date            AS created_at,
                        sk.notes
                    FROM stock sk
                    INNER JOIN product prod ON sk.product = prod.id
                    WHERE sk.provider = @providerId
                      AND sk.amount > 0
                      AND sk.stock_id IS NULL
                      AND COALESCE(sk.active, 0) = 1

                    ORDER BY created_at DESC";

                using var cmd = connection.CreateCommand();
                cmd.CommandText = sql;
                var param = cmd.CreateParameter();
                param.ParameterName = "@providerId";
                param.Value = id;
                cmd.Parameters.Add(param);

                var results = new List<object>();
                using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    results.Add(new
                    {
                        id = reader.GetInt32(0),
                        providerId = reader.GetInt32(1),
                        quantity = Convert.ToDecimal(reader.GetValue(2)),
                        unitCost = Convert.ToDecimal(reader.GetValue(3)),
                        totalCost = Convert.ToDecimal(reader.GetValue(4)),
                        supplyName = reader.GetString(5),
                        supplyUnit = reader.IsDBNull(6) ? "" : reader.GetString(6),
                        createdAt = reader.GetDateTime(7).ToString("o"),
                        notes = reader.IsDBNull(8) ? null : reader.GetString(8)
                    });
                }

                _logger.LogInformation("Found {count} supply entries for provider {id}", results.Count, id);
                return Ok(results);
            }
            finally
            {
                await _context.Database.CloseConnectionAsync();
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving supply entries for provider: {id}", id);
            return StatusCode(500, new { message = "Error retrieving supply entries", error = ex.Message });
        }
    }

    /// <summary>
    /// Get providers by store ID
    /// </summary>
    [HttpGet("store/{storeId}")]
    [Authorize]
    [ProducesResponseType(200)]
    [ProducesResponseType(401)]
    [ProducesResponseType(500)]
    public async Task<ActionResult<IEnumerable<ProviderDto>>> GetProvidersByStore(int storeId)
    {
        try
        {
            var providers = await _providerService.GetProvidersByStoreIdAsync(storeId);
            return Ok(providers);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving providers for store: {storeId}", storeId);
            return StatusCode(500, new { message = "Error retrieving providers", error = ex.Message });
        }
    }

    /// <summary>
    /// Get expenses linked to a provider
    /// </summary>
    [HttpGet("{id}/expenses")]
    [Authorize]
    public async Task<ActionResult<IEnumerable<object>>> GetProviderExpenses(int id)
    {
        try
        {
            await _context.Database.OpenConnectionAsync();
            try
            {
                using var connection = _context.Database.GetDbConnection();
                var sql = @"
                    SELECT
                        e.id,
                        e.provider_id,
                        COALESCE(e.amount_total, e.amount, 0) AS amount,
                        COALESCE(e.description, '') AS description,
                        COALESCE(es.name, '') AS category,
                        e.date,
                        e.date AS created_at
                    FROM expenses e
                    LEFT JOIN expense_subcategory es ON e.subcategory_id = es.id
                    WHERE e.provider_id = @providerId
                    ORDER BY e.date DESC";

                using var cmd = connection.CreateCommand();
                cmd.CommandText = sql;
                var p = cmd.CreateParameter(); p.ParameterName = "@providerId"; p.Value = id;
                cmd.Parameters.Add(p);

                var results = new List<object>();
                using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    results.Add(new
                    {
                        id = reader.GetInt32(0),
                        providerId = reader.GetInt32(1),
                        amount = Convert.ToDecimal(reader.GetValue(2)),
                        description = reader.GetString(3),
                        category = reader.GetString(4),
                        date = reader.GetDateTime(5).ToString("o"),
                        createdAt = reader.GetDateTime(6).ToString("o"),
                    });
                }
                return Ok(results);
            }
            finally { await _context.Database.CloseConnectionAsync(); }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving expenses for provider: {id}", id);
            return StatusCode(500, new { message = "Error retrieving expenses", error = ex.Message });
        }
    }

    /// <summary>
    /// Get preferred supplies for a provider
    /// </summary>
    [HttpGet("{id}/preferred-supplies")]
    [Authorize]
    public async Task<ActionResult<IEnumerable<object>>> GetProviderPreferredSupplies(int id)
    {
        try
        {
            await _context.Database.OpenConnectionAsync();
            try
            {
                using var connection = _context.Database.GetDbConnection();
                var sql = @"
                    SELECT
                        s.id,
                        s.name,
                        COALESCE(um.symbol, um.name, '') AS unit,
                        COALESCE((
                            SELECT SUM(se.amount)
                            FROM supply_entry se
                            WHERE se.supply_id = s.id AND se.active = 1
                        ), 0) AS stock,
                        (
                            SELECT se.unit_cost
                            FROM supply_entry se
                            WHERE se.supply_id = s.id AND se.amount > 0 AND se.active = 1
                            ORDER BY se.created_at DESC LIMIT 1
                        ) AS lastPurchasePrice,
                        (
                            SELECT se.created_at
                            FROM supply_entry se
                            WHERE se.supply_id = s.id AND se.amount > 0 AND se.active = 1
                            ORDER BY se.created_at DESC LIMIT 1
                        ) AS lastPurchaseDate
                    FROM supplies s
                    LEFT JOIN unit_measures um ON s.unit_measure_id = um.id
                    WHERE s.preferred_provider_id = @providerId AND s.active = 1
                    ORDER BY s.name";

                using var cmd = connection.CreateCommand();
                cmd.CommandText = sql;
                var p = cmd.CreateParameter(); p.ParameterName = "@providerId"; p.Value = id;
                cmd.Parameters.Add(p);

                var results = new List<object>();
                using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    results.Add(new
                    {
                        id = reader.GetInt32(0),
                        name = reader.GetString(1),
                        unit = reader.IsDBNull(2) ? "" : reader.GetString(2),
                        stock = Convert.ToDecimal(reader.GetValue(3)),
                        lastPurchasePrice = reader.IsDBNull(4) ? (decimal?)null : Convert.ToDecimal(reader.GetValue(4)),
                        lastPurchaseDate = reader.IsDBNull(5) ? null : reader.GetDateTime(5).ToString("o"),
                    });
                }
                return Ok(results);
            }
            finally { await _context.Database.CloseConnectionAsync(); }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving preferred supplies for provider: {id}", id);
            return StatusCode(500, new { message = "Error retrieving preferred supplies", error = ex.Message });
        }
    }

    /// <summary>
    /// Get service costs for a provider (not applicable for inventory providers — returns empty)
    /// </summary>
    [HttpGet("{id}/service-costs")]
    [Authorize]
    public ActionResult<IEnumerable<object>> GetProviderServiceCosts(int id)
        => Ok(Array.Empty<object>());

    /// <summary>
    /// Get summary KPIs for a provider
    /// </summary>
    [HttpGet("{id}/summary")]
    [Authorize]
    public async Task<ActionResult<object>> GetProviderSummary(int id)
    {
        try
        {
            await _context.Database.OpenConnectionAsync();
            try
            {
                using var connection = _context.Database.GetDbConnection();

                // Expenses aggregate
                var expSql = @"
                    SELECT
                        COALESCE(SUM(COALESCE(amount_total, amount, 0)), 0) AS totalSpent,
                        COUNT(*) AS totalTransactions
                    FROM expenses
                    WHERE provider_id = @pid";

                using var expCmd = connection.CreateCommand();
                expCmd.CommandText = expSql;
                var ep = expCmd.CreateParameter(); ep.ParameterName = "@pid"; ep.Value = id;
                expCmd.Parameters.Add(ep);

                decimal totalSpent = 0; int totalTransactions = 0;
                using (var r = await expCmd.ExecuteReaderAsync())
                {
                    if (await r.ReadAsync())
                    {
                        totalSpent = Convert.ToDecimal(r.GetValue(0));
                        totalTransactions = Convert.ToInt32(r.GetValue(1));
                    }
                }

                // Stock aggregate (productos gp-inventory)
                var skSql = @"
                    SELECT
                        COUNT(*) AS stockTransactions,
                        MAX(date) AS lastPurchaseDate,
                        COALESCE(AVG(COALESCE(cost, 0)), 0) AS avgAmount
                    FROM stock
                    WHERE provider = @pid AND amount > 0 AND stock_id IS NULL AND COALESCE(active, 0) = 1";

                using var skCmd = connection.CreateCommand();
                skCmd.CommandText = skSql;
                var sp = skCmd.CreateParameter(); sp.ParameterName = "@pid"; sp.Value = id;
                skCmd.Parameters.Add(sp);

                int stockTransactions = 0; string? lastPurchaseDate = null; decimal avgAmount = 0;
                using (var r = await skCmd.ExecuteReaderAsync())
                {
                    if (await r.ReadAsync())
                    {
                        stockTransactions = Convert.ToInt32(r.GetValue(0));
                        lastPurchaseDate = r.IsDBNull(1) ? null : r.GetDateTime(1).ToString("o");
                        avgAmount = Convert.ToDecimal(r.GetValue(2));
                    }
                }

                // Supply entry aggregate (insumos gp-factory)
                var seSql = @"
                    SELECT COUNT(*), MAX(created_at)
                    FROM supply_entry
                    WHERE provider_id = @pid AND amount > 0 AND active = 1 AND supply_entry_id IS NULL";

                using var seCmd = connection.CreateCommand();
                seCmd.CommandText = seSql;
                var sep2 = seCmd.CreateParameter(); sep2.ParameterName = "@pid"; sep2.Value = id;
                seCmd.Parameters.Add(sep2);

                int supplyEntryTransactions = 0; string? seLastDate = null;
                using (var r = await seCmd.ExecuteReaderAsync())
                {
                    if (await r.ReadAsync())
                    {
                        supplyEntryTransactions = Convert.ToInt32(r.GetValue(0));
                        seLastDate = r.IsDBNull(1) ? null : r.GetDateTime(1).ToString("o");
                    }
                }

                // Pick most recent last purchase date
                if (seLastDate != null && (lastPurchaseDate == null || string.Compare(seLastDate, lastPurchaseDate) > 0))
                    lastPurchaseDate = seLastDate;

                // Preferred supplies count
                var supSql = "SELECT COUNT(*) FROM supplies WHERE preferred_provider_id = @pid AND active = 1";
                using var supCmd = connection.CreateCommand();
                supCmd.CommandText = supSql;
                var spp = supCmd.CreateParameter(); spp.ParameterName = "@pid"; spp.Value = id;
                supCmd.Parameters.Add(spp);
                var activeSuppliesCount = Convert.ToInt32(await supCmd.ExecuteScalarAsync() ?? 0);

                return Ok(new
                {
                    providerId = id,
                    totalSpent = totalSpent,
                    totalTransactions = totalTransactions + stockTransactions + supplyEntryTransactions,
                    lastPurchaseDate = lastPurchaseDate,
                    averageTransactionAmount = avgAmount,
                    activeSuppliesCount = activeSuppliesCount,
                    expensesTotal = totalSpent,
                    serviceCostsTotal = 0m,
                });
            }
            finally { await _context.Database.CloseConnectionAsync(); }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving summary for provider: {id}", id);
            return StatusCode(500, new { message = "Error retrieving summary", error = ex.Message });
        }
    }
}
