using GPInventory.Application.DTOs.Production;
using GPInventory.Application.Interfaces;
using GPInventory.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace GPInventory.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class SupplyEntriesController : ControllerBase
{
    private readonly ISupplyEntryService _supplyEntryService;
    private readonly ApplicationDbContext _context;
    private readonly ILogger<SupplyEntriesController> _logger;

    public SupplyEntriesController(
        ISupplyEntryService supplyEntryService,
        ApplicationDbContext context,
        ILogger<SupplyEntriesController> logger)
    {
        _supplyEntryService = supplyEntryService;
        _context = context;
        _logger = logger;
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<SupplyEntryDto>>> GetAllSupplyEntries()
    {
        try
        {
            var supplyEntries = await _supplyEntryService.GetAllAsync();
            return Ok(supplyEntries);
        }
        catch (Exception ex)
        {
            return StatusCode(500, $"Error retrieving supply entries: {ex.Message}");
        }
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<SupplyEntryDto>> GetSupplyEntry(int id)
    {
        try
        {
            var supplyEntry = await _supplyEntryService.GetByIdAsync(id);
            if (supplyEntry == null)
            {
                return NotFound($"Supply entry with ID {id} not found");
            }
            return Ok(supplyEntry);
        }
        catch (Exception ex)
        {
            return StatusCode(500, $"Error retrieving supply entry: {ex.Message}");
        }
    }

    [HttpGet("by-supply/{supplyId}")]
    public async Task<ActionResult<IEnumerable<SupplyEntryDto>>> GetSupplyEntriesBySupplyId(int supplyId)
    {
        try
        {
            var supplyEntries = await _supplyEntryService.GetBySupplyIdAsync(supplyId);
            return Ok(supplyEntries);
        }
        catch (Exception ex)
        {
            return StatusCode(500, $"Error retrieving supply entries: {ex.Message}");
        }
    }

    [HttpGet("by-process/{processDoneId}")]
    public async Task<ActionResult<IEnumerable<SupplyEntryDto>>> GetSupplyEntriesByProcessDoneId(int processDoneId)
    {
        try
        {
            var supplyEntries = await _supplyEntryService.GetByProcessDoneIdAsync(processDoneId);
            return Ok(supplyEntries);
        }
        catch (Exception ex)
        {
            return StatusCode(500, $"Error retrieving supply entries: {ex.Message}");
        }
    }

    [HttpGet("stock/{supplyId}")]
    public async Task<ActionResult<SupplyStockDto>> GetSupplyStock(int supplyId)
    {
        try
        {
            var stock = await _supplyEntryService.GetSupplyStockAsync(supplyId);
            if (stock == null)
            {
                return NotFound($"Supply with ID {supplyId} not found");
            }
            return Ok(stock);
        }
        catch (Exception ex)
        {
            return StatusCode(500, $"Error retrieving supply stock: {ex.Message}");
        }
    }

    [HttpGet("stocks")]
    public async Task<ActionResult<IEnumerable<SupplyStockDto>>> GetAllSupplyStocks([FromQuery] int? businessId = null)
    {
        try
        {
            var stocks = await _supplyEntryService.GetAllSupplyStocksAsync(businessId);
            return Ok(stocks);
        }
        catch (Exception ex)
        {
            return StatusCode(500, $"Error retrieving supply stocks: {ex.Message}");
        }
    }

    [HttpGet("history/{supplyId}")]
    public async Task<ActionResult<IEnumerable<SupplyEntryDto>>> GetSupplyHistory(int supplyId, [FromQuery] int supplyEntryId)
    {
        try
        {
            var history = await _supplyEntryService.GetSupplyHistoryAsync(supplyEntryId, supplyId);
            return Ok(history);
        }
        catch (Exception ex)
        {
            return StatusCode(500, $"Error retrieving supply history: {ex.Message}");
        }
    }

    [HttpPost]
    public async Task<ActionResult<SupplyEntryDto>> CreateSupplyEntry([FromBody] CreateSupplyEntryDto createSupplyEntryDto)
    {
        try
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var supplyEntry = await _supplyEntryService.CreateAsync(createSupplyEntryDto);
            return CreatedAtAction(nameof(GetSupplyEntry), new { id = supplyEntry.Id }, supplyEntry);
        }
        catch (Exception ex)
        {
            return StatusCode(500, $"Error creating supply entry: {ex.Message}");
        }
    }

    [HttpPut("{id}")]
    public async Task<ActionResult<SupplyEntryDto>> UpdateSupplyEntry(int id, [FromBody] UpdateSupplyEntryDto updateSupplyEntryDto)
    {
        try
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var supplyEntry = await _supplyEntryService.UpdateAsync(id, updateSupplyEntryDto);
            return Ok(supplyEntry);
        }
        catch (InvalidOperationException ex)
        {
            return NotFound(ex.Message);
        }
        catch (Exception ex)
        {
            return StatusCode(500, $"Error updating supply entry: {ex.Message}");
        }
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteSupplyEntry(int id)
    {
        try
        {
            await _supplyEntryService.DeleteAsync(id);
            return NoContent();
        }
        catch (Exception ex)
        {
            return StatusCode(500, $"Error deleting supply entry: {ex.Message}");
        }
    }

    /// <summary>
    /// Remover stock de un supply entry específico (agregar registro negativo)
    /// </summary>
    [HttpPost("entry/{entryId}/remove")]
    public async Task<ActionResult<object>> RemoveStockFromSupplyEntry(int entryId, [FromBody] RemoveSupplyStockRequest request)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(request.Notes))
            {
                return BadRequest(new { message = "El motivo de la salida es obligatorio" });
            }

            _logger.LogInformation("🔄 Removiendo {amount} unidades del supply entry {entryId}. Motivo: {notes}", request.Amount, entryId, request.Notes);

            await _context.Database.OpenConnectionAsync();
            
            try
            {
                using var connection = _context.Database.GetDbConnection();
                
                // Verificar que el entry existe y calcular el stock disponible real
                var entryQuery = @"
                    SELECT 
                        se.id,
                        se.amount,
                        se.unit_cost,
                        se.supply_id,
                        se.provider_id,
                        se.active
                    FROM supply_entry se
                    WHERE se.id = @entryId 
                    AND se.amount > 0 
                    AND se.active = 1
                    AND se.supply_entry_id IS NULL";

                using var cmd = connection.CreateCommand();
                cmd.CommandText = entryQuery;
                var entryIdParam = cmd.CreateParameter();
                entryIdParam.ParameterName = "@entryId";
                entryIdParam.Value = entryId;
                cmd.Parameters.Add(entryIdParam);

                using var reader = await cmd.ExecuteReaderAsync();
                
                if (!await reader.ReadAsync())
                {
                    await reader.CloseAsync();
                    return BadRequest(new { message = "Entry no encontrado o sin stock disponible" });
                }

                var originalAmount = reader.GetInt32(1); // amount es INT en la BD
                var unitCost = reader.GetDecimal(2);
                var supplyId = reader.GetInt32(3);
                var providerId = reader.GetInt32(4);

                await reader.CloseAsync();

                // Calcular cuánto se ha removido o usado de este entry (registros negativos)
                var removalsQuery = @"
                    SELECT COALESCE(SUM(ABS(amount)), 0) as total_removed
                    FROM supply_entry
                    WHERE supply_entry_id = @entryId 
                    AND amount < 0
                    AND active = 1";

                using var removalCmd = connection.CreateCommand();
                removalCmd.CommandText = removalsQuery;
                var removalEntryIdParam = removalCmd.CreateParameter();
                removalEntryIdParam.ParameterName = "@entryId";
                removalEntryIdParam.Value = entryId;
                removalCmd.Parameters.Add(removalEntryIdParam);

                var removedAmountObj = await removalCmd.ExecuteScalarAsync();
                var removedAmount = Convert.ToInt32(removedAmountObj ?? 0); // amount es INT

                // Calcular el stock disponible real
                var availableInEntry = originalAmount - removedAmount;

                _logger.LogInformation("📦 Entry {entryId} - Original: {original}, Removido: {removed}, Disponible: {available}", 
                    entryId, originalAmount, removedAmount, availableInEntry);

                if (availableInEntry < request.Amount)
                {
                    return BadRequest(new { message = $"Stock insuficiente. Disponible: {availableInEntry}, Solicitado: {request.Amount}" });
                }

                // Crear registro negativo vinculado al entry original
                var removeStockQuery = @"
                    INSERT INTO supply_entry (amount, unit_cost, supply_id, provider_id, supply_entry_id, tag, active, created_at, updated_at)
                    VALUES (@amount, @unitCost, @supplyId, @providerId, @supplyEntryId, @tag, 1, NOW(), NOW())";

                using var insertCmd = connection.CreateCommand();
                insertCmd.CommandText = removeStockQuery;
                
                var amountParam = insertCmd.CreateParameter();
                amountParam.ParameterName = "@amount";
                amountParam.Value = -request.Amount;
                insertCmd.Parameters.Add(amountParam);

                var costParam = insertCmd.CreateParameter();
                costParam.ParameterName = "@unitCost";
                costParam.Value = unitCost;
                insertCmd.Parameters.Add(costParam);

                var supplyIdParam = insertCmd.CreateParameter();
                supplyIdParam.ParameterName = "@supplyId";
                supplyIdParam.Value = supplyId;
                insertCmd.Parameters.Add(supplyIdParam);

                var providerIdParam = insertCmd.CreateParameter();
                providerIdParam.ParameterName = "@providerId";
                providerIdParam.Value = providerId;
                insertCmd.Parameters.Add(providerIdParam);

                var supplyEntryIdParam = insertCmd.CreateParameter();
                supplyEntryIdParam.ParameterName = "@supplyEntryId";
                supplyEntryIdParam.Value = entryId;
                insertCmd.Parameters.Add(supplyEntryIdParam);

                var tagParam = insertCmd.CreateParameter();
                tagParam.ParameterName = "@tag";
                tagParam.Value = request.Notes;
                insertCmd.Parameters.Add(tagParam);

                await insertCmd.ExecuteNonQueryAsync();

                // Si la cantidad a eliminar es igual a la cantidad disponible, desactivar el entry
                if (availableInEntry == request.Amount)
                {
                    var deactivateQuery = @"
                        UPDATE supply_entry 
                        SET active = 0, updated_at = NOW()
                        WHERE id = @entryId";

                    using var deactivateCmd = connection.CreateCommand();
                    deactivateCmd.CommandText = deactivateQuery;
                    var deactivateEntryIdParam = deactivateCmd.CreateParameter();
                    deactivateEntryIdParam.ParameterName = "@entryId";
                    deactivateEntryIdParam.Value = entryId;
                    deactivateCmd.Parameters.Add(deactivateEntryIdParam);

                    await deactivateCmd.ExecuteNonQueryAsync();
                    
                    _logger.LogInformation("🔄 Entry {entryId} desactivado completamente (stock agotado)", entryId);
                }

                _logger.LogInformation("✅ Stock de insumo removido exitosamente");

                return Ok(new { 
                    message = "Stock removido exitosamente",
                    removedAmount = request.Amount,
                    entryId = entryId
                });
            }
            finally
            {
                await _context.Database.CloseConnectionAsync();
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Error removiendo stock del supply entry {entryId}", entryId);
            return StatusCode(500, new { message = "Error interno del servidor", error = ex.Message });
        }
    }
}

public class RemoveSupplyStockRequest
{
    public decimal Amount { get; set; }
    public string Notes { get; set; } = string.Empty;
}
