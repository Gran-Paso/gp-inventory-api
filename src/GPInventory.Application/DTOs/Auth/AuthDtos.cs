namespace GPInventory.Application.DTOs.Auth;

public class LoginDto
{
    public string Email { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string? ClientApp { get; set; } // "gp-factory", "gp-expenses", "gp-inventory", "gran-paso"
}

public class RefreshTokenDto
{
    public string RefreshToken { get; set; } = string.Empty;
    public string? ClientApp { get; set; }
}

public class RegisterDto
{
    public string Email { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public char? Gender { get; set; }
    public DateTime? BirthDate { get; set; }
    public int? Phone { get; set; }
}

public class AuthResponseDto
{
    public string AccessToken { get; set; } = string.Empty;
    public string RefreshToken { get; set; } = string.Empty;
    public DateTime ExpiresAt { get; set; }
    public UserDto User { get; set; } = null!;
    public List<string> Permissions { get; set; } = new();
}

public class UserDto
{
    public int Id { get; set; }
    public string Email { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public char? Gender { get; set; }
    public DateTime? BirthDate { get; set; }
    public int? Phone { get; set; }
    public bool Active { get; set; }
    public string Role { get; set; } = string.Empty;
    public List<UserRoleDto> Roles { get; set; } = new List<UserRoleDto>();
    public List<int> BusinessIds { get; set; } = new();
    public List<BusinessInfoDto> Businesses { get; set; } = new();
}

public class BusinessInfoDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Logo { get; set; }
}

public class UserRoleDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public int BusinessId { get; set; }
    public string BusinessName { get; set; } = string.Empty;
}

public class ResetPasswordDto
{
    public string Email { get; set; } = string.Empty;
    public string NewPassword { get; set; } = string.Empty;
    public string Token { get; set; } = string.Empty;
}

/// <summary>
/// Clase para representar la información agrupada de negocio y rol del JWT
/// </summary>
public class BusinessRoleInfo
{
    public int BusinessId { get; set; }
    public string BusinessName { get; set; } = string.Empty;
    public int RoleId { get; set; }
    public string RoleName { get; set; } = string.Empty;
}

public class AssignRoleDto
{
    public string Email { get; set; } = string.Empty;
    public int RoleId { get; set; }
    public int BusinessId { get; set; }
}
