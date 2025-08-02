using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using GPInventory.Application.Interfaces;
using GPInventory.Application.DTOs.Auth;

namespace GPInventory.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class TokenInfoController : ControllerBase
{
    private readonly ITokenService _tokenService;
    private readonly ILogger<TokenInfoController> _logger;

    public TokenInfoController(ITokenService tokenService, ILogger<TokenInfoController> logger)
    {
        _tokenService = tokenService;
        _logger = logger;
    }

    /// <summary>
    /// Obtiene información detallada del token JWT con la nueva estructura mejorada
    /// </summary>
    [HttpGet("info")]
    [Authorize]
    [ProducesResponseType(200)]
    [ProducesResponseType(401)]
    public ActionResult<object> GetTokenInfo([FromQuery] int? selectedBusinessId = null)
    {
        try
        {
            var token = Request.Headers["Authorization"].ToString().Replace("Bearer ", "");
            
            if (string.IsNullOrEmpty(token))
            {
                return Unauthorized(new { message = "Token no encontrado" });
            }

            var userId = _tokenService.GetUserIdFromToken(token);
            var businessRoles = _tokenService.GetBusinessRolesFromToken(token);
            var primaryBusiness = _tokenService.GetPrimaryBusinessFromToken(token);

            // Determinar el negocio activo
            var activeBusiness = businessRoles.FirstOrDefault(br => br.BusinessId == selectedBusinessId) 
                               ?? primaryBusiness 
                               ?? businessRoles.FirstOrDefault();

            // Negocios disponibles para selección (excluyendo el activo)
            var availableBusinesses = businessRoles
                .Where(br => br.BusinessId != activeBusiness?.BusinessId)
                .Select(br => new
                {
                    businessId = br.BusinessId,
                    businessName = br.BusinessName,
                    roleId = br.RoleId,
                    roleName = br.RoleName
                })
                .ToList();

            return Ok(new
            {
                message = "Información del JWT con negocio activo",
                userId = userId,
                activeBusiness = activeBusiness != null ? new
                {
                    businessId = activeBusiness.BusinessId,
                    businessName = activeBusiness.BusinessName,
                    roleId = activeBusiness.RoleId,
                    roleName = activeBusiness.RoleName,
                    isSelected = selectedBusinessId.HasValue && selectedBusinessId.Value == activeBusiness.BusinessId,
                    isPrimary = primaryBusiness?.BusinessId == activeBusiness.BusinessId
                } : null,
                availableBusinesses = availableBusinesses,
                totalBusinesses = businessRoles.Count,
                hasMultipleBusinesses = businessRoles.Count > 1
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al obtener información del token");
            return StatusCode(500, new { message = "Error interno del servidor" });
        }
    }

    /// <summary>
    /// Verifica acceso a un negocio específico
    /// </summary>
    [HttpGet("access/{businessId}")]
    [Authorize]
    [ProducesResponseType(200)]
    [ProducesResponseType(401)]
    [ProducesResponseType(403)]
    public ActionResult<object> CheckBusinessAccess(int businessId)
    {
        try
        {
            var token = Request.Headers["Authorization"].ToString().Replace("Bearer ", "");
            
            if (string.IsNullOrEmpty(token))
            {
                return Unauthorized(new { message = "Token no encontrado" });
            }

            var hasAccess = _tokenService.HasAccessToBusiness(token, businessId);
            var roleInBusiness = _tokenService.GetRoleInBusiness(token, businessId);

            if (!hasAccess)
            {
                return Forbid($"No tienes acceso al negocio con ID {businessId}");
            }

            return Ok(new
            {
                message = "Acceso autorizado al negocio",
                businessId = businessId,
                hasAccess = hasAccess,
                roleInBusiness = roleInBusiness
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al verificar acceso al negocio");
            return StatusCode(500, new { message = "Error interno del servidor" });
        }
    }

    /// <summary>
    /// Obtiene todos los negocios a los que el usuario tiene acceso
    /// </summary>
    [HttpGet("businesses")]
    [Authorize]
    [ProducesResponseType(200)]
    [ProducesResponseType(401)]
    public ActionResult<object> GetUserBusinesses()
    {
        try
        {
            var token = Request.Headers["Authorization"].ToString().Replace("Bearer ", "");
            
            if (string.IsNullOrEmpty(token))
            {
                return Unauthorized(new { message = "Token no encontrado" });
            }

            var businessRoles = _tokenService.GetBusinessRolesFromToken(token);
            var primaryBusiness = _tokenService.GetPrimaryBusinessFromToken(token);

            return Ok(new
            {
                message = "Negocios del usuario con estructura mejorada",
                primaryBusiness = primaryBusiness,
                totalBusinesses = businessRoles.Count,
                businesses = businessRoles.Select(br => new
                {
                    businessId = br.BusinessId,
                    businessName = br.BusinessName,
                    roleId = br.RoleId,
                    roleName = br.RoleName,
                    isPrimary = primaryBusiness?.BusinessId == br.BusinessId
                }).ToList()
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al obtener negocios del usuario");
            return StatusCode(500, new { message = "Error interno del servidor" });
        }
    }

    /// <summary>
    /// Obtiene el contexto actual del usuario (negocio activo y disponibles para cambio)
    /// </summary>
    [HttpGet("current-context")]
    [Authorize]
    [ProducesResponseType(200)]
    [ProducesResponseType(401)]
    public ActionResult<object> GetCurrentContext([FromQuery] int? selectedBusinessId = null)
    {
        try
        {
            var token = Request.Headers["Authorization"].ToString().Replace("Bearer ", "");
            
            if (string.IsNullOrEmpty(token))
            {
                return Unauthorized(new { message = "Token no encontrado" });
            }

            var userId = _tokenService.GetUserIdFromToken(token);
            var businessRoles = _tokenService.GetBusinessRolesFromToken(token);
            var primaryBusiness = _tokenService.GetPrimaryBusinessFromToken(token);

            // Si no hay negocios disponibles
            if (!businessRoles.Any())
            {
                return Ok(new
                {
                    message = "Usuario sin negocios asignados",
                    userId = userId,
                    activeBusiness = (object?)null,
                    availableBusinesses = new List<object>(),
                    totalBusinesses = 0,
                    hasMultipleBusinesses = false
                });
            }

            // Determinar el negocio activo
            var activeBusiness = businessRoles.FirstOrDefault(br => br.BusinessId == selectedBusinessId) 
                               ?? primaryBusiness 
                               ?? businessRoles.FirstOrDefault();

            if (activeBusiness == null)
            {
                return BadRequest(new { message = "No se pudo determinar el negocio activo" });
            }

            // Negocios disponibles para selección (excluyendo el activo)
            var availableBusinesses = businessRoles
                .Where(br => br.BusinessId != activeBusiness.BusinessId)
                .Select(br => new
                {
                    businessId = br.BusinessId,
                    businessName = br.BusinessName,
                    roleId = br.RoleId,
                    roleName = br.RoleName
                })
                .ToList();

            return Ok(new
            {
                message = "Contexto actual del usuario",
                userId = userId,
                activeBusiness = new
                {
                    businessId = activeBusiness.BusinessId,
                    businessName = activeBusiness.BusinessName,
                    roleId = activeBusiness.RoleId,
                    roleName = activeBusiness.RoleName,
                    isSelected = selectedBusinessId.HasValue && selectedBusinessId.Value == activeBusiness.BusinessId,
                    isPrimary = primaryBusiness?.BusinessId == activeBusiness.BusinessId
                },
                availableBusinesses = availableBusinesses,
                totalBusinesses = businessRoles.Count,
                hasMultipleBusinesses = businessRoles.Count > 1
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al obtener contexto actual del usuario");
            return StatusCode(500, new { message = "Error interno del servidor" });
        }
    }

    /// <summary>
    /// Cambia el contexto de negocio activo y devuelve la información actualizada
    /// </summary>
    [HttpPost("switch-business/{businessId}")]
    [Authorize]
    [ProducesResponseType(200)]
    [ProducesResponseType(401)]
    [ProducesResponseType(403)]
    [ProducesResponseType(404)]
    public ActionResult<object> SwitchBusiness(int businessId)
    {
        try
        {
            var token = Request.Headers["Authorization"].ToString().Replace("Bearer ", "");
            
            if (string.IsNullOrEmpty(token))
            {
                return Unauthorized(new { message = "Token no encontrado" });
            }

            var userId = _tokenService.GetUserIdFromToken(token);
            var businessRoles = _tokenService.GetBusinessRolesFromToken(token);

            // Verificar que el usuario tiene acceso al negocio solicitado
            var targetBusiness = businessRoles.FirstOrDefault(br => br.BusinessId == businessId);
            
            if (targetBusiness == null)
            {
                return Forbid($"No tienes acceso al negocio con ID {businessId}");
            }

            // Negocios disponibles para selección (excluyendo el activo)
            var availableBusinesses = businessRoles
                .Where(br => br.BusinessId != businessId)
                .Select(br => new
                {
                    businessId = br.BusinessId,
                    businessName = br.BusinessName,
                    roleId = br.RoleId,
                    roleName = br.RoleName
                })
                .ToList();

            return Ok(new
            {
                message = $"Cambiado exitosamente al negocio {targetBusiness.BusinessName}",
                userId = userId,
                activeBusiness = new
                {
                    businessId = targetBusiness.BusinessId,
                    businessName = targetBusiness.BusinessName,
                    roleId = targetBusiness.RoleId,
                    roleName = targetBusiness.RoleName,
                    isSelected = true
                },
                availableBusinesses = availableBusinesses,
                totalBusinesses = businessRoles.Count,
                hasMultipleBusinesses = businessRoles.Count > 1
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al cambiar de negocio");
            return StatusCode(500, new { message = "Error interno del servidor" });
        }
    }
}
