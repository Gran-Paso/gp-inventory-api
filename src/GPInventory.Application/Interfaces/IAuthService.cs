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
}

public interface IPasswordService
{
    string HashPassword(string password, string salt);
    bool VerifyPassword(string password, string hashedPassword, string salt);
    string GenerateSalt();
}
