namespace GPInventory.Application.DTOs.Auth;

public class LoginDto
{
    public string Email { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
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
    public string Token { get; set; } = string.Empty;
    public UserDto User { get; set; } = null!;
}

public class UserDto
{
    public int Id { get; set; }
    public string Email { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public char? Gender { get; set; }
    public DateTime? BirthDate { get; set; }
    public int? Phone { get; set; }
    public bool Active { get; set; }
    public List<UserRoleDto> Roles { get; set; } = new List<UserRoleDto>();
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
