using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using System.Security.Claims;
using System.Text.Json;

namespace GPInventory.Api.Authorization;

/// <summary>
/// Autorización personalizada para GP Expenses.
/// Permite acceso a:
/// - Usuarios con systemRole 'super_admin'
/// - Usuarios con roles: Dueño(2), Administrador(3), Contador(6)
/// Nota: Cofundador(1) ya no se usa pero se mantiene en la lista por compatibilidad
/// </summary>
public class ExpensesAuthorizeAttribute : TypeFilterAttribute
{
    public ExpensesAuthorizeAttribute() : base(typeof(ExpensesAuthorizeFilter))
    {
    }
}

public class ExpensesAuthorizeFilter : IAuthorizationFilter
{
    private static readonly int[] AllowedRoleIds = { 1, 2, 3, 6 }; // [Legacy: Cofundador], Dueño, Administrador, Contador
    private readonly ILogger<ExpensesAuthorizeFilter> _logger;

    public ExpensesAuthorizeFilter(ILogger<ExpensesAuthorizeFilter> logger)
    {
        _logger = logger;
    }

    public void OnAuthorization(AuthorizationFilterContext context)
    {
        // Verificar si el usuario está autenticado
        if (!context.HttpContext.User.Identity?.IsAuthenticated ?? true)
        {
            _logger.LogWarning("[ExpensesAuthorize] Unauthorized access attempt - user not authenticated");
            context.Result = new UnauthorizedResult();
            return;
        }

        // Obtener el email del usuario desde los claims
        var userEmail = context.HttpContext.User.FindFirst(ClaimTypes.Email)?.Value;
        
        if (string.IsNullOrEmpty(userEmail))
        {
            _logger.LogWarning("[ExpensesAuthorize] Unauthorized access attempt - email claim not found");
            context.Result = new UnauthorizedResult();
            return;
        }

        // Permitir acceso directo a super_admin
        var systemRole = context.HttpContext.User.FindFirst("systemRole")?.Value;
        if (systemRole == "super_admin")
        {
            _logger.LogInformation("[ExpensesAuthorize] Access granted for {Email} - super_admin bypass", userEmail);
            return;
        }

        // Obtener los roles desde el claim "roles" (JSON array)
        // El claim puede venir como "roles" o como ClaimTypes.Role (http://schemas.microsoft.com/ws/2008/06/identity/claims/role)
        var rolesClaim = context.HttpContext.User.FindFirst("roles")?.Value 
                      ?? context.HttpContext.User.FindFirst(ClaimTypes.Role)?.Value;
        
        if (string.IsNullOrEmpty(rolesClaim))
        {
            _logger.LogWarning("[ExpensesAuthorize] Access denied for {Email} - no roles claim found", userEmail);
            context.Result = new ForbidResult();
            return;
        }

        try
        {
            // Parsear el JSON de roles
            var rolesArray = JsonSerializer.Deserialize<JsonElement[]>(rolesClaim, new JsonSerializerOptions 
            { 
                PropertyNameCaseInsensitive = true 
            });
            if (rolesArray == null || rolesArray.Length == 0)
            {
                _logger.LogWarning("[ExpensesAuthorize] Access denied for {Email} - roles array is empty", userEmail);
                context.Result = new ForbidResult();
                return;
            }

            // Extraer los role IDs
            var userRoleIds = new List<int>();
            foreach (var roleElement in rolesArray)
            {
                if (roleElement.TryGetProperty("roleId", out var roleIdProp) && roleIdProp.TryGetInt32(out int roleId))
                {
                    userRoleIds.Add(roleId);
                }
            }

            if (!userRoleIds.Any())
            {
                _logger.LogWarning("[ExpensesAuthorize] Access denied for {Email} - no valid role IDs found", userEmail);
                context.Result = new ForbidResult();
                return;
            }

            // Verificar si alguno de los roles del usuario está permitido
            var hasPermission = userRoleIds.Any(roleId => AllowedRoleIds.Contains(roleId));

            if (!hasPermission)
            {
                _logger.LogWarning(
                    "[ExpensesAuthorize] Access denied for {Email} - user roles [{Roles}] not in allowed list [{AllowedRoles}]",
                    userEmail,
                    string.Join(", ", userRoleIds),
                    string.Join(", ", AllowedRoleIds)
                );
                context.Result = new ForbidResult();
                return;
            }

            _logger.LogInformation("[ExpensesAuthorize] Access granted for {Email} with roles [{Roles}]", userEmail, string.Join(", ", userRoleIds));
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "[ExpensesAuthorize] Failed to parse roles claim for {Email}", userEmail);
            context.Result = new ForbidResult();
        }
    }
}
