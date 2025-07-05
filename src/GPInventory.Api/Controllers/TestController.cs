using GPInventory.Api.TestUtils;
using Microsoft.AspNetCore.Mvc;

namespace GPInventory.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class TestController : ControllerBase
{
    [HttpGet("jwt-roles")]
    public IActionResult TestJwtRoles()
    {
        try
        {
            JwtRoleTestHelper.TestJwtRolesContent();
            return Ok("JWT roles test completed. Check console output.");
        }
        catch (Exception ex)
        {
            return BadRequest($"Error testing JWT roles: {ex.Message}");
        }
    }
}
