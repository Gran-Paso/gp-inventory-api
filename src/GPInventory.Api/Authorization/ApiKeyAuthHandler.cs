using GPInventory.Infrastructure.Data;
using Microsoft.AspNetCore.Authentication;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;

namespace GPInventory.Api.Authorization;

public class ApiKeyAuthOptions : AuthenticationSchemeOptions { }

/// <summary>
/// Manejador de autenticación por API Key para canales externos (Webadas, etc.).
/// Espera el header: X-Api-Key: &lt;clave&gt;
/// La clave se hashea con SHA-256 y se busca en la tabla business_api_keys.
/// </summary>
public class ApiKeyAuthHandler : AuthenticationHandler<ApiKeyAuthOptions>
{
    private const string ApiKeyHeaderName = "X-Api-Key";
    private readonly ApplicationDbContext _context;

    public ApiKeyAuthHandler(
        IOptionsMonitor<ApiKeyAuthOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder,
        ApplicationDbContext context)
        : base(options, logger, encoder)
    {
        _context = context;
    }

    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Headers.TryGetValue(ApiKeyHeaderName, out var rawKey) || string.IsNullOrWhiteSpace(rawKey))
        {
            return AuthenticateResult.Fail("Missing X-Api-Key header");
        }

        var keyHash = ComputeSha256(rawKey.ToString().Trim());

        var apiKey = await _context.BusinessApiKeys
            .Where(k => k.KeyHash == keyHash && k.Active)
            .FirstOrDefaultAsync();

        if (apiKey == null)
        {
            Logger.LogWarning("ApiKey auth failed: key not found or inactive");
            return AuthenticateResult.Fail("Invalid or inactive API Key");
        }

        if (apiKey.ExpiresAt.HasValue && apiKey.ExpiresAt.Value < DateTime.UtcNow)
        {
            Logger.LogWarning("ApiKey auth failed: key expired (keyId={KeyId})", apiKey.Id);
            return AuthenticateResult.Fail("API Key has expired");
        }

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, apiKey.Id.ToString()),
            new("business_id", apiKey.BusinessId.ToString()),
            new("api_key_id", apiKey.Id.ToString()),
            new("api_key_label", apiKey.Label ?? string.Empty),
        };

        // Agregar scopes como claims individuales
        if (!string.IsNullOrWhiteSpace(apiKey.Scopes))
        {
            try
            {
                var scopes = JsonSerializer.Deserialize<List<string>>(apiKey.Scopes);
                if (scopes != null)
                {
                    foreach (var scope in scopes)
                        claims.Add(new Claim("scope", scope));
                }
            }
            catch (Exception ex)
            {
                Logger.LogWarning(ex, "Could not parse scopes for api key {KeyId}", apiKey.Id);
            }
        }

        var identity = new ClaimsIdentity(claims, Scheme.Name);
        var principal = new ClaimsPrincipal(identity);
        var ticket = new AuthenticationTicket(principal, Scheme.Name);

        Logger.LogInformation("ApiKey auth succeeded: business_id={BusinessId}, label={Label}", apiKey.BusinessId, apiKey.Label);
        return AuthenticateResult.Success(ticket);
    }

    private static string ComputeSha256(string input)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(input));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }
}
