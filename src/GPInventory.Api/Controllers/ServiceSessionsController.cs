using GPInventory.Application.DTOs.Services;
using GPInventory.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MySqlConnector;

namespace GPInventory.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class ServiceSessionsController : ControllerBase
{
    private readonly IServiceSessionService _sessionService;
    private readonly ILogger<ServiceSessionsController> _logger;
    private readonly IConfiguration _configuration;

    public ServiceSessionsController(IServiceSessionService sessionService, ILogger<ServiceSessionsController> logger, IConfiguration configuration)
    {
        _sessionService = sessionService;
        _logger = logger;
        _configuration = configuration;
    }

    private MySqlConnection GetConnection()
        => new(_configuration.GetConnectionString("DefaultConnection")!);

    private static bool IsNull(MySqlDataReader r, string col) => r.IsDBNull(r.GetOrdinal(col));

    // GET api/ServiceSessions/{id}
    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetById(int id)
    {
        try
        {
            var session = await _sessionService.GetByIdAsync(id);
            return Ok(session);
        }
        catch (InvalidOperationException ex)
        {
            return NotFound(new { message = ex.Message });
        }
    }

    // GET api/ServiceSessions/business/{businessId}?from=&to=
    [HttpGet("business/{businessId:int}")]
    public async Task<IActionResult> GetByBusiness(int businessId, [FromQuery] DateTime? from = null, [FromQuery] DateTime? to = null)
    {
        var sessions = await _sessionService.GetByBusinessAsync(businessId, from, to);
        return Ok(sessions);
    }

    // GET api/ServiceSessions/service/{serviceId}?from=&to=
    [HttpGet("service/{serviceId:int}")]
    public async Task<IActionResult> GetByService(int serviceId, [FromQuery] DateTime? from = null, [FromQuery] DateTime? to = null)
    {
        var sessions = await _sessionService.GetByServiceAsync(serviceId, from, to);
        return Ok(sessions);
    }

    // GET api/ServiceSessions/plan/{servicePlanId}/upcoming?days=
    [HttpGet("plan/{servicePlanId:int}/upcoming")]
    public async Task<IActionResult> GetUpcomingByPlan(int servicePlanId, [FromQuery] int days = 30)
    {
        var sessions = await _sessionService.GetUpcomingByPlanAsync(servicePlanId, days);
        return Ok(sessions);
    }

