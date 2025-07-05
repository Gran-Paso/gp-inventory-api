using GPInventory.Application.DTOs.Auth;
using GPInventory.Application.Interfaces;
using GPInventory.Infrastructure.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace GPInventory.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class EmergencyController : ControllerBase
{
    private readonly ApplicationDbContext _context;
    private readonly ITokenService _tokenService;
    private readonly ILogger<EmergencyController> _logger;

    public EmergencyController(
        ApplicationDbContext context, 
        ITokenService tokenService,
        ILogger<EmergencyController> logger)
    {
        _context = context;
        _tokenService = tokenService;
        _logger = logger;
    }

    [HttpPost("force-password-reset")]
    public async Task<IActionResult> ForcePasswordReset([FromBody] ResetPasswordDto resetDto)
    {
        try
        {
            var user = await _context.Users
                .FirstOrDefaultAsync(u => u.Mail == resetDto.Email);

            if (user == null)
            {
                return NotFound(new { message = "User not found" });
            }

            _logger.LogInformation($"Current password for {resetDto.Email}: " +
                $"Length={user.Password?.Length}, " +
                $"Starts with={user.Password?.Substring(0, Math.Min(10, user.Password?.Length ?? 0))}");

            // Force update password directly using BCrypt
            user.Password = BCrypt.Net.BCrypt.HashPassword(resetDto.NewPassword, 12);
            user.Salt = string.Empty; // Clear salt as it's not needed for BCrypt
            user.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            _logger.LogInformation($"Password reset completed for {resetDto.Email}");

            return Ok(new { 
                message = "Password reset successfully", 
                email = resetDto.Email,
                newPasswordLength = user.Password.Length,
                isBCryptFormat = user.Password.StartsWith("$2")
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error during force password reset for {resetDto.Email}");
            return StatusCode(500, new { message = "An error occurred during password reset", error = ex.Message });
        }
    }

    [HttpGet("check-password-format/{email}")]
    public async Task<IActionResult> CheckPasswordFormat(string email)
    {
        try
        {
            var user = await _context.Users
                .FirstOrDefaultAsync(u => u.Mail == email);

            if (user == null)
            {
                return NotFound(new { message = "User not found" });
            }

            var info = new
            {
                email = user.Mail,
                passwordLength = user.Password?.Length ?? 0,
                passwordStart = user.Password?.Substring(0, Math.Min(15, user.Password?.Length ?? 0)),
                saltLength = user.Salt?.Length ?? 0,
                saltStart = user.Salt?.Substring(0, Math.Min(15, user.Salt?.Length ?? 0)),
                isBCryptFormat = user.Password?.StartsWith("$2") ?? false,
                active = user.Active
            };

            return Ok(info);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error checking password format for {email}");
            return StatusCode(500, new { message = "An error occurred", error = ex.Message });
        }
    }

    [HttpPost("assign-role")]
    public async Task<IActionResult> AssignRole([FromBody] AssignRoleDto assignRoleDto)
    {
        try
        {
            var user = await _context.Users
                .FirstOrDefaultAsync(u => u.Mail == assignRoleDto.Email);

            if (user == null)
            {
                return NotFound(new { message = "User not found" });
            }

            var role = await _context.Roles
                .FirstOrDefaultAsync(r => r.Id == assignRoleDto.RoleId);

            if (role == null)
            {
                return NotFound(new { message = "Role not found" });
            }

            var business = await _context.Businesses
                .FirstOrDefaultAsync(b => b.Id == assignRoleDto.BusinessId);

            if (business == null)
            {
                return NotFound(new { message = "Business not found" });
            }

            // Check if assignment already exists
            var existingAssignment = await _context.UserHasBusinesses
                .FirstOrDefaultAsync(ub => ub.UserId == user.Id && 
                                         ub.BusinessId == assignRoleDto.BusinessId && 
                                         ub.RoleId == assignRoleDto.RoleId);

            if (existingAssignment != null)
            {
                return BadRequest(new { message = "User already has this role in this business" });
            }

            // Create new assignment
            var userHasBusiness = new GPInventory.Domain.Entities.UserHasBusiness
            {
                UserId = user.Id,
                BusinessId = assignRoleDto.BusinessId,
                RoleId = assignRoleDto.RoleId
            };

            _context.UserHasBusinesses.Add(userHasBusiness);
            await _context.SaveChangesAsync();

            return Ok(new { 
                message = "Role assigned successfully",
                userId = user.Id,
                roleId = assignRoleDto.RoleId,
                businessId = assignRoleDto.BusinessId,
                roleName = role.Name,
                businessName = business.CompanyName
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error assigning role to user {assignRoleDto.Email}");
            return StatusCode(500, new { message = "An error occurred", error = ex.Message });
        }
    }

    [HttpGet("list-roles-and-businesses")]
    public async Task<IActionResult> ListRolesAndBusinesses()
    {
        try
        {
            var roles = await _context.Roles.ToListAsync();
            var businesses = await _context.Businesses.ToListAsync();

            return Ok(new { 
                roles = roles.Select(r => new { r.Id, r.Name }),
                businesses = businesses.Select(b => new { b.Id, Name = b.CompanyName })
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error listing roles and businesses");
            return StatusCode(500, new { message = "An error occurred", error = ex.Message });
        }
    }
}
