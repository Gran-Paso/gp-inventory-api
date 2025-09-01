using AutoMapper;
using GPInventory.Application.DTOs.Auth;
using GPInventory.Application.Interfaces;
using GPInventory.Domain.Entities;
using System.Security.Cryptography;

namespace GPInventory.Application.Services;

public class AuthService : IAuthService
{
    private readonly IUserRepository _userRepository;
    private readonly ITokenService _tokenService;
    private readonly IPasswordService _passwordService;
    private readonly IMapper _mapper;

    public AuthService(
        IUserRepository userRepository,
        ITokenService tokenService,
        IPasswordService passwordService,
        IMapper mapper)
    {
        _userRepository = userRepository;
        _tokenService = tokenService;
        _passwordService = passwordService;
        _mapper = mapper;
    }

    public async Task<AuthResponseDto> LoginAsync(LoginDto loginDto)
    {
        var user = await _userRepository.GetByEmailWithRolesAsync(loginDto.Email);
        
        if (user == null || !user.Active)
        {
            throw new UnauthorizedAccessException("Invalid credentials");
        }

        // Add detailed logging for password verification
        try
        {
            if (!user.VerifyPassword(loginDto.Password))
            {
                throw new UnauthorizedAccessException("Invalid credentials");
            }
        }
        catch (Exception ex)
        {
            // Log detailed error information
            System.Diagnostics.Debug.WriteLine($"Password verification failed for user {loginDto.Email}");
            System.Diagnostics.Debug.WriteLine($"Password length: {user.Password?.Length ?? 0}");
            System.Diagnostics.Debug.WriteLine($"Password starts with: {user.Password?.Substring(0, Math.Min(10, user.Password.Length)) ?? "null"}");
            System.Diagnostics.Debug.WriteLine($"Salt length: {user.Salt?.Length ?? 0}");
            System.Diagnostics.Debug.WriteLine($"Exception: {ex.Message}");
            
            throw new UnauthorizedAccessException("Invalid credentials - password format issue");
        }

        var userDto = _mapper.Map<UserDto>(user);
        var token = _tokenService.GenerateToken(userDto);

        return new AuthResponseDto
        {
            AccessToken = token,
            RefreshToken = "", // Se generará en el controller
            ExpiresAt = DateTime.UtcNow.AddHours(8),
            User = userDto,
            Permissions = new List<string>()
        };
    }

    public async Task<AuthResponseDto> RegisterAsync(RegisterDto registerDto)
    {
        if (await _userRepository.ExistsAsync(registerDto.Email))
        {
            throw new InvalidOperationException("User with this email already exists");
        }

        var salt = _passwordService.GenerateSalt();
        var hashedPassword = _passwordService.HashPassword(registerDto.Password, salt);

        var user = new User(
            registerDto.Email,
            registerDto.Name,
            registerDto.LastName,
            registerDto.Password, // Pass raw password, it will be hashed in constructor
            salt)
        {
            Gender = registerDto.Gender,
            BirthDate = registerDto.BirthDate,
            Phone = registerDto.Phone,
            Active = true
        };

        await _userRepository.AddAsync(user);
        await _userRepository.SaveChangesAsync();

        var userDto = _mapper.Map<UserDto>(user);
        var token = _tokenService.GenerateToken(userDto);

        return new AuthResponseDto
        {
            AccessToken = token,
            RefreshToken = "", // Se generará en el controller
            ExpiresAt = DateTime.UtcNow.AddHours(8),
            User = userDto,
            Permissions = new List<string>()
        };
    }

    public Task<bool> ValidateTokenAsync(string token)
    {
        return Task.FromResult(_tokenService.ValidateToken(token));
    }

    public async Task<UserDto?> GetUserByEmailAsync(string email)
    {
        var user = await _userRepository.GetByEmailWithRolesAsync(email);
        return user != null ? _mapper.Map<UserDto>(user) : null;
    }

    public async Task<AuthResponseDto> ResetPasswordAsync(ResetPasswordDto resetDto)
    {
        var user = await _userRepository.GetByEmailWithRolesAsync(resetDto.Email);
        
        if (user == null)
        {
            throw new InvalidOperationException("User not found");
        }

        // Update password using BCrypt
        user.UpdatePassword(resetDto.NewPassword);
        
        await _userRepository.UpdateAsync(user);
        await _userRepository.SaveChangesAsync();

        var userDto = _mapper.Map<UserDto>(user);
        var token = _tokenService.GenerateToken(userDto);

        return new AuthResponseDto
        {
            AccessToken = token,
            RefreshToken = "", // Se generará en el controller
            ExpiresAt = DateTime.UtcNow.AddHours(8),
            User = userDto,
            Permissions = new List<string>()
        };
    }

    public Task<AuthResponseDto> RefreshTokenAsync(RefreshTokenDto refreshDto)
    {
        // Por ahora, implementación básica que retorna error
        // Se debe implementar con lógica de refresh tokens cuando se agregue el repositorio
        throw new UnauthorizedAccessException("Refresh token functionality not implemented yet");
    }

    public Task LogoutAsync(string userEmail, string? clientApp = null)
    {
        // Por ahora, implementación básica
        // Se debe implementar con lógica de revocación de tokens cuando se agregue el repositorio
        return Task.CompletedTask;
    }
}
