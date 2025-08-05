using GPInventory.Application.DTOs.Notifications;

namespace GPInventory.Application.Interfaces
{
    public interface INotificationService
    {
        // Gestión de plantillas de notificaciones
        Task<NotificationDto> CreateNotificationTemplateAsync(CreateNotificationDto createDto);
        Task<IEnumerable<NotificationDto>> GetNotificationTemplatesAsync();
        Task<NotificationDto?> GetNotificationTemplateAsync(int id);
        Task<bool> UpdateNotificationTemplateAsync(int id, CreateNotificationDto updateDto);
        Task<bool> DeleteNotificationTemplateAsync(int id);

        // Gestión de notificaciones de usuario
        Task<UserNotificationDto> CreateUserNotificationAsync(CreateUserNotificationDto createDto);
        Task<UserNotificationDto> CreateDirectUserNotificationAsync(CreateDirectUserNotificationDto createDto);
        Task<IEnumerable<UserNotificationDto>> GetUserNotificationsAsync(int userId, bool? isRead = null);
        Task<int> GetUnreadCountAsync(int userId);
        Task<bool> MarkAsReadAsync(int userId, int notificationId);
        Task<bool> MarkAllAsReadAsync(int userId);

        // Método principal para crear notificación renderizada
        Task<UserNotificationDto> SendNotificationToUserAsync(int userId, int notificationTemplateId, Dictionary<string, string> variables);

        // Métodos de conveniencia para tipos específicos de notificaciones
        Task SendQuickSaleNotificationAsync(int userId, string amount, string businessName);
        Task SendLowStockNotificationAsync(int userId, string productName, int quantity, string businessName);
        Task SendOrderNotificationAsync(int userId, string orderNumber, string amount);
        Task SendBreakevenNotificationAsync(int businessId, string businessName, decimal totalRevenue, decimal totalCosts);

        // Método para actualizar notificaciones existentes
        Task<int> UpdateExistingNotificationsAsync();
    }
}
