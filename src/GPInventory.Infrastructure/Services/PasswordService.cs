using GPInventory.Application.Interfaces;

namespace GPInventory.Infrastructure.Services;

public class PasswordService : IPasswordService
{
    public string HashPassword(string password, string salt)
    {
        // For new passwords, ignore salt and use BCrypt directly
        return BCrypt.Net.BCrypt.HashPassword(password, 12);
    }

    public bool VerifyPassword(string password, string hashedPassword, string salt)
    {
        try
        {
            // Try BCrypt verification first (for new passwords)
            return BCrypt.Net.BCrypt.Verify(password, hashedPassword);
        }
        catch (BCrypt.Net.SaltParseException)
        {
            // Fallback for legacy passwords with separate salt
            return BCrypt.Net.BCrypt.Verify(password + salt, hashedPassword);
        }
    }

    public string GenerateSalt()
    {
        // Salt is no longer needed for new passwords using BCrypt
        return string.Empty;
    }
}
