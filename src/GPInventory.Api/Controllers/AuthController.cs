using GPInventory.Application.DTOs.Auth;
using GPInventory.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Cors;
using Microsoft.AspNetCore.Mvc;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;

namespace GPInventory.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[EnableCors("AllowFrontend")]
public class AuthController : ControllerBase
{
    private readonly IAuthService _authService;
    private readonly ITokenService _tokenService;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<AuthController> _logger;

    public AuthController(IAuthService authService, ITokenService tokenService, IServiceScopeFactory scopeFactory, ILogger<AuthController> logger)
    {
        _authService = authService;
        _tokenService = tokenService;
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    [HttpGet("cors-test")]
    public IActionResult CorsTest()
    {
        return Ok(new { message = "CORS is working!", timestamp = DateTime.UtcNow });
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginDto loginDto)
    {
        try
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var result = await _authService.LoginAsync(loginDto);
            return Ok(result);
        }
        catch (UnauthorizedAccessException ex)
        {
            _logger.LogWarning("Login failed for email: {Email}. Reason: {Reason}", loginDto.Email, ex.Message);
            return Unauthorized(new { message = "Invalid credentials" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during login for email: {Email}", loginDto.Email);
            return StatusCode(500, new { message = "An error occurred during login" });
        }
    }

    [HttpPost("google-login")]
    public async Task<IActionResult> GoogleLogin([FromBody] GoogleLoginDto googleLoginDto)
    {
        try
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var result = await _authService.GoogleLoginAsync(googleLoginDto);
            return Ok(result);
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning("Google login failed. Reason: {Reason}", ex.Message);
            return BadRequest(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during Google login");
            return StatusCode(500, new { message = "An error occurred during Google login" });
        }
    }

    [HttpPost("register")]
    public async Task<IActionResult> Register([FromBody] RegisterDto registerDto)
    {
        try
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var result = await _authService.RegisterAsync(registerDto);
            return Ok(result);
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning("Registration failed for email: {Email}. Reason: {Reason}", registerDto.Email, ex.Message);
            return BadRequest(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during registration for email: {Email}", registerDto.Email);
            return StatusCode(500, new { message = "An error occurred during registration" });
        }
    }

    [HttpPost("validate-token")]
    [Authorize]
    public async Task<IActionResult> ValidateToken()
    {
        try
        {
            var token = Request.Headers["Authorization"].ToString().Replace("Bearer ", "");
            
            if (string.IsNullOrEmpty(token))
            {
                return Unauthorized(new { message = "Token is required" });
            }

            var isValid = await _authService.ValidateTokenAsync(token);
            
            if (!isValid)
            {
                return Unauthorized(new { message = "Invalid token" });
            }

            return Ok(new { message = "Token is valid", valid = true });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during token validation");
            return StatusCode(500, new { message = "An error occurred during token validation" });
        }
    }

    [HttpGet("me")]
    [Authorize]
    public async Task<IActionResult> GetCurrentUser()
    {
        try
        {
            var userEmail = User.FindFirst(System.Security.Claims.ClaimTypes.Email)?.Value;
            
            if (string.IsNullOrEmpty(userEmail))
            {
                return Unauthorized(new { message = "User not found in token" });
            }

            var user = await _authService.GetUserByEmailAsync(userEmail);
            
            if (user == null)
            {
                return NotFound(new { message = "User not found" });
            }

            return Ok(user);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting current user");
            return StatusCode(500, new { message = "An error occurred while getting user information" });
        }
    }

    [HttpPost("forgot-password")]
    public async Task<IActionResult> ForgotPassword([FromBody] ForgotPasswordDto dto)
    {
        try
        {
            await _authService.ForgotPasswordAsync(dto);
            // Always 200 — never reveal whether the email exists
            return Ok(new { message = "Si el correo está registrado, recibirás un enlace en los próximos minutos." });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during forgot-password for email: {Email}", dto.Email);
            return StatusCode(500, new { message = "Error al procesar la solicitud" });
        }
    }

    [HttpGet("verify-email")]
    public async Task<IActionResult> VerifyEmail([FromQuery] string token)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(token))
                return BadRequest(new { message = "Token requerido." });

            var success = await _authService.VerifyEmailAsync(token);
            if (!success)
                return BadRequest(new { message = "El enlace de verificación es inválido o ha expirado." });

            return Ok(new { message = "Correo verificado correctamente." });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during email verification");
            return StatusCode(500, new { message = "Error al verificar el correo." });
        }
    }

    [HttpPost("reset-password")]
    public async Task<IActionResult> ResetPassword([FromBody] ResetPasswordDto resetDto)
    {
        try
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);            }

            var result = await _authService.ResetPasswordAsync(resetDto);
            return Ok(result);
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning("Password reset failed for email: {Email}. Reason: {Reason}", resetDto.Email, ex.Message);
            return BadRequest(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during password reset for email: {Email}", resetDto.Email);
            return StatusCode(500, new { message = "An error occurred during password reset" });
        }
    }

    [Authorize]
    [HttpPut("update-profile")]
    public async Task<IActionResult> UpdateProfile([FromBody] UpdateProfileDto updateDto)
    {
        try
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            // Obtener el email del usuario desde el token JWT
            var userEmail = User.FindFirst(System.Security.Claims.ClaimTypes.Email)?.Value;
            
            _logger.LogInformation("Update profile request for user: {Email}", userEmail);
            _logger.LogInformation("Profile data - Gender: {Gender}, BirthDate: {BirthDate}, Phone: {Phone}", 
                updateDto.Gender, updateDto.BirthDate, updateDto.Phone);
            
            if (string.IsNullOrEmpty(userEmail))
            {
                _logger.LogWarning("User email not found in token");
                return Unauthorized(new { message = "User email not found in token" });
            }

            await _authService.UpdateProfileAsync(userEmail, updateDto);
            _logger.LogInformation("Profile updated successfully for user: {Email}", userEmail);
            return Ok(new { message = "Profile updated successfully" });
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning("Profile update failed for user. Reason: {Reason}", ex.Message);
            return BadRequest(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during profile update");
            return StatusCode(500, new { message = "An error occurred during profile update" });
        }
    }

    /// <summary>
    /// SSE endpoint: notifies the client when a business is assigned to their account.
    /// The JWT token is passed as a query param because EventSource cannot set headers.
    /// </summary>
    [HttpGet("sse/business-watch")]
    public async Task WatchBusinessAssignment([FromQuery] string token, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(token) || !_tokenService.ValidateToken(token))
        {
            Response.StatusCode = 401;
            await Response.WriteAsync("event: error\ndata: unauthorized\n\n");
            return;
        }

        // Extract email from JWT claims (read-only, already validated above)
        var handler = new JwtSecurityTokenHandler();
        var jwt = handler.ReadJwtToken(token);
        var email = jwt.Claims
            .FirstOrDefault(c => c.Type == ClaimTypes.Email || c.Type == "email")?.Value;

        if (string.IsNullOrEmpty(email))
        {
            Response.StatusCode = 401;
            return;
        }

        Response.ContentType = "text/event-stream";
        Response.Headers["Cache-Control"] = "no-cache";
        Response.Headers["X-Accel-Buffering"] = "no";
        Response.Headers["Connection"] = "keep-alive";

        var deadline = DateTime.UtcNow.AddMinutes(30);

        try
        {
            while (!ct.IsCancellationRequested && DateTime.UtcNow < deadline)
            {
                // Create a fresh DI scope per iteration so EF Core doesn't return cached entities
                await using var scope = _scopeFactory.CreateAsyncScope();
                var authService = scope.ServiceProvider.GetRequiredService<IAuthService>();
                var user = await authService.GetUserByEmailAsync(email);

                _logger.LogInformation("SSE business-watch: email={Email} businesses={Count} roles={Roles}",
                    email,
                    user?.Businesses?.Count ?? -1,
                    user?.Roles?.Count ?? -1);

                if (user?.Roles?.Count > 0)
                {
                    await Response.WriteAsync("event: business_assigned\ndata: ok\n\n");
                    await Response.Body.FlushAsync(ct);
                    break;
                }

                // Keepalive comment — prevents browser/proxy from closing the connection
                await Response.WriteAsync(": ping\n\n");
                await Response.Body.FlushAsync(ct);

                await Task.Delay(5_000, ct);
            }
        }
        catch (OperationCanceledException)
        {
            // Client disconnected — expected, no action needed
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "SSE business-watch error for {Email}", email);
        }
    }
}
