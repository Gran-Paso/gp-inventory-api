#pragma warning disable CS8601
using GPInventory.Api.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Cors;
using Microsoft.AspNetCore.Mvc;
using MySqlConnector;

namespace GPInventory.Api.Controllers;

/// <summary>
/// Gestión de servicios (gp-services).
///
/// Modelo de venta:
///   service_sale      → orden (N servicios por orden), equivalente a "sales"
///   service_sale_item → ítem de la orden, equivalente a "sales_detail"
///
/// Al marcar una venta como "completed", se generan automáticamente registros en
/// expenses (uno por service_cost_item × servicio en la orden), vinculados por
/// service_sale_id — conecta con gp-expenses.
/// </summary>
[ApiController]
[Route("api/[controller]")]
[EnableCors("AllowFrontend")]
[Authorize]
public class ServicesController : ControllerBase
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<ServicesController> _logger;

    // Mapeo cost_type → expense subcategory_id (seed en create_services_tables.sql)
    private static readonly Dictionary<string, int> CostTypeToSubcategory = new()
    {
        ["material"]  = 49,  // Materiales de servicio
        ["labor"]     = 50,  // Mano de obra
        ["external"]  = 51,  // Subcontratación
        ["overhead"]  = 52,  // Costo general de servicio
        ["other"]     = 52,
    };

    public ServicesController(IConfiguration configuration, ILogger<ServicesController> logger)
    {
        _configuration = configuration;
        _logger = logger;
    }

    private MySqlConnection GetConnection()
        => new(_configuration.GetConnectionString("DefaultConnection")!);

    // IsDBNull por nombre — compatible con DbDataReader y MySqlDataReader
    private static bool IsNull(MySqlDataReader r, string col) => r.IsDBNull(r.GetOrdinal(col));

    // ================================================================
    // CATEGORÍAS
    // ================================================================

    [HttpGet("categories")]
    public async Task<IActionResult> GetCategories([FromQuery] int businessId)
    {
        try
        {
            using var conn = GetConnection();
            await conn.OpenAsync();
            using var cmd = new MySqlCommand(@"
                SELECT id, name, description, business_id, active, created_at
                FROM service_category
                WHERE business_id = @B AND active = 1
                ORDER BY name", conn);
            cmd.Parameters.AddWithValue("@B", businessId);
            using var r = await cmd.ExecuteReaderAsync();
            var list = new List<object>();
            while (await r.ReadAsync())
                list.Add(new
                {
                    id          = r.GetInt32("id"),
                    name        = r.GetString("name"),
                    description = IsNull(r, "description") ? "" : r.GetString("description"),
                    businessId  = r.GetInt32("business_id"),
                    active      = r.GetBoolean("active"),
                    createdAt   = r.GetDateTime("created_at").ToString("yyyy-MM-dd HH:mm:ss"),
                });
            return Ok(list);
        }
        catch (Exception ex) { return Err(ex, "GetCategories"); }
    }

    [HttpPost("categories")]
    [HrAuthorize("manage_services", false)]
    public async Task<IActionResult> CreateCategory([FromBody] CategoryRequest req)
    {
        try
        {
            using var conn = GetConnection();
            await conn.OpenAsync();
            using var cmd = new MySqlCommand(@"
                INSERT INTO service_category (name, description, business_id, active)
                VALUES (@Name, @Desc, @B, 1); SELECT LAST_INSERT_ID();", conn);
            cmd.Parameters.AddWithValue("@Name", req.Name);
            cmd.Parameters.AddWithValue("@Desc", (object?)req.Description ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@B",    req.BusinessId);
            return Ok(new { id = Convert.ToInt32(await cmd.ExecuteScalarAsync()) });
        }
        catch (Exception ex) { return Err(ex, "CreateCategory"); }
    }

    [HttpPut("categories/{id}")]
    [HrAuthorize("manage_services", false)]
    public async Task<IActionResult> UpdateCategory(int id, [FromBody] CategoryRequest req)
    {
        try
        {
            using var conn = GetConnection();
            await conn.OpenAsync();
            using var cmd = new MySqlCommand(
                "UPDATE service_category SET name=@Name,description=@Desc WHERE id=@Id AND business_id=@B", conn);
            cmd.Parameters.AddWithValue("@Id",   id);
            cmd.Parameters.AddWithValue("@Name", req.Name);
            cmd.Parameters.AddWithValue("@Desc", (object?)req.Description ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@B",    req.BusinessId);
            await cmd.ExecuteNonQueryAsync();
            return Ok(new { message = "Categoría actualizada" });
        }
        catch (Exception ex) { return Err(ex, "UpdateCategory"); }
    }

    [HttpDelete("categories/{id}")]
    [HrAuthorize("manage_services", false)]
    public async Task<IActionResult> DeleteCategory(int id, [FromQuery] int businessId)
    {
        try
        {
            using var conn = GetConnection();
            await conn.OpenAsync();
            using var cmd = new MySqlCommand(
                "UPDATE service_category SET active=0 WHERE id=@Id AND business_id=@B", conn);
            cmd.Parameters.AddWithValue("@Id", id);
            cmd.Parameters.AddWithValue("@B",  businessId);
            await cmd.ExecuteNonQueryAsync();
            return Ok(new { message = "Categoría eliminada" });
        }
        catch (Exception ex) { return Err(ex, "DeleteCategory"); }
    }

    // ================================================================
    // SERVICIOS
    // ================================================================

    [HttpGet]
    public async Task<IActionResult> GetServices(
        [FromQuery] int businessId,
        [FromQuery] int? categoryId = null,
        [FromQuery] int? storeId = null,
        [FromQuery] bool? active = true)
    {
        try
        {
            using var conn = GetConnection();
            await conn.OpenAsync();
            using var cmd = new MySqlCommand(@"
                SELECT s.id, s.name, s.description, s.base_price, s.duration_minutes,
                       s.business_id, s.store_id, s.category_id, s.active, s.created_at, s.updated_at,
                       sc.name AS category_name, st.name AS store_name,
                       (SELECT COUNT(*) FROM service_cost_item ci WHERE ci.service_id = s.id)              AS cost_items_count,
                       (SELECT COALESCE(SUM(ci.amount*ci.quantity),0) FROM service_cost_item ci
                        WHERE ci.service_id = s.id)                                                        AS total_cost
                FROM service s
                LEFT JOIN service_category sc ON s.category_id = sc.id
                LEFT JOIN store            st ON s.store_id    = st.id
                WHERE s.business_id = @B
                  AND (@Cat IS NULL OR s.category_id = @Cat)
                  AND (@Str IS NULL OR s.store_id    = @Str)
                  AND (@Act IS NULL OR s.active      = @Act)
                ORDER BY s.name", conn);
            cmd.Parameters.AddWithValue("@B",   businessId);
            cmd.Parameters.AddWithValue("@Cat", (object?)categoryId ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@Str", (object?)storeId    ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@Act", active.HasValue ? (object)(active.Value ? 1 : 0) : DBNull.Value);
            using var r = await cmd.ExecuteReaderAsync();
            var list = new List<object>();
            while (await r.ReadAsync())
                list.Add(new
                {
                    id              = r.GetInt32("id"),
                    name            = r.GetString("name"),
                    description     = IsNull(r, "description")      ? "" : r.GetString("description"),
                    basePrice       = r.GetDecimal("base_price"),
                    durationMinutes = IsNull(r, "duration_minutes")  ? (int?)null : r.GetInt32("duration_minutes"),
                    businessId      = r.GetInt32("business_id"),
                    storeId         = IsNull(r, "store_id")          ? (int?)null : r.GetInt32("store_id"),
                    categoryId      = IsNull(r, "category_id")       ? (int?)null : r.GetInt32("category_id"),
                    categoryName    = IsNull(r, "category_name")     ? "" : r.GetString("category_name"),
                    storeName       = IsNull(r, "store_name")        ? "" : r.GetString("store_name"),
                    active          = r.GetBoolean("active"),
                    costItemsCount  = r.GetInt32("cost_items_count"),
                    totalCost       = r.GetDecimal("total_cost"),
                    createdAt       = r.GetDateTime("created_at").ToString("yyyy-MM-dd HH:mm:ss"),
                    updatedAt       = r.GetDateTime("updated_at").ToString("yyyy-MM-dd HH:mm:ss"),
                });
            return Ok(list);
        }
        catch (Exception ex) { return Err(ex, "GetServices"); }
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetServiceById(int id)
    {
        try
        {
            using var conn = GetConnection();
            await conn.OpenAsync();

            using var svcCmd = new MySqlCommand(@"
                SELECT s.id, s.name, s.description, s.base_price, s.duration_minutes,
                       s.business_id, s.store_id, s.category_id, s.active, s.created_at, s.updated_at,
                       sc.name AS category_name, st.name AS store_name
                FROM service s
                LEFT JOIN service_category sc ON s.category_id = sc.id
                LEFT JOIN store            st ON s.store_id    = st.id
                WHERE s.id = @Id", conn);
            svcCmd.Parameters.AddWithValue("@Id", id);
            using var sr = await svcCmd.ExecuteReaderAsync();
            if (!await sr.ReadAsync()) return NotFound(new { error = "Servicio no encontrado" });
            var svc = new
            {
                id              = sr.GetInt32("id"),
                name            = sr.GetString("name"),
                description     = IsNull(sr, "description")     ? "" : sr.GetString("description"),
                basePrice       = sr.GetDecimal("base_price"),
                durationMinutes = IsNull(sr, "duration_minutes") ? (int?)null : sr.GetInt32("duration_minutes"),
                businessId      = sr.GetInt32("business_id"),
                storeId         = IsNull(sr, "store_id")         ? (int?)null : sr.GetInt32("store_id"),
                categoryId      = IsNull(sr, "category_id")      ? (int?)null : sr.GetInt32("category_id"),
                categoryName    = IsNull(sr, "category_name")    ? "" : sr.GetString("category_name"),
                storeName       = IsNull(sr, "store_name")       ? "" : sr.GetString("store_name"),
                active          = sr.GetBoolean("active"),
                createdAt       = sr.GetDateTime("created_at").ToString("yyyy-MM-dd HH:mm:ss"),
                updatedAt       = sr.GetDateTime("updated_at").ToString("yyyy-MM-dd HH:mm:ss"),
            };
            await sr.CloseAsync();

            using var costCmd = new MySqlCommand(@"
                SELECT ci.id, ci.name, ci.description, ci.cost_type, ci.amount, ci.quantity, ci.unit,
                       ci.is_externalized, ci.provider_id, ci.provider_name, ci.receipt_type_id, ci.sort_order,
                       ci.employee_id, ci.employee_name,
                       ci.linked_service_id, ci.linked_service_name,
                       p.name AS provider_display_name,
                       e.contract_type AS emp_contract_type,
                       e.hourly_rate   AS emp_hourly_rate
                FROM service_cost_item ci
                LEFT JOIN provider     p  ON ci.provider_id  = p.id
                LEFT JOIN hr_employee  e  ON ci.employee_id  = e.id
                WHERE ci.service_id=@Id ORDER BY ci.sort_order, ci.id", conn);
            costCmd.Parameters.AddWithValue("@Id", id);
            using var cr = await costCmd.ExecuteReaderAsync();
            var costItems = new List<object>();
            while (await cr.ReadAsync())
                costItems.Add(new
                {
                    id                   = cr.GetInt32("id"),
                    name                 = cr.GetString("name"),
                    description          = IsNull(cr, "description")           ? "" : cr.GetString("description"),
                    costType             = cr.GetString("cost_type"),
                    amount               = cr.GetDecimal("amount"),
                    quantity             = cr.GetDecimal("quantity"),
                    unit                 = IsNull(cr, "unit")                  ? "" : cr.GetString("unit"),
                    isExternalized       = cr.GetBoolean("is_externalized"),
                    providerId           = IsNull(cr, "provider_id")           ? (int?)null : cr.GetInt32("provider_id"),
                    providerName         = IsNull(cr, "provider_display_name") ? (IsNull(cr, "provider_name") ? "" : cr.GetString("provider_name")) : cr.GetString("provider_display_name"),
                    receiptTypeId        = IsNull(cr, "receipt_type_id")       ? (int?)null : cr.GetInt32("receipt_type_id"),
                    sortOrder            = cr.GetInt32("sort_order"),
                    employeeId           = IsNull(cr, "employee_id")           ? (int?)null : cr.GetInt32("employee_id"),
                    employeeName         = IsNull(cr, "employee_name")         ? null : cr.GetString("employee_name"),
                    employeeContractType = IsNull(cr, "emp_contract_type")     ? null : cr.GetString("emp_contract_type"),
                    employeeHourlyRate   = IsNull(cr, "emp_hourly_rate")       ? (decimal?)null : cr.GetDecimal("emp_hourly_rate"),
                    linkedServiceId      = IsNull(cr, "linked_service_id")     ? (int?)null : cr.GetInt32("linked_service_id"),
                    linkedServiceName    = IsNull(cr, "linked_service_name")   ? null : cr.GetString("linked_service_name"),
                });
            await cr.CloseAsync();

            using var subCmd = new MySqlCommand(@"
                SELECT ss.id, ss.child_service_id, ss.quantity, ss.additional_cost, ss.notes, ss.sort_order,
                       svc2.name AS sub_name, svc2.base_price AS sub_price
                FROM service_sub_service ss
                INNER JOIN service svc2 ON ss.child_service_id = svc2.id
                WHERE ss.parent_service_id=@Id ORDER BY ss.sort_order, ss.id", conn);
            subCmd.Parameters.AddWithValue("@Id", id);
            using var subR = await subCmd.ExecuteReaderAsync();
            var subServices = new List<object>();
            while (await subR.ReadAsync())
                subServices.Add(new
                {
                    id               = subR.GetInt32("id"),
                    childServiceId   = subR.GetInt32("child_service_id"),
                    childServiceName = subR.GetString("sub_name"),
                    childBasePrice   = subR.GetDecimal("sub_price"),
                    quantity         = subR.GetDecimal("quantity"),
                    additionalCost   = subR.GetDecimal("additional_cost"),
                    notes            = IsNull(subR, "notes") ? "" : subR.GetString("notes"),
                    sortOrder        = subR.GetInt32("sort_order"),
                });

            return Ok(new { service = svc, costItems, subServices });
        }
        catch (Exception ex) { return Err(ex, "GetServiceById"); }
    }

    [HttpPost]
    [HrAuthorize("manage_services", false)]
    public async Task<IActionResult> CreateService([FromBody] ServiceRequest req)
    {
        try
        {
            using var conn = GetConnection();
            await conn.OpenAsync();
            using var cmd = new MySqlCommand(@"
                INSERT INTO service (name,description,category_id,business_id,store_id,base_price,duration_minutes,active)
                VALUES (@Name,@Desc,@Cat,@B,@Str,@Price,@Dur,1); SELECT LAST_INSERT_ID();", conn);
            cmd.Parameters.AddWithValue("@Name",  req.Name);
            cmd.Parameters.AddWithValue("@Desc",  (object?)req.Description    ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@Cat",   (object?)req.CategoryId     ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@B",     req.BusinessId);
            cmd.Parameters.AddWithValue("@Str",   (object?)req.StoreId        ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@Price", req.BasePrice);
            cmd.Parameters.AddWithValue("@Dur",   (object?)req.DurationMinutes ?? DBNull.Value);
            return Ok(new { id = Convert.ToInt32(await cmd.ExecuteScalarAsync()) });
        }
        catch (Exception ex) { return Err(ex, "CreateService"); }
    }

    [HttpPut("{id}")]
    [HrAuthorize("manage_services", false)]
    public async Task<IActionResult> UpdateService(int id, [FromBody] ServiceRequest req)
    {
        try
        {
            using var conn = GetConnection();
            await conn.OpenAsync();
            using var cmd = new MySqlCommand(@"
                UPDATE service SET name=@Name,description=@Desc,category_id=@Cat,
                    store_id=@Str,base_price=@Price,duration_minutes=@Dur,updated_at=NOW()
                WHERE id=@Id AND business_id=@B", conn);
            cmd.Parameters.AddWithValue("@Id",    id);
            cmd.Parameters.AddWithValue("@Name",  req.Name);
            cmd.Parameters.AddWithValue("@Desc",  (object?)req.Description    ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@Cat",   (object?)req.CategoryId     ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@B",     req.BusinessId);
            cmd.Parameters.AddWithValue("@Str",   (object?)req.StoreId        ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@Price", req.BasePrice);
            cmd.Parameters.AddWithValue("@Dur",   (object?)req.DurationMinutes ?? DBNull.Value);
            await cmd.ExecuteNonQueryAsync();
            return Ok(new { message = "Servicio actualizado" });
        }
        catch (Exception ex) { return Err(ex, "UpdateService"); }
    }

    [HttpDelete("{id}")]
    [HrAuthorize("manage_services", false)]
    public async Task<IActionResult> DeleteService(int id, [FromQuery] int businessId)
    {
        try
        {
            using var conn = GetConnection();
            await conn.OpenAsync();
            using var cmd = new MySqlCommand(
                "UPDATE service SET active=0 WHERE id=@Id AND business_id=@B", conn);
            cmd.Parameters.AddWithValue("@Id", id);
            cmd.Parameters.AddWithValue("@B",  businessId);
            await cmd.ExecuteNonQueryAsync();
            return Ok(new { message = "Servicio eliminado" });
        }
        catch (Exception ex) { return Err(ex, "DeleteService"); }
    }

    // ================================================================
    // PROVIDERS (lista y creación rápida, para el selector en cost items)
    // ================================================================

    [HttpGet("providers")]
    public async Task<IActionResult> GetProviders([FromQuery] int businessId)
    {
        try
        {
            using var conn = GetConnection();
            await conn.OpenAsync();
            using var cmd = new MySqlCommand(@"
                SELECT id, name, mail
                FROM provider
                WHERE (id_business=@B OR id_business IS NULL) AND active=1
                ORDER BY name", conn);
            cmd.Parameters.AddWithValue("@B", businessId);
            using var r = await cmd.ExecuteReaderAsync();
            var list = new List<object>();
            while (await r.ReadAsync())
                list.Add(new
                {
                    id   = r.GetInt32("id"),
                    name = r.GetString("name"),
                    mail = IsNull(r, "mail") ? "" : r.GetString("mail"),
                });
            return Ok(list);
        }
        catch (Exception ex) { return Err(ex, "GetProviders"); }
    }

    [HttpPost("providers")]
    [HrAuthorize("manage_services")]
    public async Task<IActionResult> CreateProvider([FromBody] CreateServiceProviderRequest req)
    {
        if (string.IsNullOrWhiteSpace(req.Name))
            return BadRequest(new { error = "El nombre es requerido" });
        try
        {
            using var conn = GetConnection();
            await conn.OpenAsync();
            using var cmd = new MySqlCommand(@"
                INSERT INTO provider (name, business_id, active, created_at, updated_at)
                VALUES (@Name, @B, 1, NOW(), NOW());
                SELECT LAST_INSERT_ID();", conn);
            cmd.Parameters.AddWithValue("@Name", req.Name.Trim());
            cmd.Parameters.AddWithValue("@B",    req.BusinessId);
            var newId = Convert.ToInt32(await cmd.ExecuteScalarAsync());
            return Ok(new { id = newId, name = req.Name.Trim() });
        }
        catch (Exception ex) { return Err(ex, "CreateProvider"); }
    }

    // ================================================================
    // EMPLOYEES (selector para cost items)
    // ================================================================

    /// <summary>
    /// GET /api/services/employees?businessId=X
    /// Devuelve empleados activos del negocio para asignarlos como ítems de costo de mano de obra.
    /// </summary>
    [HttpGet("employees")]
    public async Task<IActionResult> GetEmployeesForCostItems([FromQuery] int businessId)
    {
        try
        {
            using var conn = GetConnection();
            await conn.OpenAsync();
            using var cmd = new MySqlCommand(@"
                SELECT e.id, e.first_name, e.last_name, e.contract_type, e.current_salary,
                       e.position_id, p.name AS position_name, p.schedule_type,
                       e.hourly_rate,
                       e.department_id, d.name AS department_name
                FROM hr_employee e
                LEFT JOIN hr_position   p ON p.id = e.position_id
                LEFT JOIN hr_department d ON d.id = e.department_id
                WHERE e.business_id=@B AND e.active=1 AND e.status='active'
                ORDER BY e.contract_type, e.first_name, e.last_name", conn);
            cmd.Parameters.AddWithValue("@B", businessId);
            using var r = await cmd.ExecuteReaderAsync();
            var list = new List<object>();
            while (await r.ReadAsync())
            {
                var contractType  = r.GetString("contract_type");
                var currentSalary = r.GetDecimal("current_salary");
                var storedRate    = IsNull(r, "hourly_rate") ? 0m : r.GetDecimal("hourly_rate");
                var scheduleType  = IsNull(r, "schedule_type") ? null : r.GetString("schedule_type");

                // Fórmula legal chilena (Art. 55 Código del Trabajo):
                // valor hora = sueldo mensual ÷ (horas semanales × 4)
                // Para honorarios se usa la tarifa almacenada (cobran por hora, no sueldo mensual).
                decimal computedRate = storedRate;
                if (contractType != "honorarios" && currentSalary > 0)
                {
                    var divisor = scheduleType switch
                    {
                        "full_time"  => 168m, // 42 hrs × 4
                        "part_time"  => 84m,  // 21 hrs × 4
                        _            => 0m,
                    };
                    if (divisor > 0)
                        computedRate = Math.Round(currentSalary / divisor);
                }

                list.Add(new
                {
                    id             = r.GetInt32("id"),
                    firstName      = r.GetString("first_name"),
                    lastName       = r.GetString("last_name"),
                    fullName       = $"{r.GetString("first_name")} {r.GetString("last_name")}",
                    contractType,
                    currentSalary,
                    positionId     = IsNull(r, "position_id")   ? (int?)null : r.GetInt32("position_id"),
                    positionName   = IsNull(r, "position_name") ? null : r.GetString("position_name"),
                    hourlyRate     = computedRate,
                    departmentId   = IsNull(r, "department_id")   ? (int?)null : r.GetInt32("department_id"),
                    departmentName = IsNull(r, "department_name") ? null : r.GetString("department_name"),
                });
            }
            return Ok(list);
        }
        catch (Exception ex) { return Err(ex, "GetEmployeesForCostItems"); }
    }

    // ================================================================
    // COST ITEMS
    // ================================================================

    [HttpPost("{serviceId}/cost-items")]
    [HrAuthorize("manage_services", false)]
    public async Task<IActionResult> AddCostItem(int serviceId, [FromBody] CostItemRequest req)
    {
        try
        {
            using var conn = GetConnection();
            await conn.OpenAsync();
            using var cmd = new MySqlCommand(@"
                INSERT INTO service_cost_item
                    (service_id,name,description,cost_type,amount,quantity,unit,is_externalized,provider_id,provider_name,receipt_type_id,sort_order,employee_id,employee_name,linked_service_id,linked_service_name)
                VALUES (@Svc,@Name,@Desc,@Type,@Amt,@Qty,@Unit,@Ext,@ProvId,@Prov,@RecType,@Ord,@EmpId,@EmpName,@LnkId,@LnkName);
                SELECT LAST_INSERT_ID();", conn);
            cmd.Parameters.AddWithValue("@Svc",     serviceId);
            cmd.Parameters.AddWithValue("@Name",    req.Name);
            cmd.Parameters.AddWithValue("@Desc",    (object?)req.Description  ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@Type",    req.CostType ?? "other");
            cmd.Parameters.AddWithValue("@Amt",     req.Amount);
            cmd.Parameters.AddWithValue("@Qty",     req.Quantity ?? 1m);
            cmd.Parameters.AddWithValue("@Unit",    (object?)req.Unit         ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@Ext",     req.IsExternalized ? 1 : 0);
            cmd.Parameters.AddWithValue("@ProvId",  (object?)req.ProviderId   ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@Prov",    (object?)req.ProviderName ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@RecType", (object?)req.ReceiptTypeId ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@Ord",     req.SortOrder ?? 0);
            cmd.Parameters.AddWithValue("@EmpId",   (object?)req.EmployeeId   ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@EmpName", (object?)req.EmployeeName ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@LnkId",   (object?)req.LinkedServiceId   ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@LnkName", (object?)req.LinkedServiceName ?? DBNull.Value);
            return Ok(new { id = Convert.ToInt32(await cmd.ExecuteScalarAsync()) });
        }
        catch (Exception ex) { return Err(ex, "AddCostItem"); }
    }

    [HttpPut("{serviceId}/cost-items/{itemId}")]
    [HrAuthorize("manage_services", false)]
    public async Task<IActionResult> UpdateCostItem(int serviceId, int itemId, [FromBody] CostItemRequest req)
    {
        try
        {
            using var conn = GetConnection();
            await conn.OpenAsync();
            using var cmd = new MySqlCommand(@"
                UPDATE service_cost_item
                SET name=@Name,description=@Desc,cost_type=@Type,amount=@Amt,
                    quantity=@Qty,unit=@Unit,is_externalized=@Ext,
                    provider_id=@ProvId,provider_name=@Prov,receipt_type_id=@RecType,
                    sort_order=@Ord,employee_id=@EmpId,employee_name=@EmpName,
                    linked_service_id=@LnkId,linked_service_name=@LnkName
                WHERE id=@ItemId AND service_id=@Svc", conn);
            cmd.Parameters.AddWithValue("@ItemId",  itemId);
            cmd.Parameters.AddWithValue("@Svc",     serviceId);
            cmd.Parameters.AddWithValue("@Name",    req.Name);
            cmd.Parameters.AddWithValue("@Desc",    (object?)req.Description   ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@Type",    req.CostType ?? "other");
            cmd.Parameters.AddWithValue("@Amt",     req.Amount);
            cmd.Parameters.AddWithValue("@Qty",     req.Quantity ?? 1m);
            cmd.Parameters.AddWithValue("@Unit",    (object?)req.Unit          ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@Ext",     req.IsExternalized ? 1 : 0);
            cmd.Parameters.AddWithValue("@ProvId",  (object?)req.ProviderId    ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@Prov",    (object?)req.ProviderName  ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@RecType", (object?)req.ReceiptTypeId ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@Ord",     req.SortOrder ?? 0);
            cmd.Parameters.AddWithValue("@EmpId",   (object?)req.EmployeeId    ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@EmpName", (object?)req.EmployeeName  ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@LnkId",   (object?)req.LinkedServiceId   ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@LnkName", (object?)req.LinkedServiceName ?? DBNull.Value);
            await cmd.ExecuteNonQueryAsync();
            return Ok(new { message = "Ítem actualizado" });
        }
        catch (Exception ex) { return Err(ex, "UpdateCostItem"); }
    }

    [HttpDelete("{serviceId}/cost-items/{itemId}")]
    [HrAuthorize("manage_services", false)]
    public async Task<IActionResult> DeleteCostItem(int serviceId, int itemId)
    {
        try
        {
            using var conn = GetConnection();
            await conn.OpenAsync();
            using var cmd = new MySqlCommand(
                "DELETE FROM service_cost_item WHERE id=@ItemId AND service_id=@Svc", conn);
            cmd.Parameters.AddWithValue("@ItemId", itemId);
            cmd.Parameters.AddWithValue("@Svc",    serviceId);
            await cmd.ExecuteNonQueryAsync();
            return Ok(new { message = "Ítem eliminado" });
        }
        catch (Exception ex) { return Err(ex, "DeleteCostItem"); }
    }

    // ================================================================
    // SUB-SERVICIOS
    // ================================================================

    [HttpPost("{serviceId}/sub-services")]
    [HrAuthorize("manage_services", false)]
    public async Task<IActionResult> AddSubService(int serviceId, [FromBody] SubServiceRequest req)
    {
        if (req.ChildServiceId == serviceId)
            return BadRequest(new { error = "Un servicio no puede ser sub-servicio de sí mismo" });
        try
        {
            using var conn = GetConnection();
            await conn.OpenAsync();
            using var cmd = new MySqlCommand(@"
                INSERT INTO service_sub_service
                    (parent_service_id,child_service_id,quantity,additional_cost,notes,sort_order)
                VALUES (@Parent,@Child,@Qty,@Extra,@Notes,@Ord)
                ON DUPLICATE KEY UPDATE quantity=VALUES(quantity),additional_cost=VALUES(additional_cost),
                    notes=VALUES(notes),sort_order=VALUES(sort_order);
                SELECT LAST_INSERT_ID();", conn);
            cmd.Parameters.AddWithValue("@Parent", serviceId);
            cmd.Parameters.AddWithValue("@Child",  req.ChildServiceId);
            cmd.Parameters.AddWithValue("@Qty",    req.Quantity ?? 1m);
            cmd.Parameters.AddWithValue("@Extra",  req.AdditionalCost ?? 0m);
            cmd.Parameters.AddWithValue("@Notes",  (object?)req.Notes ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@Ord",    req.SortOrder ?? 0);
            return Ok(new { id = Convert.ToInt32(await cmd.ExecuteScalarAsync()) });
        }
        catch (Exception ex) { return Err(ex, "AddSubService"); }
    }

    [HttpDelete("{serviceId}/sub-services/{linkId}")]
    [HrAuthorize("manage_services", false)]
    public async Task<IActionResult> RemoveSubService(int serviceId, int linkId)
    {
        try
        {
            using var conn = GetConnection();
            await conn.OpenAsync();
            using var cmd = new MySqlCommand(
                "DELETE FROM service_sub_service WHERE id=@LinkId AND parent_service_id=@Svc", conn);
            cmd.Parameters.AddWithValue("@LinkId", linkId);
            cmd.Parameters.AddWithValue("@Svc",    serviceId);
            await cmd.ExecuteNonQueryAsync();
            return Ok(new { message = "Sub-servicio desvinculado" });
        }
        catch (Exception ex) { return Err(ex, "RemoveSubService"); }
    }

    // ================================================================
    // VENTAS  (service_sale + service_sale_item)
    // ================================================================

    /// <summary>
    /// GET /api/services/users/search?businessId=X&amp;q=Y
    /// Busca usuarios registrados en Gran Paso que pertenecen al negocio.
    /// </summary>
    [HttpGet("users/search")]
    public async Task<IActionResult> SearchUsers([FromQuery] int businessId, [FromQuery] string? q = null)
    {
        try
        {
            using var conn = GetConnection();
            await conn.OpenAsync();
            using var cmd = new MySqlCommand(@"
                SELECT u.id, u.name, u.lastname,
                       CONCAT(u.name,' ',u.lastname) AS full_name,
                       u.mail AS email, CAST(u.phone AS CHAR) AS phone
                FROM user u
                INNER JOIN user_has_business ub ON ub.id_user=u.id AND ub.id_business=@B
                WHERE u.active=1
                  AND (@Q IS NULL
                       OR u.name    LIKE @Q
                       OR u.lastname LIKE @Q
                       OR u.mail    LIKE @Q
                       OR u.phone   LIKE @Q)
                ORDER BY u.name, u.lastname
                LIMIT 20", conn);
            cmd.Parameters.AddWithValue("@B", businessId);
            var likeQ = string.IsNullOrWhiteSpace(q) ? (object)DBNull.Value : $"%{q.Trim()}%";
            cmd.Parameters.AddWithValue("@Q", likeQ);
            using var r = await cmd.ExecuteReaderAsync();
            var list = new List<object>();
            while (await r.ReadAsync())
                list.Add(new
                {
                    id       = r.GetInt32("id"),
                    name     = r.GetString("full_name"),
                    email    = IsNull(r, "email") ? "" : r.GetString("email"),
                    phone    = IsNull(r, "phone") ? "" : r.GetString("phone"),
                });
            return Ok(list);
        }
        catch (Exception ex) { return Err(ex, "SearchUsers"); }
    }

    [HttpGet("sales")]
    public async Task<IActionResult> GetSales(
        [FromQuery] int businessId,
        [FromQuery] string? status = null,
        [FromQuery] int? storeId = null,
        [FromQuery] DateTime? startDate = null,
        [FromQuery] DateTime? endDate = null,
        [FromQuery] DateTime? scheduledStart = null,
        [FromQuery] DateTime? scheduledEnd = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50)
    {
        try
        {
            using var conn = GetConnection();
            await conn.OpenAsync();
            using var cmd = new MySqlCommand(@"
                SELECT ss.id, ss.business_id, ss.store_id, ss.total_amount, ss.status,
                       ss.document_type, ss.payment_type, ss.installments_count, ss.payment_start_date,
                       ss.payment_method_id, pm.name AS payment_method_name,
                       ss.user_id,
                       CONCAT(u.name,' ',u.lastname) AS user_full_name,
                       u.mail AS user_email, CAST(u.phone AS CHAR) AS user_phone,
                       ss.client_name, ss.client_rut, ss.client_email, ss.client_phone,
                       ss.date, ss.scheduled_date, ss.completed_date, ss.notes,
                       ss.created_at, ss.updated_at,
                       st.name AS store_name,
                       (SELECT COUNT(*) FROM service_sale_item si WHERE si.sale_id=ss.id) AS items_count,
                       (SELECT GROUP_CONCAT(svc.name ORDER BY si.id SEPARATOR ', ')
                          FROM service_sale_item si
                          INNER JOIN service svc ON si.service_id=svc.id
                          WHERE si.sale_id=ss.id)                                         AS services_summary,
                       (SELECT COALESCE(SUM(ci.amount * ci.quantity), 0)
                          FROM service_sale_item si2
                          INNER JOIN service_cost_item ci ON ci.service_id=si2.service_id
                          WHERE si2.sale_id=ss.id)                                        AS total_cost
                FROM service_sale ss
                LEFT JOIN store st ON ss.store_id=st.id
                LEFT JOIN user u ON ss.user_id=u.id
                LEFT JOIN payment_methods pm ON ss.payment_method_id = pm.id
                WHERE ss.business_id=@B
                  AND (@Status     IS NULL OR ss.status         =@Status)
                  AND (@StoreId    IS NULL OR ss.store_id       =@StoreId)
                  AND (@Start      IS NULL OR ss.date           >=@Start)
                  AND (@End        IS NULL OR ss.date           <=@End)
                  AND (@SchedStart IS NULL OR ss.scheduled_date >=@SchedStart)
                  AND (@SchedEnd   IS NULL OR ss.scheduled_date <=@SchedEnd)
                ORDER BY ss.date DESC, ss.id DESC
                LIMIT @Size OFFSET @Offset", conn);
            cmd.Parameters.AddWithValue("@B",          businessId);
            cmd.Parameters.AddWithValue("@Status",      (object?)status       ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@StoreId",     (object?)storeId      ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@Start",       (object?)startDate    ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@End",         (object?)endDate      ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@SchedStart",  (object?)scheduledStart ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@SchedEnd",    (object?)scheduledEnd   ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@Size",        pageSize);
            cmd.Parameters.AddWithValue("@Offset",      (page - 1) * pageSize);
            using var r = await cmd.ExecuteReaderAsync();
            var list = new List<object>();
            while (await r.ReadAsync())
                list.Add(new
                {
                    id              = r.GetInt32("id"),
                    businessId      = r.GetInt32("business_id"),
                    storeId         = IsNull(r, "store_id")         ? (int?)null    : r.GetInt32("store_id"),
                    storeName       = IsNull(r, "store_name")        ? ""            : r.GetString("store_name"),
                    totalAmount     = r.GetDecimal("total_amount"),
                    status           = r.GetString("status"),
                    documentType     = IsNull(r, "document_type") ? "none" : r.GetString("document_type"),
                    paymentType      = r.GetInt32("payment_type"),
                    installmentsCount = r.GetInt32("installments_count"),
                    paymentStartDate  = IsNull(r, "payment_start_date") ? (string?)null : r.GetDateTime("payment_start_date").ToString("yyyy-MM-dd"),
                    paymentMethodId   = IsNull(r, "payment_method_id")   ? (int?)null   : r.GetInt32("payment_method_id"),
                    paymentMethodName = IsNull(r, "payment_method_name")  ? ""           : r.GetString("payment_method_name"),
                    userId           = IsNull(r, "user_id")           ? (int?)null    : r.GetInt32("user_id"),
                    userName        = IsNull(r, "user_full_name")     ? ""            : r.GetString("user_full_name"),
                    userEmail       = IsNull(r, "user_email")         ? ""            : r.GetString("user_email"),
                    userPhone       = IsNull(r, "user_phone")         ? ""            : r.GetString("user_phone"),
                    clientName      = IsNull(r, "client_name")       ? ""            : r.GetString("client_name"),
                    clientRut       = IsNull(r, "client_rut")        ? ""            : r.GetString("client_rut"),
                    clientEmail     = IsNull(r, "client_email")      ? ""            : r.GetString("client_email"),
                    clientPhone     = IsNull(r, "client_phone")      ? ""            : r.GetString("client_phone"),
                    date            = r.GetDateTime("date").ToString("yyyy-MM-dd"),
                    scheduledDate   = IsNull(r, "scheduled_date")    ? (string?)null : r.GetDateTime("scheduled_date").ToString("yyyy-MM-dd HH:mm:ss"),
                    completedDate   = IsNull(r, "completed_date")    ? (string?)null : r.GetDateTime("completed_date").ToString("yyyy-MM-dd HH:mm:ss"),
                    notes           = IsNull(r, "notes")              ? ""            : r.GetString("notes"),
                    itemsCount      = r.GetInt32("items_count"),
                    servicesSummary = IsNull(r, "services_summary")   ? ""            : r.GetString("services_summary"),
                    totalCost       = r.GetDecimal("total_cost"),
                    createdAt       = r.GetDateTime("created_at").ToString("yyyy-MM-dd HH:mm:ss"),
                    updatedAt       = r.GetDateTime("updated_at").ToString("yyyy-MM-dd HH:mm:ss"),
                });
            return Ok(list);
        }
        catch (Exception ex) { return Err(ex, "GetSales"); }
    }

    [HttpGet("sales/{id}")]
    public async Task<IActionResult> GetSaleById(int id)
    {
        try
        {
            using var conn = GetConnection();
            await conn.OpenAsync();

            using var hCmd = new MySqlCommand(@"
                SELECT ss.*,
                       st.name AS store_name,
                       CONCAT(u.name,' ',u.lastname) AS user_full_name,
                       u.mail AS user_email, CAST(u.phone AS CHAR) AS user_phone,
                       pm.name AS payment_method_name
                FROM service_sale ss
                LEFT JOIN store st ON ss.store_id=st.id
                LEFT JOIN user u ON ss.user_id=u.id
                LEFT JOIN payment_methods pm ON ss.payment_method_id = pm.id
                WHERE ss.id=@Id", conn);
            hCmd.Parameters.AddWithValue("@Id", id);
            using var hr = await hCmd.ExecuteReaderAsync();
            if (!await hr.ReadAsync()) return NotFound(new { error = "Venta no encontrada" });
            var sale = new
            {
                id            = hr.GetInt32("id"),
                businessId    = hr.GetInt32("business_id"),
                storeId       = IsNull(hr, "store_id")       ? (int?)null    : hr.GetInt32("store_id"),
                storeName     = IsNull(hr, "store_name")      ? ""            : hr.GetString("store_name"),
                totalAmount   = hr.GetDecimal("total_amount"),
                status        = hr.GetString("status"),
                documentType  = IsNull(hr, "document_type") ? "none" : hr.GetString("document_type"),
                paymentType   = hr.GetInt32("payment_type"),
                installmentsCount = hr.GetInt32("installments_count"),
                paymentStartDate  = IsNull(hr, "payment_start_date") ? (string?)null : hr.GetDateTime("payment_start_date").ToString("yyyy-MM-dd"),
                paymentMethodId   = IsNull(hr, "payment_method_id")   ? (int?)null   : hr.GetInt32("payment_method_id"),
                paymentMethodName = IsNull(hr, "payment_method_name")  ? ""           : hr.GetString("payment_method_name"),
                userId        = IsNull(hr, "user_id")         ? (int?)null    : hr.GetInt32("user_id"),
                userName      = IsNull(hr, "user_full_name")   ? ""            : hr.GetString("user_full_name"),
                userEmail     = IsNull(hr, "user_email")       ? ""            : hr.GetString("user_email"),
                userPhone     = IsNull(hr, "user_phone")       ? ""            : hr.GetString("user_phone"),
                clientName    = IsNull(hr, "client_name")     ? ""            : hr.GetString("client_name"),
                clientRut     = IsNull(hr, "client_rut")      ? ""            : hr.GetString("client_rut"),
                clientEmail   = IsNull(hr, "client_email")    ? ""            : hr.GetString("client_email"),
                clientPhone   = IsNull(hr, "client_phone")    ? ""            : hr.GetString("client_phone"),
                date          = hr.GetDateTime("date").ToString("yyyy-MM-dd"),
                scheduledDate = IsNull(hr, "scheduled_date")  ? (string?)null : hr.GetDateTime("scheduled_date").ToString("yyyy-MM-dd HH:mm:ss"),
                completedDate = IsNull(hr, "completed_date")  ? (string?)null : hr.GetDateTime("completed_date").ToString("yyyy-MM-dd HH:mm:ss"),
                notes         = IsNull(hr, "notes")            ? ""            : hr.GetString("notes"),
                createdAt     = hr.GetDateTime("created_at").ToString("yyyy-MM-dd HH:mm:ss"),
                updatedAt     = hr.GetDateTime("updated_at").ToString("yyyy-MM-dd HH:mm:ss"),
            };
            await hr.CloseAsync();

            using var iCmd = new MySqlCommand(@"
                SELECT si.id, si.service_id, si.quantity, si.unit_price, si.subtotal, si.notes, si.is_completed,
                       svc.name AS service_name, svc.description AS service_desc,
                       (SELECT COUNT(*) FROM service_cost_item ci WHERE ci.service_id=si.service_id) AS cost_items_count
                FROM service_sale_item si
                INNER JOIN service svc ON si.service_id=svc.id
                WHERE si.sale_id=@Id ORDER BY si.id", conn);
            iCmd.Parameters.AddWithValue("@Id", id);
            using var ir = await iCmd.ExecuteReaderAsync();
            var items = new List<object>();
            while (await ir.ReadAsync())
                items.Add(new
                {
                    id                 = ir.GetInt32("id"),
                    serviceId          = ir.GetInt32("service_id"),
                    serviceName        = ir.GetString("service_name"),
                    serviceDescription = IsNull(ir, "service_desc") ? "" : ir.GetString("service_desc"),
                    quantity           = ir.GetDecimal("quantity"),
                    unitPrice          = ir.GetDecimal("unit_price"),
                    subtotal           = ir.GetDecimal("subtotal"),
                    notes              = IsNull(ir, "notes") ? "" : ir.GetString("notes"),
                    isCompleted        = ir.GetBoolean("is_completed"),
                    costItemsCount     = ir.GetInt32("cost_items_count"),
                });
            return Ok(new { sale, items });
        }
        catch (Exception ex) { return Err(ex, "GetSaleById"); }
    }

    /// <summary>
    /// POST /api/services/sales
    /// Body: { businessId, storeId?, clientName?, clientRut?, clientEmail?, clientPhone?,
    ///         status, date, scheduledDate?, notes?, createdBy?,
    ///         items: [{ serviceId, unitPrice, quantity?, notes? }] }
    ///
    /// Al crear con status="completed", genera automáticamente expenses en gp-expenses
    /// (un expense por service_cost_item × servicio en la orden).
    /// </summary>
    [HttpPost("sales")]
    [HrAuthorize("manage_sales", false)]
    public async Task<IActionResult> CreateSale([FromBody] SaleRequest req)
    {
        if (req.Items == null || req.Items.Count == 0)
            return BadRequest(new { error = "La venta debe contener al menos un servicio" });

        using var conn = GetConnection();
        await conn.OpenAsync();
        using var tx = await conn.BeginTransactionAsync();
        try
        {
            var total = req.Items.Sum(i => i.UnitPrice * (i.Quantity ?? 1m));

            using var headCmd = new MySqlCommand(@"
                INSERT INTO service_sale
                    (business_id,store_id,user_id,client_name,client_rut,client_email,client_phone,
                     total_amount,status,date,scheduled_date,notes,created_by,
                     document_type,payment_type,installments_count,payment_start_date,payment_method_id)
                VALUES (@B,@Str,@UserId,@CName,@CRut,@CEmail,@CPhone,
                        @Total,@Status,@Date,@Sched,@Notes,@CreatedBy,
                        @DocType,@PayType,@Installments,@PayStart,@PMId);
                SELECT LAST_INSERT_ID();", conn, tx);
            headCmd.Parameters.AddWithValue("@B",          req.BusinessId);
            headCmd.Parameters.AddWithValue("@Str",        (object?)req.StoreId       ?? DBNull.Value);
            headCmd.Parameters.AddWithValue("@UserId",     (object?)req.UserId        ?? DBNull.Value);
            headCmd.Parameters.AddWithValue("@CName",      (object?)req.ClientName    ?? DBNull.Value);
            headCmd.Parameters.AddWithValue("@CRut",       (object?)req.ClientRut     ?? DBNull.Value);
            headCmd.Parameters.AddWithValue("@CEmail",     (object?)req.ClientEmail   ?? DBNull.Value);
            headCmd.Parameters.AddWithValue("@CPhone",     (object?)req.ClientPhone   ?? DBNull.Value);
            headCmd.Parameters.AddWithValue("@Total",      total);
            headCmd.Parameters.AddWithValue("@Status",     req.Status ?? "pending");
            headCmd.Parameters.AddWithValue("@Date",       req.Date.ToString("yyyy-MM-dd"));
            headCmd.Parameters.AddWithValue("@Sched",      (object?)req.ScheduledDate ?? DBNull.Value);
            headCmd.Parameters.AddWithValue("@Notes",      (object?)req.Notes         ?? DBNull.Value);
            headCmd.Parameters.AddWithValue("@CreatedBy",  (object?)req.CreatedBy     ?? DBNull.Value);
            headCmd.Parameters.AddWithValue("@DocType",    req.DocumentType ?? "none");
            headCmd.Parameters.AddWithValue("@PayType",    req.PaymentType      ?? 1);
            headCmd.Parameters.AddWithValue("@Installments", req.InstallmentsCount ?? 1);
            headCmd.Parameters.AddWithValue("@PayStart",   (object?)req.PaymentStartDate ?? DBNull.Value);
            headCmd.Parameters.AddWithValue("@PMId",       (object?)req.PaymentMethodId  ?? DBNull.Value);
            var saleId = Convert.ToInt32(await headCmd.ExecuteScalarAsync());

            foreach (var item in req.Items)
            {
                var qty = item.Quantity ?? 1m;
                using var itemCmd = new MySqlCommand(@"
                    INSERT INTO service_sale_item (sale_id,service_id,quantity,unit_price,subtotal,notes)
                    VALUES (@Sale,@Svc,@Qty,@Price,@Sub,@Notes)", conn, tx);
                itemCmd.Parameters.AddWithValue("@Sale",  saleId);
                itemCmd.Parameters.AddWithValue("@Svc",   item.ServiceId);
                itemCmd.Parameters.AddWithValue("@Qty",   qty);
                itemCmd.Parameters.AddWithValue("@Price", item.UnitPrice);
                itemCmd.Parameters.AddWithValue("@Sub",   item.UnitPrice * qty);
                itemCmd.Parameters.AddWithValue("@Notes", (object?)item.Notes ?? DBNull.Value);
                await itemCmd.ExecuteNonQueryAsync();
            }

            if (string.Equals(req.Status, "completed", StringComparison.OrdinalIgnoreCase))
            {
                var payType  = req.PaymentType      ?? 1;
                var payCount = req.InstallmentsCount ?? 1;
                var payStart = req.PaymentStartDate;
                foreach (var svcId in req.Items.Select(i => i.ServiceId).Distinct())
                    await CreateExpensesForService(conn, tx, saleId, svcId, req.BusinessId, req.StoreId, req.Date,
                                                   payType, payCount, payStart);
            }

            await tx.CommitAsync();
            return Ok(new { id = saleId, totalAmount = total });
        }
        catch (Exception ex)
        {
            await tx.RollbackAsync();
            return Err(ex, "CreateSale");
        }
    }

    [HttpPut("sales/{id}")]
    [HrAuthorize("manage_sales", false)]
    public async Task<IActionResult> UpdateSale(int id, [FromBody] UpdateSaleRequest req)
    {
        using var conn = GetConnection();
        await conn.OpenAsync();
        using var tx = await conn.BeginTransactionAsync();
        try
        {
            using var prevCmd = new MySqlCommand(
                "SELECT status,business_id,store_id,date,document_type,payment_type,installments_count,payment_start_date FROM service_sale WHERE id=@Id", conn, tx);
            prevCmd.Parameters.AddWithValue("@Id", id);
            using var pr = await prevCmd.ExecuteReaderAsync();
            if (!await pr.ReadAsync())
            {
                await pr.CloseAsync(); await tx.RollbackAsync();
                return NotFound(new { error = "Venta no encontrada" });
            }
            var prevStatus = pr.GetString("status");
            var bizId      = pr.GetInt32("business_id");
            var stId       = pr.IsDBNull(pr.GetOrdinal("store_id")) ? (int?)null : pr.GetInt32("store_id");
            var saleDate   = pr.GetDateTime("date");
            var prevDocType= pr.IsDBNull(pr.GetOrdinal("document_type"))       ? "none"        : pr.GetString("document_type");
            var payType    = pr.IsDBNull(pr.GetOrdinal("payment_type"))        ? 1            : pr.GetInt32("payment_type");
            var payCount   = pr.IsDBNull(pr.GetOrdinal("installments_count"))  ? 1            : pr.GetInt32("installments_count");
            var payStart   = pr.IsDBNull(pr.GetOrdinal("payment_start_date"))  ? (DateTime?)null : pr.GetDateTime("payment_start_date");
            await pr.CloseAsync();

            var sets = new List<string> { "updated_at=NOW()" };
            using var upd = new MySqlCommand("", conn, tx);
            upd.Parameters.AddWithValue("@Id", id);
            if (req.Status        != null) { sets.Add("status=@St");              upd.Parameters.AddWithValue("@St",  req.Status);
                                             if (req.Status == "completed") sets.Add("completed_date=NOW()"); }
            if (req.DocumentType  != null) { sets.Add("document_type=@DT");       upd.Parameters.AddWithValue("@DT",    req.DocumentType); }
            if (req.UserId.HasValue)       { sets.Add("user_id=@UID");            upd.Parameters.AddWithValue("@UID",   req.UserId.Value == 0 ? (object)DBNull.Value : req.UserId.Value); }
            if (req.ClientName    != null) { sets.Add("client_name=@CN");         upd.Parameters.AddWithValue("@CN",    req.ClientName); }
            if (req.ClientRut     != null) { sets.Add("client_rut=@CR");          upd.Parameters.AddWithValue("@CR",    req.ClientRut); }
            if (req.ClientEmail   != null) { sets.Add("client_email=@CE");        upd.Parameters.AddWithValue("@CE",    req.ClientEmail); }
            if (req.ClientPhone   != null) { sets.Add("client_phone=@CP");        upd.Parameters.AddWithValue("@CP",    req.ClientPhone); }
            if (req.Notes         != null) { sets.Add("notes=@Notes");            upd.Parameters.AddWithValue("@Notes", req.Notes); }
            if (req.ScheduledDate.HasValue){ sets.Add("scheduled_date=@Sd");      upd.Parameters.AddWithValue("@Sd",    req.ScheduledDate.Value); }
            if (req.PaymentType.HasValue)  { sets.Add("payment_type=@PT");        upd.Parameters.AddWithValue("@PT",    req.PaymentType.Value);        payType  = req.PaymentType.Value; }
            if (req.InstallmentsCount.HasValue) { sets.Add("installments_count=@IC"); upd.Parameters.AddWithValue("@IC", req.InstallmentsCount.Value); payCount = req.InstallmentsCount.Value; }
            if (req.PaymentStartDate.HasValue)  { sets.Add("payment_start_date=@PS"); upd.Parameters.AddWithValue("@PS", req.PaymentStartDate.Value);  payStart = req.PaymentStartDate.Value; }
            if (req.PaymentMethodId.HasValue)   { sets.Add("payment_method_id=@PMId"); upd.Parameters.AddWithValue("@PMId", req.PaymentMethodId.Value == 0 ? (object)DBNull.Value : req.PaymentMethodId.Value); }

            upd.CommandText = $"UPDATE service_sale SET {string.Join(",", sets)} WHERE id=@Id";
            await upd.ExecuteNonQueryAsync();

            bool nowDone = string.Equals(req.Status, "completed", StringComparison.OrdinalIgnoreCase);
            bool wasDone = string.Equals(prevStatus, "completed",  StringComparison.OrdinalIgnoreCase);
            if (nowDone && !wasDone)
            {
                using var chk = new MySqlCommand("SELECT COUNT(*) FROM expenses WHERE service_sale_id=@Id", conn, tx);
                chk.Parameters.AddWithValue("@Id", id);
                if (Convert.ToInt32(await chk.ExecuteScalarAsync()) == 0)
                {
                    using var siCmd = new MySqlCommand(
                        "SELECT DISTINCT service_id FROM service_sale_item WHERE sale_id=@Id", conn, tx);
                    siCmd.Parameters.AddWithValue("@Id", id);
                    using var siR = await siCmd.ExecuteReaderAsync();
                    var svcIds = new List<int>();
                    while (await siR.ReadAsync()) svcIds.Add(siR.GetInt32("service_id"));
                    await siR.CloseAsync();

                    foreach (var svcId in svcIds)
                        await CreateExpensesForService(conn, tx, id, svcId, bizId, stId, saleDate,
                                                       payType, payCount, payStart);
                }
            }

            await tx.CommitAsync();
            return Ok(new { message = "Venta actualizada" });
        }
        catch (Exception ex)
        {
            await tx.RollbackAsync();
            return Err(ex, "UpdateSale");
        }
    }

    [HttpDelete("sales/{id}")]
    [HrAuthorize("manage_sales", false)]
    public async Task<IActionResult> CancelSale(int id, [FromQuery] int businessId)
    {
        try
        {
            using var conn = GetConnection();
            await conn.OpenAsync();
            using var cmd = new MySqlCommand(
                "UPDATE service_sale SET status='cancelled',updated_at=NOW() WHERE id=@Id AND business_id=@B", conn);
            cmd.Parameters.AddWithValue("@Id", id);
            cmd.Parameters.AddWithValue("@B",  businessId);
            await cmd.ExecuteNonQueryAsync();
            return Ok(new { message = "Venta cancelada" });
        }
        catch (Exception ex) { return Err(ex, "CancelSale"); }
    }

    // ================================================================
    // ESTADÍSTICAS
    // ================================================================

    [HttpGet("stats")]
    public async Task<IActionResult> GetStats(
        [FromQuery] int businessId,
        [FromQuery] DateTime? startDate = null,
        [FromQuery] DateTime? endDate = null)
    {
        try
        {
            using var conn = GetConnection();
            await conn.OpenAsync();
            using var cmd = new MySqlCommand(@"
                SELECT
                    (SELECT COUNT(*) FROM service           WHERE business_id=@B AND active=1)                   AS total_services,
                    (SELECT COUNT(*) FROM service_category  WHERE business_id=@B AND active=1)                   AS total_categories,
                    (SELECT COUNT(*) FROM service_sale      WHERE business_id=@B
                        AND (@S IS NULL OR date>=@S) AND (@E IS NULL OR date<=@E))                               AS total_sales,
                    (SELECT COALESCE(SUM(total_amount),0)   FROM service_sale WHERE business_id=@B
                        AND status='completed'
                        AND (@S IS NULL OR date>=@S) AND (@E IS NULL OR date<=@E))                               AS revenue_completed,
                    (SELECT COUNT(*) FROM service_sale      WHERE business_id=@B AND status='pending'
                        AND (@S IS NULL OR date>=@S) AND (@E IS NULL OR date<=@E))                               AS pending_sales,
                    (SELECT COUNT(*) FROM service_sale      WHERE business_id=@B AND status='in_progress'
                        AND (@S IS NULL OR date>=@S) AND (@E IS NULL OR date<=@E))                               AS in_progress_sales", conn);
            cmd.Parameters.AddWithValue("@B", businessId);
            cmd.Parameters.AddWithValue("@S", (object?)startDate ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@E", (object?)endDate   ?? DBNull.Value);
            using var r = await cmd.ExecuteReaderAsync();
            if (!await r.ReadAsync()) return Ok(new { });
            return Ok(new
            {
                totalServices    = r.GetInt32("total_services"),
                totalCategories  = r.GetInt32("total_categories"),
                totalSales       = r.GetInt32("total_sales"),
                revenueCompleted = r.GetDecimal("revenue_completed"),
                pendingSales     = r.GetInt32("pending_sales"),
                inProgressSales  = r.GetInt32("in_progress_sales"),
            });
        }
        catch (Exception ex) { return Err(ex, "GetStats"); }
    }

    // ================================================================
    // LOW STOCK ALERTS
    // ================================================================

    [HttpGet("low-stock")]
    public async Task<IActionResult> GetLowStockSupplies([FromQuery] int businessId)
    {
        try
        {
            using var conn = GetConnection();
            await conn.OpenAsync();
            using var cmd = new MySqlCommand(@"
                SELECT s.id, s.name, s.minimum_stock,
                       um.symbol AS unit_symbol,
                       COALESCE(SUM(CASE WHEN se.active = 1 THEN se.amount ELSE 0 END), 0) AS current_stock
                FROM supplies s
                LEFT JOIN unit_measures um ON s.unit_measure_id = um.id
                LEFT JOIN supply_entry se ON s.id = se.supply_id
                WHERE s.business_id = @B
                  AND s.active = 1
                  AND s.minimum_stock IS NOT NULL
                  AND s.minimum_stock > 0
                GROUP BY s.id, s.name, s.minimum_stock, um.symbol
                HAVING current_stock <= minimum_stock
                ORDER BY (current_stock / NULLIF(minimum_stock, 0)) ASC, s.name
                LIMIT 10", conn);
            cmd.Parameters.AddWithValue("@B", businessId);
            using var r = await cmd.ExecuteReaderAsync();
            var list = new List<object>();
            while (await r.ReadAsync())
                list.Add(new
                {
                    id           = r.GetInt32("id"),
                    name         = r.GetString("name"),
                    currentStock = r.GetDecimal("current_stock"),
                    minimumStock = r.GetInt32("minimum_stock"),
                    unitSymbol   = IsNull(r, "unit_symbol") ? "" : r.GetString("unit_symbol"),
                });
            return Ok(list);
        }
        catch (Exception ex) { return Err(ex, "GetLowStockSupplies"); }
    }

    // ================================================================
    // EXPENSES DE UNA VENTA
    // ================================================================

    [HttpGet("sales/{saleId}/expenses")]
    public async Task<IActionResult> GetSaleExpenses(int saleId)
    {
        try
        {
            using var conn = GetConnection();
            await conn.OpenAsync();
            using var cmd = new MySqlCommand(@"
                SELECT e.id, e.description,
                       COALESCE(e.amount_total, e.amount) AS amount,
                       e.date, e.notes, e.is_paid,
                       e.provider_id, e.receipt_type_id, e.subcategory_id,
                       p.name AS provider_name,
                       s.name AS subcategory_name
                FROM expenses e
                LEFT JOIN provider p ON e.provider_id = p.id
                LEFT JOIN expense_subcategory s ON e.subcategory_id = s.id
                WHERE e.service_sale_id = @SaleId
                ORDER BY e.id", conn);
            cmd.Parameters.AddWithValue("@SaleId", saleId);
            using var r = await cmd.ExecuteReaderAsync();
            var list = new List<object>();
            while (await r.ReadAsync())
                list.Add(new
                {
                    id              = r.GetInt32("id"),
                    description     = r.GetString("description"),
                    amount          = r.GetDecimal("amount"),
                    date            = r.GetDateTime("date").ToString("yyyy-MM-dd"),
                    notes           = IsNull(r, "notes") ? "" : r.GetString("notes"),
                    isPaid          = r.GetBoolean("is_paid"),
                    providerId      = IsNull(r, "provider_id") ? (int?)null : r.GetInt32("provider_id"),
                    providerName    = IsNull(r, "provider_name") ? "" : r.GetString("provider_name"),
                    receiptTypeId   = IsNull(r, "receipt_type_id") ? (int?)null : r.GetInt32("receipt_type_id"),
                    subcategoryId   = IsNull(r, "subcategory_id") ? (int?)null : r.GetInt32("subcategory_id"),
                    subcategoryName = IsNull(r, "subcategory_name") ? "" : r.GetString("subcategory_name"),
                });
            return Ok(list);
        }
        catch (Exception ex) { return Err(ex, "GetSaleExpenses"); }
    }

    [HttpPatch("sales/{saleId}/expenses/{expenseId}/toggle-paid")]
    [HrAuthorize("manage_sales", false)]
    public async Task<IActionResult> ToggleExpensePaid(int saleId, int expenseId)
    {
        try
        {
            using var conn = GetConnection();
            await conn.OpenAsync();
            using var cmd = new MySqlCommand(@"
                UPDATE expenses
                SET is_paid = NOT is_paid
                WHERE id = @ExpId AND service_sale_id = @SaleId", conn);
            cmd.Parameters.AddWithValue("@ExpId", expenseId);
            cmd.Parameters.AddWithValue("@SaleId", saleId);
            var rows = await cmd.ExecuteNonQueryAsync();
            if (rows == 0) return NotFound(new { error = "Gasto no encontrado" });
            return Ok(new { message = "Estado actualizado" });
        }
        catch (Exception ex) { return Err(ex, "ToggleExpensePaid"); }
    }

    // GET api/services/expenses?businessId=X&status=paid|unpaid
    [HttpGet("expenses")]
    [HrAuthorize("view_sales", false)]
    public async Task<IActionResult> GetAllServiceExpenses([FromQuery] int businessId, [FromQuery] string? status = null)
    {
        try
        {
            using var conn = GetConnection();
            await conn.OpenAsync();
            var sql = @"
                SELECT e.id, e.description, COALESCE(e.amount_total, e.amount) AS amount,
                       e.date, e.notes, e.is_paid, e.service_sale_id,
                       e.provider_id, p.name AS provider_name,
                       e.receipt_type_id, e.subcategory_id, s.name AS subcategory_name,
                       ss.client_name, ss.status AS sale_status
                FROM expenses e
                LEFT JOIN provider p ON e.provider_id = p.id
                LEFT JOIN expense_subcategory s ON e.subcategory_id = s.id
                LEFT JOIN service_sale ss ON e.service_sale_id = ss.id
                WHERE e.business_id = @B AND e.service_sale_id IS NOT NULL";
            if (status == "paid")     sql += " AND e.is_paid = 1";
            else if (status == "unpaid") sql += " AND e.is_paid = 0";
            sql += " ORDER BY e.date DESC, e.id DESC";

            using var cmd = new MySqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@B", businessId);
            using var r = await cmd.ExecuteReaderAsync();
            var list = new List<object>();
            while (await r.ReadAsync())
                list.Add(new
                {
                    id               = r.GetInt32("id"),
                    description      = r.GetString("description"),
                    amount           = r.GetDecimal("amount"),
                    date             = r.GetDateTime("date").ToString("yyyy-MM-dd"),
                    notes            = IsNull(r, "notes") ? "" : r.GetString("notes"),
                    isPaid           = r.GetBoolean("is_paid"),
                    serviceSaleId    = IsNull(r, "service_sale_id") ? (int?)null : r.GetInt32("service_sale_id"),
                    providerId       = IsNull(r, "provider_id") ? (int?)null : r.GetInt32("provider_id"),
                    providerName     = IsNull(r, "provider_name") ? "" : r.GetString("provider_name"),
                    receiptTypeId    = IsNull(r, "receipt_type_id") ? (int?)null : r.GetInt32("receipt_type_id"),
                    subcategoryId    = IsNull(r, "subcategory_id") ? (int?)null : r.GetInt32("subcategory_id"),
                    subcategoryName  = IsNull(r, "subcategory_name") ? "" : r.GetString("subcategory_name"),
                    clientName       = IsNull(r, "client_name") ? "" : r.GetString("client_name"),
                    saleStatus       = IsNull(r, "sale_status") ? "" : r.GetString("sale_status"),
                });
            return Ok(list);
        }
        catch (Exception ex) { return Err(ex, "GetAllServiceExpenses"); }
    }

    // GET api/services/projected-expenses?businessId=X
    // Gastos proyectados basados en cost items de ventas activas (pending/in_progress)
    [HttpGet("projected-expenses")]
    [HrAuthorize("view_sales", false)]
    public async Task<IActionResult> GetProjectedServiceExpenses([FromQuery] int businessId)
    {
        try
        {
            using var conn = GetConnection();
            await conn.OpenAsync();
            using var cmd = new MySqlCommand(@"
                SELECT 
                    ci.id AS cost_item_id,
                    ci.name AS cost_item_name,
                    ci.description,
                    ci.cost_type,
                    ci.amount,
                    ci.quantity,
                    (ci.amount * ci.quantity) AS total_amount,
                    ci.employee_id,
                    ci.employee_name,
                    ci.provider_name,
                    s.id AS service_id,
                    s.name AS service_name,
                    ss.id AS sale_id,
                    ss.client_name,
                    ss.status AS sale_status,
                    ss.date AS sale_date,
                    ss.scheduled_date,
                    e.contract_type AS employee_contract_type
                FROM service_sale_item ssi
                INNER JOIN service_sale ss ON ssi.sale_id = ss.id
                INNER JOIN service s ON ssi.service_id = s.id
                INNER JOIN service_cost_item ci ON ci.service_id = s.id
                LEFT JOIN hr_employee e ON ci.employee_id = e.id
                WHERE ss.business_id = @B
                  AND ss.status IN ('pending', 'in_progress')
                  AND (
                    -- Solo incluir si es: proveedor externo o empleado de honorarios
                    (ci.employee_id IS NOT NULL AND e.contract_type = 'honorarios')
                    OR (ci.provider_name IS NOT NULL AND ci.provider_name != '')
                  )
                ORDER BY ss.scheduled_date, ss.date, ss.id, ci.sort_order", conn);
            cmd.Parameters.AddWithValue("@B", businessId);
            using var r = await cmd.ExecuteReaderAsync();
            var list = new List<object>();
            while (await r.ReadAsync())
                list.Add(new
                {
                    costItemId       = r.GetInt32("cost_item_id"),
                    costItemName     = r.GetString("cost_item_name"),
                    description      = IsNull(r, "description") ? "" : r.GetString("description"),
                    costType         = r.GetString("cost_type"),
                    amount           = r.GetDecimal("amount"),
                    quantity         = r.GetDecimal("quantity"),
                    totalAmount      = r.GetDecimal("total_amount"),
                    employeeId       = IsNull(r, "employee_id") ? (int?)null : r.GetInt32("employee_id"),
                    employeeName     = IsNull(r, "employee_name") ? "" : r.GetString("employee_name"),
                    providerName     = IsNull(r, "provider_name") ? "" : r.GetString("provider_name"),
                    serviceId        = r.GetInt32("service_id"),
                    serviceName      = r.GetString("service_name"),
                    saleId           = r.GetInt32("sale_id"),
                    clientName       = IsNull(r, "client_name") ? "" : r.GetString("client_name"),
                    saleStatus       = r.GetString("sale_status"),
                    saleDate         = r.GetDateTime("sale_date").ToString("yyyy-MM-dd"),
                    scheduledDate    = IsNull(r, "scheduled_date") ? null : r.GetDateTime("scheduled_date").ToString("yyyy-MM-dd"),
                    employeeContractType = IsNull(r, "employee_contract_type") ? "" : r.GetString("employee_contract_type"),
                });
            return Ok(list);
        }
        catch (Exception ex) { return Err(ex, "GetProjectedServiceExpenses"); }
    }

    [HttpPatch("sales/{saleId}/items/{itemId}/toggle-completed")]
    [HrAuthorize("manage_sales", false)]
    public async Task<IActionResult> ToggleSaleItemCompleted(int saleId, int itemId)
    {
        try
        {
            using var conn = GetConnection();
            await conn.OpenAsync();
            using var cmd = new MySqlCommand(@"
                UPDATE service_sale_item
                SET is_completed = NOT is_completed
                WHERE id = @ItemId AND sale_id = @SaleId", conn);
            cmd.Parameters.AddWithValue("@ItemId", itemId);
            cmd.Parameters.AddWithValue("@SaleId", saleId);
            var rows = await cmd.ExecuteNonQueryAsync();
            if (rows == 0) return NotFound(new { error = "Ítem no encontrado" });
            return Ok(new { message = "Estado actualizado" });
        }
        catch (Exception ex) { return Err(ex, "ToggleSaleItemCompleted"); }
    }

    /// <summary>
    /// POST /services/sales/{saleId}/items
    /// Agrega un ítem de servicio a una venta existente (solo en estado pending o in_progress).
    /// </summary>
    [HttpPost("sales/{saleId}/items")]
    [HrAuthorize("manage_sales", false)]
    public async Task<IActionResult> AddSaleItem(int saleId, [FromBody] SaleItemRequest req)
    {
        try
        {
            using var conn = GetConnection();
            await conn.OpenAsync();
            using var tx = await conn.BeginTransactionAsync();
            try
            {
                // Verificar que la venta existe y está en estado editable
                using var chkCmd = new MySqlCommand(
                    "SELECT status, total_amount FROM service_sale WHERE id=@SaleId", conn, tx);
                chkCmd.Parameters.AddWithValue("@SaleId", saleId);
                using var chkR = await chkCmd.ExecuteReaderAsync();
                if (!await chkR.ReadAsync())
                {
                    await chkR.CloseAsync(); await tx.RollbackAsync();
                    return NotFound(new { error = "Venta no encontrada" });
                }
                var status      = chkR.GetString("status");
                var prevTotal   = chkR.GetDecimal("total_amount");
                await chkR.CloseAsync();

                if (status != "pending" && status != "in_progress")
                {
                    await tx.RollbackAsync();
                    return BadRequest(new { error = "Solo se pueden editar ventas en estado pendiente o en progreso" });
                }

                var qty = req.Quantity ?? 1m;
                var subtotal = req.UnitPrice * qty;

                using var insCmd = new MySqlCommand(@"
                    INSERT INTO service_sale_item (sale_id,service_id,quantity,unit_price,subtotal,notes)
                    VALUES (@Sale,@Svc,@Qty,@Price,@Sub,@Notes);
                    SELECT LAST_INSERT_ID();", conn, tx);
                insCmd.Parameters.AddWithValue("@Sale",  saleId);
                insCmd.Parameters.AddWithValue("@Svc",   req.ServiceId);
                insCmd.Parameters.AddWithValue("@Qty",   qty);
                insCmd.Parameters.AddWithValue("@Price", req.UnitPrice);
                insCmd.Parameters.AddWithValue("@Sub",   subtotal);
                insCmd.Parameters.AddWithValue("@Notes", (object?)req.Notes ?? DBNull.Value);
                var newId = Convert.ToInt32(await insCmd.ExecuteScalarAsync());

                // Actualizar total_amount de la venta
                using var totCmd = new MySqlCommand(
                    "UPDATE service_sale SET total_amount=total_amount+@Sub, updated_at=NOW() WHERE id=@SaleId",
                    conn, tx);
                totCmd.Parameters.AddWithValue("@Sub", subtotal);
                totCmd.Parameters.AddWithValue("@SaleId", saleId);
                await totCmd.ExecuteNonQueryAsync();

                await tx.CommitAsync();
                return Ok(new { id = newId, message = "Ítem agregado" });
            }
            catch
            {
                await tx.RollbackAsync();
                throw;
            }
        }
        catch (Exception ex) { return Err(ex, "AddSaleItem"); }
    }

    /// <summary>
    /// DELETE /services/sales/{saleId}/items/{itemId}
    /// Elimina un ítem de servicio de una venta existente (solo en estado pending o in_progress).
    /// </summary>
    [HttpDelete("sales/{saleId}/items/{itemId}")]
    [HrAuthorize("manage_sales", false)]
    public async Task<IActionResult> DeleteSaleItem(int saleId, int itemId)
    {
        try
        {
            using var conn = GetConnection();
            await conn.OpenAsync();
            using var tx = await conn.BeginTransactionAsync();
            try
            {
                // Verificar estado y obtener subtotal del ítem
                using var chkCmd = new MySqlCommand(
                    "SELECT ss.status FROM service_sale ss WHERE ss.id=@SaleId", conn, tx);
                chkCmd.Parameters.AddWithValue("@SaleId", saleId);
                var status = (string?)await chkCmd.ExecuteScalarAsync();
                if (status == null)
                {
                    await tx.RollbackAsync();
                    return NotFound(new { error = "Venta no encontrada" });
                }
                if (status != "pending" && status != "in_progress")
                {
                    await tx.RollbackAsync();
                    return BadRequest(new { error = "Solo se pueden editar ventas en estado pendiente o en progreso" });
                }

                using var getCmd = new MySqlCommand(
                    "SELECT subtotal FROM service_sale_item WHERE id=@ItemId AND sale_id=@SaleId",
                    conn, tx);
                getCmd.Parameters.AddWithValue("@ItemId", itemId);
                getCmd.Parameters.AddWithValue("@SaleId", saleId);
                var subtotalObj = await getCmd.ExecuteScalarAsync();
                if (subtotalObj == null)
                {
                    await tx.RollbackAsync();
                    return NotFound(new { error = "Ítem no encontrado" });
                }
                var subtotal = Convert.ToDecimal(subtotalObj);

                using var delCmd = new MySqlCommand(
                    "DELETE FROM service_sale_item WHERE id=@ItemId AND sale_id=@SaleId",
                    conn, tx);
                delCmd.Parameters.AddWithValue("@ItemId", itemId);
                delCmd.Parameters.AddWithValue("@SaleId", saleId);
                await delCmd.ExecuteNonQueryAsync();

                // Actualizar total_amount de la venta
                using var totCmd = new MySqlCommand(
                    "UPDATE service_sale SET total_amount=GREATEST(0,total_amount-@Sub), updated_at=NOW() WHERE id=@SaleId",
                    conn, tx);
                totCmd.Parameters.AddWithValue("@Sub", subtotal);
                totCmd.Parameters.AddWithValue("@SaleId", saleId);
                await totCmd.ExecuteNonQueryAsync();

                await tx.CommitAsync();
                return Ok(new { message = "Ítem eliminado" });
            }
            catch
            {
                await tx.RollbackAsync();
                throw;
            }
        }
        catch (Exception ex) { return Err(ex, "DeleteSaleItem"); }
    }

    // ================================================================
    // Supplies en ventas
    // ================================================================

    /// <summary>
    /// GET /services/supplies?businessId={businessId}&categoryId={categoryId}
    /// Obtiene la lista de insumos (supplies) filtrados por negocio y opcionalmente por categoría.
    /// Para mostrar solo insumos de servicios, filtrar por una categoría específica.
    /// </summary>
    [HttpGet("supplies")]
    public async Task<IActionResult> GetSupplies([FromQuery] int businessId, [FromQuery] int? categoryId)
    {
        try
        {
            using var cn = GetConnection();
            await cn.OpenAsync();
            
            var sql = @"
                SELECT s.id, s.name, s.sku, s.description, s.unit_measure_id, 
                       s.supply_category_id, s.minimum_stock, s.active, s.fixed_expense_id,
                       um.name AS unit_measure_name, um.symbol AS unit_measure_symbol,
                       sc.name AS category_name,
                       fe.subcategory_id, fe.amount AS fixed_expense_amount,
                       es.name AS expense_subcategory_name,
                       ec.id AS expense_category_id, ec.name AS expense_category_name,
                       COALESCE(SUM(CASE WHEN se.active = 1 THEN se.amount ELSE 0 END), 0) AS current_stock
                FROM supplies s
                LEFT JOIN unit_measures um ON s.unit_measure_id = um.id
                LEFT JOIN supply_categories sc ON s.supply_category_id = sc.id
                LEFT JOIN supply_entry se ON s.id = se.supply_id
                LEFT JOIN fixed_expense fe ON s.fixed_expense_id = fe.id
                LEFT JOIN expense_subcategory es ON fe.subcategory_id = es.id
                LEFT JOIN expense_category ec ON es.expense_category_id = ec.id
                WHERE s.business_id = @BusinessId
                  AND s.active = 1";
            
            if (categoryId.HasValue)
                sql += " AND s.supply_category_id = @CategoryId";
            
            sql += " GROUP BY s.id ORDER BY s.name";
            
            using var cmd = new MySqlCommand(sql, cn);
            cmd.Parameters.AddWithValue("@BusinessId", businessId);
            if (categoryId.HasValue)
                cmd.Parameters.AddWithValue("@CategoryId", categoryId.Value);
            
            using var r = await cmd.ExecuteReaderAsync();
            var supplies = new List<object>();
            while (await r.ReadAsync())
            {
                supplies.Add(new
                {
                    id = r.GetInt32("id"),
                    name = r.GetString("name"),
                    sku = IsNull(r, "sku") ? null : r.GetString("sku"),
                    description = IsNull(r, "description") ? null : r.GetString("description"),
                    unitMeasureId = r.GetInt32("unit_measure_id"),
                    unitMeasureName = IsNull(r, "unit_measure_name") ? null : r.GetString("unit_measure_name"),
                    unitMeasureSymbol = IsNull(r, "unit_measure_symbol") ? null : r.GetString("unit_measure_symbol"),
                    supplyCategoryId = IsNull(r, "supply_category_id") ? (int?)null : r.GetInt32("supply_category_id"),
                    categoryName = IsNull(r, "category_name") ? null : r.GetString("category_name"),
                    minimumStock = IsNull(r, "minimum_stock") ? (int?)null : r.GetInt32("minimum_stock"),
                    currentStock = r.GetDecimal("current_stock"),
                    active = r.GetBoolean("active"),
                    fixedExpenseId = IsNull(r, "fixed_expense_id") ? (int?)null : r.GetInt32("fixed_expense_id"),
                    fixedExpenseAmount = IsNull(r, "fixed_expense_amount") ? (decimal?)null : r.GetDecimal("fixed_expense_amount"),
                    subcategoryId = IsNull(r, "subcategory_id") ? (int?)null : r.GetInt32("subcategory_id"),
                    expenseSubcategoryName = IsNull(r, "expense_subcategory_name") ? null : r.GetString("expense_subcategory_name"),
                    expenseCategoryId = IsNull(r, "expense_category_id") ? (int?)null : r.GetInt32("expense_category_id"),
                    expenseCategoryName = IsNull(r, "expense_category_name") ? null : r.GetString("expense_category_name")
                });
            }
            return Ok(supplies);
        }
        catch (Exception ex) { return Err(ex, "GetSupplies"); }
    }

    /// <summary>
    /// GET /services/sales/{saleId}/supplies
    /// Obtiene los insumos consumidos en una venta específica.
    /// </summary>
    [HttpGet("sales/{saleId}/supplies")]
    public async Task<IActionResult> GetSaleSupplies(int saleId)
    {
        try
        {
            using var cn = GetConnection();
            await cn.OpenAsync();
            using var cmd = new MySqlCommand(@"
                SELECT sss.id, sss.sale_id, sss.supply_id, sss.quantity, sss.unit_cost, sss.notes,
                       s.name AS supply_name, s.sku, 
                       um.name AS unit_measure_name, um.symbol AS unit_measure_symbol
                FROM service_sale_supply sss
                INNER JOIN supplies s ON sss.supply_id = s.id
                LEFT JOIN unit_measures um ON s.unit_measure_id = um.id
                WHERE sss.sale_id = @SaleId
                ORDER BY sss.id
            ", cn);
            cmd.Parameters.AddWithValue("@SaleId", saleId);
            using var r = await cmd.ExecuteReaderAsync();
            var supplies = new List<object>();
            while (await r.ReadAsync())
            {
                supplies.Add(new
                {
                    id = r.GetInt32("id"),
                    saleId = r.GetInt32("sale_id"),
                    supplyId = r.GetInt32("supply_id"),
                    supplyName = r.GetString("supply_name"),
                    sku = IsNull(r, "sku") ? null : r.GetString("sku"),
                    quantity = r.GetDecimal("quantity"),
                    unitCost = r.GetDecimal("unit_cost"),
                    unitMeasureName = IsNull(r, "unit_measure_name") ? null : r.GetString("unit_measure_name"),
                    unitMeasureSymbol = IsNull(r, "unit_measure_symbol") ? null : r.GetString("unit_measure_symbol"),
                    notes = IsNull(r, "notes") ? null : r.GetString("notes")
                });
            }
            return Ok(supplies);
        }
        catch (Exception ex) { return Err(ex, "GetSaleSupplies"); }
    }

    /// <summary>
    /// POST /services/sales/{saleId}/supplies
    /// Agrega un insumo consumido a una venta usando algoritmo FIFO.
    /// Descuenta stock automáticamente desde los lotes más antiguos.
    /// </summary>
    [HttpPost("sales/{saleId}/supplies")]
    [HrAuthorize("manage_sales", false)]
    public async Task<IActionResult> AddSupplyToSale(int saleId, [FromBody] AddSaleSupplyRequest req)
    {
        try
        {
            var quantity = req.Quantity ?? 1m;

            using var cn = GetConnection();
            await cn.OpenAsync();
            using var tx = await cn.BeginTransactionAsync();

            try
            {
                // 1. Obtener lotes disponibles en orden FIFO
                using var availCmd = new MySqlCommand(@"
                    SELECT parent.id, parent.unit_cost,
                        parent.amount + COALESCE(
                            (SELECT SUM(child.amount)
                             FROM supply_entry child
                             WHERE child.supply_entry_id = parent.id AND child.active = 1),
                            0
                        ) AS available_amount
                    FROM supply_entry parent
                    WHERE parent.supply_id = @SupplyId
                      AND parent.amount > 0
                      AND parent.active = 1
                    HAVING available_amount > 0
                    ORDER BY parent.created_at ASC
                ", cn, tx);
                availCmd.Parameters.AddWithValue("@SupplyId", req.SupplyId);

                var fifoEntries = new List<(int id, decimal unitCost, decimal availableAmount)>();
                using (var r = await availCmd.ExecuteReaderAsync())
                {
                    while (await r.ReadAsync())
                        fifoEntries.Add((r.GetInt32(0), r.GetDecimal(1), r.GetDecimal(2)));
                }

                if (!fifoEntries.Any())
                {
                    await tx.RollbackAsync();
                    return BadRequest(new { message = "No hay stock disponible para este insumo" });
                }

                var totalAvailable = fifoEntries.Sum(e => e.availableAmount);
                if (totalAvailable < quantity)
                {
                    await tx.RollbackAsync();
                    return BadRequest(new { message = $"Stock insuficiente. Disponible: {totalAvailable}, requerido: {quantity}" });
                }

                // 2. Consumir FIFO — crear supply_entry negativos
                var remainingQty = quantity;
                var totalCost = 0m;

                foreach (var entry in fifoEntries)
                {
                    if (remainingQty <= 0) break;

                    var consumeFromEntry = Math.Min(remainingQty, entry.availableAmount);

                    using var insertCmd = new MySqlCommand(@"
                        INSERT INTO supply_entry (unit_cost, amount, provider_id, supply_id, supply_entry_id, active, created_at, updated_at)
                        VALUES (@UnitCost, @Amount, 1, @SupplyId, @ParentEntryId, 1, UTC_TIMESTAMP(), UTC_TIMESTAMP());
                    ", cn, tx);
                    insertCmd.Parameters.AddWithValue("@UnitCost", entry.unitCost);
                    insertCmd.Parameters.AddWithValue("@Amount", -consumeFromEntry);
                    insertCmd.Parameters.AddWithValue("@SupplyId", req.SupplyId);
                    insertCmd.Parameters.AddWithValue("@ParentEntryId", entry.id);
                    await insertCmd.ExecuteNonQueryAsync();

                    totalCost += entry.unitCost * consumeFromEntry;

                    // Desactivar lote si se agotó completamente
                    if (entry.availableAmount - consumeFromEntry <= 0)
                    {
                        using var deactivateCmd = new MySqlCommand(
                            "UPDATE supply_entry SET active = 0, updated_at = UTC_TIMESTAMP() WHERE id = @Id",
                            cn, tx);
                        deactivateCmd.Parameters.AddWithValue("@Id", entry.id);
                        await deactivateCmd.ExecuteNonQueryAsync();
                    }

                    remainingQty -= consumeFromEntry;
                }

                var weightedAvgCost = totalCost / quantity;

                // 3. Registrar en service_sale_supply con costo FIFO ponderado
                using var saleCmd = new MySqlCommand(@"
                    INSERT INTO service_sale_supply (sale_id, supply_id, quantity, unit_cost, notes)
                    VALUES (@SaleId, @SupplyId, @Quantity, @UnitCost, @Notes);
                    SELECT LAST_INSERT_ID();
                ", cn, tx);
                saleCmd.Parameters.AddWithValue("@SaleId", saleId);
                saleCmd.Parameters.AddWithValue("@SupplyId", req.SupplyId);
                saleCmd.Parameters.AddWithValue("@Quantity", quantity);
                saleCmd.Parameters.AddWithValue("@UnitCost", weightedAvgCost);
                saleCmd.Parameters.AddWithValue("@Notes", req.Notes ?? (object)DBNull.Value);
                var newId = Convert.ToInt32(await saleCmd.ExecuteScalarAsync());

                await tx.CommitAsync();
                return Ok(new { id = newId, message = "Supply added to sale", unitCost = weightedAvgCost });
            }
            catch
            {
                await tx.RollbackAsync();
                throw;
            }
        }
        catch (Exception ex) { return Err(ex, "AddSupplyToSale"); }
    }

    /// <summary>
    /// DELETE /services/sales/{saleId}/supplies/{supplyRecordId}
    /// Elimina un registro de insumo consumido de una venta.
    /// </summary>
    [HttpDelete("sales/{saleId}/supplies/{supplyRecordId}")]
    [HrAuthorize("manage_sales", false)]
    public async Task<IActionResult> RemoveSupplyFromSale(int saleId, int supplyRecordId)
    {
        try
        {
            using var cn = GetConnection();
            await cn.OpenAsync();
            using var cmd = new MySqlCommand(@"
                DELETE FROM service_sale_supply
                WHERE id = @Id AND sale_id = @SaleId
            ", cn);
            cmd.Parameters.AddWithValue("@Id", supplyRecordId);
            cmd.Parameters.AddWithValue("@SaleId", saleId);
            await cmd.ExecuteNonQueryAsync();
            return Ok(new { message = "Supply removed from sale" });
        }
        catch (Exception ex) { return Err(ex, "RemoveSupplyFromSale"); }
    }

    // ================================================================
    // HELPER: generar expenses desde los cost items de UN servicio
    // expenses.service_sale_id vincula la orden con gp-expenses
    // ================================================================
    private async Task CreateExpensesForService(
        MySqlConnection conn, MySqlTransaction tx,
        int saleId, int serviceId, int businessId, int? storeId, DateTime saleDate,
        int paymentType = 1, int installmentsCount = 1, DateTime? paymentStartDate = null)
    {
        using var nameCmd = new MySqlCommand("SELECT name FROM service WHERE id=@Id", conn, tx);
        nameCmd.Parameters.AddWithValue("@Id", serviceId);
        var svcName = (string)(await nameCmd.ExecuteScalarAsync() ?? "Servicio");

        using var costCmd = new MySqlCommand(@"
            SELECT ci.name, ci.cost_type, ci.amount, ci.quantity, ci.description, 
                   ci.provider_id, ci.provider_name, ci.receipt_type_id,
                   ci.employee_id, e.contract_type AS employee_contract_type
            FROM service_cost_item ci
            LEFT JOIN hr_employee e ON e.id = ci.employee_id
            WHERE ci.service_id=@Id ORDER BY ci.sort_order, ci.id", conn, tx);
        costCmd.Parameters.AddWithValue("@Id", serviceId);
        using var cr = await costCmd.ExecuteReaderAsync();
        var items = new List<(string name, string type, decimal amt, decimal qty, string desc, int? provId, string prov, int? receiptType, int? empId, string empContract)>();
        while (await cr.ReadAsync())
            items.Add((
                cr.GetString("name"),
                cr.GetString("cost_type"),
                cr.GetDecimal("amount"),
                cr.GetDecimal("quantity"),
                cr.IsDBNull(cr.GetOrdinal("description"))    ? "" : cr.GetString("description"),
                cr.IsDBNull(cr.GetOrdinal("provider_id"))    ? (int?)null : cr.GetInt32("provider_id"),
                cr.IsDBNull(cr.GetOrdinal("provider_name"))  ? "" : cr.GetString("provider_name"),
                cr.IsDBNull(cr.GetOrdinal("receipt_type_id")) ? (int?)null : cr.GetInt32("receipt_type_id"),
                cr.IsDBNull(cr.GetOrdinal("employee_id"))    ? (int?)null : cr.GetInt32("employee_id"),
                cr.IsDBNull(cr.GetOrdinal("employee_contract_type")) ? "" : cr.GetString("employee_contract_type")
            ));
        await cr.CloseAsync();

        if (items.Count == 0)
        {
            using var gCmd = new MySqlCommand(@"
                INSERT INTO expenses (subcategory_id,amount,description,date,business_id,store_id,
                                      expense_type_id,service_sale_id,notes)
                VALUES (52,0,@Desc,@Date,@B,@Str,2,@Sale,@Notes);
                SELECT LAST_INSERT_ID();", conn, tx);
            gCmd.Parameters.AddWithValue("@Desc",  $"Servicio: {svcName} (orden #{saleId})");
            gCmd.Parameters.AddWithValue("@Date",  saleDate.ToString("yyyy-MM-dd"));
            gCmd.Parameters.AddWithValue("@B",     businessId);
            gCmd.Parameters.AddWithValue("@Str",   (object?)storeId ?? DBNull.Value);
            gCmd.Parameters.AddWithValue("@Sale",  saleId);
            gCmd.Parameters.AddWithValue("@Notes", $"Generado desde orden #{saleId}");
            var gExpId = Convert.ToInt32(await gCmd.ExecuteScalarAsync());
            if (paymentType == 2 && installmentsCount > 1)
                await CreatePaymentPlan(conn, tx, gExpId, 0m, installmentsCount, paymentStartDate ?? saleDate);
            return;
        }

        foreach (var (name, type, amt, qty, desc, provId, prov, receiptType, empId, empContract) in items)
        {
            // SOLO crear expense si es: proveedor externo, o empleado de honorarios
            // Empleados por contrato (indefinido/plazo_fijo) no generan gasto porque ya están en planilla
            var isContractEmployee = empId.HasValue && (empContract == "indefinido" || empContract == "plazo_fijo");
            if (isContractEmployee)
            {
                // No crear expense para empleados por contrato, solo para honorarios o externos
                continue;
            }

            var subcatId  = CostTypeToSubcategory.GetValueOrDefault(type, 52);
            var descText  = $"{svcName} — {name} (orden #{saleId})";
            var notes     = string.IsNullOrEmpty(prov)
                ? (string.IsNullOrEmpty(desc) ? $"Generado desde orden #{saleId}" : desc)
                : $"Proveedor: {prov}. {desc}".TrimEnd('.');

            var totalAmt = amt * qty;
            bool hasIva  = receiptType == 1 || receiptType == 3;
            decimal? amtNet = hasIva ? Math.Round(totalAmt / 1.19m) : (decimal?)null;
            decimal? amtIva = hasIva ? totalAmt - amtNet!.Value     : (decimal?)null;

            using var eCmd = new MySqlCommand(@"
                INSERT INTO expenses (subcategory_id,amount,description,date,business_id,store_id,
                                      expense_type_id,service_sale_id,notes,provider_id,receipt_type_id,
                                      amount_net,amount_iva,amount_total)
                VALUES (@Sub,@Amt,@Desc,@Date,@B,@Str,2,@Sale,@Notes,@ProvId,@RecType,
                        @AmtNet,@AmtIva,@AmtTotal);
                SELECT LAST_INSERT_ID();", conn, tx);
            eCmd.Parameters.AddWithValue("@Sub",      subcatId);
            eCmd.Parameters.AddWithValue("@Amt",      totalAmt);
            eCmd.Parameters.AddWithValue("@Desc",     descText);
            eCmd.Parameters.AddWithValue("@Date",     saleDate.ToString("yyyy-MM-dd"));
            eCmd.Parameters.AddWithValue("@B",        businessId);
            eCmd.Parameters.AddWithValue("@Str",      (object?)storeId     ?? DBNull.Value);
            eCmd.Parameters.AddWithValue("@Sale",     saleId);
            eCmd.Parameters.AddWithValue("@Notes",    notes);
            eCmd.Parameters.AddWithValue("@ProvId",   (object?)provId       ?? DBNull.Value);
            eCmd.Parameters.AddWithValue("@RecType",  (object?)receiptType  ?? DBNull.Value);
            eCmd.Parameters.AddWithValue("@AmtNet",   (object?)amtNet       ?? DBNull.Value);
            eCmd.Parameters.AddWithValue("@AmtIva",   (object?)amtIva       ?? DBNull.Value);
            eCmd.Parameters.AddWithValue("@AmtTotal", totalAmt);
            var expId = Convert.ToInt32(await eCmd.ExecuteScalarAsync());
            if (paymentType == 2 && installmentsCount > 1)
                await CreatePaymentPlan(conn, tx, expId, totalAmt, installmentsCount, paymentStartDate ?? saleDate);
        }
        _logger.LogInformation("[Services] expenses creados desde orden #{S} servicio #{V}",
            saleId, serviceId);
    }

    // ================================================================
    // HELPER: crear payment_plan + payment_installment para un expense
    // Llamado desde CreateExpensesForService cuando paymentType==2
    // ================================================================
    private async Task CreatePaymentPlan(
        MySqlConnection conn, MySqlTransaction tx,
        int expenseId, decimal totalAmount, int installmentsCount, DateTime startDate)
    {
        // Crear el plan de pago
        using var ppCmd = new MySqlCommand(@"
            INSERT INTO payment_plan (expense_id, type, expressed_in_uf, installments_count, start_date, created_at)
            VALUES (@ExpId, 2, 0, @Cnt, @Start, NOW());
            SELECT LAST_INSERT_ID();", conn, tx);
        ppCmd.Parameters.AddWithValue("@ExpId", expenseId);
        ppCmd.Parameters.AddWithValue("@Cnt",   installmentsCount);
        ppCmd.Parameters.AddWithValue("@Start", startDate.ToString("yyyy-MM-dd"));
        var planId = Convert.ToInt32(await ppCmd.ExecuteScalarAsync());

        // Vincular el plan al expense
        using var linkCmd = new MySqlCommand(
            "UPDATE expenses SET payment_plan_id=@PlanId WHERE id=@ExpId", conn, tx);
        linkCmd.Parameters.AddWithValue("@PlanId", planId);
        linkCmd.Parameters.AddWithValue("@ExpId",  expenseId);
        await linkCmd.ExecuteNonQueryAsync();

        // Crear N cuotas distribuidas uniformemente
        var baseAmt     = totalAmount > 0 ? Math.Floor(totalAmount / installmentsCount) : 0m;
        var distributed = 0m;
        for (int i = 1; i <= installmentsCount; i++)
        {
            var instAmt = (i == installmentsCount && totalAmount > 0)
                ? totalAmount - distributed
                : baseAmt;
            distributed += instAmt;

            using var instCmd = new MySqlCommand(@"
                INSERT INTO payment_installment
                    (payment_plan_id, installment_number, due_date, amount_clp, status, expense_id, created_at)
                VALUES (@PlanId, @Num, @Due, @Amt, 'pendiente', @ExpId, NOW())", conn, tx);
            instCmd.Parameters.AddWithValue("@PlanId", planId);
            instCmd.Parameters.AddWithValue("@Num",    i);
            instCmd.Parameters.AddWithValue("@Due",    startDate.AddMonths(i - 1).ToString("yyyy-MM-dd"));
            instCmd.Parameters.AddWithValue("@Amt",    instAmt);
            instCmd.Parameters.AddWithValue("@ExpId",  expenseId);
            await instCmd.ExecuteNonQueryAsync();
        }
    }

    private ObjectResult Err(Exception ex, string action)
    {
        _logger.LogError(ex, "[ServicesController.{Action}] {Msg}", action, ex.Message);
        return StatusCode(500, new { error = ex.Message });
    }
}

// ================================================================
// DTOs de Request
// ================================================================

public record CategoryRequest(string Name, string? Description, int BusinessId);

public record ServiceRequest(
    string Name, string? Description, int? CategoryId,
    int BusinessId, int? StoreId, decimal BasePrice, int? DurationMinutes);

public record CostItemRequest(
    string Name, string? Description, string? CostType,
    decimal Amount, decimal? Quantity, string? Unit,
    bool IsExternalized, int? ProviderId, string? ProviderName,
    int? ReceiptTypeId, int? SortOrder,
    int? EmployeeId, string? EmployeeName,
    int? LinkedServiceId, string? LinkedServiceName);

public record CreateServiceProviderRequest(string Name, int BusinessId);

public record SubServiceRequest(
    int ChildServiceId, decimal? Quantity, decimal? AdditionalCost,
    string? Notes, int? SortOrder);

public record SaleItemRequest(int ServiceId, decimal UnitPrice, decimal? Quantity, string? Notes);

public record SaleRequest(
    int BusinessId, int? StoreId, int? UserId,
    string? ClientName, string? ClientRut, string? ClientEmail, string? ClientPhone,
    string? Status, DateTime Date, DateTime? ScheduledDate, string? Notes, int? CreatedBy,
    List<SaleItemRequest> Items,
    string? DocumentType,
    int? PaymentType, int? InstallmentsCount, DateTime? PaymentStartDate,
    int? PaymentMethodId);

public record UpdateSaleRequest(
    string? Status, int? UserId,
    string? ClientName, string? ClientRut,
    string? ClientEmail, string? ClientPhone,
    DateTime? ScheduledDate, string? Notes,
    string? DocumentType,
    int? PaymentType, int? InstallmentsCount, DateTime? PaymentStartDate,
    int? PaymentMethodId);

public record AddSaleSupplyRequest(
    int SupplyId, decimal? Quantity, decimal? UnitCost, string? Notes);

