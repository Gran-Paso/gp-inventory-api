using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace GPInventory.Api.Hubs;

/// <summary>
/// Hub de tiempo real para el módulo CRM.
/// Los clientes se unen al grupo "business-{id}" y reciben eventos
/// cuando otro usuario modifica datos de ese negocio.
/// </summary>
[Authorize]
public class CrmHub : Hub
{
    private readonly ILogger<CrmHub> _logger;

    public CrmHub(ILogger<CrmHub> logger)
    {
        _logger = logger;
    }

    /// El cliente llama a esto justo después de conectarse.
    public async Task JoinBusiness(int businessId)
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, GroupName(businessId));
        _logger.LogInformation("CrmHub: {C} joined business-{B}", Context.ConnectionId, businessId);
    }

    public async Task LeaveBusiness(int businessId)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, GroupName(businessId));
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        _logger.LogInformation("CrmHub: {C} disconnected", Context.ConnectionId);
        await base.OnDisconnectedAsync(exception);
    }

    public static string GroupName(int businessId) => $"crm-business-{businessId}";
}
