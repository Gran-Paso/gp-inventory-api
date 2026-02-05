using AutoMapper;
using GPInventory.Application.DTOs.Auth;
using GPInventory.Application.Interfaces;
using GPInventory.Domain.Entities;
using System.Security.Cryptography;
using Google.Apis.Auth;

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
        
        // Calculate app permissions based on roles
        userDto.AppPermissions = CalculateAppPermissions(userDto);
        
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

    public async Task<AuthResponseDto> GoogleLoginAsync(GoogleLoginDto googleLoginDto)
    {
        try
        {
            // Validar el token de Google
            var payload = await GoogleJsonWebSignature.ValidateAsync(googleLoginDto.GoogleToken);

            if (payload == null)
            {
                throw new InvalidOperationException("Invalid Google token");
            }

            // Buscar usuario por email
            var user = await _userRepository.GetByEmailWithRolesAsync(payload.Email);

            // Si el usuario no existe, crear uno nuevo (auto-registro)
            if (user == null)
            {
                // Separar nombre y apellido del nombre completo de Google
                var nameParts = payload.Name.Split(' ', 2);
                var firstName = nameParts[0];
                var lastName = nameParts.Length > 1 ? nameParts[1] : "";

                // Generar password aleatorio (no se usará, pero es requerido)
                var salt = _passwordService.GenerateSalt();
                var randomPassword = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));

                user = new User(
                    payload.Email,
                    firstName,
                    lastName,
                    randomPassword,
                    salt)
                {
                    Active = true,
                    // Puedes agregar foto de perfil si la usas: ProfilePicture = payload.Picture
                };

                await _userRepository.AddAsync(user);
                await _userRepository.SaveChangesAsync();

                // Recargar usuario con roles
                user = await _userRepository.GetByEmailWithRolesAsync(payload.Email);
            }

            if (!user!.Active)
            {
                throw new InvalidOperationException("User account is inactive");
            }

            var userDto = _mapper.Map<UserDto>(user);

            // Calculate app permissions based on roles
            userDto.AppPermissions = CalculateAppPermissions(userDto);

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
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Google authentication failed: {ex.Message}");
        }
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
        
        // Calculate app permissions based on roles
        userDto.AppPermissions = CalculateAppPermissions(userDto);
        
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
        if (user == null) return null;
        
        var userDto = _mapper.Map<UserDto>(user);
        userDto.AppPermissions = CalculateAppPermissions(userDto);
        return userDto;
    }

    private Dictionary<string, bool> CalculateAppPermissions(UserDto user)
    {
        // Super Admin tiene acceso a todo
        if (user.SystemRole == "super_admin")
        {
            return new Dictionary<string, bool>
            {
                { "gp-expenses", true },
                { "gp-inventory", true },
                { "gp-factory", true },
                { "gp-auth", true }
            };
        }

        // Admin tiene acceso a todo excepto puede tener restricciones específicas si se necesitan
        if (user.SystemRole == "admin")
        {
            return new Dictionary<string, bool>
            {
                { "gp-expenses", true },
                { "gp-inventory", true },
                { "gp-factory", true },
                { "gp-auth", true }
            };
        }

        // Get all unique role IDs from user's roles across all businesses
        var userRoleIds = user.Roles.Select(r => r.Id).Distinct().ToList();
        
        // Define role IDs
        // 1: Cofundador, 2: Dueño, 3: Administrador, 4: Vendedor, 5: Staff, 6: Contador, 7: RRHH, 8: Bodeguero
        
        // Role-based access rules:
        // - Cofundador, Dueño, Administrador: Access to ALL apps
        // - Contador: Only gp-expenses
        // - Bodeguero: gp-factory and gp-inventory
        // - Vendedor: ONLY gp-inventory
        // - Staff, RRHH: gp-factory and gp-inventory
        
        var adminRoles = new[] { 1, 2, 3 }; // Full access
        var isAdmin = userRoleIds.Any(roleId => adminRoles.Contains(roleId));
        var isContador = userRoleIds.Contains(6);
        var isBodeguero = userRoleIds.Contains(8);
        var isVendedor = userRoleIds.Contains(4);
        
        // Admin roles have access to everything
        if (isAdmin)
        {
            return new Dictionary<string, bool>
            {
                { "gp-expenses", true },
                { "gp-inventory", true },
                { "gp-factory", true },
                { "gp-auth", true }
            };
        }
        
        // Contador: Only expenses
        if (isContador)
        {
            return new Dictionary<string, bool>
            {
                { "gp-expenses", true },
                { "gp-inventory", false },
                { "gp-factory", false },
                { "gp-auth", true }
            };
        }
        
        // Vendedor: ONLY inventory
        if (isVendedor)
        {
            return new Dictionary<string, bool>
            {
                { "gp-expenses", false },
                { "gp-inventory", true },
                { "gp-factory", false },
                { "gp-auth", true }
            };
        }
        
        // Bodeguero: Only factory and inventory
        if (isBodeguero)
        {
            return new Dictionary<string, bool>
            {
                { "gp-expenses", false },
                { "gp-inventory", true },
                { "gp-factory", true },
                { "gp-auth", true }
            };
        }
        
        // Default: Factory and Inventory access (for Staff, RRHH, etc.)
        return new Dictionary<string, bool>
        {
            { "gp-expenses", false },
            { "gp-inventory", true },
            { "gp-factory", true },
            { "gp-auth", true }
        };
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

    public async Task UpdateProfileAsync(string userEmail, UpdateProfileDto updateDto)
    {
        var user = await _userRepository.GetByEmailAsync(userEmail);
        if (user == null)
        {
            throw new InvalidOperationException("Usuario no encontrado");
        }

        // Actualizar solo los campos proporcionados
        if (updateDto.Gender.HasValue)
        {
            user.Gender = updateDto.Gender;
        }
        if (updateDto.BirthDate.HasValue)
        {
            user.BirthDate = updateDto.BirthDate;
        }
        if (updateDto.Phone.HasValue)
        {
            user.Phone = updateDto.Phone;
        }

        await _userRepository.UpdateAsync(user);
        await _userRepository.SaveChangesAsync();
    }
}
