#pragma warning disable CS8601 // Possible null reference assignment for Dictionary<string, object> values
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Cors;
using Microsoft.AspNetCore.Mvc;
using MySqlConnector;
using System.Data;

namespace GPInventory.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[EnableCors("AllowFrontend")]
[Authorize] // Cambiado: solo requiere estar autenticado, verificamos systemRole en cada método
public class AdminController : ControllerBase
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<AdminController> _logger;

    public AdminController(IConfiguration configuration, ILogger<AdminController> logger)
    {
        _configuration = configuration;
        _logger = logger;
    }

    private MySqlConnection GetConnection()
    {
        return new MySqlConnection(_configuration.GetConnectionString("DefaultConnection"));
    }

    private bool IsSuperAdmin()
    {
        var systemRole = User.Claims.FirstOrDefault(c => c.Type == "systemRole")?.Value;
        _logger.LogInformation($"Checking super_admin access. SystemRole claim: {systemRole}");
        return systemRole == "super_admin";
    }

    /// <summary>
    /// Obtiene todos los usuarios con sus negocios y roles asociados
    /// </summary>
    [HttpGet("users")]
    public async Task<IActionResult> GetAllUsers()
    {
        if (!IsSuperAdmin())
        {
            _logger.LogWarning($"Unauthorized access attempt to GetAllUsers by user {User.Identity?.Name}");
            return Forbid();
        }

        try
        {
            var users = new List<object>();

            using var conn = GetConnection();
            await conn.OpenAsync();

            var query = @"
                SELECT 
                    u.id,
                    u.mail,
                    u.name,
                    u.lastname,
                    CONCAT(u.name, ' ', u.lastname) as full_name,
                    u.gender,
                    u.birthdate,
                    u.phone,
                    u.active,
                    u.system_role,
                    u.created_at,
                    u.updated_at
                FROM user u
                ORDER BY u.name, u.lastname";

            using var cmd = new MySqlCommand(query, conn);
            using var reader = await cmd.ExecuteReaderAsync();

            var userList = new List<Dictionary<string, object>>();
            while (await reader.ReadAsync())
            {
#pragma warning disable CS8601
                var user = new Dictionary<string, object>
                {
                    ["id"] = reader.GetInt32("id"),
                    ["email"] = reader.GetString("mail")!,
                    ["name"] = reader.GetString("name")!,
                    ["lastName"] = reader.GetString("lastname")!,
                    ["fullName"] = reader.GetString("full_name")!,
                    ["gender"] = reader.IsDBNull("gender") ? (object)"" : reader.GetString("gender")!,
                    ["birthDate"] = reader.IsDBNull("birthdate") ? (object)"" : reader.GetDateTime("birthdate").ToString("yyyy-MM-dd")!,
                    ["phone"] = reader.IsDBNull("phone") ? (object)0 : reader.GetInt32("phone"),
                    ["active"] = reader.GetBoolean("active"),
                    ["systemRole"] = reader.GetString("system_role")!,
                    ["createdAt"] = reader.GetDateTime("created_at").ToString("yyyy-MM-dd HH:mm:ss")!,
                    ["updatedAt"] = reader.GetDateTime("updated_at").ToString("yyyy-MM-dd HH:mm:ss")!
                };
#pragma warning restore CS8601
                userList.Add(user);
            }

            reader.Close();

            // Obtener negocios y roles para cada usuario
            foreach (var user in userList)
            {
                var userId = (int)user["id"];
                
                var businessQuery = @"
                    SELECT DISTINCT
                        b.id as business_id,
                        b.company_name as business_name,
                        b.company_name,
                        b.logo
                    FROM user_has_business ub
                    INNER JOIN business b ON ub.id_business = b.id
                    WHERE ub.id_user = @UserId AND b.active = 1";

                using var businessCmd = new MySqlCommand(businessQuery, conn);
                businessCmd.Parameters.AddWithValue("@UserId", userId);
                using var businessReader = await businessCmd.ExecuteReaderAsync();

                var businesses = new List<Dictionary<string, object>>();
                while (await businessReader.ReadAsync())
                {
#pragma warning disable CS8601
                    businesses.Add(new Dictionary<string, object>
                    {
                        ["businessId"] = businessReader.GetInt32("business_id"),
                        ["businessName"] = businessReader.GetString("business_name")!,
                        ["companyName"] = businessReader.IsDBNull("company_name") ? (object)"" : businessReader.GetString("company_name")!,
                        ["logo"] = businessReader.IsDBNull("logo") ? (object)"" : businessReader.GetString("logo")!
                    });
#pragma warning restore CS8601
                }
                businessReader.Close();

                // Obtener roles para cada negocio
                var rolesQuery = @"
                    SELECT 
                        ub.id_role as role_id,
                        r.name as role_name,
                        ub.id_business as business_id,
                        b.company_name as business_name
                    FROM user_has_business ub
                    INNER JOIN role r ON ub.id_role = r.id
                    INNER JOIN business b ON ub.id_business = b.id
                    WHERE ub.id_user = @UserId";

                using var rolesCmd = new MySqlCommand(rolesQuery, conn);
                rolesCmd.Parameters.AddWithValue("@UserId", userId);
                using var rolesReader = await rolesCmd.ExecuteReaderAsync();

                var roles = new List<Dictionary<string, object>>();
                while (await rolesReader.ReadAsync())
                {
                    roles.Add(new Dictionary<string, object>
                    {
                        ["roleId"] = rolesReader.GetInt32("role_id"),
                        ["roleName"] = rolesReader.GetString("role_name"),
                        ["businessId"] = rolesReader.GetInt32("business_id"),
                        ["businessName"] = rolesReader.GetString("business_name")
                    });
                }
                rolesReader.Close();

                user["businesses"] = businesses;
                user["roles"] = roles;
                user["businessCount"] = businesses.Count;
                user["roleCount"] = roles.Count;
            }

            return Ok(userList);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting all users");
            return StatusCode(500, new { message = "Error retrieving users", error = ex.Message });
        }
    }

    /// <summary>
    /// Obtiene un usuario específico por ID con toda su información
    /// </summary>
    [HttpGet("users/{id}")]
    public async Task<IActionResult> GetUserById(int id)
    {
        if (!IsSuperAdmin()) return Forbid();

        try
        {
            using var conn = GetConnection();
            await conn.OpenAsync();

            var query = @"
                SELECT 
                    u.id,
                    u.mail,
                    u.name,
                    u.lastname,
                    CONCAT(u.name, ' ', u.lastname) as full_name,
                    u.gender,
                    u.birthdate,
                    u.phone,
                    u.active,
                    u.system_role,
                    u.created_at,
                    u.updated_at
                FROM user u
                WHERE u.id = @Id";

            using var cmd = new MySqlCommand(query, conn);
            cmd.Parameters.AddWithValue("@Id", id);
            using var reader = await cmd.ExecuteReaderAsync();

            if (!await reader.ReadAsync())
            {
                return NotFound(new { message = "User not found" });
            }

#pragma warning disable CS8601
            var user = new Dictionary<string, object>
            {
                ["id"] = reader.GetInt32("id"),
                ["email"] = reader.GetString("mail")!,
                ["name"] = reader.GetString("name")!,
                ["lastName"] = reader.GetString("lastname")!,
                ["fullName"] = reader.GetString("full_name")!,
                ["gender"] = reader.IsDBNull("gender") ? (object)"" : reader.GetString("gender")!,
                ["birthDate"] = reader.IsDBNull("birthdate") ? (object)"" : reader.GetDateTime("birthdate").ToString("yyyy-MM-dd")!,
                ["phone"] = reader.IsDBNull("phone") ? (object)0 : reader.GetInt32("phone"),
                ["active"] = reader.GetBoolean("active"),
                ["systemRole"] = reader.GetString("system_role")!,
                ["createdAt"] = reader.GetDateTime("created_at").ToString("yyyy-MM-dd HH:mm:ss")!,
                ["updatedAt"] = reader.GetDateTime("updated_at").ToString("yyyy-MM-dd HH:mm:ss")!
            };
#pragma warning restore CS8601

            reader.Close();

            // Obtener negocios y roles
            var businessQuery = @"
                SELECT DISTINCT
                    b.id as business_id,
                    b.company_name as business_name,
                    b.company_name,
                    b.logo
                FROM user_has_business ub
                INNER JOIN business b ON ub.id_business = b.id
                WHERE ub.id_user = @UserId AND b.active = 1";

            using var businessCmd = new MySqlCommand(businessQuery, conn);
            businessCmd.Parameters.AddWithValue("@UserId", id);
            using var businessReader = await businessCmd.ExecuteReaderAsync();

            var businesses = new List<Dictionary<string, object>>();
            while (await businessReader.ReadAsync())
            {
#pragma warning disable CS8601
                businesses.Add(new Dictionary<string, object>
                {
                    ["businessId"] = businessReader.GetInt32("business_id"),
                    ["businessName"] = businessReader.GetString("business_name")!,
                    ["companyName"] = businessReader.IsDBNull("company_name") ? (object)"" : businessReader.GetString("company_name")!,
                    ["logo"] = businessReader.IsDBNull("logo") ? (object)"" : businessReader.GetString("logo")!
                });
#pragma warning restore CS8601
            }
            businessReader.Close();

            var rolesQuery = @"
                SELECT 
                    ub.id_role as role_id,
                    r.name as role_name,
                    ub.id_business as business_id,
                    b.company_name as business_name
                FROM user_has_business ub
                INNER JOIN role r ON ub.id_role = r.id
                INNER JOIN business b ON ub.id_business = b.id
                WHERE ub.id_user = @UserId";

            using var rolesCmd = new MySqlCommand(rolesQuery, conn);
            rolesCmd.Parameters.AddWithValue("@UserId", id);
            using var rolesReader = await rolesCmd.ExecuteReaderAsync();

            var roles = new List<Dictionary<string, object>>();
            while (await rolesReader.ReadAsync())
            {
                roles.Add(new Dictionary<string, object>
                {
                    ["roleId"] = rolesReader.GetInt32("role_id"),
                    ["roleName"] = rolesReader.GetString("role_name"),
                    ["businessId"] = rolesReader.GetInt32("business_id"),
                    ["businessName"] = rolesReader.GetString("business_name")
                });
            }
            rolesReader.Close();

            user["businesses"] = businesses;
            user["roles"] = roles;

            return Ok(user);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting user {UserId}", id);
            return StatusCode(500, new { message = "Error retrieving user", error = ex.Message });
        }
    }

    /// <summary>
    /// Obtiene todos los negocios disponibles
    /// </summary>
    [HttpGet("businesses")]
    public async Task<IActionResult> GetAllBusinesses()
    {
        if (!IsSuperAdmin()) return Forbid();

        try
        {
            using var conn = GetConnection();
            await conn.OpenAsync();

            var query = @"
                SELECT 
                    id,
                    company_name,
                    company_name as name,
                    logo,
                    active,
                    created_at
                FROM business
                ORDER BY company_name";

            using var cmd = new MySqlCommand(query, conn);
            using var reader = await cmd.ExecuteReaderAsync();

            var businesses = new List<Dictionary<string, object>>();
            while (await reader.ReadAsync())
            {
#pragma warning disable CS8601
                businesses.Add(new Dictionary<string, object>
                {
                    ["id"] = reader.GetInt32("id"),
                    ["name"] = reader.IsDBNull("company_name") ? (object)"" : reader.GetString("company_name")!,
                    ["companyName"] = reader.IsDBNull("company_name") ? (object)"" : reader.GetString("company_name")!,
                    ["logo"] = reader.IsDBNull("logo") ? (object)"" : reader.GetString("logo")!,
                    ["active"] = reader.GetBoolean("active"),
                    ["createdAt"] = reader.GetDateTime("created_at").ToString("yyyy-MM-dd HH:mm:ss")!
                });
#pragma warning restore CS8601
            }

            return Ok(businesses);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting businesses");
            return StatusCode(500, new { message = "Error retrieving businesses", error = ex.Message });
        }
    }

    /// <summary>
    /// Crea un nuevo negocio
    /// </summary>
    [HttpPost("businesses")]
    public async Task<IActionResult> CreateBusiness([FromBody] CreateBusinessRequest request)
    {
        if (!IsSuperAdmin()) return Forbid();

        try
        {
            if (string.IsNullOrWhiteSpace(request.CompanyName))
            {
                return BadRequest(new { message = "Company name is required" });
            }

            using var conn = GetConnection();
            await conn.OpenAsync();

            var insertQuery = @"
                INSERT INTO business (company_name, logo, active, created_at)
                VALUES (@CompanyName, @Logo, 1, @Now)";

            using var cmd = new MySqlCommand(insertQuery, conn);
            cmd.Parameters.AddWithValue("@CompanyName", request.CompanyName);
            cmd.Parameters.AddWithValue("@Logo", string.IsNullOrWhiteSpace(request.Logo) ? DBNull.Value : request.Logo);
            cmd.Parameters.AddWithValue("@Now", DateTime.UtcNow);

            await cmd.ExecuteNonQueryAsync();
            var newId = cmd.LastInsertedId;

            _logger.LogInformation("Business created with ID {BusinessId}", newId);

            return Ok(new { 
                message = "Business created successfully", 
                id = newId,
                companyName = request.CompanyName
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating business");
            return StatusCode(500, new { message = "Error creating business", error = ex.Message });
        }
    }

    /// <summary>
    /// Actualiza un negocio existente
    /// </summary>
    [HttpPut("businesses/{id}")]
    public async Task<IActionResult> UpdateBusiness(int id, [FromBody] UpdateBusinessRequest request)
    {
        if (!IsSuperAdmin()) return Forbid();

        try
        {
            if (string.IsNullOrWhiteSpace(request.CompanyName))
            {
                return BadRequest(new { message = "Company name is required" });
            }

            using var conn = GetConnection();
            await conn.OpenAsync();

            var updateQuery = @"
                UPDATE business 
                SET company_name = @CompanyName, 
                    logo = @Logo
                WHERE id = @Id";

            using var cmd = new MySqlCommand(updateQuery, conn);
            cmd.Parameters.AddWithValue("@CompanyName", request.CompanyName);
            cmd.Parameters.AddWithValue("@Logo", string.IsNullOrWhiteSpace(request.Logo) ? DBNull.Value : request.Logo);
            cmd.Parameters.AddWithValue("@Id", id);

            var rowsAffected = await cmd.ExecuteNonQueryAsync();

            if (rowsAffected > 0)
            {
                _logger.LogInformation("Business {BusinessId} updated", id);
                return Ok(new { message = "Business updated successfully", id });
            }

            return NotFound(new { message = "Business not found" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating business {BusinessId}", id);
            return StatusCode(500, new { message = "Error updating business", error = ex.Message });
        }
    }

    /// <summary>
    /// Activa o desactiva un negocio
    /// </summary>
    [HttpPatch("businesses/{id}/active")]
    public async Task<IActionResult> UpdateBusinessActiveStatus(int id, [FromBody] UpdateActiveStatusRequest request)
    {
        if (!IsSuperAdmin()) return Forbid();

        try
        {
            using var conn = GetConnection();
            await conn.OpenAsync();

            var updateQuery = @"
                UPDATE business 
                SET active = @Active
                WHERE id = @Id";

            using var cmd = new MySqlCommand(updateQuery, conn);
            cmd.Parameters.AddWithValue("@Active", request.Active);
            cmd.Parameters.AddWithValue("@Id", id);

            var rowsAffected = await cmd.ExecuteNonQueryAsync();

            if (rowsAffected > 0)
            {
                _logger.LogInformation("Business {BusinessId} active status updated to {Active}", id, request.Active);
                return Ok(new { message = "Business status updated successfully", id, active = request.Active });
            }

            return NotFound(new { message = "Business not found" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating business status {BusinessId}", id);
            return StatusCode(500, new { message = "Error updating business status", error = ex.Message });
        }
    }

    /// <summary>
    /// Obtiene todos los roles disponibles
    /// </summary>
    [HttpGet("roles")]
    public async Task<IActionResult> GetAllRoles()
    {
        if (!IsSuperAdmin()) return Forbid();

        try
        {
            using var conn = GetConnection();
            await conn.OpenAsync();

            var query = @"
                SELECT 
                    id,
                    name
                FROM role
                ORDER BY name";

            using var cmd = new MySqlCommand(query, conn);
            using var reader = await cmd.ExecuteReaderAsync();

            var roles = new List<Dictionary<string, object>>();
            while (await reader.ReadAsync())
            {
                roles.Add(new Dictionary<string, object>
                {
                    ["id"] = reader.GetInt32("id"),
                    ["name"] = reader.GetString("name")
                });
            }

            return Ok(roles);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting roles");
            return StatusCode(500, new { message = "Error retrieving roles", error = ex.Message });
        }
    }

    /// <summary>
    /// Asigna un usuario a un negocio con un rol específico
    /// </summary>
    [HttpPost("users/{userId}/assign-business")]
    public async Task<IActionResult> AssignBusinessToUser(int userId, [FromBody] AssignBusinessRequest request)
    {
        if (!IsSuperAdmin()) return Forbid();

        try
        {
            if (request.BusinessId <= 0 || request.RoleId <= 0)
            {
                return BadRequest(new { message = "BusinessId and RoleId are required" });
            }

            using var conn = GetConnection();
            await conn.OpenAsync();

            // Verificar que el usuario existe
            var userCheck = "SELECT COUNT(*) FROM user WHERE id = @UserId";
            using var userCmd = new MySqlCommand(userCheck, conn);
            userCmd.Parameters.AddWithValue("@UserId", userId);
            var userExists = Convert.ToInt32(await userCmd.ExecuteScalarAsync()) > 0;

            if (!userExists)
            {
                return NotFound(new { message = "User not found" });
            }

            // Verificar que el negocio existe y está activo
            var businessCheck = "SELECT COUNT(*) FROM business WHERE id = @BusinessId AND active = 1";
            using var businessCmd = new MySqlCommand(businessCheck, conn);
            businessCmd.Parameters.AddWithValue("@BusinessId", request.BusinessId);
            var businessExists = Convert.ToInt32(await businessCmd.ExecuteScalarAsync()) > 0;

            if (!businessExists)
            {
                return NotFound(new { message = "Business not found or inactive" });
            }

            // Verificar que el rol existe
            var roleCheck = "SELECT COUNT(*) FROM role WHERE id = @RoleId";
            using var roleCmd = new MySqlCommand(roleCheck, conn);
            roleCmd.Parameters.AddWithValue("@RoleId", request.RoleId);
            var roleExists = Convert.ToInt32(await roleCmd.ExecuteScalarAsync()) > 0;

            if (!roleExists)
            {
                return NotFound(new { message = "Role not found" });
            }

            // Verificar si ya existe la asignación
            var existingCheck = @"
                SELECT COUNT(*) 
                FROM user_has_business 
                WHERE id_user = @UserId AND id_business = @BusinessId AND id_role = @RoleId";
            using var existingCmd = new MySqlCommand(existingCheck, conn);
            existingCmd.Parameters.AddWithValue("@UserId", userId);
            existingCmd.Parameters.AddWithValue("@BusinessId", request.BusinessId);
            existingCmd.Parameters.AddWithValue("@RoleId", request.RoleId);
            var alreadyExists = Convert.ToInt32(await existingCmd.ExecuteScalarAsync()) > 0;

            if (alreadyExists)
            {
                return Conflict(new { message = "User is already assigned to this business with this role" });
            }

            // Insertar la asignación
            var insertQuery = @"
                INSERT INTO user_has_business (id_user, id_business, id_role, created_at, updated_at)
                VALUES (@UserId, @BusinessId, @RoleId, @Now, @Now)";

            using var insertCmd = new MySqlCommand(insertQuery, conn);
            insertCmd.Parameters.AddWithValue("@UserId", userId);
            insertCmd.Parameters.AddWithValue("@BusinessId", request.BusinessId);
            insertCmd.Parameters.AddWithValue("@RoleId", request.RoleId);
            insertCmd.Parameters.AddWithValue("@Now", DateTime.UtcNow);

            var rowsAffected = await insertCmd.ExecuteNonQueryAsync();

            if (rowsAffected > 0)
            {
                _logger.LogInformation("User {UserId} assigned to business {BusinessId} with role {RoleId}", 
                    userId, request.BusinessId, request.RoleId);

                return Ok(new { 
                    message = "User successfully assigned to business",
                    userId = userId,
                    businessId = request.BusinessId,
                    roleId = request.RoleId
                });
            }

            return StatusCode(500, new { message = "Failed to assign user to business" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error assigning user {UserId} to business", userId);
            return StatusCode(500, new { message = "Error assigning user to business", error = ex.Message });
        }
    }

    /// <summary>
    /// Remueve la asignación de un usuario a un negocio específico
    /// </summary>
    [HttpDelete("users/{userId}/business/{businessId}/role/{roleId}")]
    public async Task<IActionResult> RemoveBusinessFromUser(int userId, int businessId, int roleId)
    {
        if (!IsSuperAdmin()) return Forbid();

        try
        {
            using var conn = GetConnection();
            await conn.OpenAsync();

            var deleteQuery = @"
                DELETE FROM user_has_business 
                WHERE id_user = @UserId AND id_business = @BusinessId AND id_role = @RoleId";

            using var cmd = new MySqlCommand(deleteQuery, conn);
            cmd.Parameters.AddWithValue("@UserId", userId);
            cmd.Parameters.AddWithValue("@BusinessId", businessId);
            cmd.Parameters.AddWithValue("@RoleId", roleId);

            var rowsAffected = await cmd.ExecuteNonQueryAsync();

            if (rowsAffected > 0)
            {
                _logger.LogInformation("User {UserId} removed from business {BusinessId} with role {RoleId}", 
                    userId, businessId, roleId);
                return Ok(new { message = "User successfully removed from business" });
            }

            return NotFound(new { message = "Assignment not found" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error removing user {UserId} from business {BusinessId}", userId, businessId);
            return StatusCode(500, new { message = "Error removing user from business", error = ex.Message });
        }
    }

    /// <summary>
    /// Actualiza el systemRole de un usuario
    /// </summary>
    [HttpPatch("users/{userId}/system-role")]
    public async Task<IActionResult> UpdateUserSystemRole(int userId, [FromBody] UpdateSystemRoleRequest request)
    {
        try
        {
            var validRoles = new[] { "super_admin", "admin", "operator", "none" };
            if (!validRoles.Contains(request.SystemRole))
            {
                return BadRequest(new { message = "Invalid system role. Valid values: super_admin, admin, operator, none" });
            }

            using var conn = GetConnection();
            await conn.OpenAsync();

            var updateQuery = @"
                UPDATE User 
                SET system_role = @SystemRole, updated_at = @Now
                WHERE id = @UserId";

            using var cmd = new MySqlCommand(updateQuery, conn);
            cmd.Parameters.AddWithValue("@SystemRole", request.SystemRole);
            cmd.Parameters.AddWithValue("@Now", DateTime.UtcNow);
            cmd.Parameters.AddWithValue("@UserId", userId);

            var rowsAffected = await cmd.ExecuteNonQueryAsync();

            if (rowsAffected > 0)
            {
                _logger.LogInformation("User {UserId} system role updated to {SystemRole}", userId, request.SystemRole);
                return Ok(new { message = "System role updated successfully", userId, systemRole = request.SystemRole });
            }

            return NotFound(new { message = "User not found" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating system role for user {UserId}", userId);
            return StatusCode(500, new { message = "Error updating system role", error = ex.Message });
        }
    }

    /// <summary>
    /// Activa o desactiva un usuario
    /// </summary>
    [HttpPatch("users/{userId}/active")]
    public async Task<IActionResult> UpdateUserActiveStatus(int userId, [FromBody] UpdateActiveStatusRequest request)
    {
        try
        {
            using var conn = GetConnection();
            await conn.OpenAsync();

            var updateQuery = @"
                UPDATE users 
                SET active = @Active, updated_at = @Now
                WHERE id = @UserId";

            using var cmd = new MySqlCommand(updateQuery, conn);
            cmd.Parameters.AddWithValue("@Active", request.Active);
            cmd.Parameters.AddWithValue("@Now", DateTime.UtcNow);
            cmd.Parameters.AddWithValue("@UserId", userId);

            var rowsAffected = await cmd.ExecuteNonQueryAsync();

            if (rowsAffected > 0)
            {
                _logger.LogInformation("User {UserId} active status updated to {Active}", userId, request.Active);
                return Ok(new { message = "User status updated successfully", userId, active = request.Active });
            }

            return NotFound(new { message = "User not found" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating active status for user {UserId}", userId);
            return StatusCode(500, new { message = "Error updating business status", error = ex.Message });
        }
    }

    /// <summary>
    /// Sube una imagen y retorna la URL
    /// </summary>
    [HttpPost("upload-image")]
    public async Task<IActionResult> UploadImage([FromForm] IFormFile file)
    {
        if (!IsSuperAdmin()) return Forbid();

        try
        {
            if (file == null || file.Length == 0)
            {
                return BadRequest(new { message = "No file provided" });
            }

            // Validar tipo de archivo
            var allowedExtensions = new[] { ".jpg", ".jpeg", ".png", ".gif", ".webp" };
            var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
            
            if (!allowedExtensions.Contains(extension))
            {
                return BadRequest(new { message = "Invalid file type. Only images are allowed." });
            }

            // Validar tamaño (5MB máximo)
            if (file.Length > 5 * 1024 * 1024)
            {
                return BadRequest(new { message = "File size exceeds 5MB limit" });
            }

            // Crear directorio si no existe
            var uploadsPath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads");
            if (!Directory.Exists(uploadsPath))
            {
                Directory.CreateDirectory(uploadsPath);
            }

            // Generar nombre único
            var fileName = $"{Guid.NewGuid()}{extension}";
            var filePath = Path.Combine(uploadsPath, fileName);

            // Guardar archivo
            using (var stream = new FileStream(filePath, FileMode.Create))
            {
                await file.CopyToAsync(stream);
            }

            // Retornar URL relativa
            var fileUrl = $"/uploads/{fileName}";

            _logger.LogInformation("Image uploaded: {FileName}", fileName);

            return Ok(new { url = fileUrl, fileName });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error uploading image");
            return StatusCode(500, new { message = "Error uploading image", error = ex.Message });
        }
    }

    /// <summary>
    /// Obtiene todas las tiendas de un negocio
    /// </summary>
    [HttpGet("businesses/{businessId}/stores")]
    public async Task<IActionResult> GetStoresByBusiness(int businessId)
    {
        if (!IsSuperAdmin()) return Forbid();

        try
        {
            using var conn = GetConnection();
            await conn.OpenAsync();

            var query = @"
                SELECT 
                    id,
                    name,
                    location,
                    id_business,
                    id_manager,
                    open_hour,
                    close_hour,
                    active
                FROM store
                WHERE id_business = @BusinessId
                ORDER BY name";

            using var cmd = new MySqlCommand(query, conn);
            cmd.Parameters.AddWithValue("@BusinessId", businessId);
            using var reader = await cmd.ExecuteReaderAsync();

            var stores = new List<Dictionary<string, object>>();
            while (await reader.ReadAsync())
            {
                stores.Add(new Dictionary<string, object>
                {
                    ["id"] = reader.GetInt32("id"),
                    ["name"] = reader.GetString("name"),
                    ["location"] = reader.IsDBNull("location") ? null : reader.GetString("location"),
                    ["businessId"] = reader.GetInt32("id_business"),
                    ["managerId"] = reader.IsDBNull("id_manager") ? null : reader.GetInt32("id_manager"),
                    ["openHour"] = reader.IsDBNull("open_hour") ? null : reader.GetTimeSpan("open_hour").ToString(@"hh\:mm"),
                    ["closeHour"] = reader.IsDBNull("close_hour") ? null : reader.GetTimeSpan("close_hour").ToString(@"hh\:mm"),
                    ["active"] = reader.GetBoolean("active")
                });
            }

            return Ok(stores);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting stores for business {BusinessId}", businessId);
            return StatusCode(500, new { message = "Error retrieving stores", error = ex.Message });
        }
    }

    /// <summary>
    /// Crea una nueva tienda para un negocio
    /// </summary>
    [HttpPost("businesses/{businessId}/stores")]
    public async Task<IActionResult> CreateStore(int businessId, [FromBody] CreateStoreRequest request)
    {
        if (!IsSuperAdmin()) return Forbid();

        try
        {
            if (string.IsNullOrWhiteSpace(request.Name))
            {
                return BadRequest(new { message = "Store name is required" });
            }

            using var conn = GetConnection();
            await conn.OpenAsync();

            // Verificar que el negocio existe
            var businessCheck = "SELECT COUNT(*) FROM business WHERE id = @BusinessId";
            using var businessCmd = new MySqlCommand(businessCheck, conn);
            businessCmd.Parameters.AddWithValue("@BusinessId", businessId);
            var businessExists = Convert.ToInt32(await businessCmd.ExecuteScalarAsync()) > 0;

            if (!businessExists)
            {
                return NotFound(new { message = "Business not found" });
            }

            var insertQuery = @"
                INSERT INTO store (name, location, id_business, open_hour, close_hour, active)
                VALUES (@Name, @Location, @BusinessId, @OpenHour, @CloseHour, 1)";

            using var cmd = new MySqlCommand(insertQuery, conn);
            cmd.Parameters.AddWithValue("@Name", request.Name);
            cmd.Parameters.AddWithValue("@Location", string.IsNullOrWhiteSpace(request.Location) ? DBNull.Value : request.Location);
            cmd.Parameters.AddWithValue("@BusinessId", businessId);
            cmd.Parameters.AddWithValue("@OpenHour", string.IsNullOrWhiteSpace(request.OpenHour) ? DBNull.Value : request.OpenHour);
            cmd.Parameters.AddWithValue("@CloseHour", string.IsNullOrWhiteSpace(request.CloseHour) ? DBNull.Value : request.CloseHour);

            await cmd.ExecuteNonQueryAsync();
            var newId = cmd.LastInsertedId;

            _logger.LogInformation("Store created with ID {StoreId} for business {BusinessId}", newId, businessId);

            return Ok(new { 
                message = "Store created successfully", 
                id = newId,
                name = request.Name
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating store for business {BusinessId}", businessId);
            return StatusCode(500, new { message = "Error creating store", error = ex.Message });
        }
    }

    /// <summary>
    /// Actualiza una tienda existente
    /// </summary>
    [HttpPut("stores/{id}")]
    public async Task<IActionResult> UpdateStore(int id, [FromBody] UpdateStoreRequest request)
    {
        if (!IsSuperAdmin()) return Forbid();

        try
        {
            if (string.IsNullOrWhiteSpace(request.Name))
            {
                return BadRequest(new { message = "Store name is required" });
            }

            using var conn = GetConnection();
            await conn.OpenAsync();

            var updateQuery = @"
                UPDATE store 
                SET name = @Name,
                    location = @Location,
                    open_hour = @OpenHour,
                    close_hour = @CloseHour
                WHERE id = @Id";

            using var cmd = new MySqlCommand(updateQuery, conn);
            cmd.Parameters.AddWithValue("@Name", request.Name);
            cmd.Parameters.AddWithValue("@Location", string.IsNullOrWhiteSpace(request.Location) ? DBNull.Value : request.Location);
            cmd.Parameters.AddWithValue("@OpenHour", string.IsNullOrWhiteSpace(request.OpenHour) ? DBNull.Value : request.OpenHour);
            cmd.Parameters.AddWithValue("@CloseHour", string.IsNullOrWhiteSpace(request.CloseHour) ? DBNull.Value : request.CloseHour);
            cmd.Parameters.AddWithValue("@Id", id);

            var rowsAffected = await cmd.ExecuteNonQueryAsync();

            if (rowsAffected > 0)
            {
                _logger.LogInformation("Store {StoreId} updated", id);
                return Ok(new { message = "Store updated successfully", id });
            }

            return NotFound(new { message = "Store not found" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating store {StoreId}", id);
            return StatusCode(500, new { message = "Error updating store", error = ex.Message });
        }
    }

    /// <summary>
    /// Activa o desactiva una tienda
    /// </summary>
    [HttpPatch("stores/{id}/active")]
    public async Task<IActionResult> UpdateStoreActiveStatus(int id, [FromBody] UpdateActiveStatusRequest request)
    {
        if (!IsSuperAdmin()) return Forbid();

        try
        {
            using var conn = GetConnection();
            await conn.OpenAsync();

            var updateQuery = @"
                UPDATE store 
                SET active = @Active
                WHERE id = @Id";

            using var cmd = new MySqlCommand(updateQuery, conn);
            cmd.Parameters.AddWithValue("@Active", request.Active);
            cmd.Parameters.AddWithValue("@Id", id);

            var rowsAffected = await cmd.ExecuteNonQueryAsync();

            if (rowsAffected > 0)
            {
                _logger.LogInformation("Store {StoreId} active status updated to {Active}", id, request.Active);
                return Ok(new { message = "Store status updated successfully", id, active = request.Active });
            }

            return NotFound(new { message = "Store not found" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating store status {StoreId}", id);
            return StatusCode(500, new { message = "Error updating store status", error = ex.Message });
        }
    }

    /// <summary>
    /// Elimina una tienda
    /// </summary>
    [HttpDelete("stores/{id}")]
    public async Task<IActionResult> DeleteStore(int id)
    {
        if (!IsSuperAdmin()) return Forbid();

        try
        {
            using var conn = GetConnection();
            await conn.OpenAsync();

            var deleteQuery = "DELETE FROM store WHERE id = @Id";

            using var cmd = new MySqlCommand(deleteQuery, conn);
            cmd.Parameters.AddWithValue("@Id", id);

            var rowsAffected = await cmd.ExecuteNonQueryAsync();

            if (rowsAffected > 0)
            {
                _logger.LogInformation("Store {StoreId} deleted", id);
                return Ok(new { message = "Store deleted successfully" });
            }

            return NotFound(new { message = "Store not found" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting store {StoreId}", id);
            return StatusCode(500, new { message = "Error deleting store", error = ex.Message });
        }
    }
}

public class AssignBusinessRequest
{
    public int BusinessId { get; set; }
    public int RoleId { get; set; }
}

public class CreateBusinessRequest
{
    public string CompanyName { get; set; } = string.Empty;
    public string? Logo { get; set; }
}

public class UpdateBusinessRequest
{
    public string CompanyName { get; set; } = string.Empty;
    public string? Logo { get; set; }
}

public class UpdateSystemRoleRequest
{
    public string SystemRole { get; set; } = "none";
}

public class UpdateActiveStatusRequest
{
    public bool Active { get; set; }
}

public class CreateStoreRequest
{
    public string Name { get; set; } = string.Empty;
    public string? Location { get; set; }
    public string? OpenHour { get; set; }
    public string? CloseHour { get; set; }
}

public class UpdateStoreRequest
{
    public string Name { get; set; } = string.Empty;
    public string? Location { get; set; }
    public string? OpenHour { get; set; }
    public string? CloseHour { get; set; }
}
