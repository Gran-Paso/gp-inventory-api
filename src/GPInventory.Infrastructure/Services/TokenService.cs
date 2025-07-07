using GPInventory.Application.DTOs.Auth;
using GPInventory.Application.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

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

        // Add role claims for each business the user belongs to - IMPROVED STRUCTURE
        foreach (var userRole in user.Roles)
        {
            // Traditional role claims (mantener compatibilidad)
            claims.Add(new Claim(ClaimTypes.Role, userRole.Name));
            
            // Improved grouped claims - cada business/role tiene su propio conjunto de claims agrupados
            var businessIndex = user.Roles.IndexOf(userRole);
            
            // Prefijo con índice para agrupar datos relacionados
            claims.Add(new Claim($"business_{businessIndex}_id", userRole.BusinessId.ToString()));
            claims.Add(new Claim($"business_{businessIndex}_name", userRole.BusinessName));
            claims.Add(new Claim($"role_{businessIndex}_id", userRole.Id.ToString()));
            claims.Add(new Claim($"role_{businessIndex}_name", userRole.Name));
            
            // Claims combinados para acceso rápido
            claims.Add(new Claim($"access_{businessIndex}", $"business:{userRole.BusinessId}|role:{userRole.Id}|name:{userRole.Name}"));
            
            // Mantener el formato anterior para compatibilidad hacia atrás
            claims.Add(new Claim("roleId", userRole.Id.ToString()));
            claims.Add(new Claim("businessId", userRole.BusinessId.ToString()));
            claims.Add(new Claim("businessName", userRole.BusinessName));
            claims.Add(new Claim($"role:{userRole.BusinessId}", userRole.Name));
        }

        // Add summary claims for easy access
        if (user.Roles.Any())
        {
            claims.Add(new Claim("total_businesses", user.Roles.Count.ToString()));
            claims.Add(new Claim("primary_business_id", user.Roles.First().BusinessId.ToString()));
            claims.Add(new Claim("primary_business_name", user.Roles.First().BusinessName));
            claims.Add(new Claim("primary_role_id", user.Roles.First().Id.ToString()));
            claims.Add(new Claim("primary_role_name", user.Roles.First().Name));
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
    /// Obtiene los datos agrupados de negocios y roles del token JWT mejorado
    /// </summary>
    public List<BusinessRoleInfo> GetBusinessRolesFromToken(string token)
    {
        try
        {
            var tokenHandler = new JwtSecurityTokenHandler();
            var jwt = tokenHandler.ReadJwtToken(token);
            var claims = jwt.Claims.ToList();
            
            var businessRoles = new List<BusinessRoleInfo>();
            
            // Buscar el número total de negocios
            var totalBusinessesClaim = claims.FirstOrDefault(c => c.Type == "total_businesses")?.Value;
            if (!int.TryParse(totalBusinessesClaim, out int totalBusinesses))
            {
                return businessRoles; // Retorna lista vacía si no se encuentra
            }
            
            // Extraer datos agrupados para cada negocio
            for (int i = 0; i < totalBusinesses; i++)
            {
                var businessIdClaim = claims.FirstOrDefault(c => c.Type == $"business_{i}_id")?.Value;
                var businessNameClaim = claims.FirstOrDefault(c => c.Type == $"business_{i}_name")?.Value;
                var roleIdClaim = claims.FirstOrDefault(c => c.Type == $"role_{i}_id")?.Value;
                var roleNameClaim = claims.FirstOrDefault(c => c.Type == $"role_{i}_name")?.Value;
                
                if (int.TryParse(businessIdClaim, out int businessId) && 
                    int.TryParse(roleIdClaim, out int roleId) &&
                    !string.IsNullOrEmpty(businessNameClaim) &&
                    !string.IsNullOrEmpty(roleNameClaim))
                {
                    businessRoles.Add(new BusinessRoleInfo
                    {
                        BusinessId = businessId,
                        BusinessName = businessNameClaim,
                        RoleId = roleId,
                        RoleName = roleNameClaim
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
            var tokenHandler = new JwtSecurityTokenHandler();
            var jwt = tokenHandler.ReadJwtToken(token);
            var claims = jwt.Claims.ToList();
            
            var businessIdClaim = claims.FirstOrDefault(c => c.Type == "primary_business_id")?.Value;
            var businessNameClaim = claims.FirstOrDefault(c => c.Type == "primary_business_name")?.Value;
            var roleIdClaim = claims.FirstOrDefault(c => c.Type == "primary_role_id")?.Value;
            var roleNameClaim = claims.FirstOrDefault(c => c.Type == "primary_role_name")?.Value;
            
            if (int.TryParse(businessIdClaim, out int businessId) && 
                int.TryParse(roleIdClaim, out int roleId) &&
                !string.IsNullOrEmpty(businessNameClaim) &&
                !string.IsNullOrEmpty(roleNameClaim))
            {
                return new BusinessRoleInfo
                {
                    BusinessId = businessId,
                    BusinessName = businessNameClaim,
                    RoleId = roleId,
                    RoleName = roleNameClaim
                };
            }
            
            return null;
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
