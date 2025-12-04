using GPInventory.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace GPInventory.Api.Controllers;

[ApiController]
[Route("api/expense-types")]
[Authorize]
public class ExpenseTypesController : ControllerBase
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<ExpenseTypesController> _logger;

    public ExpenseTypesController(ApplicationDbContext context, ILogger<ExpenseTypesController> logger)
    {
        _context = context;
        _logger = logger;
    }

    /// <summary>
    /// Get all expense types (Gasto, Costo, Inversión)
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetExpenseTypes()
    {
        try
        {
            var connection = _context.Database.GetDbConnection();
            await connection.OpenAsync();

            using var command = connection.CreateCommand();
            command.CommandText = @"
                SELECT 
                    id,
                    name,
                    code,
                    description,
                    is_active,
                    created_at
                FROM expense_types
                WHERE is_active = 1
                ORDER BY name";

            var expenseTypes = new List<object>();

            using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                expenseTypes.Add(new
                {
                    id = reader.GetInt32(reader.GetOrdinal("id")),
                    name = reader.GetString(reader.GetOrdinal("name")),
                    code = reader.GetString(reader.GetOrdinal("code")),
                    description = reader.IsDBNull(reader.GetOrdinal("description")) ? null : reader.GetString(reader.GetOrdinal("description")),
                    is_active = reader.GetBoolean(reader.GetOrdinal("is_active")),
                    created_at = reader.GetDateTime(reader.GetOrdinal("created_at"))
                });
            }

            return Ok(expenseTypes);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving expense types");
            return StatusCode(500, new { message = "Error al obtener los tipos de egresos" });
        }
    }

    /// <summary>
    /// Get a specific expense type by ID
    /// </summary>
    [HttpGet("{id}")]
    public async Task<IActionResult> GetExpenseType(int id)
    {
        try
        {
            var connection = _context.Database.GetDbConnection();
            await connection.OpenAsync();

            using var command = connection.CreateCommand();
            command.CommandText = @"
                SELECT 
                    id,
                    name,
                    code,
                    description,
                    is_active,
                    created_at
                FROM expense_types
                WHERE id = @id";

            command.Parameters.Add(new MySqlConnector.MySqlParameter("@id", id));

            using var reader = await command.ExecuteReaderAsync();
            if (!await reader.ReadAsync())
            {
                return NotFound(new { message = "Tipo de egreso no encontrado" });
            }

            var expenseType = new
            {
                id = reader.GetInt32(reader.GetOrdinal("id")),
                name = reader.GetString(reader.GetOrdinal("name")),
                code = reader.GetString(reader.GetOrdinal("code")),
                description = reader.IsDBNull(reader.GetOrdinal("description")) ? null : reader.GetString(reader.GetOrdinal("description")),
                is_active = reader.GetBoolean(reader.GetOrdinal("is_active")),
                created_at = reader.GetDateTime(reader.GetOrdinal("created_at"))
            };

            return Ok(expenseType);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving expense type: {Id}", id);
            return StatusCode(500, new { message = "Error al obtener el tipo de egreso" });
        }
    }
}
