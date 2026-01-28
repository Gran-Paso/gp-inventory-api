using GPInventory.Application.DTOs.Production;
using GPInventory.Application.Interfaces;
using GPInventory.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace GPInventory.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class ProcessDonesController : ControllerBase
{
    private readonly IProcessDoneService _processDoneService;
    private readonly ApplicationDbContext _context;
    private readonly ILogger<ProcessDonesController> _logger;

    public ProcessDonesController(
        IProcessDoneService processDoneService,
        ApplicationDbContext context,
        ILogger<ProcessDonesController> logger)
    {
        _processDoneService = processDoneService;
        _context = context;
        _logger = logger;
    }

    private int? GetUserIdFromClaims()
    {
        var userIdClaim = User.FindFirst("sub") 
            ?? User.FindFirst("user_id") 
            ?? User.FindFirst("userId") 
            ?? User.FindFirst("id")
            ?? User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier);
        
        if (userIdClaim != null && int.TryParse(userIdClaim.Value, out int userId))
        {
            return userId;
        }
        return null;
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<ProcessDoneDto>>> GetProcessDones()
    {
        try
        {
            var processDones = await _processDoneService.GetAllProcessDonesAsync();
            return Ok(processDones);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving process dones");
            return StatusCode(500, "Internal server error");
        }
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<ProcessDoneDto>> GetProcessDone(int id)
    {
        try
        {
            var processDone = await _processDoneService.GetProcessDoneByIdAsync(id);
            return Ok(processDone);
        }
        catch (KeyNotFoundException ex)
        {
            _logger.LogWarning(ex, "ProcessDone not found: {ProcessDoneId}", id);
            return NotFound(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving process done {ProcessDoneId}", id);
            return StatusCode(500, "Internal server error");
        }
    }

    [HttpGet("process/{processId}")]
    public async Task<ActionResult<IEnumerable<ProcessDoneDto>>> GetProcessDonesByProcess(int processId)
    {
        try
        {
            var processDones = await _processDoneService.GetProcessDonesByProcessIdAsync(processId);
            return Ok(processDones);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving process dones for process {ProcessId}", processId);
            return StatusCode(500, "Internal server error");
        }
    }

    [HttpPost]
    public async Task<ActionResult<ProcessDoneDto>> CreateProcessDone([FromBody] CreateProcessDoneDto createProcessDoneDto)
    {
        try
        {
            // Extraer userId del token JWT si no viene en el DTO
            if (!createProcessDoneDto.CreatedByUserId.HasValue)
            {
                // Intentar múltiples claim types comunes
                var userIdClaim = User.FindFirst("sub") 
                    ?? User.FindFirst("user_id") 
                    ?? User.FindFirst("userId") 
                    ?? User.FindFirst("id")
                    ?? User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier);
                
                if (userIdClaim != null && int.TryParse(userIdClaim.Value, out int userId))
                {
                    createProcessDoneDto.CreatedByUserId = userId;
                    _logger.LogInformation("UserId extracted from token: {UserId}", userId);
                }
                else
                {
                    // Log todos los claims disponibles para debug
                    _logger.LogWarning("Could not extract userId from token. Available claims: {Claims}", 
                        string.Join(", ", User.Claims.Select(c => $"{c.Type}={c.Value}")));
                }
            }
            
            var processDone = await _processDoneService.CreateProcessDoneAsync(createProcessDoneDto);
            return CreatedAtAction(nameof(GetProcessDone), new { id = processDone.Id }, processDone);
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "Invalid operation when creating process done");
            return BadRequest(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating process done");
            return StatusCode(500, "Internal server error");
        }
    }

    [HttpPut("{id}/stage")]
    public async Task<ActionResult<ProcessDoneDto>> UpdateProcessDoneStage(int id, [FromBody] int stage)
    {
        try
        {
            var processDone = await _processDoneService.UpdateProcessDoneStageAsync(id, stage);
            return Ok(processDone);
        }
        catch (KeyNotFoundException ex)
        {
            _logger.LogWarning(ex, "ProcessDone not found when updating stage");
            return NotFound(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating process done stage");
            return StatusCode(500, "Internal server error");
        }
    }

    [HttpPut("{id}/amount")]
    public async Task<ActionResult<ProcessDoneDto>> UpdateProcessDoneAmount(int id, [FromBody] object amountData, [FromQuery] bool isLastSupply = false)
    {
        try
        {
            int amount;
            
            // Try to parse as object first, then as direct value
            if (amountData is JsonElement jsonElement)
            {
                if (jsonElement.ValueKind == JsonValueKind.Number)
                {
                    // Direct number value
                    amount = jsonElement.GetInt32();
                }
                else if (jsonElement.ValueKind == JsonValueKind.Object && jsonElement.TryGetProperty("quantity", out var quantityProp))
                {
                    // Object with quantity property
                    amount = quantityProp.GetInt32();
                    
                    // Check if isLastSupply is specified in the body
                    if (jsonElement.TryGetProperty("isLastSupply", out var isLastProp))
                    {
                        isLastSupply = isLastProp.GetBoolean();
                    }
                }
                else if (jsonElement.ValueKind == JsonValueKind.Object && jsonElement.TryGetProperty("amount", out var amountProp))
                {
                    // Object with amount property
                    amount = amountProp.GetInt32();
                    
                    // Check if isLastSupply is specified in the body
                    if (jsonElement.TryGetProperty("isLastSupply", out var isLastProp))
                    {
                        isLastSupply = isLastProp.GetBoolean();
                    }
                }
                else
                {
                    return BadRequest("Invalid amount format. Expected a number or object with 'quantity' or 'amount' property.");
                }
            }
            else if (amountData is int directAmount)
            {
                amount = directAmount;
            }
            else
            {
                return BadRequest("Invalid amount format.");
            }
            
            var processDone = await _processDoneService.UpdateProcessDoneAmountAsync(id, amount, isLastSupply);
            return Ok(processDone);
        }
        catch (KeyNotFoundException ex)
        {
            _logger.LogWarning(ex, "ProcessDone not found when updating quantity");
            return NotFound(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating process done quantity");
            return StatusCode(500, "Internal server error");
        }
    }

    // Compatibility endpoint for frontend
    [HttpPut("{id}/quantity")]
    public async Task<ActionResult<ProcessDoneDto>> UpdateProcessDoneQuantity(int id, [FromBody] object quantityData, [FromQuery] bool isLastSupply = false)
    {
        // Redirect to the new method with flexible parsing
        return await UpdateProcessDoneAmount(id, quantityData, isLastSupply);
    }

    [HttpPost("{id}/supply-entry")]
    public async Task<ActionResult<ProcessDoneDto>> AddSupplyEntryToProcess(int id, [FromBody] CreateSupplyUsageDto supplyUsage)
    {
        try
        {
            var userId = GetUserIdFromClaims();
            var processDone = await _processDoneService.AddSupplyEntryAsync(id, supplyUsage, userId);
            return Ok(processDone);
        }
        catch (KeyNotFoundException ex)
        {
            _logger.LogWarning(ex, "ProcessDone not found when adding supply entry");
            return NotFound(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error adding supply entry to process done");
            return StatusCode(500, "Internal server error");
        }
    }

    /// <summary>
    /// Obtiene el historial completo de ejecuciones de un proceso específico con métricas agregadas
    /// </summary>
    [HttpGet("process/{processId}/history")]
    public async Task<ActionResult<ProcessHistoryDto>> GetProcessHistory(
        int processId, 
        [FromQuery] DateTime? startDate = null, 
        [FromQuery] DateTime? endDate = null)
    {
        try
        {
            // Si no se especifican fechas, usar últimos 7 días por defecto
            var start = startDate ?? DateTime.UtcNow.AddDays(-7);
            var end = endDate ?? DateTime.UtcNow;

            _logger.LogInformation("📊 Obteniendo historial del proceso {processId} desde {start} hasta {end}", processId, start, end);

            using var connection = _context.Database.GetDbConnection();
            await connection.OpenAsync();
            
            try
            {
                // Query para obtener el nombre del proceso
                var processNameQuery = @"
                    SELECT name 
                    FROM processes 
                    WHERE id = @processId";

                using var nameCmd = connection.CreateCommand();
                nameCmd.CommandText = processNameQuery;
                var processIdParam = nameCmd.CreateParameter();
                processIdParam.ParameterName = "@processId";
                processIdParam.Value = processId;
                nameCmd.Parameters.Add(processIdParam);

                var processName = (await nameCmd.ExecuteScalarAsync())?.ToString() ?? "Proceso";

                // Query principal para obtener las ejecuciones del proceso
                var historyQuery = @"
                    SELECT 
                        pd.id,
                        pd.completed_at,
                        pd.start_date,
                        pd.end_date,
                        pd.amount as quantity,
                        pd.notes,
                        pd.cost,
                        u.name as responsible_user,
                        c.name as product_name,
                        COALESCE(m.status, 'pending') as manufacture_status,
                        m.id as manufacture_id
                    FROM process_done pd
                    LEFT JOIN user u ON pd.created_by_user_id = u.id
                    LEFT JOIN component_production cp ON pd.id = cp.process_done_id
                    LEFT JOIN components c ON cp.component_id = c.id
                    LEFT JOIN manufacture m ON pd.id = m.process_done_id
                    WHERE pd.process_id = @processId
                    AND pd.completed_at >= @startDate
                    AND pd.completed_at <= @endDate
                    ORDER BY pd.completed_at DESC";

                using var historyCmd = connection.CreateCommand();
                historyCmd.CommandText = historyQuery;
                
                var pidParam = historyCmd.CreateParameter();
                pidParam.ParameterName = "@processId";
                pidParam.Value = processId;
                historyCmd.Parameters.Add(pidParam);

                var startParam = historyCmd.CreateParameter();
                startParam.ParameterName = "@startDate";
                startParam.Value = start;
                historyCmd.Parameters.Add(startParam);

                var endParam = historyCmd.CreateParameter();
                endParam.ParameterName = "@endDate";
                endParam.Value = end;
                historyCmd.Parameters.Add(endParam);

                var executions = new List<ProcessExecutionDto>();
                decimal totalDuration = 0;
                int totalExecutions = 0;
                int totalIncidents = 0;
                string? lastResponsible = null;
                string? lastProduct = null;

                using var reader = await historyCmd.ExecuteReaderAsync();
                
                while (await reader.ReadAsync())
                {
                    totalExecutions++;

                    var id = reader.GetInt32(0);
                    var completedAt = reader.GetDateTime(1);
                    var startDateVal = reader.IsDBNull(2) ? (DateTime?)null : reader.GetDateTime(2);
                    var endDateVal = reader.IsDBNull(3) ? (DateTime?)null : reader.GetDateTime(3);
                    var quantity = reader.GetInt32(4);
                    var notes = reader.IsDBNull(5) ? null : reader.GetString(5);
                    var cost = reader.GetDecimal(6);
                    var responsibleUser = reader.IsDBNull(7) ? "Desconocido" : reader.GetString(7);
                    var productName = reader.IsDBNull(8) ? "N/A" : reader.GetString(8);
                    var manufactureStatus = reader.IsDBNull(9) ? "pending" : reader.GetString(9);
                    var manufactureId = reader.IsDBNull(10) ? (int?)null : reader.GetInt32(10);

                    // Calcular duración si hay fechas
                    decimal? durationMinutes = null;
                    if (startDateVal.HasValue && endDateVal.HasValue)
                    {
                        durationMinutes = (decimal)(endDateVal.Value - startDateVal.Value).TotalMinutes;
                        totalDuration += durationMinutes.Value;
                    }

                    // Contar incidentes (ejecuciones con notas)
                    if (!string.IsNullOrWhiteSpace(notes))
                    {
                        totalIncidents++;
                    }

                    // Guardar datos de la última ejecución
                    if (lastResponsible == null)
                    {
                        lastResponsible = responsibleUser;
                        lastProduct = productName;
                    }

                    executions.Add(new ProcessExecutionDto
                    {
                        Id = id,
                        CompletedAt = completedAt,
                        DurationMinutes = durationMinutes ?? 0,
                        Quantity = quantity,
                        ResponsibleUser = responsibleUser,
                        ProductGenerated = productName,
                        HasNotes = !string.IsNullOrWhiteSpace(notes),
                        Notes = notes,
                        TotalCost = cost,
                        ManufactureStatus = manufactureStatus,
                        ManufactureId = manufactureId
                    });
                }

                await reader.CloseAsync();

                // Obtener supply usages y component usages para cada ejecución
                foreach (var execution in executions)
                {
                    // Supply usages
                    var supplyQuery = @"
                        SELECT 
                            se.supply_id,
                            s.name as supply_name,
                            ABS(se.amount) as quantity_used,
                            se.unit_cost
                        FROM supply_entry se
                        INNER JOIN supplies s ON se.supply_id = s.Id
                        WHERE se.process_done_id = @processDoneId
                        AND se.amount < 0
                        ORDER BY se.id";

                    using var supplyCmd = connection.CreateCommand();
                    supplyCmd.CommandText = supplyQuery;
                    var pdIdParam = supplyCmd.CreateParameter();
                    pdIdParam.ParameterName = "@processDoneId";
                    pdIdParam.Value = execution.Id;
                    supplyCmd.Parameters.Add(pdIdParam);

                    var supplyUsages = new List<SupplyUsageDetailDto>();
                    using var supplyReader = await supplyCmd.ExecuteReaderAsync();
                    
                    while (await supplyReader.ReadAsync())
                    {
                        supplyUsages.Add(new SupplyUsageDetailDto
                        {
                            SupplyId = supplyReader.GetInt32(0),
                            SupplyName = supplyReader.GetString(1),
                            QuantityUsed = supplyReader.GetInt32(2),
                            UnitCost = supplyReader.GetDecimal(3)
                        });
                    }
                    
                    await supplyReader.CloseAsync();
                    execution.SupplyUsages = supplyUsages;

                    // Component usages
                    var componentQuery = @"
                        SELECT 
                            cp.component_id,
                            c.name as component_name,
                            ABS(cp.produced_amount) as quantity_used,
                            cp.cost
                        FROM component_production cp
                        INNER JOIN components c ON cp.component_id = c.id
                        WHERE cp.process_done_id = @processDoneId
                        AND cp.produced_amount < 0
                        ORDER BY cp.id";

                    using var componentCmd = connection.CreateCommand();
                    componentCmd.CommandText = componentQuery;
                    var pdIdParam2 = componentCmd.CreateParameter();
                    pdIdParam2.ParameterName = "@processDoneId";
                    pdIdParam2.Value = execution.Id;
                    componentCmd.Parameters.Add(pdIdParam2);

                    var componentUsages = new List<ComponentUsageDetailDto>();
                    using var componentReader = await componentCmd.ExecuteReaderAsync();
                    
                    while (await componentReader.ReadAsync())
                    {
                        componentUsages.Add(new ComponentUsageDetailDto
                        {
                            ComponentId = componentReader.GetInt32(0),
                            ComponentName = componentReader.GetString(1),
                            QuantityUsed = componentReader.GetInt32(2),
                            Cost = componentReader.GetDecimal(3)
                        });
                    }
                    
                    await componentReader.CloseAsync();
                    execution.ComponentUsages = componentUsages;
                }

                var history = new ProcessHistoryDto
                {
                    ProcessId = processId,
                    ProcessName = processName,
                    StartDate = start,
                    EndDate = end,
                    TotalDurationMinutes = totalDuration,
                    TotalExecutions = totalExecutions,
                    TotalIncidents = totalIncidents,
                    LastResponsible = lastResponsible ?? "N/A",
                    LastProductGenerated = lastProduct ?? "N/A",
                    Executions = executions
                };

                _logger.LogInformation("✅ Historial obtenido: {executions} ejecuciones encontradas", totalExecutions);

                return Ok(history);
            }
            finally
            {
                await connection.CloseAsync();
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Error obteniendo historial del proceso {ProcessId}", processId);
            return StatusCode(500, new { message = "Error interno del servidor", error = ex.Message });
        }
    }

    /// <summary>
    /// Obtiene el estado situacional del dashboard con KPIs y microcopy contextual
    /// </summary>
    [HttpGet("dashboard/situational/{businessId}")]
    public async Task<ActionResult<object>> GetDashboardSituational(int businessId)
    {
        try
        {
            _logger.LogInformation("📊 Obteniendo estado situacional del dashboard para business {businessId}", businessId);

            using var connection = _context.Database.GetDbConnection();
            await connection.OpenAsync();
            
            try
            {
                // Query para obtener procesos con su estado
                var processesQuery = @"
                    SELECT 
                        p.id,
                        p.name,
                        p.production_time,
                        p.active as is_active,
                        COUNT(DISTINCT pd.id) as total_completions,
                        MAX(pd.completed_at) as last_completed_at,
                        CASE 
                            WHEN EXISTS (
                                SELECT 1 FROM process_supplies ps
                                INNER JOIN supplies s ON ps.supply_id = s.Id
                                LEFT JOIN (
                                    SELECT supply_id, SUM(amount) as total_stock
                                    FROM supply_entry
                                    WHERE active = 1
                                    GROUP BY supply_id
                                ) se ON s.Id = se.supply_id
                                WHERE ps.process_id = p.id
                                AND (se.total_stock IS NULL OR se.total_stock < 10)
                            ) THEN 1
                            ELSE 0
                        END as has_critical_stock
                    FROM processes p
                    INNER JOIN product pr ON p.product_id = pr.Id
                    LEFT JOIN process_done pd ON p.id = pd.process_id AND pd.completed_at >= DATE_SUB(NOW(), INTERVAL 24 HOUR)
                    WHERE pr.business = @businessId
                    GROUP BY p.id, p.name, p.production_time, p.active";

                using var processCmd = connection.CreateCommand();
                processCmd.CommandText = processesQuery;
                var businessIdParam = processCmd.CreateParameter();
                businessIdParam.ParameterName = "@businessId";
                businessIdParam.Value = businessId;
                processCmd.Parameters.Add(businessIdParam);

                var processes = new List<object>();
                int activeProcesses = 0;
                int inactiveProcesses = 0;
                int completedToday = 0;
                double totalProductionTime = 0;
                DateTime? lastCompletedAt = null;
                string? lastCompletedProcess = null;

                using var reader = await processCmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    var isActive = reader.GetBoolean(3);
                    var totalCompletions = reader.GetInt32(4);
                    var lastCompletion = reader.IsDBNull(5) ? (DateTime?)null : reader.GetDateTime(5);
                    var hasCriticalStock = reader.GetBoolean(6);

                    if (isActive) activeProcesses++;
                    else inactiveProcesses++;

                    completedToday += totalCompletions;
                    totalProductionTime += reader.GetInt32(2);

                    if (lastCompletion.HasValue && (!lastCompletedAt.HasValue || lastCompletion > lastCompletedAt))
                    {
                        lastCompletedAt = lastCompletion;
                        lastCompletedProcess = reader.GetString(1);
                    }

                    processes.Add(new
                    {
                        id = reader.GetInt32(0),
                        name = reader.GetString(1),
                        productionTime = reader.GetInt32(2),
                        isActive = isActive,
                        completionsToday = totalCompletions,
                        hasCriticalStock = hasCriticalStock
                    });
                }
                await reader.CloseAsync();

                int totalProcesses = processes.Count;
                int avgProductionTime = totalProcesses > 0 ? (int)(totalProductionTime / totalProcesses) : 0;

                // Calcular tiempo promedio ENTRE procesos (tiempo entre el fin de uno y el inicio del siguiente) - SOLO HOY
                var avgTimeBetweenQuery = @"
                    WITH ordered_processes AS (
                        SELECT 
                            pd.end_date,
                            LEAD(pd.start_date) OVER (ORDER BY pd.end_date) as next_start_date
                        FROM process_done pd
                        INNER JOIN processes p ON pd.process_id = p.id
                        INNER JOIN product pr ON p.product_id = pr.Id
                        WHERE pr.business = @businessId
                        AND pd.end_date IS NOT NULL
                        AND pd.start_date IS NOT NULL
                        AND DATE(pd.completed_at) = CURDATE()
                    )
                    SELECT AVG(TIMESTAMPDIFF(MINUTE, end_date, next_start_date)) as avg_time_between,
                           COUNT(*) as count_intervals
                    FROM ordered_processes
                    WHERE next_start_date IS NOT NULL
                    AND TIMESTAMPDIFF(MINUTE, end_date, next_start_date) >= 0";

                using var avgTimeCmd = connection.CreateCommand();
                avgTimeCmd.CommandText = avgTimeBetweenQuery;
                var businessIdParamAvg = avgTimeCmd.CreateParameter();
                businessIdParamAvg.ParameterName = "@businessId";
                businessIdParamAvg.Value = businessId;
                avgTimeCmd.Parameters.Add(businessIdParamAvg);

                int avgTimeBetween = avgProductionTime; // Por defecto usar el tiempo configurado
                int countIntervals = 0;

                using var avgReader = await avgTimeCmd.ExecuteReaderAsync();
                if (await avgReader.ReadAsync())
                {
                    var avgResult = avgReader.IsDBNull(0) ? (decimal?)null : avgReader.GetDecimal(0);
                    countIntervals = avgReader.IsDBNull(1) ? 0 : avgReader.GetInt32(1);
                    
                    if (avgResult.HasValue && avgResult.Value >= 0)
                    {
                        avgTimeBetween = (int)Math.Round(avgResult.Value);
                    }
                }
                await avgReader.CloseAsync();

                _logger.LogInformation("📊 Tiempo promedio entre procesos HOY: {avgTimeBetween} min (basado en {count} intervalos). Tiempo configurado: {avgProductionTime} min", 
                    avgTimeBetween, countIntervals, avgProductionTime);

                // Calcular procesos afectados por stock crítico
                // Stock crítico: cuando el stock disponible es menor al necesario
                var criticalStockQuery = @"
                    SELECT COUNT(DISTINCT p.id)
                    FROM processes p
                    INNER JOIN product pr ON p.product_id = pr.Id
                    INNER JOIN process_supplies ps ON p.id = ps.process_id
                    INNER JOIN supplies s ON ps.supply_id = s.Id
                    LEFT JOIN (
                        SELECT supply_id, SUM(amount) as total_stock
                        FROM supply_entry
                        WHERE active = 1
                        GROUP BY supply_id
                    ) se ON s.Id = se.supply_id
                    WHERE pr.business = @businessId
                    AND p.active = 1
                    AND (se.total_stock IS NULL OR se.total_stock < 10)";

                using var criticalCmd = connection.CreateCommand();
                criticalCmd.CommandText = criticalStockQuery;
                var businessIdParam2 = criticalCmd.CreateParameter();
                businessIdParam2.ParameterName = "@businessId";
                businessIdParam2.Value = businessId;
                criticalCmd.Parameters.Add(businessIdParam2);

                var criticalStockCount = Convert.ToInt32(await criticalCmd.ExecuteScalarAsync() ?? 0);

                // Generar microcopy contextual
                string microcopy;
                if (activeProcesses > 0)
                {
                    microcopy = $"Tu fábrica está en marcha con {activeProcesses} proceso{(activeProcesses > 1 ? "s" : "")} activo{(activeProcesses > 1 ? "s" : "")} y un tiempo promedio entre procesos de {avgTimeBetween} min.";
                }
                else if (lastCompletedAt.HasValue && lastCompletedProcess != null)
                {
                    var hoursSince = (DateTime.UtcNow - lastCompletedAt.Value).TotalHours;
                    var timeText = hoursSince < 1 
                        ? "hace menos de 1 hora" 
                        : $"hace {(int)hoursSince} hora{((int)hoursSince > 1 ? "s" : "")}";
                    microcopy = $"No hay procesos en marcha. Último completado: {lastCompletedProcess} {timeText}.";
                }
                else
                {
                    microcopy = "No hay procesos registrados. Comienza creando tu primer proceso de producción.";
                }

                var result = new
                {
                    microcopy,
                    kpis = new
                    {
                        completed = new { count = completedToday, label = "Completados Hoy", color = "green" },
                        criticalStock = new { count = criticalStockCount, label = "Stock Crítico", color = "red" },
                        active = new { count = activeProcesses, label = "Activos", color = "blue" },
                        inactive = new { count = inactiveProcesses, label = "Inactivos", color = "gray" }
                    },
                    processes,
                    averageProductionTime = avgProductionTime,
                    averageTimeBetweenProcesses = avgTimeBetween,
                    lastUpdate = DateTime.UtcNow
                };

                _logger.LogInformation("✅ Estado situacional obtenido: {activeProcesses} activos, tiempo entre procesos: {avgTimeBetween} min", activeProcesses, avgTimeBetween);

                return Ok(result);
            }
            finally
            {
                await connection.CloseAsync();
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Error obteniendo estado situacional del dashboard");
            return StatusCode(500, new { message = "Error interno del servidor", error = ex.Message });
        }
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteProcessDone(int id)
    {
        try
        {
            await _processDoneService.DeleteProcessDoneAsync(id);
            return NoContent();
        }
        catch (KeyNotFoundException ex)
        {
            _logger.LogWarning(ex, "ProcessDone not found for deletion: {ProcessDoneId}", id);
            return NotFound(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting process done {ProcessDoneId}", id);
            return StatusCode(500, "Internal server error");
        }
    }
}

// DTOs for request bodies
public class QuantityUpdateRequest
{
    public int Quantity { get; set; }
}
