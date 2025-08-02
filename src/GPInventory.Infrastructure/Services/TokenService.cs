using GPInventory.Application.DTOs.Auth;
using GPInventory.Application.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using System.Text.Json;

namespace GPInventory.Infrastructure.Services;

public class TokenService : ITokenService
{
    private readonly IConfiguration _configuration;
    private readonly string _secretKey;
    private readonly string _issuer;
    private readonly string _audience;

    public TokenService(IConfiguration configuration)
    {
        _configuration = configuration;
        _secretKey = _configuration["JwtSettings:SecretKey"] ?? throw new InvalidOperationException("JWT SecretKey is not configured");
        _issuer = _configuration["JwtSettings:Issuer"] ?? "GPInventory";
        _audience = _configuration["JwtSettings:Audience"] ?? "GPInventory";
    }

    public string GenerateToken(UserDto user)
    {
        var tokenHandler = new JwtSecurityTokenHandler();
        var key = Encoding.ASCII.GetBytes(_secretKey);
        
        var claims = new List<Claim>
        {
            new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new Claim(ClaimTypes.Email, user.Email),
            new Claim(ClaimTypes.Name, $"{user.Name} {user.LastName}"),
            new Claim("userId", user.Id.ToString())
        };

        // Estructura limpia de roles - array de objetos JSON
        if (user.Roles.Any())
        {
            var rolesArray = user.Roles.Select(r => new
            {
                businessId = r.BusinessId,
                businessName = r.BusinessName,
                roleId = r.Id,
                roleName = r.Name
            }).ToArray();

            var rolesJson = JsonSerializer.Serialize(rolesArray, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            });
            
            claims.Add(new Claim("roles", rolesJson));
            
            // Claims básicos para compatibilidad y acceso rápido
            claims.Add(new Claim("primaryBusinessId", user.Roles.First().BusinessId.ToString()));
            claims.Add(new Claim("totalBusinesses", user.Roles.Count.ToString()));
        }

        var tokenDescriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(claims),
            Expires = DateTime.UtcNow.AddHours(24),
            Issuer = _issuer,
            Audience = _audience,
            SigningCredentials = new SigningCredentials(new SymmetricSecurityKey(key), SecurityAlgorithms.HmacSha256Signature)
        };

        var token = tokenHandler.CreateToken(tokenDescriptor);
        return tokenHandler.WriteToken(token);
    }

    public bool ValidateToken(string token)
    {
        try
        {
            var tokenHandler = new JwtSecurityTokenHandler();
            var key = Encoding.ASCII.GetBytes(_secretKey);

            tokenHandler.ValidateToken(token, new TokenValidationParameters
            {
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = new SymmetricSecurityKey(key),
                ValidateIssuer = true,
                ValidIssuer = _issuer,
                ValidateAudience = true,
                ValidAudience = _audience,
                ValidateLifetime = true,
                ClockSkew = TimeSpan.Zero
            }, out SecurityToken validatedToken);

            return true;
        }
        catch
        {
            return false;
        }
    }

    public int? GetUserIdFromToken(string token)
    {
        try
        {
            var tokenHandler = new JwtSecurityTokenHandler();
            var jwt = tokenHandler.ReadJwtToken(token);
            
            var userIdClaim = jwt.Claims.FirstOrDefault(c => c.Type == "userId")?.Value;
            if (int.TryParse(userIdClaim, out int userId))
            {
                return userId;
            }
            return null;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Obtiene los datos de negocios y roles del token JWT con estructura de array JSON
    /// </summary>
    public List<BusinessRoleInfo> GetBusinessRolesFromToken(string token)
    {
        try
        {
            var tokenHandler = new JwtSecurityTokenHandler();
            var jwt = tokenHandler.ReadJwtToken(token);
            
            var rolesClaim = jwt.Claims.FirstOrDefault(c => c.Type == "roles")?.Value;
            if (string.IsNullOrEmpty(rolesClaim))
            {
                return new List<BusinessRoleInfo>();
            }
            
            var rolesArray = JsonSerializer.Deserialize<JsonElement[]>(rolesClaim);
            if (rolesArray == null)
            {
                return new List<BusinessRoleInfo>();
            }

            var businessRoles = new List<BusinessRoleInfo>();
            
            foreach (var roleElement in rolesArray)
            {
                if (roleElement.TryGetProperty("businessId", out var businessIdProp) &&
                    roleElement.TryGetProperty("businessName", out var businessNameProp) &&
                    roleElement.TryGetProperty("roleId", out var roleIdProp) &&
                    roleElement.TryGetProperty("roleName", out var roleNameProp))
                {
                    businessRoles.Add(new BusinessRoleInfo
                    {
                        BusinessId = businessIdProp.GetInt32(),
                        BusinessName = businessNameProp.GetString() ?? "",
                        RoleId = roleIdProp.GetInt32(),
                        RoleName = roleNameProp.GetString() ?? ""
                    });
                }
            }
            
            return businessRoles;
        }
        catch
        {
            return new List<BusinessRoleInfo>();
        }
    }

    /// <summary>
    /// Obtiene los datos del negocio primario del usuario
    /// </summary>
    public BusinessRoleInfo? GetPrimaryBusinessFromToken(string token)
    {
        try
        {
            var businessRoles = GetBusinessRolesFromToken(token);
            if (!businessRoles.Any())
            {
                return null;
            }

            var tokenHandler = new JwtSecurityTokenHandler();
            var jwt = tokenHandler.ReadJwtToken(token);
            var primaryBusinessIdClaim = jwt.Claims.FirstOrDefault(c => c.Type == "primaryBusinessId")?.Value;
            
            if (int.TryParse(primaryBusinessIdClaim, out int primaryBusinessId))
            {
                return businessRoles.FirstOrDefault(br => br.BusinessId == primaryBusinessId);
            }
            
            // Si no se encuentra el primario, devolver el primero
            return businessRoles.First();
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Verifica si el usuario tiene acceso a un negocio específico
    /// </summary>
    public bool HasAccessToBusiness(string token, int businessId)
    {
        var businessRoles = GetBusinessRolesFromToken(token);
        return businessRoles.Any(br => br.BusinessId == businessId);
    }

    /// <summary>
    /// Obtiene el rol del usuario en un negocio específico
    /// </summary>
    public BusinessRoleInfo? GetRoleInBusiness(string token, int businessId)
    {
        var businessRoles = GetBusinessRolesFromToken(token);
        return businessRoles.FirstOrDefault(br => br.BusinessId == businessId);
    }
}
