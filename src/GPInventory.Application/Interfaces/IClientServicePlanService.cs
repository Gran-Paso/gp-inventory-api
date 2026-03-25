using GPInventory.Application.DTOs.Services;

namespace GPInventory.Application.Interfaces;

/// <summary>
/// Servicio para gestión de planes activos de clientes
/// </summary>
public interface IClientServicePlanService
{
    Task<ClientServicePlanDto> GetByIdAsync(int id);
    Task<IEnumerable<ClientServicePlanDto>> GetByClientAsync(int clientId);
    Task<IEnumerable<ClientServicePlanDto>> GetActiveByClientAsync(int clientId);
    Task<IEnumerable<ClientServicePlanDto>> GetActiveByBusinessAsync(int businessId);
    Task<IEnumerable<ClientServicePlanDto>> GetExpiringPlansAsync(int businessId, int daysThreshold = 7);
    Task<IEnumerable<ClientServicePlanDto>> GetHighRiskPlansAsync(int businessId);
    Task<ClientServicePlanDto> PurchasePlanAsync(PurchaseServicePlanDto dto, int userId);
    Task<PlanEnrollmentGroupDto> PurchaseGroupEnrollmentAsync(PurchaseGroupEnrollmentDto dto, int userId);
    Task<PlanEnrollmentGroupDto> GetGroupByIdAsync(int groupId);
    Task<IEnumerable<PlanEnrollmentGroupDto>> GetGroupsByPayerAsync(int payerClientId);
    Task<ClientServicePlanDto> FreezePlanAsync(int id, FreezePlanDto dto);
    Task<ClientServicePlanDto> UnfreezePlanAsync(int id);
    Task<ClientServicePlanDto> CancelPlanAsync(int id, CancelPlanDto dto);
    Task ExpireOldPlansAsync(int businessId);
    Task<ClientDashboardDto> GetClientDashboardAsync(int clientId);
    Task<DeferredRevenueReportDto> GetDeferredRevenueReportAsync(int businessId);
}
