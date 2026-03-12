using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.Caching.Memory;
using MySqlConnector;
using System.Security.Claims;
using System.Text.Json;

namespace GPInventory.Api.Authorization;

/// <summary>
/// Autorización de dos capas para los módulos de GP (HR, Inventory, Factory, Services, Expenses).
///
/// Capa 1 – Acceso al módulo:
///   Si requireSystemRole=true (por defecto), el rol de sistema JWT debe ser {1,2,3,6}
///   (Cofundador, Dueño, Administrador, Contador) para el negocio solicitado.
///   Si requireSystemRole=false, solo se valida que el JWT sea válido (cualquier empleado).
///   El super_admin siempre pasa.
///
/// Capa 2 – Permiso granular (opcional):
///   Si se especifica un 'permission' (p. ej. "manage_payroll"), se comprueba
///   la tabla hr_business_role para el usuario en ese negocio.
///   - Si el usuario NO tiene hr_business_role → acceso total (propietario/admin).
///   - Si el usuario TIENE hr_business_role → se verifica que la clave de permiso
///     sea true en el JSON de permissions.
///
/// Los resultados del DB se cachean 5 minutos por (userId, businessId).
/// </summary>
public class HrAuthorizeAttribute : TypeFilterAttribute
{
    /// <param name="permission">Clave de permiso granular, p.ej. "manage_payroll". Vacío = solo capa 1.</param>
    /// <param name="requireSystemRole">
    ///   true  = requiere rol de sistema {1,2,3,6} (para módulos financieros/admin).
    ///   false = cualquier usuario autenticado pasa la capa 1 (para módulos operacionales).
    /// </param>
    /// <param name="orPermission">
    ///   Permiso alternativo (OR). Si el usuario tiene <paramref name="permission"/> O
    ///   <paramref name="orPermission"/>, se concede acceso. Útil para endpoints de lectura
    ///   que aceptan tanto "view_X" como "manage_X".
    /// </param>
    public HrAuthorizeAttribute(string permission = "", bool requireSystemRole = true, string orPermission = "")
        : base(typeof(HrAuthorizeFilter))
    {
        Arguments = new object[] { permission, requireSystemRole, orPermission };
    }
}

public class HrAuthorizeFilter : IAsyncAuthorizationFilter
{
    private static readonly int[] HrSystemRoles = { 1, 2, 3, 6 };
    private static readonly TimeSpan CacheTtl = TimeSpan.FromMinutes(5);

    private readonly string _requiredPermission;
    private readonly string _orPermission;
    private readonly bool _requireSystemRole;
    private readonly IConfiguration _configuration;
    private readonly IMemoryCache _cache;
    private readonly ILogger<HrAuthorizeFilter> _logger;

    public HrAuthorizeFilter(
        string requiredPermission,
        bool requireSystemRole,
        string orPermission,
        IConfiguration configuration,
        IMemoryCache cache,
        ILogger<HrAuthorizeFilter> logger)
    {
        _requiredPermission = requiredPermission;
        _orPermission = orPermission;
        _requireSystemRole = requireSystemRole;
        _configuration = configuration;
        _cache = cache;
        _logger = logger;
    }

