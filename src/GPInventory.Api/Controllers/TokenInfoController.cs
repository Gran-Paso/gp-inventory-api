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
    public ActionResult<object> GetTokenInfo()
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

            return Ok(new
            {
                message = "Información del JWT con estructura mejorada",
                userId = userId,
                totalBusinesses = businessRoles.Count,
                primaryBusiness = primaryBusiness,
                allBusinessRoles = businessRoles,
                accessSummary = businessRoles.Select(br => new
                {
                    businessId = br.BusinessId,
                    businessName = br.BusinessName,
                    roleId = br.RoleId,
                    roleName = br.RoleName,
                    accessString = $"business:{br.BusinessId}|role:{br.RoleId}|name:{br.RoleName}"
                }).ToList()
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
}
