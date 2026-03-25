using GPInventory.Application.DTOs.Services;

namespace GPInventory.Application.Interfaces;

public interface IServiceSessionExpenseService
{
    /// <summary>Obtener todos los gastos pendientes de un negocio.</summary>
    Task<IEnumerable<ServiceSessionExpenseDto>> GetByBusinessAsync(int businessId, string? status = null);

    /// <summary>Obtener todos los gastos de una sesión concreta.</summary>
    Task<IEnumerable<ServiceSessionExpenseDto>> GetBySessionAsync(int sessionId);

    /// <summary>Obtener un gasto por ID.</summary>
    Task<ServiceSessionExpenseDto> GetByIdAsync(int id);

    /// <summary>Crear manualmente un gasto pendiente para una sesión.</summary>
    Task<ServiceSessionExpenseDto> CreateManualAsync(CreateSessionExpenseManualDto dto, int userId);

    /// <summary>Asignar destinatario del pago (empleado RR.HH. o persona externa).</summary>
    Task<ServiceSessionExpenseDto> AssignPayeeAsync(int id, AssignPayeeDto dto);

    /// <summary>Marcar como pagado. Opcionalmente genera registro en tabla expenses.</summary>
    Task<ServiceSessionExpenseDto> MarkPaidAsync(int id, MarkSessionExpensePaidDto dto, int userId);

    /// <summary>Cancelar un gasto pendiente.</summary>
    Task<ServiceSessionExpenseDto> CancelAsync(int id, string? reason = null);

    /// <summary>Genera automáticamente gastos a partir de los cost_items del servicio de la sesión.</summary>
    Task<IEnumerable<ServiceSessionExpenseDto>> GenerateFromSessionAsync(int sessionId);
}