    public async Task OnAuthorizationAsync(AuthorizationFilterContext context)
    {
        // ── 1. Debe estar autenticado ─────────────────────────────────────────
        if (!(context.HttpContext.User.Identity?.IsAuthenticated ?? false))
        {
            context.Result = new UnauthorizedResult();
            return;
        }

        var userEmail = context.HttpContext.User.FindFirst("email")?.Value
                     ?? context.HttpContext.User.FindFirst(ClaimTypes.Email)?.Value
                     ?? "(sin email)";

        // ── 2. super_admin bypasa todo ────────────────────────────────────────
        var systemRole = context.HttpContext.User.FindFirst("systemRole")?.Value;
        if (systemRole == "super_admin")
        {
            _logger.LogInformation("[HrAuthorize] super_admin bypass para {Email}", userEmail);
            return;
        }

        // ── 3. Capa 1: verificar acceso al módulo ────────────────────────────────
        if (_requireSystemRole)
        {
            // Módulos financieros/admin: requiere rol de sistema {1,2,3,6}
            var rolesClaim = context.HttpContext.User.FindFirst("roles")?.Value
                          ?? context.HttpContext.User.FindFirst(ClaimTypes.Role)?.Value;

            if (string.IsNullOrEmpty(rolesClaim))
            {
                _logger.LogWarning("[HrAuthorize] Sin claim 'roles' para {Email}", userEmail);
                context.Result = new ForbidResult();
                return;
            }

            // Obtener businessId de la solicitud
            int? businessId = await ExtractBusinessIdAsync(context);
            if (businessId is null)
            {
                _logger.LogWarning("[HrAuthorize] No se pudo extraer businessId para {Email}", userEmail);
                context.Result = new ForbidResult();
                return;
            }

            try
            {
                var rolesArray = JsonSerializer.Deserialize<JsonElement[]>(
                    rolesClaim,
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

                if (rolesArray == null || rolesArray.Length == 0)
                {
                    context.Result = new ForbidResult();
                    return;
                }

                bool hasSystemAccess = false;
                foreach (var roleEl in rolesArray)
                {
                    if (!roleEl.TryGetProperty("roleId", out var ridProp) || !ridProp.TryGetInt32(out int roleId))
                        continue;
                    if (!HrSystemRoles.Contains(roleId))
                        continue;

                    if (roleEl.TryGetProperty("businessId", out var bidProp) && bidProp.TryGetInt32(out int claimBiz))
                    {
                        if (claimBiz == businessId) { hasSystemAccess = true; break; }
                    }
                    else
                    {
                        hasSystemAccess = true; break;
                    }
                }

                if (!hasSystemAccess)
                {
                    _logger.LogWarning("[HrAuthorize] {Email} no tiene rol de sistema para negocio {BizId}", userEmail, businessId);
                    context.Result = new ForbidResult();
                    return;
                }

                // Si no hay permiso granular requerido → capa 1 es suficiente
                if (string.IsNullOrEmpty(_requiredPermission))
                {
                    _logger.LogInformation("[HrAuthorize] Acceso por sistema concedido a {Email}", userEmail);
                    return;
                }

                await CheckGranularPermissionAsync(context, userEmail, businessId.Value);
            }
            catch (JsonException ex)
            {
                _logger.LogError(ex, "[HrAuthorize] Error parseando roles para {Email}", userEmail);
                context.Result = new ForbidResult();
            }
        }
        else
        {
            // Módulos operacionales: cualquier usuario autenticado pasa capa 1
            // Solo verificar capa 2 si hay permiso granular requerido
            if (string.IsNullOrEmpty(_requiredPermission))
            {
                _logger.LogInformation("[HrAuthorize] Acceso operacional sin restricciones para {Email}", userEmail);
                return;
            }

            int? businessId = await ExtractBusinessIdAsync(context);
            if (businessId is null)
            {
                // Sin businessId → confiar en el [Authorize] base
                return;
            }

            await CheckGranularPermissionAsync(context, userEmail, businessId.Value);
        }
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Helpers
    // ──────────────────────────────────────────────────────────────────────────

    /// <summary>Verifica el permiso granular (Capa 2). Modifica context.Result si se deniega.</summary>
    private async Task CheckGranularPermissionAsync(AuthorizationFilterContext context, string userEmail, int businessId)
    {
        var userIdClaim = context.HttpContext.User.FindFirst("sub")
                       ?? context.HttpContext.User.FindFirst("user_id")
                       ?? context.HttpContext.User.FindFirst("userId")
                       ?? context.HttpContext.User.FindFirst("id")
                       ?? context.HttpContext.User.FindFirst(ClaimTypes.NameIdentifier);

        if (!int.TryParse(userIdClaim?.Value, out int userId))
        {
            _logger.LogInformation("[HrAuthorize] Sin userId claim; permiso granular omitido para {Email}", userEmail);
            return; // Fail-open sin userId
        }

        var cacheKey = $"hr_perms_{userId}_{businessId}";
        if (!_cache.TryGetValue(cacheKey, out Dictionary<string, bool>? permissions))
        {
            permissions = await LoadPermissionsAsync(userId, businessId);
            _cache.Set(cacheKey, permissions, CacheTtl);
        }

        // null = sin hr_business_role → acceso completo
        if (permissions == null)
        {
            _logger.LogInformation("[HrAuthorize] Sin rol HR específico → acceso completo para {Email}", userEmail);
            return;
        }

        // OR logic: acceso si tiene el permiso principal O el permiso alternativo
        var primary   = !string.IsNullOrEmpty(_requiredPermission) && permissions.TryGetValue(_requiredPermission, out bool a) && a;
        var secondary = !string.IsNullOrEmpty(_orPermission)       && permissions.TryGetValue(_orPermission,       out bool b) && b;

        if (!primary && !secondary)
        {
            var permDesc = string.IsNullOrEmpty(_orPermission)
                ? $"'{_requiredPermission}'"
                : $"'{_requiredPermission}' o '{_orPermission}'";
            _logger.LogWarning(
                "[HrAuthorize] {Email} no tiene permiso {PermDesc} en negocio {BizId}",
                userEmail, permDesc, businessId);
            context.Result = new ForbidResult();
            return;
        }

        var grantedPerm = primary ? _requiredPermission : _orPermission;
        _logger.LogInformation("[HrAuthorize] Permiso '{Perm}' concedido a {Email}", grantedPerm, userEmail);
    }

    /// <summary>
    /// Intenta extraer businessId de: query string → route values → cuerpo JSON.
    /// </summary>
    private static async Task<int?> ExtractBusinessIdAsync(AuthorizationFilterContext context)
    {
        var req = context.HttpContext.Request;

        // 1. Query string — singular
        if (int.TryParse(req.Query["businessId"].FirstOrDefault(), out int qBid))
            return qBid;

        // 1b. Query string — plural (BusinessIds[0] / businessIds[0])
        var pluralKey = req.Query.ContainsKey("BusinessIds") ? "BusinessIds"
                      : req.Query.ContainsKey("businessIds") ? "businessIds"
                      : null;
        if (pluralKey != null && int.TryParse(req.Query[pluralKey].FirstOrDefault(), out int qBidPlural))
            return qBidPlural;

        // 2. Route values
        if (int.TryParse(req.RouteValues["businessId"]?.ToString(), out int rBid))
            return rBid;

        // 3. JSON body (sin consumir el stream)
        if (req.ContentType?.Contains("application/json", StringComparison.OrdinalIgnoreCase) == true
            && req.ContentLength is > 0)
        {
            req.EnableBuffering();
            req.Body.Position = 0;
            using var reader = new StreamReader(req.Body, leaveOpen: true);
            var body = await reader.ReadToEndAsync();
            req.Body.Position = 0;

            if (!string.IsNullOrEmpty(body))
            {
                try
                {
                    using var doc = JsonDocument.Parse(body);
                    if (doc.RootElement.TryGetProperty("businessId", out var bid)
                        && bid.TryGetInt32(out int jBid))
                        return jBid;
                }
                catch { /* ignorar */ }
            }
        }

        return null;
    }

    /// <summary>
    /// Carga el JSON de permisos del hr_business_role asignado al usuario.
    /// Retorna null si el usuario no tiene rol HR (→ acceso completo).
    /// </summary>
    private async Task<Dictionary<string, bool>?> LoadPermissionsAsync(int userId, int businessId)
    {
        try
        {
            using var conn = new MySqlConnection(_configuration.GetConnectionString("DefaultConnection")!);
            await conn.OpenAsync();

            using var cmd = new MySqlCommand(@"
                SELECT r.permissions
                FROM user_has_business ub
                LEFT JOIN hr_business_role r ON r.id = ub.hr_business_role_id
                WHERE ub.id_user = @UID AND ub.id_business = @BID
                LIMIT 1", conn);
            cmd.Parameters.AddWithValue("@UID", userId);
            cmd.Parameters.AddWithValue("@BID", businessId);

            using var reader = await cmd.ExecuteReaderAsync();
            if (!await reader.ReadAsync())
                return null; // Usuario no pertenece al negocio

            if (reader.IsDBNull(0))
                return null; // Sin hr_business_role → acceso completo

            var json = reader.GetString(0);
            return JsonSerializer.Deserialize<Dictionary<string, bool>>(
                json,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[HrAuthorize] Error consultando permisos para user={UID} biz={BID}", userId, businessId);
            return null; // Fail-open: no bloquear por error de DB
        }
    }
}
