using GPInventory.Application.DTOs.Services;
using GPInventory.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GPInventory.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class SessionExpensesController : ControllerBase
{
    private readonly IServiceSessionExpenseService _service;
    private readonly ILogger<SessionExpensesController> _logger;

    public SessionExpensesController(IServiceSessionExpenseService service, ILogger<SessionExpensesController> logger)
    {
        _service = service;
        _logger  = logger;
    }

    // GET api/SessionExpenses/business/{businessId}?status=
    [HttpGet("business/{businessId:int}")]
    public async Task<IActionResult> GetByBusiness(int businessId, [FromQuery] string? status = null)
    {
        var rows = await _service.GetByBusinessAsync(businessId, status);
        return Ok(rows);
    }

    // GET api/SessionExpenses/session/{sessionId}
    [HttpGet("session/{sessionId:int}")]
    public async Task<IActionResult> GetBySession(int sessionId)
    {
        var rows = await _service.GetBySessionAsync(sessionId);
        return Ok(rows);
    }

    // GET api/SessionExpenses/{id}
    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetById(int id)
    {
        try
        {
            var row = await _service.GetByIdAsync(id);
            return Ok(row);
        }
        catch (InvalidOperationException ex)
        {
            return NotFound(new { message = ex.Message });
        }
    }

    // POST api/SessionExpenses/manual
    [HttpPost("manual")]
    public async Task<IActionResult> CreateManual([FromBody] CreateSessionExpenseManualDto dto)
    {
        try
        {
            var row = await _service.CreateManualAsync(dto, GetUserId());
            return CreatedAtAction(nameof(GetById), new { id = row.Id }, row);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    // PUT api/SessionExpenses/{id}/payee
    [HttpPut("{id:int}/payee")]
    public async Task<IActionResult> AssignPayee(int id, [FromBody] AssignPayeeDto dto)
    {
        try
        {
            var row = await _service.AssignPayeeAsync(id, dto);
            return Ok(row);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    // PUT api/SessionExpenses/{id}/paid
    [HttpPut("{id:int}/paid")]
    public async Task<IActionResult> MarkPaid(int id, [FromBody] MarkSessionExpensePaidDto dto)
    {
        try
        {
            var row = await _service.MarkPaidAsync(id, dto, GetUserId());
            return Ok(row);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    // DELETE api/SessionExpenses/{id}/cancel
    [HttpDelete("{id:int}/cancel")]
    public async Task<IActionResult> Cancel(int id, [FromQuery] string? reason = null)
    {
        try
        {
            var row = await _service.CancelAsync(id, reason);
            return Ok(row);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    // POST api/SessionExpenses/generate/{sessionId}
    // Regenerates expenses for an existing session (idempotent: skips if already exist)
    [HttpPost("generate/{sessionId:int}")]
    public async Task<IActionResult> Generate(int sessionId)
    {
        try
        {
            var rows = await _service.GenerateFromSessionAsync(sessionId);
            return Ok(rows);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    // ── Private ───────────────────────────────────────────────────────────────

    private int GetUserId()
    {
        var claim = User.FindFirst("sub")?.Value
                 ?? User.FindFirst("user_id")?.Value
                 ?? User.FindFirst("userId")?.Value
                 ?? User.FindFirst("id")?.Value
                 ?? User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;

        return int.TryParse(claim, out var id) ? id : 0;
    }
}
