using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text.Json;
using GPInventory.Application.DTOs.Auth;
using GPInventory.Infrastructure.Services;
using Microsoft.Extensions.Configuration;

namespace GPInventory.Api.TestUtils;

public static class JwtRoleTestHelper
{
    public static void TestJwtRolesContent()
    {
        var configuration = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json")
            .Build();

        var tokenService = new TokenService(configuration);
        
        // Create a test user with roles
        var testUser = new UserDto
        {
            Id = 1,
            Email = "test@example.com",
            Name = "Test",
            LastName = "User",
            Active = true,
            Roles = new List<UserRoleDto>
            {
                new UserRoleDto
                {
                    Id = 1,
                    Name = "Administrator",
                    BusinessId = 1,
                    BusinessName = "Test Business"
                },
                new UserRoleDto
                {
                    Id = 2,
                    Name = "Manager",
                    BusinessId = 2,
                    BusinessName = "Another Business"
                }
            }
        };

        // Generate token
        var token = tokenService.GenerateToken(testUser);
        
        Console.WriteLine("Generated JWT Token:");
        Console.WriteLine(token);
        Console.WriteLine();
        
        // Parse and display token claims
        var handler = new JwtSecurityTokenHandler();
        var jsonToken = handler.ReadJwtToken(token);
        
        Console.WriteLine("JWT Claims:");
        foreach (var claim in jsonToken.Claims)
        {
            Console.WriteLine($"  {claim.Type}: {claim.Value}");
        }
        
        Console.WriteLine();
        Console.WriteLine("Role-specific claims found:");
        
        var roleClaims = jsonToken.Claims.Where(c => c.Type == ClaimTypes.Role).ToList();
        var roleIdClaims = jsonToken.Claims.Where(c => c.Type == "roleId").ToList();
        var businessIdClaims = jsonToken.Claims.Where(c => c.Type == "businessId").ToList();
        var businessNameClaims = jsonToken.Claims.Where(c => c.Type == "businessName").ToList();
        
        Console.WriteLine($"  Role names: {string.Join(", ", roleClaims.Select(c => c.Value))}");
        Console.WriteLine($"  Role IDs: {string.Join(", ", roleIdClaims.Select(c => c.Value))}");
        Console.WriteLine($"  Business IDs: {string.Join(", ", businessIdClaims.Select(c => c.Value))}");
        Console.WriteLine($"  Business names: {string.Join(", ", businessNameClaims.Select(c => c.Value))}");
        
        // Check for business-specific role claims
        var businessRoleClaims = jsonToken.Claims.Where(c => c.Type.StartsWith("role:")).ToList();
        Console.WriteLine($"  Business-specific roles: {string.Join(", ", businessRoleClaims.Select(c => $"{c.Type}={c.Value}"))}");
    }
}
