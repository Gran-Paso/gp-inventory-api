#pragma warning disable CS8601
using GPInventory.Api.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Cors;
using Microsoft.AspNetCore.Mvc;
using MySqlConnector;
using System.Security.Claims;
using System.Text.Json;

namespace GPInventory.Api.Controllers;

/// <summary>
/// GP-HR: Recursos Humanos.
/// Gestión de departamentos, cargos, empleados, liquidaciones,
/// permisos/licencias, roles de negocio y asignación de usuarios.
/// </summary>
[ApiController]
[Route("api/hr")]
[EnableCors("AllowFrontend")]
[Authorize]
public class HrController : ControllerBase
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<HrController> _logger;

    public HrController(IConfiguration configuration, ILogger<HrController> logger)
    {
        _configuration = configuration;
        _logger = logger;
    }

    private MySqlConnection GetConnection()
        => new(_configuration.GetConnectionString("DefaultConnection")!);

    private static bool IsNull(MySqlDataReader r, string col) => r.IsDBNull(r.GetOrdinal(col));

    // ================================================================
    // STATS / DASHBOARD
    // ================================================================

    [HttpGet("stats")]
    public async Task<IActionResult> GetStats([FromQuery] int businessId)
    {
        try
        {
            using var conn = GetConnection();
            await conn.OpenAsync();
            using var cmd = new MySqlCommand(@"
                SELECT
                    COUNT(*)                                              AS totalEmployees,
                    SUM(status = 'active')                               AS activeEmployees,
                    SUM(status = 'on_leave')                             AS onLeaveEmployees,
                    (SELECT COUNT(*) FROM hr_department WHERE business_id=@B AND active=1) AS totalDepartments,
                    (SELECT COUNT(*) FROM hr_position   WHERE business_id=@B AND active=1) AS totalPositions,
                    (SELECT COUNT(*) FROM hr_leave_request lr
                         INNER JOIN hr_employee e2 ON e2.id=lr.employee_id
                         WHERE e2.business_id=@B AND lr.status='pending')  AS pendingLeaves,
                    COALESCE((SELECT SUM(net_salary) FROM hr_payroll hp
                         WHERE hp.business_id=@B
                           AND hp.period_year  = YEAR(CURDATE())
                           AND hp.period_month = MONTH(CURDATE())), 0)    AS totalPayrollCurrentMonth,
                    (SELECT COUNT(*) FROM hr_employee WHERE business_id=@B
                         AND active=1 AND contract_type='plazo_fijo'
                         AND termination_date BETWEEN CURDATE() AND DATE_ADD(CURDATE(), INTERVAL 30 DAY))
                                                                          AS upcomingContracts
                FROM hr_employee
                WHERE business_id=@B AND active=1", conn);
            cmd.Parameters.AddWithValue("@B", businessId);
            using var r = await cmd.ExecuteReaderAsync();
            if (!await r.ReadAsync()) return Ok(new { });
            return Ok(new
            {
                totalEmployees           = r.GetInt32("totalEmployees"),
                activeEmployees          = r.GetInt32("activeEmployees"),
                onLeaveEmployees         = r.GetInt32("onLeaveEmployees"),
                totalDepartments         = r.GetInt32("totalDepartments"),
                totalPositions           = r.GetInt32("totalPositions"),
                pendingLeaves            = r.GetInt32("pendingLeaves"),
                totalPayrollCurrentMonth = r.GetDecimal("totalPayrollCurrentMonth"),
                upcomingContracts        = r.GetInt32("upcomingContracts"),
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error HR stats");
            return StatusCode(500, new { message = "Error obteniendo estadísticas" });
        }
    }

    // ================================================================
    // PERMISOS DEL USUARIO ACTUAL
    // ================================================================

    /// <summary>
    /// Retorna el mapa de permisos granulares del usuario autenticado para un negocio.
    /// Si no tiene hr_business_role asignado, retorna fullAccess=true.
    /// El frontend usa este endpoint al iniciar sesión para construir su store de permisos.
    /// </summary>
    [HttpGet("my-permissions")]
    public async Task<IActionResult> GetMyPermissions([FromQuery] int businessId)
    {
        try
        {
            // super_admin siempre tiene acceso completo, sin consultar la BD
            var jwtSystemRole = User.FindFirst("systemRole")?.Value;
            if (jwtSystemRole == "super_admin")
            {
                return Ok(new
                {
                    fullAccess      = true,
                    systemRoleId    = (int?)null,
                    systemRoleName  = "Super Admin",
                    hrRoleName      = (string?)null,
                    permissions     = (object?)null,
                    businessApps    = Array.Empty<string>(), // super_admin no está limitado por apps
                });
            }

            // Extraer userId desde los claims JWT
            var userIdClaim = User.FindFirst("sub")
                           ?? User.FindFirst("user_id")
                           ?? User.FindFirst("userId")
                           ?? User.FindFirst("id")
                           ?? User.FindFirst(ClaimTypes.NameIdentifier);

            if (!int.TryParse(userIdClaim?.Value, out int userId))
                return Unauthorized(new { message = "No se pudo identificar al usuario" });

            using var conn = GetConnection();
            await conn.OpenAsync();

            // 1. Obtener permisos HR del usuario en el negocio
            using var cmd = new MySqlCommand(@"
                SELECT r.permissions, r.name AS hr_role_name, ub.id_role AS system_role_id, ro.name AS role_name
                FROM user_has_business ub
                LEFT JOIN hr_business_role r  ON r.id  = ub.hr_business_role_id
                LEFT JOIN role              ro ON ro.id = ub.id_role
                WHERE ub.id_user = @UID AND ub.id_business = @BID
                LIMIT 1", conn);
            cmd.Parameters.AddWithValue("@UID", userId);
            cmd.Parameters.AddWithValue("@BID", businessId);

            using var r = await cmd.ExecuteReaderAsync();
            if (!await r.ReadAsync())
                return NotFound(new { message = "Usuario no pertenece a este negocio" });

            var systemRoleId   = IsNull(r, "system_role_id") ? (int?)null : r.GetInt32("system_role_id");
            var roleName       = IsNull(r, "role_name")      ? null       : r.GetString("role_name");
            var hrRoleName     = IsNull(r, "hr_role_name")   ? null       : r.GetString("hr_role_name");
            var permissionsJson = r.IsDBNull(r.GetOrdinal("permissions")) ? null : r.GetString("permissions");
            await r.CloseAsync();

            // 2. Obtener apps habilitadas para el negocio
            using var appsCmd = new MySqlCommand(
                "SELECT app_key FROM business_app_access WHERE business_id = @BID ORDER BY app_key", conn);
            appsCmd.Parameters.AddWithValue("@BID", businessId);
            using var appsReader = await appsCmd.ExecuteReaderAsync();
            var businessApps = new List<string>();
            while (await appsReader.ReadAsync())
                businessApps.Add(appsReader.GetString("app_key"));
            await appsReader.CloseAsync();

            // 3. Dueño siempre tiene acceso completo a todas las apps habilitadas,
            //    independientemente de si tiene un hr_business_role asignado.
            if (roleName == "Dueño")
            {
                return Ok(new
                {
                    fullAccess      = true,
                    systemRoleId,
                    systemRoleName  = roleName,
                    hrRoleName      = hrRoleName,
                    permissions     = (object?)null,
                    businessApps    = businessApps.ToArray(),
                });
            }

            // 4. Si no hay hr_business_role asignado y NO es Dueño → sin acceso
            if (permissionsJson == null)
            {
                return Ok(new
                {
                    fullAccess      = false,
                    systemRoleId,
                    systemRoleName  = roleName,
                    hrRoleName      = (string?)null,
                    permissions     = new Dictionary<string, bool>(), // permisos vacíos
                    businessApps    = businessApps.ToArray(),
                });
            }

            // 5. Tiene hr_business_role → validar permisos granulares
            var perms = JsonSerializer.Deserialize<Dictionary<string, bool>>(
                permissionsJson,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? new();

            return Ok(new
            {
                fullAccess      = false,
                systemRoleId,
                systemRoleName  = roleName,
                hrRoleName      = hrRoleName,
                permissions     = perms,
                businessApps    = businessApps.ToArray(),
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error HR getMyPermissions");
            return StatusCode(500, new { message = "Error obteniendo permisos" });
        }
    }

    // ================================================================
    // DEPARTAMENTOS
    // ================================================================

    [HttpGet("departments")]
    public async Task<IActionResult> GetDepartments([FromQuery] int businessId)
    {
        try
        {
            using var conn = GetConnection();
            await conn.OpenAsync();
            using var cmd = new MySqlCommand(@"
                SELECT d.id, d.business_id, d.name, d.description,
                       d.manager_employee_id, d.active, d.created_at, d.updated_at,
                       CONCAT(e.first_name,' ',e.last_name) AS manager_name,
                       (SELECT COUNT(*) FROM hr_employee WHERE department_id=d.id AND active=1) AS employee_count
                FROM hr_department d
                LEFT JOIN hr_employee e ON e.id = d.manager_employee_id
                WHERE d.business_id=@B AND d.active=1
                ORDER BY d.name", conn);
            cmd.Parameters.AddWithValue("@B", businessId);
            using var r = await cmd.ExecuteReaderAsync();
            var list = new List<object>();
            while (await r.ReadAsync())
                list.Add(new
                {
                    id                = r.GetInt32("id"),
                    businessId        = r.GetInt32("business_id"),
                    name              = r.GetString("name"),
                    description       = IsNull(r, "description") ? null : r.GetString("description"),
                    managerEmployeeId = IsNull(r, "manager_employee_id") ? (int?)null : r.GetInt32("manager_employee_id"),
                    managerName       = IsNull(r, "manager_name") ? null : r.GetString("manager_name"),
                    employeeCount     = r.GetInt32("employee_count"),
                    active            = r.GetBoolean("active"),
                    createdAt         = r.GetDateTime("created_at"),
                    updatedAt         = r.GetDateTime("updated_at"),
                });
            return Ok(list);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error HR getDepartments");
            return StatusCode(500, new { message = "Error obteniendo departamentos" });
        }
    }

    [HttpPost("departments")]
    [HrAuthorize("manage_departments", requireSystemRole: false)]
    public async Task<IActionResult> CreateDepartment([FromBody] JsonElement body)
    {
        try
        {
            var businessId   = body.GetProperty("businessId").GetInt32();
            var name         = body.GetProperty("name").GetString()!;
            var description  = body.TryGetProperty("description", out var d) ? d.GetString() : null;
            var managerId    = body.TryGetProperty("managerEmployeeId", out var m) && m.ValueKind == JsonValueKind.Number ? (int?)m.GetInt32() : null;

            using var conn = GetConnection();
            await conn.OpenAsync();
            using var cmd = new MySqlCommand(@"
                INSERT INTO hr_department (business_id, name, description, manager_employee_id)
                VALUES (@B, @N, @D, @M);
                SELECT LAST_INSERT_ID();", conn);
            cmd.Parameters.AddWithValue("@B", businessId);
            cmd.Parameters.AddWithValue("@N", name);
            cmd.Parameters.AddWithValue("@D", description ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@M", managerId ?? (object)DBNull.Value);
            var newId = Convert.ToInt32(await cmd.ExecuteScalarAsync());
            return Ok(new { id = newId, businessId, name, description, managerEmployeeId = managerId, employeeCount = 0, active = true });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error HR createDepartment");
            return StatusCode(500, new { message = "Error creando departamento" });
        }
    }

    [HttpPut("departments/{id}")]
    [HrAuthorize("manage_departments", requireSystemRole: false)]
    public async Task<IActionResult> UpdateDepartment(int id, [FromBody] JsonElement body)
    {
        try
        {
            var name        = body.TryGetProperty("name", out var n) ? n.GetString() : null;
            var description = body.TryGetProperty("description", out var d) ? d.GetString() : null;
            var managerId   = body.TryGetProperty("managerEmployeeId", out var m) && m.ValueKind == JsonValueKind.Number ? (int?)m.GetInt32() : null;

            using var conn = GetConnection();
            await conn.OpenAsync();
            using var cmd = new MySqlCommand(@"
                UPDATE hr_department SET
                    name = COALESCE(@N, name),
                    description = COALESCE(@D, description),
                    manager_employee_id = @M,
                    updated_at = NOW()
                WHERE id=@ID", conn);
            cmd.Parameters.AddWithValue("@N", name ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@D", description ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@M", managerId ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@ID", id);
            await cmd.ExecuteNonQueryAsync();
            return Ok(new { id, name, description, managerEmployeeId = managerId });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error HR updateDepartment");
            return StatusCode(500, new { message = "Error actualizando departamento" });
        }
    }

    [HttpDelete("departments/{id}")]
    [HrAuthorize("manage_departments", requireSystemRole: false)]
    public async Task<IActionResult> DeleteDepartment(int id)
    {
        try
        {
            using var conn = GetConnection();
            await conn.OpenAsync();
            using var cmd = new MySqlCommand("UPDATE hr_department SET active=0, updated_at=NOW() WHERE id=@ID", conn);
            cmd.Parameters.AddWithValue("@ID", id);
            await cmd.ExecuteNonQueryAsync();
            return NoContent();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error HR deleteDepartment");
            return StatusCode(500, new { message = "Error eliminando departamento" });
        }
    }

    // ================================================================
    // CARGOS / POSICIONES
    // ================================================================

    [HttpGet("positions")]
    public async Task<IActionResult> GetPositions([FromQuery] int businessId)
    {
        try
        {
            using var conn = GetConnection();
            await conn.OpenAsync();
            using var cmd = new MySqlCommand(@"
                SELECT p.id, p.business_id, p.department_id, p.name, p.description,
                       p.schedule_type, p.monthly_salary, p.hourly_rate, p.active,
                       p.created_at, p.updated_at, d.name AS department_name,
                       (SELECT COUNT(*) FROM hr_employee WHERE position_id=p.id AND active=1) AS employee_count
                FROM hr_position p
                LEFT JOIN hr_department d ON d.id = p.department_id
                WHERE p.business_id=@B AND p.active=1
                ORDER BY p.name", conn);
            cmd.Parameters.AddWithValue("@B", businessId);
            using var r = await cmd.ExecuteReaderAsync();
            var list = new List<object>();
            while (await r.ReadAsync())
                list.Add(new
                {
                    id             = r.GetInt32("id"),
                    businessId     = r.GetInt32("business_id"),
                    departmentId   = IsNull(r, "department_id") ? (int?)null : r.GetInt32("department_id"),
                    departmentName = IsNull(r, "department_name") ? null : r.GetString("department_name"),
                    name           = r.GetString("name"),
                    description    = IsNull(r, "description") ? null : r.GetString("description"),
                    scheduleType   = r.GetString("schedule_type"),
                    monthlySalary  = r.GetDecimal("monthly_salary"),
                    hourlyRate     = r.GetDecimal("hourly_rate"),
                    employeeCount  = r.GetInt32("employee_count"),
                    active         = r.GetBoolean("active"),
                    createdAt      = r.GetDateTime("created_at"),
                    updatedAt      = r.GetDateTime("updated_at"),
                });
            return Ok(list);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error HR getPositions");
            return StatusCode(500, new { message = "Error obteniendo cargos" });
        }
    }

    [HttpPost("positions")]
    [HrAuthorize("manage_positions", requireSystemRole: false)]
    public async Task<IActionResult> CreatePosition([FromBody] JsonElement body)
    {
        try
        {
            var businessId    = body.GetProperty("businessId").GetInt32();
            var name          = body.GetProperty("name").GetString()!;
            var description   = body.TryGetProperty("description", out var d) ? d.GetString() : null;
            var scheduleType   = body.TryGetProperty("scheduleType", out var st) ? st.GetString() : "full_time";
            var monthlySalary = body.TryGetProperty("monthlySalary", out var ms) ? ms.GetDecimal() : 0m;
            var hourlyRate    = body.TryGetProperty("hourlyRate", out var hr) ? hr.GetDecimal() : 0m;
            var departmentId  = body.TryGetProperty("departmentId", out var di) && di.ValueKind == JsonValueKind.Number ? (int?)di.GetInt32() : null;

            using var conn = GetConnection();
            await conn.OpenAsync();
            using var cmd = new MySqlCommand(@"
                INSERT INTO hr_position (business_id, department_id, name, description, schedule_type, monthly_salary, hourly_rate)
                VALUES (@B, @DI, @N, @D, @ST, @MS, @HR);
                SELECT LAST_INSERT_ID();", conn);
            cmd.Parameters.AddWithValue("@B",  businessId);
            cmd.Parameters.AddWithValue("@DI", departmentId ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@N",  name);
            cmd.Parameters.AddWithValue("@D",  description ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@ST", scheduleType);
            cmd.Parameters.AddWithValue("@MS", monthlySalary);
            cmd.Parameters.AddWithValue("@HR", hourlyRate);
            var newId = Convert.ToInt32(await cmd.ExecuteScalarAsync());
            return Ok(new { id = newId, businessId, departmentId, name, description, scheduleType, monthlySalary, hourlyRate, employeeCount = 0, active = true });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error HR createPosition");
            return StatusCode(500, new { message = "Error creando cargo" });
        }
    }

    [HttpPut("positions/{id}")]
    [HrAuthorize("manage_positions", requireSystemRole: false)]
    public async Task<IActionResult> UpdatePosition(int id, [FromBody] JsonElement body)
    {
        try
        {
            using var conn = GetConnection();
            await conn.OpenAsync();
            using var cmd = new MySqlCommand(@"
                UPDATE hr_position SET
                    name          = COALESCE(@N,  name),
                    description   = COALESCE(@D,  description),
                    schedule_type = COALESCE(@ST, schedule_type),
                    monthly_salary= COALESCE(@MS, monthly_salary),
                    hourly_rate   = COALESCE(@HR, hourly_rate),
                    department_id = @DI,
                    updated_at    = NOW()
                WHERE id=@ID", conn);
            cmd.Parameters.AddWithValue("@N",  body.TryGetProperty("name", out var n)            ? n.GetString() : (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@D",  body.TryGetProperty("description", out var d)     ? d.GetString() : (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@ST", body.TryGetProperty("scheduleType", out var st)   ? st.GetString() : (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@MS", body.TryGetProperty("monthlySalary", out var ms) && ms.ValueKind == JsonValueKind.Number ? ms.GetDecimal() : (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@HR", body.TryGetProperty("hourlyRate", out var hr)    && hr.ValueKind == JsonValueKind.Number ? hr.GetDecimal() : (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@DI", body.TryGetProperty("departmentId", out var di)  && di.ValueKind == JsonValueKind.Number ? di.GetInt32() : (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@ID", id);
            await cmd.ExecuteNonQueryAsync();
            return Ok(new { id });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error HR updatePosition");
            return StatusCode(500, new { message = "Error actualizando cargo" });
        }
    }

    [HttpDelete("positions/{id}")]
    [HrAuthorize("manage_positions", requireSystemRole: false)]
    public async Task<IActionResult> DeletePosition(int id)
    {
        try
        {
            using var conn = GetConnection();
            await conn.OpenAsync();
            using var cmd = new MySqlCommand("UPDATE hr_position SET active=0, updated_at=NOW() WHERE id=@ID", conn);
            cmd.Parameters.AddWithValue("@ID", id);
            await cmd.ExecuteNonQueryAsync();
            return NoContent();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error HR deletePosition");
            return StatusCode(500, new { message = "Error eliminando cargo" });
        }
    }

    // ================================================================
    // EMPLEADOS
    // ================================================================

    [HttpGet("employees")]
    public async Task<IActionResult> GetEmployees([FromQuery] int businessId, [FromQuery] string? status, [FromQuery] int? departmentId)
    {
        try
        {
            using var conn = GetConnection();
            await conn.OpenAsync();
            using var cmd = new MySqlCommand(@"
                SELECT e.id, e.business_id, e.user_id, u.mail AS user_email,
                       e.first_name, e.last_name, e.email, e.phone, e.rut,
                       e.birth_date, e.hire_date, e.termination_date,
                       e.position_id, p.name AS position_name,
                       e.department_id, d.name AS department_name,
                       e.contract_type, e.status, e.current_salary, e.hourly_rate, e.notes,
                       e.active, e.created_at, e.updated_at,
                       uhb.hr_business_role_id AS user_hr_role_id
                FROM hr_employee e
                LEFT JOIN hr_position   p ON p.id = e.position_id
                LEFT JOIN hr_department d ON d.id = e.department_id
                LEFT JOIN user          u ON u.id = e.user_id
                LEFT JOIN (SELECT id_user, id_business, ANY_VALUE(hr_business_role_id) AS hr_business_role_id
                           FROM user_has_business GROUP BY id_user, id_business) uhb
                          ON uhb.id_user = e.user_id AND uhb.id_business = e.business_id
                WHERE e.business_id=@B AND e.active=1
                  AND (@S IS NULL OR e.status=@S)
                  AND (@D IS NULL OR e.department_id=@D)
                ORDER BY e.first_name, e.last_name", conn);
            cmd.Parameters.AddWithValue("@B", businessId);
            cmd.Parameters.AddWithValue("@S", string.IsNullOrEmpty(status) ? (object)DBNull.Value : status);
            cmd.Parameters.AddWithValue("@D", departmentId ?? (object)DBNull.Value);
            using var r = await cmd.ExecuteReaderAsync();
            var list = new List<object>();
            while (await r.ReadAsync())
                list.Add(new
                {
                    id              = r.GetInt32("id"),
                    businessId      = r.GetInt32("business_id"),
                    userId          = IsNull(r, "user_id") ? (int?)null : r.GetInt32("user_id"),
                    userEmail       = IsNull(r, "user_email") ? null : r.GetString("user_email"),
                    userHrRoleId    = IsNull(r, "user_hr_role_id") ? (int?)null : r.GetInt32("user_hr_role_id"),
                    firstName       = r.GetString("first_name"),
                    lastName        = r.GetString("last_name"),
                    email           = IsNull(r, "email") ? null : r.GetString("email"),
                    phone           = IsNull(r, "phone") ? null : r.GetString("phone"),
                    rut             = IsNull(r, "rut") ? null : r.GetString("rut"),
                    birthDate       = IsNull(r, "birth_date") ? null : r.GetDateTime("birth_date").ToString("yyyy-MM-dd"),
                    hireDate        = r.GetDateTime("hire_date").ToString("yyyy-MM-dd"),
                    terminationDate = IsNull(r, "termination_date") ? null : r.GetDateTime("termination_date").ToString("yyyy-MM-dd"),
                    positionId      = IsNull(r, "position_id") ? (int?)null : r.GetInt32("position_id"),
                    positionName    = IsNull(r, "position_name") ? null : r.GetString("position_name"),
                    departmentId    = IsNull(r, "department_id") ? (int?)null : r.GetInt32("department_id"),
                    departmentName  = IsNull(r, "department_name") ? null : r.GetString("department_name"),
                    contractType    = r.GetString("contract_type"),
                    status          = r.GetString("status"),
                    currentSalary   = r.GetDecimal("current_salary"),
                    hourlyRate      = r.GetDecimal("hourly_rate"),
                    notes           = IsNull(r, "notes") ? null : r.GetString("notes"),
                    active          = r.GetBoolean("active"),
                    createdAt       = r.GetDateTime("created_at"),
                    updatedAt       = r.GetDateTime("updated_at"),
                });
            return Ok(list);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error HR getEmployees");
            return StatusCode(500, new { message = "Error obteniendo empleados" });
        }
    }

    [HttpGet("employees/{id}")]
    public async Task<IActionResult> GetEmployeeById(int id)
    {
        try
        {
            using var conn = GetConnection();
            await conn.OpenAsync();
            using var cmd = new MySqlCommand(@"
                SELECT e.*, u.mail AS user_email,
                       p.name AS position_name, d.name AS department_name,
                       uhb.hr_business_role_id AS user_hr_role_id
                FROM hr_employee e
                LEFT JOIN hr_position   p ON p.id = e.position_id
                LEFT JOIN hr_department d ON d.id = e.department_id
                LEFT JOIN user          u ON u.id = e.user_id
                LEFT JOIN (SELECT id_user, id_business, ANY_VALUE(hr_business_role_id) AS hr_business_role_id
                           FROM user_has_business GROUP BY id_user, id_business) uhb
                          ON uhb.id_user = e.user_id AND uhb.id_business = e.business_id
                WHERE e.id=@ID", conn);
            cmd.Parameters.AddWithValue("@ID", id);
            using var r = await cmd.ExecuteReaderAsync();
            if (!await r.ReadAsync()) return NotFound();
            return Ok(new
            {
                id              = r.GetInt32("id"),
                businessId      = r.GetInt32("business_id"),
                userId          = IsNull(r, "user_id") ? (int?)null : r.GetInt32("user_id"),
                userEmail       = IsNull(r, "user_email") ? null : r.GetString("user_email"),
                userHrRoleId    = IsNull(r, "user_hr_role_id") ? (int?)null : r.GetInt32("user_hr_role_id"),
                firstName       = r.GetString("first_name"),
                lastName        = r.GetString("last_name"),
                email           = IsNull(r, "email") ? null : r.GetString("email"),
                phone           = IsNull(r, "phone") ? null : r.GetString("phone"),
                rut             = IsNull(r, "rut") ? null : r.GetString("rut"),
                birthDate       = IsNull(r, "birth_date") ? null : r.GetDateTime("birth_date").ToString("yyyy-MM-dd"),
                hireDate        = r.GetDateTime("hire_date").ToString("yyyy-MM-dd"),
                terminationDate = IsNull(r, "termination_date") ? null : r.GetDateTime("termination_date").ToString("yyyy-MM-dd"),
                positionId      = IsNull(r, "position_id") ? (int?)null : r.GetInt32("position_id"),
                positionName    = IsNull(r, "position_name") ? null : r.GetString("position_name"),
                departmentId    = IsNull(r, "department_id") ? (int?)null : r.GetInt32("department_id"),
                departmentName  = IsNull(r, "department_name") ? null : r.GetString("department_name"),
                contractType    = r.GetString("contract_type"),
                status          = r.GetString("status"),
                currentSalary   = r.GetDecimal("current_salary"),
                notes           = IsNull(r, "notes") ? null : r.GetString("notes"),
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error HR getEmployeeById");
            return StatusCode(500, new { message = "Error obteniendo empleado" });
        }
    }

    [HttpPost("employees")]
    [HrAuthorize("manage_employees", requireSystemRole: false)]
    public async Task<IActionResult> CreateEmployee([FromBody] JsonElement body)
    {
        try
        {
            using var conn = GetConnection();
            await conn.OpenAsync();
            using var cmd = new MySqlCommand(@"
                INSERT INTO hr_employee
                    (business_id, user_id, first_name, last_name, email, phone, rut,
                     birth_date, hire_date, position_id, department_id,
                     contract_type, current_salary, hourly_rate, notes)
                VALUES
                    (@B, @UID, @FN, @LN, @EM, @PH, @RUT,
                     @BD, @HD, @PID, @DID,
                     @CT, @SAL, @HR, @NO);
                SELECT LAST_INSERT_ID();", conn);
            cmd.Parameters.AddWithValue("@B",   body.GetProperty("businessId").GetInt32());
            cmd.Parameters.AddWithValue("@UID", body.TryGetProperty("userId", out var uid) && uid.ValueKind == JsonValueKind.Number ? uid.GetInt32() : (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@FN",  body.GetProperty("firstName").GetString()!);
            cmd.Parameters.AddWithValue("@LN",  body.GetProperty("lastName").GetString()!);
            cmd.Parameters.AddWithValue("@EM",  body.TryGetProperty("email", out var em) ? em.GetString() : (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@PH",  body.TryGetProperty("phone", out var ph) ? ph.GetString() : (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@RUT", body.TryGetProperty("rut", out var rut) ? rut.GetString() : (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@BD",  body.TryGetProperty("birthDate", out var bd) && bd.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(bd.GetString()) ? bd.GetString() : (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@HD",  body.GetProperty("hireDate").GetString()!);
            cmd.Parameters.AddWithValue("@PID", body.TryGetProperty("positionId", out var pid) && pid.ValueKind == JsonValueKind.Number ? pid.GetInt32() : (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@DID", body.TryGetProperty("departmentId", out var did) && did.ValueKind == JsonValueKind.Number ? did.GetInt32() : (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@CT",  body.TryGetProperty("contractType", out var ct) ? ct.GetString() : "indefinido");
            cmd.Parameters.AddWithValue("@SAL", body.TryGetProperty("currentSalary", out var sal) && sal.ValueKind == JsonValueKind.Number ? sal.GetDecimal() : 0m);
            cmd.Parameters.AddWithValue("@HR",  body.TryGetProperty("hourlyRate", out var hr) && hr.ValueKind == JsonValueKind.Number ? hr.GetDecimal() : 0m);
            cmd.Parameters.AddWithValue("@NO",  body.TryGetProperty("notes", out var no) ? no.GetString() : (object)DBNull.Value);
            var newId = Convert.ToInt32(await cmd.ExecuteScalarAsync());
            return Ok(new { id = newId });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error HR createEmployee");
            return StatusCode(500, new { message = "Error creando empleado" });
        }
    }

    [HttpPut("employees/{id}")]
    [HrAuthorize("manage_employees", requireSystemRole: false)]
    public async Task<IActionResult> UpdateEmployee(int id, [FromBody] JsonElement body)
    {
        try
        {
            using var conn = GetConnection();
            await conn.OpenAsync();
            using var cmd = new MySqlCommand(@"
                UPDATE hr_employee SET
                    first_name     = COALESCE(@FN,  first_name),
                    last_name      = COALESCE(@LN,  last_name),
                    email          = COALESCE(@EM,  email),
                    phone          = COALESCE(@PH,  phone),
                    rut            = COALESCE(@RUT, rut),
                    birth_date     = COALESCE(@BD,  birth_date),
                    position_id    = @PID,
                    department_id  = @DID,
                    contract_type  = COALESCE(@CT,  contract_type),
                    status         = COALESCE(@ST,  status),
                    current_salary = COALESCE(@SAL, current_salary),
                    hourly_rate    = COALESCE(@HR,  hourly_rate),
                    termination_date = @TD,
                    notes          = COALESCE(@NO,  notes),
                    updated_at     = NOW()
                WHERE id=@ID", conn);
            cmd.Parameters.AddWithValue("@FN",  body.TryGetProperty("firstName", out var fn)   ? fn.GetString()   : (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@LN",  body.TryGetProperty("lastName", out var ln)    ? ln.GetString()   : (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@EM",  body.TryGetProperty("email", out var em)       ? em.GetString()   : (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@PH",  body.TryGetProperty("phone", out var ph)       ? ph.GetString()   : (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@RUT", body.TryGetProperty("rut", out var rut)        ? rut.GetString()  : (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@BD",  body.TryGetProperty("birthDate", out var bd) && bd.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(bd.GetString()) ? bd.GetString() : (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@PID", body.TryGetProperty("positionId", out var pid)  && pid.ValueKind == JsonValueKind.Number ? pid.GetInt32()   : (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@DID", body.TryGetProperty("departmentId", out var did) && did.ValueKind == JsonValueKind.Number ? did.GetInt32()   : (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@CT",  body.TryGetProperty("contractType", out var ct) ? ct.GetString()  : (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@ST",  body.TryGetProperty("status", out var st)      ? st.GetString()   : (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@SAL", body.TryGetProperty("currentSalary", out var sal) && sal.ValueKind == JsonValueKind.Number ? sal.GetDecimal() : (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@HR",  body.TryGetProperty("hourlyRate", out var hr) && hr.ValueKind == JsonValueKind.Number ? hr.GetDecimal() : (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@TD",  body.TryGetProperty("terminationDate", out var td) && td.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(td.GetString()) ? td.GetString() : (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@NO",  body.TryGetProperty("notes", out var no)       ? no.GetString()   : (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@ID",  id);
            await cmd.ExecuteNonQueryAsync();
            return Ok(new { id });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error HR updateEmployee");
            return StatusCode(500, new { message = "Error actualizando empleado" });
        }
    }

    [HttpPost("employees/{id}/terminate")]
    [HrAuthorize("manage_employees", requireSystemRole: false)]
    public async Task<IActionResult> TerminateEmployee(int id, [FromBody] JsonElement body)
    {
        try
        {
            var terminationDate = body.GetProperty("terminationDate").GetString()!;
            if (string.IsNullOrWhiteSpace(terminationDate))
                return BadRequest(new { message = "La fecha de término es requerida" });
            
            using var conn = GetConnection();
            await conn.OpenAsync();
            using var cmd = new MySqlCommand(@"
                UPDATE hr_employee
                SET status='inactive', termination_date=@TD, updated_at=NOW()
                WHERE id=@ID", conn);
            cmd.Parameters.AddWithValue("@TD", terminationDate);
            cmd.Parameters.AddWithValue("@ID", id);
            await cmd.ExecuteNonQueryAsync();
            return Ok(new { id });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error HR terminateEmployee");
            return StatusCode(500, new { message = "Error finiquitando empleado" });
        }
    }

    // ================================================================
    // LIQUIDACIONES
    // ================================================================

    /// <summary>Preview de descuentos legales chilenos para un sueldo bruto (tasas 2025/2026).</summary>
    [HttpGet("payroll/calculate-deductions")]
    public IActionResult CalculateDeductions(
        [FromQuery] decimal grossSalary,
        [FromQuery] decimal? afpCommission,
        [FromQuery] decimal? saludRate,
        [FromQuery] string? contractType)
    {
        if (grossSalary <= 0)
            return BadRequest(new { message = "El sueldo bruto debe ser mayor a 0" });
        var calc = ComputeChileanDeductions(grossSalary, afpCommission, saludRate, contractType);
        return Ok(new
        {
            grossSalary,
            utm             = 70_000m,
            deductions      = calc.Lines.Select(l => new { type = l.Type, name = l.Name, amount = l.Amount, percentage = l.Percentage }),
            rentaImponible  = calc.RentaImponible,
            totalDeductions = calc.TotalDeductions,
            netSalary       = calc.NetSalary,
        });
    }

    [HttpGet("payroll")]
    public async Task<IActionResult> GetPayrolls([FromQuery] int businessId, [FromQuery] int? year, [FromQuery] int? month, [FromQuery] int? employeeId)
    {
        try
        {
            using var conn = GetConnection();
            await conn.OpenAsync();
            using var cmd = new MySqlCommand(@"
                SELECT p.id, p.employee_id, CONCAT(e.first_name,' ',e.last_name) AS employee_name,
                       p.business_id, p.period_year, p.period_month,
                       p.gross_salary, p.total_deductions, p.net_salary,
                       p.payment_date, p.status, p.notes, p.created_at, p.updated_at,
                       COALESCE(p.expense_id, 0) AS expense_id,
                       COALESCE(
                           (SELECT JSON_ARRAYAGG(JSON_OBJECT('type', d.type, 'name', d.name,
                                   'amount', d.amount, 'percentage', COALESCE(d.percentage, 0)))
                            FROM hr_payroll_deduction d WHERE d.payroll_id = p.id),
                           JSON_ARRAY()
                       ) AS deductions_json
                FROM hr_payroll p
                INNER JOIN hr_employee e ON e.id = p.employee_id
                WHERE p.business_id=@B
                  AND (@Y IS NULL OR p.period_year=@Y)
                  AND (@M IS NULL OR p.period_month=@M)
                  AND (@EID IS NULL OR p.employee_id=@EID)
                ORDER BY p.period_year DESC, p.period_month DESC, e.first_name", conn);
            cmd.Parameters.AddWithValue("@B",   businessId);
            cmd.Parameters.AddWithValue("@Y",   year       ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@M",   month      ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@EID", employeeId ?? (object)DBNull.Value);
            using var r = await cmd.ExecuteReaderAsync();
            var list = new List<object>();
            while (await r.ReadAsync())
            {
                var dedsJson = IsNull(r, "deductions_json") ? "[]" : r.GetString("deductions_json");
                List<object> dedsList;
                try
                {
                    var arr = JsonSerializer.Deserialize<List<JsonElement>>(dedsJson) ?? new();
                    dedsList = arr.Select(d => (object)new
                    {
                        type       = d.TryGetProperty("type",       out var tp) ? tp.GetString()  : "otro",
                        name       = d.TryGetProperty("name",       out var nm) ? nm.GetString()  : "",
                        amount     = d.TryGetProperty("amount",     out var am) ? am.GetDecimal() : 0m,
                        percentage = d.TryGetProperty("percentage", out var pc) ? pc.GetDecimal() : 0m,
                    }).ToList();
                }
                catch { dedsList = new(); }
                list.Add(new
                {
                    id              = r.GetInt32("id"),
                    employeeId      = r.GetInt32("employee_id"),
                    employeeName    = r.GetString("employee_name"),
                    businessId      = r.GetInt32("business_id"),
                    periodYear      = r.GetInt32("period_year"),
                    periodMonth     = r.GetInt32("period_month"),
                    grossSalary     = r.GetDecimal("gross_salary"),
                    totalDeductions = r.GetDecimal("total_deductions"),
                    netSalary       = r.GetDecimal("net_salary"),
                    paymentDate     = IsNull(r, "payment_date") ? null : r.GetDateTime("payment_date").ToString("yyyy-MM-dd"),
                    status          = r.GetString("status"),
                    notes           = IsNull(r, "notes") ? null : r.GetString("notes"),
                    expenseId       = r.GetInt32("expense_id"),
                    deductions      = dedsList,
                    createdAt       = r.GetDateTime("created_at"),
                    updatedAt       = r.GetDateTime("updated_at"),
                });
            }
            return Ok(list);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error HR getPayrolls");
            return StatusCode(500, new { message = "Error obteniendo liquidaciones" });
        }
    }

    [HttpPost("payroll")]
    [HrAuthorize("manage_payroll", requireSystemRole: false)]
    public async Task<IActionResult> CreatePayroll([FromBody] JsonElement body)
    {
        try
        {
            var employeeId   = body.GetProperty("employeeId").GetInt32();
            var businessId   = body.GetProperty("businessId").GetInt32();
            var periodYear   = body.GetProperty("periodYear").GetInt32();
            var periodMonth  = body.GetProperty("periodMonth").GetInt32();
            var grossSalary  = body.GetProperty("grossSalary").GetDecimal();
            var notes        = body.TryGetProperty("notes", out var no) ? no.GetString() : null;

            // Descuentos: usar los provistos o calcular automáticamente los legales chilenos
            bool hasManualDeds = body.TryGetProperty("deductions", out var dedsEl)
                              && dedsEl.ValueKind == JsonValueKind.Array
                              && dedsEl.GetArrayLength() > 0;
            decimal totalDeductions = 0m;
            ChileanDeductCalc? autoCalc = null;
            if (!hasManualDeds)
            {
                decimal? afpC = body.TryGetProperty("afpCommission", out var ac) && ac.ValueKind == JsonValueKind.Number ? ac.GetDecimal() : (decimal?)null;
                decimal? salR = body.TryGetProperty("saludRate",     out var sr) && sr.ValueKind == JsonValueKind.Number ? sr.GetDecimal() : (decimal?)null;
                string?  ctrc = body.TryGetProperty("contractType",  out var ct) && ct.ValueKind == JsonValueKind.String ? ct.GetString()  : null;
                autoCalc = ComputeChileanDeductions(grossSalary, afpC, salR, ctrc);
                totalDeductions = autoCalc.TotalDeductions;
            }
            else
            {
                foreach (var ded in dedsEl.EnumerateArray())
                    if (ded.TryGetProperty("amount", out var amt)) totalDeductions += amt.GetDecimal();
            }

            var netSalary = grossSalary - totalDeductions;

            using var conn = GetConnection();
            await conn.OpenAsync();
            using var tx = await conn.BeginTransactionAsync();
            try
            {
                using var cmd = new MySqlCommand(@"
                    INSERT INTO hr_payroll (employee_id, business_id, period_year, period_month, gross_salary, total_deductions, net_salary, notes)
                    VALUES (@EID, @B, @PY, @PM, @GS, @TD, @NS, @NO);
                    SELECT LAST_INSERT_ID();", conn, tx);
                cmd.Parameters.AddWithValue("@EID", employeeId);
                cmd.Parameters.AddWithValue("@B",   businessId);
                cmd.Parameters.AddWithValue("@PY",  periodYear);
                cmd.Parameters.AddWithValue("@PM",  periodMonth);
                cmd.Parameters.AddWithValue("@GS",  grossSalary);
                cmd.Parameters.AddWithValue("@TD",  totalDeductions);
                cmd.Parameters.AddWithValue("@NS",  netSalary);
                cmd.Parameters.AddWithValue("@NO",  notes ?? (object)DBNull.Value);
                var newId = Convert.ToInt32(await cmd.ExecuteScalarAsync());

                // Insert deductions
                if (hasManualDeds)
                {
                    foreach (var ded in dedsEl.EnumerateArray())
                    {
                        using var dedCmd = new MySqlCommand(@"
                            INSERT INTO hr_payroll_deduction (payroll_id, type, name, amount, percentage)
                            VALUES (@PID, @TYPE, @NAME, @AMT, @PCT)", conn, tx);
                        dedCmd.Parameters.AddWithValue("@PID",  newId);
                        dedCmd.Parameters.AddWithValue("@TYPE", ded.TryGetProperty("type", out var t) ? t.GetString() : "otro");
                        dedCmd.Parameters.AddWithValue("@NAME", ded.GetProperty("name").GetString()!);
                        dedCmd.Parameters.AddWithValue("@AMT",  ded.GetProperty("amount").GetDecimal());
                        dedCmd.Parameters.AddWithValue("@PCT",  ded.TryGetProperty("percentage", out var pct) && pct.ValueKind == JsonValueKind.Number ? pct.GetDecimal() : (object)DBNull.Value);
                        await dedCmd.ExecuteNonQueryAsync();
                    }
                }
                else if (autoCalc is not null)
                {
                    foreach (var line in autoCalc.Lines)
                    {
                        using var dedCmd = new MySqlCommand(@"
                            INSERT INTO hr_payroll_deduction (payroll_id, type, name, amount, percentage)
                            VALUES (@PID, @TYPE, @NAME, @AMT, @PCT)", conn, tx);
                        dedCmd.Parameters.AddWithValue("@PID",  newId);
                        dedCmd.Parameters.AddWithValue("@TYPE", line.Type);
                        dedCmd.Parameters.AddWithValue("@NAME", line.Name);
                        dedCmd.Parameters.AddWithValue("@AMT",  line.Amount);
                        dedCmd.Parameters.AddWithValue("@PCT",  line.Percentage);
                        await dedCmd.ExecuteNonQueryAsync();
                    }
                }

                await tx.CommitAsync();
                return Ok(new { id = newId, employeeId, businessId, periodYear, periodMonth, grossSalary, totalDeductions, netSalary, status = "draft" });
            }
            catch { await tx.RollbackAsync(); throw; }
        }
        catch (MySqlException ex) when (ex.Number == 1062)
        {
            return BadRequest(new { message = "Ya existe una liquidación para este empleado en ese período" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error HR createPayroll");
            return StatusCode(500, new { message = "Error creando liquidación" });
        }
    }

    [HttpPost("payroll/{id}/approve")]
    [HrAuthorize("manage_payroll")]
    public async Task<IActionResult> ApprovePayroll(int id)
    {
        try
        {
            using var conn = GetConnection();
            await conn.OpenAsync();

            // 1. Fetch payroll + employee data
            using var fetchCmd = new MySqlCommand(@"
                SELECT p.gross_salary, p.net_salary, p.period_year, p.period_month, p.business_id,
                       CONCAT(e.first_name,' ',e.last_name) AS employee_name
                FROM hr_payroll p
                INNER JOIN hr_employee e ON e.id = p.employee_id
                WHERE p.id=@ID AND p.status='draft'", conn);
            fetchCmd.Parameters.AddWithValue("@ID", id);
            using var fr = await fetchCmd.ExecuteReaderAsync();
            if (!await fr.ReadAsync())
                return BadRequest(new { message = "La liquidación no está en estado borrador o no existe" });

            var grossSalary  = fr.GetDecimal("gross_salary");
            var netSalary    = fr.GetDecimal("net_salary");
            var periodYear   = fr.GetInt32("period_year");
            var periodMonth  = fr.GetInt32("period_month");
            var businessId   = fr.GetInt32("business_id");
            var employeeName = fr.GetString("employee_name");
            await fr.CloseAsync();

            using var tx = await conn.BeginTransactionAsync();
            try
            {
                // 2. Ensure "Recursos Humanos" expense category exists
                int catId;
                using (var catCmd = new MySqlCommand(
                    "SELECT id FROM expense_category WHERE name='Recursos Humanos' LIMIT 1", conn, tx))
                {
                    catCmd.Parameters.AddWithValue("@B", businessId);
                    catId = Convert.ToInt32(await catCmd.ExecuteScalarAsync() ?? 0);
                }
                if (catId == 0)
                {
                    using var createCat = new MySqlCommand(@"
                        INSERT INTO expense_category (name, description)
                        VALUES ('Recursos Humanos', 'Gastos de personal y remuneraciones');
                        SELECT LAST_INSERT_ID();", conn, tx);
                    createCat.Parameters.AddWithValue("@B", businessId);
                    catId = Convert.ToInt32(await createCat.ExecuteScalarAsync());
                }

                // 3. Ensure "Remuneraciones" expense subcategory exists
                int subcatId;
                using (var subCmd = new MySqlCommand(
                    "SELECT id FROM expense_subcategory WHERE expense_category_id=@C AND name='Remuneraciones' LIMIT 1", conn, tx))
                {
                    subCmd.Parameters.AddWithValue("@C", catId);
                    subcatId = Convert.ToInt32(await subCmd.ExecuteScalarAsync() ?? 0);
                }
                if (subcatId == 0)
                {
                    using var createSub = new MySqlCommand(@"
                        INSERT INTO expense_subcategory (expense_category_id, name)
                        VALUES (@C, 'Remuneraciones');
                        SELECT LAST_INSERT_ID();", conn, tx);
                    createSub.Parameters.AddWithValue("@C", catId);
                    subcatId = Convert.ToInt32(await createSub.ExecuteScalarAsync());
                }

                // 4. Create expense with gross salary, dated last day of the period
                var lastDay = new DateTime(periodYear, periodMonth, DateTime.DaysInMonth(periodYear, periodMonth));
                int expenseId;
                using (var expCmd = new MySqlCommand(@"
                    INSERT INTO expenses (subcategory_id, amount, description, date, business_id, receipt_type_id, notes)
                    VALUES (@SUB, @AMT, @DESC, @DATE, @B, 4, @NOTES);
                    SELECT LAST_INSERT_ID();", conn, tx))
                {
                    expCmd.Parameters.AddWithValue("@SUB",   subcatId);
                    expCmd.Parameters.AddWithValue("@AMT",   grossSalary);
                    expCmd.Parameters.AddWithValue("@DESC",  $"Remuneración {employeeName} — {_months[periodMonth - 1]} {periodYear}");
                    expCmd.Parameters.AddWithValue("@DATE",  lastDay.ToString("yyyy-MM-dd"));
                    expCmd.Parameters.AddWithValue("@B",     businessId);
                    expCmd.Parameters.AddWithValue("@NOTES", $"Generado desde liquidación #{id}. Bruto: {grossSalary:N0}, Líquido: {netSalary:N0}");
                    expenseId = Convert.ToInt32(await expCmd.ExecuteScalarAsync());
                }

                // 5. Approve payroll and link expense
                using var approveCmd = new MySqlCommand(
                    "UPDATE hr_payroll SET status='approved', expense_id=@EID, updated_at=NOW() WHERE id=@ID", conn, tx);
                approveCmd.Parameters.AddWithValue("@EID", expenseId);
                approveCmd.Parameters.AddWithValue("@ID",  id);
                await approveCmd.ExecuteNonQueryAsync();

                await tx.CommitAsync();
                _logger.LogInformation("Liquidación #{Id} aprobada → gasto #{ExpId} creado", id, expenseId);
                return Ok(new { id, status = "approved", expenseId });
            }
            catch { await tx.RollbackAsync(); throw; }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error HR approvePayroll");
            return StatusCode(500, new { message = "Error aprobando liquidación" });
        }
    }

    [HttpPost("payroll/{id}/pay")]
    [HrAuthorize("manage_payroll")]
    public async Task<IActionResult> MarkAsPaid(int id, [FromBody] JsonElement body)
    {
        try
        {
            var paymentDate = body.GetProperty("paymentDate").GetString()!;
            using var conn = GetConnection();
            await conn.OpenAsync();
            using var cmd = new MySqlCommand("UPDATE hr_payroll SET status='paid', payment_date=@PD, updated_at=NOW() WHERE id=@ID AND status='approved'", conn);
            cmd.Parameters.AddWithValue("@PD", paymentDate);
            cmd.Parameters.AddWithValue("@ID", id);
            var rows = await cmd.ExecuteNonQueryAsync();
            if (rows == 0) return BadRequest(new { message = "La liquidación debe estar aprobada para marcarla como pagada" });
            return Ok(new { id, status = "paid", paymentDate });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error HR markAsPaid");
            return StatusCode(500, new { message = "Error marcando como pagada" });
        }
    }

    [HttpDelete("payroll/{id}")]
    [HrAuthorize("manage_payroll", requireSystemRole: false)]
    public async Task<IActionResult> DeletePayroll(int id)
    {
        try
        {
            using var conn = GetConnection();
            await conn.OpenAsync();
            using var cmd = new MySqlCommand("DELETE FROM hr_payroll WHERE id=@ID AND status='draft'", conn);
            cmd.Parameters.AddWithValue("@ID", id);
            var rows = await cmd.ExecuteNonQueryAsync();
            if (rows == 0) return BadRequest(new { message = "Solo se pueden eliminar liquidaciones en estado borrador" });
            return NoContent();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error HR deletePayroll");
            return StatusCode(500, new { message = "Error eliminando liquidación" });
        }
    }

    // ================================================================
    // TIPOS DE PERMISO
    // ================================================================

    [HttpGet("leave-types")]
    public async Task<IActionResult> GetLeaveTypes([FromQuery] int businessId)
    {
        try
        {
            using var conn = GetConnection();
            await conn.OpenAsync();
            using var cmd = new MySqlCommand(@"
                SELECT id, business_id, name, days_per_year, paid, active, created_at
                FROM hr_leave_type WHERE business_id=@B ORDER BY name", conn);
            cmd.Parameters.AddWithValue("@B", businessId);
            using var r = await cmd.ExecuteReaderAsync();
            var list = new List<object>();
            while (await r.ReadAsync())
                list.Add(new
                {
                    id          = r.GetInt32("id"),
                    businessId  = r.GetInt32("business_id"),
                    name        = r.GetString("name"),
                    daysPerYear = r.GetInt32("days_per_year"),
                    paid        = r.GetBoolean("paid"),
                    active      = r.GetBoolean("active"),
                });
            return Ok(list);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error HR getLeaveTypes");
            return StatusCode(500, new { message = "Error obteniendo tipos de permiso" });
        }
    }

    [HttpPost("leave-types")]
    [HrAuthorize("manage_leaves")]
    public async Task<IActionResult> CreateLeaveType([FromBody] JsonElement body)
    {
        try
        {
            using var conn = GetConnection();
            await conn.OpenAsync();
            using var cmd = new MySqlCommand(@"
                INSERT INTO hr_leave_type (business_id, name, days_per_year, paid)
                VALUES (@B, @N, @DPY, @P);
                SELECT LAST_INSERT_ID();", conn);
            cmd.Parameters.AddWithValue("@B",   body.GetProperty("businessId").GetInt32());
            cmd.Parameters.AddWithValue("@N",   body.GetProperty("name").GetString()!);
            cmd.Parameters.AddWithValue("@DPY", body.TryGetProperty("daysPerYear", out var dpy) ? dpy.GetInt32() : 15);
            cmd.Parameters.AddWithValue("@P",   body.TryGetProperty("paid", out var p) ? p.GetBoolean() : true);
            var newId = Convert.ToInt32(await cmd.ExecuteScalarAsync());
            return Ok(new { id = newId });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error HR createLeaveType");
            return StatusCode(500, new { message = "Error creando tipo de permiso" });
        }
    }

    // ================================================================
    // SOLICITUDES DE PERMISO
    // ================================================================

    [HttpGet("leaves")]
    public async Task<IActionResult> GetLeaveRequests([FromQuery] int businessId, [FromQuery] string? status, [FromQuery] int? employeeId)
    {
        try
        {
            using var conn = GetConnection();
            await conn.OpenAsync();
            using var cmd = new MySqlCommand(@"
                SELECT lr.id, lr.employee_id,
                       CONCAT(e.first_name,' ',e.last_name) AS employee_name,
                       lr.leave_type_id, lt.name AS leave_type_name,
                       lr.start_date, lr.end_date, lr.days_requested,
                       lr.reason, lr.status, lr.reviewed_by, lr.reviewed_at,
                       lr.created_at, lr.updated_at
                FROM hr_leave_request lr
                INNER JOIN hr_employee  e  ON e.id  = lr.employee_id
                INNER JOIN hr_leave_type lt ON lt.id = lr.leave_type_id
                WHERE e.business_id=@B
                  AND (@S   IS NULL OR lr.status=@S)
                  AND (@EID IS NULL OR lr.employee_id=@EID)
                ORDER BY lr.created_at DESC", conn);
            cmd.Parameters.AddWithValue("@B",   businessId);
            cmd.Parameters.AddWithValue("@S",   string.IsNullOrEmpty(status) ? (object)DBNull.Value : status);
            cmd.Parameters.AddWithValue("@EID", employeeId ?? (object)DBNull.Value);
            using var r = await cmd.ExecuteReaderAsync();
            var list = new List<object>();
            while (await r.ReadAsync())
                list.Add(new
                {
                    id             = r.GetInt32("id"),
                    employeeId     = r.GetInt32("employee_id"),
                    employeeName   = r.GetString("employee_name"),
                    leaveTypeId    = r.GetInt32("leave_type_id"),
                    leaveTypeName  = r.GetString("leave_type_name"),
                    startDate      = r.GetDateTime("start_date").ToString("yyyy-MM-dd"),
                    endDate        = r.GetDateTime("end_date").ToString("yyyy-MM-dd"),
                    daysRequested  = r.GetInt32("days_requested"),
                    reason         = IsNull(r, "reason") ? null : r.GetString("reason"),
                    status         = r.GetString("status"),
                    reviewedBy     = IsNull(r, "reviewed_by") ? (int?)null : r.GetInt32("reviewed_by"),
                    reviewedAt     = IsNull(r, "reviewed_at") ? null : r.GetDateTime("reviewed_at").ToString("o"),
                    createdAt      = r.GetDateTime("created_at"),
                    updatedAt      = r.GetDateTime("updated_at"),
                });
            return Ok(list);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error HR getLeaveRequests");
            return StatusCode(500, new { message = "Error obteniendo permisos" });
        }
    }

    [HttpPost("leaves")]
    [HrAuthorize("manage_leaves", requireSystemRole: false)]
    public async Task<IActionResult> CreateLeaveRequest([FromBody] JsonElement body)
    {
        try
        {
            var startDate = body.GetProperty("startDate").GetString()!;
            var endDate   = body.GetProperty("endDate").GetString()!;
            var start     = DateOnly.Parse(startDate);
            var end       = DateOnly.Parse(endDate);
            var days      = Math.Max(1, end.DayNumber - start.DayNumber + 1);

            using var conn = GetConnection();
            await conn.OpenAsync();
            using var cmd = new MySqlCommand(@"
                INSERT INTO hr_leave_request
                    (employee_id, leave_type_id, start_date, end_date, days_requested, reason)
                VALUES (@EID, @LTID, @SD, @ED, @DAYS, @R);
                SELECT LAST_INSERT_ID();", conn);
            cmd.Parameters.AddWithValue("@EID",  body.GetProperty("employeeId").GetInt32());
            cmd.Parameters.AddWithValue("@LTID", body.GetProperty("leaveTypeId").GetInt32());
            cmd.Parameters.AddWithValue("@SD",   startDate);
            cmd.Parameters.AddWithValue("@ED",   endDate);
            cmd.Parameters.AddWithValue("@DAYS", days);
            cmd.Parameters.AddWithValue("@R",    body.TryGetProperty("reason", out var r2) ? r2.GetString() : (object)DBNull.Value);
            var newId = Convert.ToInt32(await cmd.ExecuteScalarAsync());
            return Ok(new { id = newId, daysRequested = days });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error HR createLeaveRequest");
            return StatusCode(500, new { message = "Error creando solicitud de permiso" });
        }
    }

    [HttpPut("leaves/{id}/review")]
    [HrAuthorize("approve_leaves", requireSystemRole: false)]
    public async Task<IActionResult> ReviewLeaveRequest(int id, [FromBody] JsonElement body)
    {
        try
        {
            var status     = body.GetProperty("status").GetString()!;
            var reviewedBy = body.GetProperty("reviewedBy").GetInt32();

            if (status != "approved" && status != "rejected")
                return BadRequest(new { message = "Estado inválido" });

            using var conn = GetConnection();
            await conn.OpenAsync();
            using var cmd = new MySqlCommand(@"
                UPDATE hr_leave_request
                SET status=@S, reviewed_by=@RB, reviewed_at=NOW(), updated_at=NOW()
                WHERE id=@ID AND status='pending'", conn);
            cmd.Parameters.AddWithValue("@S",  status);
            cmd.Parameters.AddWithValue("@RB", reviewedBy);
            cmd.Parameters.AddWithValue("@ID", id);
            var rows = await cmd.ExecuteNonQueryAsync();
            if (rows == 0) return BadRequest(new { message = "La solicitud ya fue revisada o no existe" });
            return Ok(new { id, status });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error HR reviewLeaveRequest");
            return StatusCode(500, new { message = "Error revisando solicitud" });
        }
    }

    // ================================================================
    // ROLES DE NEGOCIO
    // ================================================================

    [HttpGet("roles")]
    public async Task<IActionResult> GetRoles([FromQuery] int businessId)
    {
        try
        {
            using var conn = GetConnection();
            await conn.OpenAsync();
            using var cmd = new MySqlCommand(@"
                SELECT r.id, r.business_id, r.name, r.description, r.permissions, r.active,
                       r.created_at, r.updated_at,
                       (SELECT COUNT(*) FROM user_has_business WHERE hr_business_role_id=r.id AND id_business=r.business_id AND active=1) AS user_count
                FROM hr_business_role r
                WHERE r.business_id=@B AND r.active=1
                ORDER BY r.name", conn);
            cmd.Parameters.AddWithValue("@B", businessId);
            using var r = await cmd.ExecuteReaderAsync();
            var list = new List<object>();
            while (await r.ReadAsync())
            {
                var permsJson = IsNull(r, "permissions") ? "{}" : r.GetString("permissions");
                var perms = JsonSerializer.Deserialize<Dictionary<string, bool>>(permsJson!) ?? new();
                list.Add(new
                {
                    id          = r.GetInt32("id"),
                    businessId  = r.GetInt32("business_id"),
                    name        = r.GetString("name"),
                    description = IsNull(r, "description") ? null : r.GetString("description"),
                    permissions = perms,
                    active      = r.GetBoolean("active"),
                    userCount   = r.GetInt32("user_count"),
                    createdAt   = r.GetDateTime("created_at"),
                    updatedAt   = r.GetDateTime("updated_at"),
                });
            }
            return Ok(list);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error HR getRoles");
            return StatusCode(500, new { message = "Error obteniendo roles" });
        }
    }

    [HttpPost("roles")]
    [HrAuthorize("manage_roles", requireSystemRole: false)]
    public async Task<IActionResult> CreateRole([FromBody] JsonElement body)
    {
        try
        {
            var perms = body.TryGetProperty("permissions", out var p) ? p.ToString() : "{}";
            using var conn = GetConnection();
            await conn.OpenAsync();
            using var cmd = new MySqlCommand(@"
                INSERT INTO hr_business_role (business_id, name, description, permissions)
                VALUES (@B, @N, @D, @P);
                SELECT LAST_INSERT_ID();", conn);
            cmd.Parameters.AddWithValue("@B", body.GetProperty("businessId").GetInt32());
            cmd.Parameters.AddWithValue("@N", body.GetProperty("name").GetString()!);
            cmd.Parameters.AddWithValue("@D", body.TryGetProperty("description", out var d) ? d.GetString() : (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@P", perms);
            var newId = Convert.ToInt32(await cmd.ExecuteScalarAsync());
            return Ok(new { id = newId });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error HR createRole");
            return StatusCode(500, new { message = "Error creando rol" });
        }
    }

    [HttpPut("roles/{id}")]
    [HrAuthorize("manage_roles", requireSystemRole: false)]
    public async Task<IActionResult> UpdateRole(int id, [FromBody] JsonElement body)
    {
        try
        {
            var perms = body.TryGetProperty("permissions", out var p) ? p.ToString() : null;
            using var conn = GetConnection();
            await conn.OpenAsync();
            using var cmd = new MySqlCommand(@"
                UPDATE hr_business_role SET
                    name        = COALESCE(@N, name),
                    description = COALESCE(@D, description),
                    permissions = COALESCE(@P, permissions),
                    updated_at  = NOW()
                WHERE id=@ID", conn);
            cmd.Parameters.AddWithValue("@N", body.TryGetProperty("name", out var n)             ? n.GetString() : (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@D", body.TryGetProperty("description", out var d)      ? d.GetString() : (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@P", perms ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@ID", id);
            await cmd.ExecuteNonQueryAsync();
            return Ok(new { id });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error HR updateRole");
            return StatusCode(500, new { message = "Error actualizando rol" });
        }
    }

    [HttpDelete("roles/{id}")]
    [HrAuthorize("manage_roles", requireSystemRole: false)]
    public async Task<IActionResult> DeleteRole(int id)
    {
        try
        {
            using var conn = GetConnection();
            await conn.OpenAsync();
            using var cmd = new MySqlCommand("UPDATE hr_business_role SET active=0, updated_at=NOW() WHERE id=@ID", conn);
            cmd.Parameters.AddWithValue("@ID", id);
            await cmd.ExecuteNonQueryAsync();
            return NoContent();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error HR deleteRole");
            return StatusCode(500, new { message = "Error eliminando rol" });
        }
    }

    // ================================================================
    // USUARIOS DEL NEGOCIO (user_has_business)
    // ================================================================

    [HttpGet("user-business")]
    public async Task<IActionResult> GetUsersInBusiness([FromQuery] int businessId)
    {
        try
        {
            using var conn = GetConnection();
            await conn.OpenAsync();
            using var cmd = new MySqlCommand(@"
                SELECT ub.id_user, ub.id_business, ub.hr_business_role_id,
                       u.name AS user_name, u.mail AS user_email,
                       r.name AS role_name
                FROM user_has_business ub
                INNER JOIN user u ON u.id = ub.id_user
                LEFT JOIN hr_business_role r ON r.id = ub.hr_business_role_id
                WHERE ub.id_business=@B AND u.active=1
                ORDER BY u.name", conn);
            cmd.Parameters.AddWithValue("@B", businessId);
            using var r = await cmd.ExecuteReaderAsync();
            var list = new List<object>();
            while (await r.ReadAsync())
                list.Add(new
                {
                    id               = r.GetInt32("id_user"),   // use userId as surrogate id
                    userId           = r.GetInt32("id_user"),
                    businessId       = r.GetInt32("id_business"),
                    userName         = IsNull(r, "user_name")  ? "Sin nombre" : r.GetString("user_name"),
                    userEmail        = IsNull(r, "user_email") ? "" : r.GetString("user_email"),
                    businessRoleId   = IsNull(r, "hr_business_role_id") ? (int?)null : r.GetInt32("hr_business_role_id"),
                    businessRoleName = IsNull(r, "role_name") ? null : r.GetString("role_name"),
                    isPrimary        = false,
                    active           = true,
                    createdAt        = DateTime.UtcNow.ToString("o"),
                });
            return Ok(list);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error HR getUsersInBusiness");
            return StatusCode(500, new { message = "Error obteniendo usuarios" });
        }
    }

    [HttpPut("user-business/{userId}/role")]
    [HrAuthorize("manage_users", requireSystemRole: false)]
    public async Task<IActionResult> AssignRoleToUser(int userId, [FromQuery] int? businessId, [FromBody] JsonElement body)
    {
        try
        {
            var roleId = body.TryGetProperty("businessRoleId", out var r) && r.ValueKind == JsonValueKind.Number
                ? (int?)r.GetInt32() : null;
            
            // Si no viene businessId en query, inferirlo de user_has_business por userId
            int finalBusinessId = businessId ?? 0;
            if (finalBusinessId == 0)
            {
                using var connCheck = GetConnection();
                await connCheck.OpenAsync();
                using var cmdCheck = new MySqlCommand(@"
                    SELECT id_business FROM user_has_business WHERE id_user=@UID LIMIT 1", connCheck);
                cmdCheck.Parameters.AddWithValue("@UID", userId);
                var bizIdObj = await cmdCheck.ExecuteScalarAsync();
                if (bizIdObj == null || !int.TryParse(bizIdObj.ToString(), out finalBusinessId))
                {
                    return BadRequest(new { message = "Usuario no pertenece a ningún negocio" });
                }
            }
            
            using var conn = GetConnection();
            await conn.OpenAsync();
            using var cmd = new MySqlCommand(@"
                UPDATE user_has_business SET hr_business_role_id=@RID
                WHERE id_user=@UID AND id_business=@BID", conn);
            cmd.Parameters.AddWithValue("@RID", roleId ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@UID", userId);
            cmd.Parameters.AddWithValue("@BID", finalBusinessId);
            await cmd.ExecuteNonQueryAsync();
            return Ok(new { userId, businessId = finalBusinessId, businessRoleId = roleId });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error HR assignRoleToUser");
            return StatusCode(500, new { message = "Error asignando rol" });
        }
    }

    [HttpDelete("user-business/{userId}")]
    [HrAuthorize("manage_users", requireSystemRole: false)]
    public async Task<IActionResult> RemoveUserFromBusiness(int userId, [FromQuery] int businessId)
    {
        try
        {
            using var conn = GetConnection();
            await conn.OpenAsync();
            using var cmd = new MySqlCommand(@"
                DELETE FROM user_has_business WHERE id_user=@UID AND id_business=@B", conn);
            cmd.Parameters.AddWithValue("@UID", userId);
            cmd.Parameters.AddWithValue("@B",   businessId);
            await cmd.ExecuteNonQueryAsync();
            return NoContent();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error HR removeUserFromBusiness");
            return StatusCode(500, new { message = "Error removiendo usuario" });
        }
    }

    // ================================================================
    // VINCULACIÓN EMPLEADO ↔ CUENTA DE USUARIO
    // ================================================================

    /// <summary>
    /// Busca usuarios activos por email o nombre para vincularlos a un empleado.
    /// Indica si el usuario ya pertenece al negocio (condición para vincular).
    /// </summary>
    [HttpGet("search-users")]
    [HrAuthorize("manage_employees", requireSystemRole: false)]
    public async Task<IActionResult> SearchUsers([FromQuery] string q, [FromQuery] int businessId)
    {
        if (string.IsNullOrWhiteSpace(q) || q.Length < 2)
            return Ok(new List<object>());

        try
        {
            using var conn = GetConnection();
            await conn.OpenAsync();
            using var cmd = new MySqlCommand(@"
                SELECT u.id, u.name, u.mail,
                       CASE WHEN ub.id_user IS NOT NULL THEN 1 ELSE 0 END AS in_business,
                       ub.hr_business_role_id,
                       r.name AS role_name
                FROM user u
                LEFT JOIN user_has_business ub ON ub.id_user = u.id AND ub.id_business = @B
                LEFT JOIN hr_business_role   r  ON r.id = ub.hr_business_role_id
                WHERE u.active = 1
                  AND (u.mail LIKE @Q OR u.name LIKE @Q)
                ORDER BY in_business DESC, u.name
                LIMIT 10", conn);
            cmd.Parameters.AddWithValue("@B", businessId);
            cmd.Parameters.AddWithValue("@Q", $"%{q}%");

            using var r = await cmd.ExecuteReaderAsync();
            var list = new List<object>();
            while (await r.ReadAsync())
                list.Add(new
                {
                    id               = r.GetInt32("id"),
                    name             = IsNull(r, "name") ? "" : r.GetString("name"),
                    email            = r.GetString("mail"),
                    inBusiness       = r.GetInt32("in_business") == 1,
                    businessRoleId   = IsNull(r, "hr_business_role_id") ? (int?)null : r.GetInt32("hr_business_role_id"),
                    businessRoleName = IsNull(r, "role_name") ? null : r.GetString("role_name"),
                });
            return Ok(list);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error HR searchUsers");
            return StatusCode(500, new { message = "Error buscando usuarios" });
        }
    }

    /// <summary>
    /// Vincula una cuenta de usuario a un empleado y opcionalmente asigna el hr_business_role.
    /// El usuario debe pertenecer al negocio (tener fila en user_has_business).
    /// </summary>
    [HttpPut("employees/{id}/link-user")]
    [HrAuthorize("manage_employees", requireSystemRole: false)]
    public async Task<IActionResult> LinkEmployeeUser(int id, [FromBody] JsonElement body)
    {
        try
        {
            var userId   = body.GetProperty("userId").GetInt32();
            var hrRoleId = body.TryGetProperty("hrBusinessRoleId", out var rp) && rp.ValueKind == JsonValueKind.Number
                           ? (int?)rp.GetInt32() : null;

            using var conn = GetConnection();
            await conn.OpenAsync();

            // Obtener businessId del empleado
            using var bizCmd = new MySqlCommand("SELECT business_id FROM hr_employee WHERE id=@ID", conn);
            bizCmd.Parameters.AddWithValue("@ID", id);
            var bizObj = await bizCmd.ExecuteScalarAsync();
            if (bizObj == null) return NotFound(new { message = "Empleado no encontrado" });
            var bizId = Convert.ToInt32(bizObj);

            // Verificar que el usuario pertenece al negocio
            using var checkCmd = new MySqlCommand(@"
                SELECT COUNT(*) FROM user_has_business WHERE id_user=@UID AND id_business=@BID", conn);
            checkCmd.Parameters.AddWithValue("@UID", userId);
            checkCmd.Parameters.AddWithValue("@BID", bizId);
            if (Convert.ToInt32(await checkCmd.ExecuteScalarAsync()) == 0)
                return BadRequest(new { message = "El usuario no pertenece a este negocio. Debe ser agregado primero desde la gestión de usuarios del sistema." });

            // Vincular usuario al empleado
            using var updCmd = new MySqlCommand(@"
                UPDATE hr_employee SET user_id=@UID, updated_at=NOW() WHERE id=@ID", conn);
            updCmd.Parameters.AddWithValue("@UID", userId);
            updCmd.Parameters.AddWithValue("@ID",  id);
            await updCmd.ExecuteNonQueryAsync();

            // Asignar hr_business_role si se especificó
            if (hrRoleId.HasValue)
            {
                using var roleCmd = new MySqlCommand(@"
                    UPDATE user_has_business SET hr_business_role_id=@RID
                    WHERE id_user=@UID AND id_business=@BID", conn);
                roleCmd.Parameters.AddWithValue("@RID", hrRoleId.Value);
                roleCmd.Parameters.AddWithValue("@UID", userId);
                roleCmd.Parameters.AddWithValue("@BID", bizId);
                await roleCmd.ExecuteNonQueryAsync();
            }

            return Ok(new { id, userId, hrBusinessRoleId = hrRoleId });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error HR linkEmployeeUser");
            return StatusCode(500, new { message = "Error vinculando usuario al empleado" });
        }
    }

    /// <summary>
    /// Desvincula la cuenta de usuario del empleado (user_id queda NULL).
    /// No elimina al usuario del negocio ni revoca su hr_business_role.
    /// </summary>
    [HttpDelete("employees/{id}/link-user")]
    [HrAuthorize("manage_employees", requireSystemRole: false)]
    public async Task<IActionResult> UnlinkEmployeeUser(int id)
    {
        try
        {
            using var conn = GetConnection();
            await conn.OpenAsync();
            using var cmd = new MySqlCommand(@"
                UPDATE hr_employee SET user_id=NULL, updated_at=NOW() WHERE id=@ID", conn);
            cmd.Parameters.AddWithValue("@ID", id);
            await cmd.ExecuteNonQueryAsync();
            return Ok(new { id });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error HR unlinkEmployeeUser");
            return StatusCode(500, new { message = "Error desvinculando usuario del empleado" });
        }
    }

    // ================================================================
    // HELPERS – DESCUENTOS LEGALES CHILE 2025/2026
    // ================================================================

    private static readonly string[] _months =
        { "Enero","Febrero","Marzo","Abril","Mayo","Junio",
          "Julio","Agosto","Septiembre","Octubre","Noviembre","Diciembre" };

    /// <summary>
    /// Calcula los descuentos legales chilenos 2025/2026.
    /// AFP pensión 10% + comisión (default 1.44%), Fonasa 7%,
    /// Cesantía 0.6% indefinido / 3% plazo fijo, Impuesto Único 2ª Cat.
    /// </summary>
    private static ChileanDeductCalc ComputeChileanDeductions(
        decimal gross, decimal? afpCommissionPct = null, decimal? saludRate = null, string? contractType = null)
    {
        const decimal UTM = 70_000m;
        var afpComm  = (afpCommissionPct ?? 1.44m) / 100m;
        var salud    = saludRate ?? 0.07m;
        var cesantia = contractType == "plazo_fijo" ? 0.03m : 0.006m;

        // AFP = pensión 10% + comisión AFP (se descuenta como un solo ítem)
        var afpTotalRate = 0.10m + afpComm;
        var afpTotal     = Math.Round(gross * afpTotalRate);
        var saludAmt     = Math.Round(gross * salud);
        var cesantiaAmt  = Math.Round(gross * cesantia);
        var rentaImp     = gross - afpTotal - saludAmt - cesantiaAmt;
        var impuesto     = Math.Max(0m, ComputeImpuestoUnico(rentaImp, UTM));

        var lines = new[]
        {
            new ChileanDeductLine("afp",            $"AFP ({afpTotalRate * 100:F2}%)",               afpTotal,    Math.Round(afpTotalRate * 100, 2)),
            new ChileanDeductLine("salud",          $"Salud Fonasa ({salud * 100:F0}%)",             saludAmt,    Math.Round(salud * 100, 2)),
            new ChileanDeductLine("seguro_cesantia",$"Seg. Cesantía ({cesantia * 100:F1}%)",         cesantiaAmt, Math.Round(cesantia * 100, 1)),
            new ChileanDeductLine("impuesto",       "Impuesto Único 2ª Cat.",                        impuesto,    gross > 0 ? Math.Round(impuesto / gross * 100, 2) : 0m),
        };
        var total = lines.Sum(l => l.Amount);
        return new ChileanDeductCalc(total, gross - total, rentaImp, lines);
    }

    /// <summary>Tabla mensual Impuesto Segunda Categoría 2025/2026 (tasa × renta − rebaja en UTM).</summary>
    private static decimal ComputeImpuestoUnico(decimal renta, decimal utm)
    {
        if (renta <= 0) return 0m;
        (decimal Lim, decimal Rate, decimal Reb)[] tramos =
        {
            (13.5m,            0m,      0m    ),
            (30m,              0.04m,   0.54m ),
            (50m,              0.08m,   1.74m ),
            (70m,              0.135m,  4.49m ),
            (90m,              0.23m,  11.14m ),
            (120m,             0.304m, 17.80m ),
            (150m,             0.35m,  23.32m ),
            (decimal.MaxValue, 0.40m,  30.82m ),
        };
        foreach (var (lim, rate, reb) in tramos)
            if (renta <= lim * utm)
                return Math.Round(rate * renta - reb * utm);
        return 0m;
    }
}

internal record ChileanDeductLine(string Type, string Name, decimal Amount, decimal Percentage);
internal record ChileanDeductCalc(decimal TotalDeductions, decimal NetSalary, decimal RentaImponible, ChileanDeductLine[] Lines);
