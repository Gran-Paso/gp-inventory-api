using System.ComponentModel.DataAnnotations;

namespace GPInventory.Domain.Entities;

public class User : BaseEntity
{
    [Required]
    [StringLength(255)]
    public string Mail { get; set; } = string.Empty;

    [Required]
    [StringLength(255)]
    public string Name { get; set; } = string.Empty;

    [Required]
    [StringLength(255)]
    public string LastName { get; set; } = string.Empty;

    public char? Gender { get; set; }

    public DateTime? BirthDate { get; set; }

    public int? Phone { get; set; }

    [Required]
    public string Password { get; set; } = string.Empty;

    [Required]
    [StringLength(255)]
    public string Salt { get; set; } = string.Empty;

    public bool Active { get; set; } = true;

    // Navigation properties
    public ICollection<UserHasBusiness> UserBusinesses { get; set; } = new List<UserHasBusiness>();

    public User()
    {
        Salt = string.Empty;
    }

    public User(string mail, string name, string lastName, string password, string salt)
    {
        Mail = mail;
        Name = name;
        LastName = lastName;
        Salt = salt;
        Password = HashPassword(password);
        Active = true;
    }

    private static string HashPassword(string password)
    {
        return BCrypt.Net.BCrypt.HashPassword(password, 12);
    }

    public bool VerifyPassword(string password)
    {
        try
        {
            // Check if password is null or empty
            if (string.IsNullOrEmpty(Password))
            {
                return false;
            }

            // First try: Check if it's a valid BCrypt hash format
            if (IsBCryptHash(Password))
            {
                try
                {
                    return BCrypt.Net.BCrypt.Verify(password, Password);
                }
                catch (BCrypt.Net.SaltParseException)
                {
                    // If BCrypt verification fails, log and fall through to other methods
                    System.Diagnostics.Debug.WriteLine($"BCrypt verification failed for hash: {Password.Substring(0, Math.Min(20, Password.Length))}");
                }
            }
            
            // Second try: Legacy password with separate salt
            if (!string.IsNullOrEmpty(Salt))
            {
                try
                {
                    // Try BCrypt with salt appended
                    return BCrypt.Net.BCrypt.Verify(password + Salt, Password);
                }
                catch (BCrypt.Net.SaltParseException)
                {
                    // If still fails, try simple hash comparison (for very old passwords)
                    try
                    {
                        var hashedInput = System.Convert.ToBase64String(
                            System.Security.Cryptography.SHA256.HashData(
                                System.Text.Encoding.UTF8.GetBytes(password + Salt)
                            )
                        );
                        return hashedInput == Password;
                    }
                    catch
                    {
                        // SHA256 also failed, continue to direct comparison
                    }
                }
            }
            
            // Third try: Direct comparison for plain text passwords (testing only)
            if (password == Password)
            {
                return true;
            }
            
            // Last resort: Try common hash formats
            return TryCommonHashFormats(password);
        }
        catch (Exception ex)
        {
            // Log the error for debugging
            System.Diagnostics.Debug.WriteLine($"Password verification failed: {ex.Message}");
            System.Diagnostics.Debug.WriteLine($"Password format: Length={Password?.Length}, Starts with={Password?.Substring(0, Math.Min(10, Password?.Length ?? 0))}");
            return false;
        }
    }

    private bool IsBCryptHash(string hash)
    {
        // BCrypt hashes start with $2, $2a, $2b, or $2y and have specific length
        return hash.Length >= 60 && 
               (hash.StartsWith("$2") || hash.StartsWith("$2a") || hash.StartsWith("$2b") || hash.StartsWith("$2y"));
    }

    private bool TryCommonHashFormats(string password)
    {
        try
        {
            // Try MD5
            using (var md5 = System.Security.Cryptography.MD5.Create())
            {
                var md5Hash = System.Convert.ToBase64String(md5.ComputeHash(System.Text.Encoding.UTF8.GetBytes(password)));
                if (md5Hash == Password) return true;
            }

            // Try SHA1
            using (var sha1 = System.Security.Cryptography.SHA1.Create())
            {
                var sha1Hash = System.Convert.ToBase64String(sha1.ComputeHash(System.Text.Encoding.UTF8.GetBytes(password)));
                if (sha1Hash == Password) return true;
            }

            // Try SHA256 without salt
            var sha256Hash = System.Convert.ToBase64String(
                System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(password))
            );
            if (sha256Hash == Password) return true;

            return false;
        }
        catch
        {
            return false;
        }
    }

    public void UpdatePassword(string newPassword)
    {
        Salt = string.Empty; // No longer needed for new passwords
        Password = HashPassword(newPassword);
        UpdatedAt = DateTime.UtcNow;
    }

    public string GetFullName()
    {
        return $"{Name} {LastName}";
    }
}
