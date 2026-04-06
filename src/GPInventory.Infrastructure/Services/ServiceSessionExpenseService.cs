using GPInventory.Application.DTOs.Services;
using GPInventory.Application.Interfaces;
using GPInventory.Domain.Entities;
using GPInventory.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace GPInventory.Infrastructure.Services;

public class ServiceSessionExpenseService : IServiceSessionExpenseService
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<ServiceSessionExpenseService> _logger;

    public ServiceSessionExpenseService(ApplicationDbContext context, ILogger<ServiceSessionExpenseService> logger)
    {
        _context = context;
        _logger  = logger;
    }

    // ── Queries ───────────────────────────────────────────────────────────────

    public async Task<IEnumerable<ServiceSessionExpenseDto>> GetByBusinessAsync(int businessId, string? status = null)
    {
        var query = _context.ServiceSessionExpenses
            .Include(e => e.ServiceSession).ThenInclude(s => s!.Service)
            .Include(e => e.ServiceCostItem)
            .Where(e => e.BusinessId == businessId);

        if (!string.IsNullOrWhiteSpace(status))
            query = query.Where(e => e.Status == status);

        var rows = await query.OrderByDescending(e => e.CreatedAt).ToListAsync();
        return rows.Select(MapToDto);
    }

    public async Task<IEnumerable<ServiceSessionExpenseDto>> GetBySessionAsync(int sessionId)
    {
        var rows = await _context.ServiceSessionExpenses
            .Include(e => e.ServiceSession).ThenInclude(s => s!.Service)
            .Include(e => e.ServiceCostItem)
            .Where(e => e.ServiceSessionId == sessionId)
            .OrderBy(e => e.Id)
            .ToListAsync();

        return rows.Select(MapToDto);
    }

    public async Task<ServiceSessionExpenseDto> GetByIdAsync(int id)
    {
        var row = await FindOrThrowAsync(id);
        return MapToDto(row);
    }

    // ── Mutations ─────────────────────────────────────────────────────────────

    public async Task<ServiceSessionExpenseDto> CreateManualAsync(CreateSessionExpenseManualDto dto, int userId)
    {
        var session = await _context.ServiceSessions.FindAsync(dto.ServiceSessionId)
            ?? throw new InvalidOperationException("Sesión no encontrada");

        var row = new ServiceSessionExpense
        {
            BusinessId         = session.BusinessId,
            StoreId            = session.StoreId,
            ServiceSessionId   = dto.ServiceSessionId,
            ServiceCostItemId  = null,
            Description        = dto.Description,
            Amount             = dto.Amount,
            Status             = "pending",
            PayeeType          = dto.PayeeType,
            PayeeEmployeeId    = dto.PayeeEmployeeId,
            PayeeEmployeeName  = dto.PayeeEmployeeName,
            PayeeExternalName  = dto.PayeeExternalName,
            Notes              = dto.Notes,
            CreatedAt          = DateTime.UtcNow,
            UpdatedAt          = DateTime.UtcNow
        };

        _context.ServiceSessionExpenses.Add(row);
        await _context.SaveChangesAsync();

        _logger.LogInformation("Gasto manual {Id} creado para sesión {SessionId}", row.Id, dto.ServiceSessionId);
        return await GetByIdAsync(row.Id);
    }

    public async Task<ServiceSessionExpenseDto> AssignPayeeAsync(int id, AssignPayeeDto dto)
    {
        var row = await FindOrThrowAsync(id);

        if (row.Status == "paid")
            throw new InvalidOperationException("No se puede modificar un gasto ya pagado");

        row.PayeeType         = dto.PayeeType;
        row.PayeeEmployeeId   = dto.PayeeEmployeeId;
        row.PayeeEmployeeName = dto.PayeeEmployeeName;
        row.PayeeExternalName = dto.PayeeExternalName;
        if (dto.Notes != null) row.Notes = dto.Notes;
        row.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        _logger.LogInformation("Destinatario asignado a gasto {Id}: {Type}", id, dto.PayeeType);
        return MapToDto(row);
    }

    public async Task<ServiceSessionExpenseDto> MarkPaidAsync(int id, MarkSessionExpensePaidDto dto, int userId)
    {
        var row = await FindOrThrowAsync(id);

        if (row.Status == "paid")
            throw new InvalidOperationException("El gasto ya está marcado como pagado");

        var now = DateTime.UtcNow;

        // Opcionalmente crear entrada en tabla expenses (gp-expenses)
        int? expenseId = null;
        if (dto.CreateExpenseRecord && dto.ExpenseSubcategoryId.HasValue)
        {
            var payeeName = row.PayeeType == "employee"
                ? row.PayeeEmployeeName
                : row.PayeeExternalName;

            var description = string.IsNullOrWhiteSpace(payeeName)
                ? row.Description
                : $"{row.Description} — {payeeName}";

            // Tipo de egreso = Costo
            var costTypeId = await _context.ExpenseTypes
                .Where(t => t.Code == "cost")
                .Select(t => (int?)t.Id)
                .FirstOrDefaultAsync();

            var expense = new Expense
            {
                Date          = (dto.PaidAt ?? now).Date,
                BusinessId    = row.BusinessId,
                StoreId       = row.StoreId,
                SubcategoryId = dto.ExpenseSubcategoryId.Value,
                Amount        = row.Amount,
                AmountTotal   = row.Amount,
                Description   = description,
                IsFixed       = false,
                ExpenseTypeId = costTypeId,
            };

            _context.Expenses.Add(expense);
            await _context.SaveChangesAsync();
            expenseId = expense.Id;
        }

        row.Status        = "paid";
        row.PaidAt        = dto.PaidAt ?? now;
        row.PaidByUserId  = userId;
        row.ExpenseId     = expenseId;
        if (dto.Notes != null) row.Notes = dto.Notes;
        row.UpdatedAt     = now;

        await _context.SaveChangesAsync();

        _logger.LogInformation("Gasto {Id} pagado. ExpenseId={ExpenseId}", id, expenseId);
        return MapToDto(row);
    }

    public async Task<ServiceSessionExpenseDto> CancelAsync(int id, string? reason = null)
    {
        var row = await FindOrThrowAsync(id);

        if (row.Status == "paid")
            throw new InvalidOperationException("No se puede cancelar un gasto ya pagado");

        row.Status    = "cancelled";
        row.Notes     = string.IsNullOrWhiteSpace(reason) ? row.Notes : $"{row.Notes}\nCancelado: {reason}".Trim();
        row.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();
        return MapToDto(row);
    }

    public async Task<IEnumerable<ServiceSessionExpenseDto>> GenerateFromSessionAsync(int sessionId)
    {
        var session = await _context.ServiceSessions
            .Include(s => s.Service)
            .FirstOrDefaultAsync(s => s.Id == sessionId)
            ?? throw new InvalidOperationException("Sesión no encontrada");

        // Cargar cost items del servicio
        var costItems = await _context.ServiceCostItems
            .Where(ci => ci.ServiceId == session.ServiceId)
            .ToListAsync();

        if (costItems.Count == 0)
        {
            _logger.LogInformation("Servicio {ServiceId} no tiene ítems de costo. No se generan gastos.", session.ServiceId);
            return Enumerable.Empty<ServiceSessionExpenseDto>();
        }

        var toAdd = costItems.Select(ci => new ServiceSessionExpense
        {
            BusinessId        = session.BusinessId,
            StoreId           = session.StoreId,
            ServiceSessionId  = sessionId,
            ServiceCostItemId = ci.Id,
            Description       = string.IsNullOrWhiteSpace(ci.Description) ? ci.Name : $"{ci.Name} — {ci.Description}",
            Amount            = ci.Amount,
            Status            = "pending",
            // Poblar destinatario desde el ítem de costo si tiene empleado asignado
            PayeeType         = ci.EmployeeId.HasValue ? "employee" :
                                !string.IsNullOrWhiteSpace(ci.ProviderName) ? "external" : null,
            PayeeEmployeeId   = ci.EmployeeId,
            PayeeEmployeeName = ci.EmployeeName,
            PayeeExternalName = ci.EmployeeId.HasValue ? null : ci.ProviderName,
            CreatedAt         = DateTime.UtcNow,
            UpdatedAt         = DateTime.UtcNow
        }).ToList();

        _context.ServiceSessionExpenses.AddRange(toAdd);
        await _context.SaveChangesAsync();

        _logger.LogInformation("{Count} gasto(s) pendiente(s) generados para sesión {SessionId}", toAdd.Count, sessionId);
        return toAdd.Select(MapToDto);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private async Task<ServiceSessionExpense> FindOrThrowAsync(int id)
    {
        return await _context.ServiceSessionExpenses
            .Include(e => e.ServiceSession).ThenInclude(s => s!.Service)
            .Include(e => e.ServiceCostItem)
            .FirstOrDefaultAsync(e => e.Id == id)
            ?? throw new InvalidOperationException($"Gasto de sesión {id} no encontrado");
    }

    private static ServiceSessionExpenseDto MapToDto(ServiceSessionExpense e)
    {
        return new ServiceSessionExpenseDto
        {
            Id                   = e.Id,
            BusinessId           = e.BusinessId,
            StoreId              = e.StoreId,
            ServiceSessionId     = e.ServiceSessionId,
            ServiceCostItemId    = e.ServiceCostItemId,
            Description          = e.Description,
            Amount               = e.Amount,
            Status               = e.Status,
            PayeeType            = e.PayeeType,
            PayeeEmployeeId      = e.PayeeEmployeeId,
            PayeeEmployeeName    = e.PayeeEmployeeName,
            PayeeExternalName    = e.PayeeExternalName,
            ExpenseId            = e.ExpenseId,
            PaidAt               = e.PaidAt,
            PaidByUserId         = e.PaidByUserId,
            Notes                = e.Notes,
            CreatedAt            = e.CreatedAt,
            UpdatedAt            = e.UpdatedAt,
            SessionDate          = e.ServiceSession?.SessionDate.ToString("yyyy-MM-dd"),
            ServiceName          = e.ServiceSession?.Service?.Name,
            CostItemDescription  = e.ServiceCostItem?.Description,
        };
    }
}
