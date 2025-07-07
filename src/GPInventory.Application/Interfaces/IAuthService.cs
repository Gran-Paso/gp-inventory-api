using GPInventory.Application.DTOs.Auth;

namespace GPInventory.Application.Interfaces;

public interface IAuthService
{
    Task<AuthResponseDto> LoginAsync(LoginDto loginDto);
    Task<AuthResponseDto> RegisterAsync(RegisterDto registerDto);
    Task<bool> ValidateTokenAsync(string token);
    Task<UserDto?> GetUserByEmailAsync(string email);
    Task<AuthResponseDto> ResetPasswordAsync(ResetPasswordDto resetDto);
}

public interface ITokenService
{
    string GenerateToken(UserDto user);
    bool ValidateToken(string token);
    int? GetUserIdFromToken(string token);
    
    // Nuevos métodos para estructura mejorada del JWT
    List<BusinessRoleInfo> GetBusinessRolesFromToken(string token);
    BusinessRoleInfo? GetPrimaryBusinessFromToken(string token);
    bool HasAccessToBusiness(string token, int businessId);
    BusinessRoleInfo? GetRoleInBusiness(string token, int businessId);
}

public interface IPasswordService
{
    string HashPassword(string password, string salt);
    bool VerifyPassword(string password, string hashedPassword, string salt);
    string GenerateSalt();
}
