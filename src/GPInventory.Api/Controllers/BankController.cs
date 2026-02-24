using GPInventory.Application.DTOs.Bank;
using GPInventory.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GPInventory.Api.Controllers;

/// <summary>
/// Handles Fintoc bank integration:
/// connections, manual sync, and pending-transaction review.
/// </summary>
[ApiController]
[Route("api/bank")]
[Authorize]
public class BankController : ControllerBase
{
    private readonly IBankService _bankService;
    private readonly ILogger<BankController> _logger;

    public BankController(IBankService bankService, ILogger<BankController> logger)
    {
        _bankService = bankService;
        _logger = logger;
    }

    // ─── Connections ──────────────────────────────────────────────────────────

    /// <summary>List all bank connections for a business.</summary>
    [HttpGet("connections")]
    public async Task<IActionResult> GetConnections([FromQuery] int businessId)
    {
        try
        {
            var connections = await _bankService.GetConnectionsAsync(businessId);
            return Ok(connections);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting bank connections for business {BusinessId}", businessId);
            return StatusCode(500, new { message = "Error al obtener conexiones bancarias." });
        }
    }

    /// <summary>
    /// Save a new Fintoc connection after the frontend widget completes OAuth.
    /// The frontend sends back the link_token from Fintoc's widget callback.
    /// </summary>
    [HttpPost("connections")]
    public async Task<IActionResult> CreateConnection([FromBody] CreateBankConnectionDto dto)
    {
        try
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);

            var connection = await _bankService.CreateConnectionAsync(dto);
            return CreatedAtAction(nameof(GetConnections),
                new { businessId = dto.BusinessId }, connection);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating bank connection");
            return StatusCode(500, new { message = "Error al guardar la conexión bancaria." });
        }
    }

    /// <summary>Soft-delete a bank connection.</summary>
    [HttpDelete("connections/{id:int}")]
    public async Task<IActionResult> DeleteConnection(int id)
    {
        try
        {
            await _bankService.DeleteConnectionAsync(id);
            return NoContent();
        }
        catch (KeyNotFoundException)
        {
            return NotFound(new { message = "Conexión no encontrada." });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting bank connection {Id}", id);
            return StatusCode(500, new { message = "Error al eliminar la conexión bancaria." });
        }
    }

    // ─── Sync ─────────────────────────────────────────────────────────────────

    /// <summary>
    /// Trigger a manual sync for a connection.
    /// Fetches movements from Fintoc and imports new debits as pending transactions.
    /// </summary>
    [HttpPost("connections/{id:int}/sync")]
    public async Task<IActionResult> Sync(int id, [FromQuery] int daysBack = 30)
    {
        try
        {
            var result = await _bankService.SyncTransactionsAsync(id, daysBack);
            return Ok(result);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "Fintoc API error during sync for connection {Id}", id);
            return StatusCode(502, new { message = "Error al comunicarse con Fintoc. Verifique las credenciales." });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error syncing bank connection {Id}", id);
            return StatusCode(500, new { message = "Error al sincronizar movimientos bancarios." });
        }
    }

    // ─── Pending transactions ─────────────────────────────────────────────────

    /// <summary>List all pending (unreviewed) bank transactions for a business.</summary>
    [HttpGet("transactions/pending")]
    public async Task<IActionResult> GetPendingTransactions([FromQuery] int businessId)
    {
        try
        {
            var transactions = await _bankService.GetPendingTransactionsAsync(businessId);
            return Ok(transactions);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting pending transactions for business {BusinessId}", businessId);
            return StatusCode(500, new { message = "Error al obtener transacciones pendientes." });
        }
    }

    /// <summary>
    /// Confirm a pending bank transaction as an Expense.
    /// Creates the expense record and marks the transaction as confirmed.
    /// </summary>
    [HttpPost("transactions/{id:int}/confirm")]
    public async Task<IActionResult> ConfirmTransaction(int id, [FromBody] ConfirmBankTransactionDto dto)
    {
        try
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);

            var transaction = await _bankService.ConfirmTransactionAsync(id, dto);
            return Ok(transaction);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error confirming bank transaction {Id}", id);
            return StatusCode(500, new { message = "Error al confirmar la transacción." });
        }
    }

    /// <summary>Dismiss a pending bank transaction (mark as ignored).</summary>
    [HttpPost("transactions/{id:int}/dismiss")]
    public async Task<IActionResult> DismissTransaction(int id)
    {
        try
        {
            var transaction = await _bankService.DismissTransactionAsync(id);
            return Ok(transaction);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error dismissing bank transaction {Id}", id);
            return StatusCode(500, new { message = "Error al descartar la transacción." });
        }
    }
}