    // POST api/ServiceSessions
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateServiceSessionDto dto)
    {
        try
        {
            var userId = GetUserId();
            var session = await _sessionService.CreateAsync(dto, userId);
            return CreatedAtAction(nameof(GetById), new { id = session.Id }, session);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    // POST api/ServiceSessions/bulk
    [HttpPost("bulk")]
    public async Task<IActionResult> CreateBulk([FromBody] CreateBulkServiceSessionsDto dto)
    {
        try
        {
            var userId = GetUserId();
            var sessions = await _sessionService.CreateBulkAsync(dto, userId);
            return Ok(sessions);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    // PUT api/ServiceSessions/{id}
    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, [FromBody] UpdateServiceSessionDto dto)
    {
        try
        {
            var session = await _sessionService.UpdateAsync(id, dto);
            return Ok(session);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    // POST api/ServiceSessions/{id}/start
    [HttpPost("{id:int}/start")]
    public async Task<IActionResult> Start(int id)
    {
        try
        {
            var session = await _sessionService.StartSessionAsync(id);
            return Ok(session);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    // POST api/ServiceSessions/{id}/complete
    [HttpPost("{id:int}/complete")]
    public async Task<IActionResult> Complete(int id)
    {
        try
        {
            var session = await _sessionService.CompleteSessionAsync(id);
            return Ok(session);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    // POST api/ServiceSessions/{id}/cancel
    [HttpPost("{id:int}/cancel")]
    public async Task<IActionResult> Cancel(int id, [FromBody] CancelSessionRequestDto? request = null)
    {
        try
        {
            var session = await _sessionService.CancelSessionAsync(id, request?.Reason);
            return Ok(session);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    // POST api/ServiceSessions/{id}/attendance
    [HttpPost("{id:int}/attendance")]
    public async Task<IActionResult> RegisterAttendance(int id, [FromBody] RegisterSessionAttendanceDto dto)
    {
        try
        {
            dto.SessionId = id;
            var userId = GetUserId();
            var attendee = await _sessionService.RegisterAttendanceAsync(dto, userId);
            return Ok(attendee);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    // PATCH api/ServiceSessions/attendance/{attendanceId}/attended
    [HttpPatch("attendance/{attendanceId:int}/attended")]
    public async Task<IActionResult> MarkAttended(int attendanceId)
    {
        try
        {
            var attendee = await _sessionService.MarkAttendedAsync(attendanceId);
            return Ok(attendee);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    // PATCH api/ServiceSessions/attendance/{attendanceId}/absent
    [HttpPatch("attendance/{attendanceId:int}/absent")]
    public async Task<IActionResult> MarkAbsent(int attendanceId)
    {
        try
        {
            var attendee = await _sessionService.MarkAbsentAsync(attendanceId);
            return Ok(attendee);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    // ── Session Cost Items ────────────────────────────────────────────────────

    // GET api/ServiceSessions/{id}/cost-items
    [HttpGet("{sessionId:int}/cost-items")]
    public async Task<IActionResult> GetCostItems(int sessionId)
    {
        try
        {
            using var conn = GetConnection();
            await conn.OpenAsync();
            using var cmd = new MySqlCommand(@"
                SELECT ci.id, ci.session_id, ci.name, ci.description, ci.cost_type,
                       ci.amount, ci.quantity, ci.unit, ci.is_externalized,
                       ci.provider_id, ci.provider_name,
                       ci.employee_id, ci.employee_name,
                       e.contract_type AS employee_contract_type,
                       COALESCE(e.hourly_rate, 0) AS employee_hourly_rate,
                       ci.sort_order
                FROM session_cost_item ci
                LEFT JOIN hr_employee e ON e.id = ci.employee_id
                WHERE ci.session_id = @Sess
                ORDER BY ci.sort_order, ci.id", conn);
            cmd.Parameters.AddWithValue("@Sess", sessionId);
            using var r = await cmd.ExecuteReaderAsync();
            var list = new List<object>();
            while (await r.ReadAsync())
                list.Add(new
                {
                    id                   = r.GetInt32("id"),
                    sessionId            = r.GetInt32("session_id"),
                    name                 = r.GetString("name"),
                    description          = IsNull(r, "description") ? "" : r.GetString("description"),
                    costType             = r.GetString("cost_type"),
                    amount               = r.GetDecimal("amount"),
                    quantity             = r.GetDecimal("quantity"),
                    unit                 = IsNull(r, "unit") ? "" : r.GetString("unit"),
                    isExternalized       = r.GetBoolean("is_externalized"),
                    providerId           = IsNull(r, "provider_id")   ? (int?)null : r.GetInt32("provider_id"),
                    providerName         = IsNull(r, "provider_name") ? null : r.GetString("provider_name"),
                    employeeId           = IsNull(r, "employee_id")   ? (int?)null : r.GetInt32("employee_id"),
                    employeeName         = IsNull(r, "employee_name") ? null : r.GetString("employee_name"),
                    employeeContractType = IsNull(r, "employee_contract_type") ? null : r.GetString("employee_contract_type"),
                    employeeHourlyRate   = r.GetDecimal("employee_hourly_rate"),
                    sortOrder            = r.GetInt32("sort_order"),
                });
            return Ok(list);
        }
        catch (Exception ex) { _logger.LogError(ex, "GetCostItems"); return StatusCode(500, new { message = ex.Message }); }
    }

    // POST api/ServiceSessions/{id}/cost-items
    [HttpPost("{sessionId:int}/cost-items")]
    public async Task<IActionResult> AddCostItem(int sessionId, [FromBody] SessionCostItemRequest req)
    {
        try
        {
            using var conn = GetConnection();
            await conn.OpenAsync();
            using var tx = await conn.BeginTransactionAsync();

            // 1. Insert session_cost_item
            using var insCmd = new MySqlCommand(@"
                INSERT INTO session_cost_item
                    (session_id,name,description,cost_type,amount,quantity,unit,is_externalized,provider_id,provider_name,employee_id,employee_name,sort_order)
                VALUES (@Sess,@Name,@Desc,@Type,@Amt,@Qty,@Unit,@Ext,@ProvId,@Prov,@EmpId,@EmpName,@Ord);
                SELECT LAST_INSERT_ID();", conn, tx);
            insCmd.Parameters.AddWithValue("@Sess",    sessionId);
            insCmd.Parameters.AddWithValue("@Name",    req.Name);
            insCmd.Parameters.AddWithValue("@Desc",    (object?)req.Description  ?? DBNull.Value);
            insCmd.Parameters.AddWithValue("@Type",    req.CostType ?? "other");
            insCmd.Parameters.AddWithValue("@Amt",     req.Amount);
            insCmd.Parameters.AddWithValue("@Qty",     req.Quantity ?? 1m);
            insCmd.Parameters.AddWithValue("@Unit",    (object?)req.Unit         ?? DBNull.Value);
            insCmd.Parameters.AddWithValue("@Ext",     req.IsExternalized ? 1 : 0);
            insCmd.Parameters.AddWithValue("@ProvId",  (object?)req.ProviderId   ?? DBNull.Value);
            insCmd.Parameters.AddWithValue("@Prov",    (object?)req.ProviderName ?? DBNull.Value);
            insCmd.Parameters.AddWithValue("@EmpId",   (object?)req.EmployeeId   ?? DBNull.Value);
            insCmd.Parameters.AddWithValue("@EmpName", (object?)req.EmployeeName ?? DBNull.Value);
            insCmd.Parameters.AddWithValue("@Ord",     req.SortOrder ?? 0);
            var newId = Convert.ToInt32(await insCmd.ExecuteScalarAsync());

            // 2. Create matching service_session_expense (gasto de sesión pendiente)
            // SOLO para: honorarios o externos. Empleados por contrato (indefinido/plazo_fijo) no generan gasto
            // porque ya están en planilla
            string? contractType = null;
            if (req.EmployeeId.HasValue)
            {
                using var empCmd = new MySqlCommand("SELECT contract_type FROM hr_employee WHERE id=@Id", conn, tx);
                empCmd.Parameters.AddWithValue("@Id", req.EmployeeId.Value);
                var ctObj = await empCmd.ExecuteScalarAsync();
                contractType = ctObj?.ToString();
            }

            // Solo crear gasto si es honorarios o proveedor externo
            var shouldCreateExpense = (req.EmployeeId.HasValue && contractType == "honorarios")
                                   || !string.IsNullOrWhiteSpace(req.ProviderName);

            if (shouldCreateExpense)
            {
                var payeeType = req.EmployeeId.HasValue ? "employee"
                              : !string.IsNullOrWhiteSpace(req.ProviderName) ? "external"
                              : (string?)null;
                var desc = string.IsNullOrWhiteSpace(req.Description) ? req.Name : $"{req.Name} — {req.Description}";
                using var expCmd = new MySqlCommand(@"
                    INSERT INTO service_session_expense
                        (business_id, store_id, service_session_id, session_cost_item_id,
                         description, amount, status,
                         payee_type, payee_employee_id, payee_employee_name, payee_external_name,
                         created_at, updated_at)
                    SELECT ss.business_id, ss.store_id, @Sess, @SciId,
                           @Desc, @Amt, 'pending',
                           @PayeeType, @EmpId, @EmpName, @ExtName,
                           NOW(), NOW()
                    FROM service_session ss WHERE ss.id = @Sess", conn, tx);
                expCmd.Parameters.AddWithValue("@Sess",      sessionId);
                expCmd.Parameters.AddWithValue("@SciId",     newId);
                expCmd.Parameters.AddWithValue("@Desc",      desc);
                expCmd.Parameters.AddWithValue("@Amt",       req.Amount * (req.Quantity ?? 1m));
                expCmd.Parameters.AddWithValue("@PayeeType", (object?)payeeType      ?? DBNull.Value);
                expCmd.Parameters.AddWithValue("@EmpId",     (object?)req.EmployeeId ?? DBNull.Value);
                expCmd.Parameters.AddWithValue("@EmpName",   (object?)req.EmployeeName ?? DBNull.Value);
                expCmd.Parameters.AddWithValue("@ExtName",   (object?)(string.IsNullOrWhiteSpace(req.ProviderName) ? null : req.ProviderName) ?? DBNull.Value);
                await expCmd.ExecuteNonQueryAsync();
            }

            await tx.CommitAsync();
            return Ok(new { id = newId });
        }
        catch (Exception ex) { _logger.LogError(ex, "AddSessionCostItem"); return StatusCode(500, new { message = ex.Message }); }
    }

    // PUT api/ServiceSessions/{id}/cost-items/{itemId}
    [HttpPut("{sessionId:int}/cost-items/{itemId:int}")]
    public async Task<IActionResult> UpdateCostItem(int sessionId, int itemId, [FromBody] SessionCostItemRequest req)
    {
        try
        {
            using var conn = GetConnection();
            await conn.OpenAsync();
            using var tx = await conn.BeginTransactionAsync();

            // 1. Update session_cost_item
            using var updCmd = new MySqlCommand(@"
                UPDATE session_cost_item
                SET name=@Name,description=@Desc,cost_type=@Type,amount=@Amt,
                    quantity=@Qty,unit=@Unit,is_externalized=@Ext,
                    provider_id=@ProvId,provider_name=@Prov,
                    employee_id=@EmpId,employee_name=@EmpName,sort_order=@Ord
                WHERE id=@ItemId AND session_id=@Sess", conn, tx);
            updCmd.Parameters.AddWithValue("@ItemId",  itemId);
            updCmd.Parameters.AddWithValue("@Sess",    sessionId);
            updCmd.Parameters.AddWithValue("@Name",    req.Name);
            updCmd.Parameters.AddWithValue("@Desc",    (object?)req.Description  ?? DBNull.Value);
            updCmd.Parameters.AddWithValue("@Type",    req.CostType ?? "other");
            updCmd.Parameters.AddWithValue("@Amt",     req.Amount);
            updCmd.Parameters.AddWithValue("@Qty",     req.Quantity ?? 1m);
            updCmd.Parameters.AddWithValue("@Unit",    (object?)req.Unit         ?? DBNull.Value);
            updCmd.Parameters.AddWithValue("@Ext",     req.IsExternalized ? 1 : 0);
            updCmd.Parameters.AddWithValue("@ProvId",  (object?)req.ProviderId   ?? DBNull.Value);
            updCmd.Parameters.AddWithValue("@Prov",    (object?)req.ProviderName ?? DBNull.Value);
            updCmd.Parameters.AddWithValue("@EmpId",   (object?)req.EmployeeId   ?? DBNull.Value);
            updCmd.Parameters.AddWithValue("@EmpName", (object?)req.EmployeeName ?? DBNull.Value);
            updCmd.Parameters.AddWithValue("@Ord",     req.SortOrder ?? 0);
            await updCmd.ExecuteNonQueryAsync();

            // 2. Update/Create/Delete linked service_session_expense según tipo de contrato
            // Consultar tipo de contrato si hay empleado
            string? contractType = null;
            if (req.EmployeeId.HasValue)
            {
                using var empCmd = new MySqlCommand("SELECT contract_type FROM hr_employee WHERE id=@Id", conn, tx);
                empCmd.Parameters.AddWithValue("@Id", req.EmployeeId.Value);
                var ctObj = await empCmd.ExecuteScalarAsync();
                contractType = ctObj?.ToString();
            }

            // Decidir si debe haber gasto: solo honorarios o externos
            var shouldHaveExpense = (req.EmployeeId.HasValue && contractType == "honorarios")
                                 || !string.IsNullOrWhiteSpace(req.ProviderName);

            if (shouldHaveExpense)
            {
                // Crear o actualizar el gasto
                var payeeType = req.EmployeeId.HasValue ? "employee"
                              : !string.IsNullOrWhiteSpace(req.ProviderName) ? "external"
                              : (string?)null;
                var desc = string.IsNullOrWhiteSpace(req.Description) ? req.Name : $"{req.Name} — {req.Description}";
                
                // Intentar actualizar primero
                using var expCmd = new MySqlCommand(@"
                    UPDATE service_session_expense
                    SET description=@Desc, amount=@Amt,
                        payee_type=@PayeeType, payee_employee_id=@EmpId,
                        payee_employee_name=@EmpName, payee_external_name=@ExtName,
                        updated_at=NOW()
                    WHERE session_cost_item_id=@SciId AND status='pending'", conn, tx);
                expCmd.Parameters.AddWithValue("@SciId",     itemId);
                expCmd.Parameters.AddWithValue("@Desc",      desc);
                expCmd.Parameters.AddWithValue("@Amt",       req.Amount * (req.Quantity ?? 1m));
                expCmd.Parameters.AddWithValue("@PayeeType", (object?)payeeType      ?? DBNull.Value);
                expCmd.Parameters.AddWithValue("@EmpId",     (object?)req.EmployeeId ?? DBNull.Value);
                expCmd.Parameters.AddWithValue("@EmpName",   (object?)req.EmployeeName ?? DBNull.Value);
                expCmd.Parameters.AddWithValue("@ExtName",   (object?)(string.IsNullOrWhiteSpace(req.ProviderName) ? null : req.ProviderName) ?? DBNull.Value);
                var rowsAffected = await expCmd.ExecuteNonQueryAsync();
                
                // Si no existía, crear uno nuevo
                if (rowsAffected == 0)
                {
                    using var insExpCmd = new MySqlCommand(@"
                        INSERT INTO service_session_expense
                            (business_id, store_id, service_session_id, session_cost_item_id,
                             description, amount, status,
                             payee_type, payee_employee_id, payee_employee_name, payee_external_name,
                             created_at, updated_at)
                        SELECT ss.business_id, ss.store_id, @Sess, @SciId,
                               @Desc, @Amt, 'pending',
                               @PayeeType, @EmpId, @EmpName, @ExtName,
                               NOW(), NOW()
                        FROM service_session ss WHERE ss.id = @Sess", conn, tx);
                    insExpCmd.Parameters.AddWithValue("@Sess",      sessionId);
                    insExpCmd.Parameters.AddWithValue("@SciId",     itemId);
                    insExpCmd.Parameters.AddWithValue("@Desc",      desc);
                    insExpCmd.Parameters.AddWithValue("@Amt",       req.Amount * (req.Quantity ?? 1m));
                    insExpCmd.Parameters.AddWithValue("@PayeeType", (object?)payeeType      ?? DBNull.Value);
                    insExpCmd.Parameters.AddWithValue("@EmpId",     (object?)req.EmployeeId ?? DBNull.Value);
                    insExpCmd.Parameters.AddWithValue("@EmpName",   (object?)req.EmployeeName ?? DBNull.Value);
                    insExpCmd.Parameters.AddWithValue("@ExtName",   (object?)(string.IsNullOrWhiteSpace(req.ProviderName) ? null : req.ProviderName) ?? DBNull.Value);
                    await insExpCmd.ExecuteNonQueryAsync();
                }
            }
            else
            {
                // No debe tener gasto (empleado por contrato), eliminar si existe
                using var delExpCmd = new MySqlCommand(@"
                    DELETE FROM service_session_expense
                    WHERE session_cost_item_id=@SciId AND status='pending'", conn, tx);
                delExpCmd.Parameters.AddWithValue("@SciId", itemId);
                await delExpCmd.ExecuteNonQueryAsync();
            }

            await tx.CommitAsync();
            return Ok(new { message = "Ítem actualizado" });
        }
        catch (Exception ex) { _logger.LogError(ex, "UpdateSessionCostItem"); return StatusCode(500, new { message = ex.Message }); }
    }

    // DELETE api/ServiceSessions/{id}/cost-items/{itemId}
    [HttpDelete("{sessionId:int}/cost-items/{itemId:int}")]
    public async Task<IActionResult> DeleteCostItem(int sessionId, int itemId)
    {
        try
        {
            using var conn = GetConnection();
            await conn.OpenAsync();
            using var tx = await conn.BeginTransactionAsync();

            // 1. Cancel linked expense first (while we still have the FK reference)
            using var expCmd = new MySqlCommand(@"
                UPDATE service_session_expense
                SET status='cancelled', updated_at=NOW()
                WHERE session_cost_item_id=@SciId AND status='pending'", conn, tx);
            expCmd.Parameters.AddWithValue("@SciId", itemId);
            await expCmd.ExecuteNonQueryAsync();

            // 2. Delete session_cost_item
            using var delCmd = new MySqlCommand(
                "DELETE FROM session_cost_item WHERE id=@ItemId AND session_id=@Sess", conn, tx);
            delCmd.Parameters.AddWithValue("@ItemId", itemId);
            delCmd.Parameters.AddWithValue("@Sess",   sessionId);
            await delCmd.ExecuteNonQueryAsync();

            await tx.CommitAsync();
            return Ok(new { message = "Ítem eliminado" });
        }
        catch (Exception ex) { _logger.LogError(ex, "DeleteSessionCostItem"); return StatusCode(500, new { message = ex.Message }); }
    }

    // ── Private ───────────────────────────────────────────────────────────────

    private int GetUserId()
    {
        var claim = User.FindFirst("sub")?.Value
                 ?? User.FindFirst("user_id")?.Value
                 ?? User.FindFirst("userId")?.Value
                 ?? User.FindFirst("id")?.Value
                 ?? User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;

        return int.TryParse(claim, out var id) ? id : 0;
    }
}

// Helper DTO for cancel body
public record CancelSessionRequestDto(string? Reason);

// DTO for session cost items
public record SessionCostItemRequest(
    string Name,
    string? Description,
    string? CostType,
    decimal Amount,
    decimal? Quantity,
    string? Unit,
    bool IsExternalized,
    int? ProviderId,
    string? ProviderName,
    int? EmployeeId,
    string? EmployeeName,
    int? SortOrder
);
