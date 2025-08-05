using GPInventory.Domain.Entities;

namespace GPInventory.Application.Interfaces
{
    public interface INotificationRepository
    {
        // Notifications (templates)
        Task<Notification> CreateAsync(Notification notification);
        Task<IEnumerable<Notification>> GetAllAsync();
        Task<Notification?> GetByIdAsync(int id);
        Task<Notification?> GetByTypeAsync(string type);
        Task<bool> UpdateAsync(Notification notification);
        Task<bool> DeleteAsync(int id);

        // User Notifications
        Task<UserNotification> CreateUserNotificationAsync(UserNotification userNotification);
        Task<IEnumerable<UserNotification>> GetUserNotificationsAsync(int userId, bool? isRead = null);
        Task<UserNotification?> GetUserNotificationAsync(int userId, int notificationId);
        Task<int> GetUnreadCountAsync(int userId);
        Task<bool> MarkAsReadAsync(int userId, int notificationId);
        Task<bool> MarkAllAsReadAsync(int userId);
        Task<bool> UpdateUserNotificationAsync(UserNotification userNotification);
        Task<IEnumerable<UserNotification>> GetUserNotificationsWithNullRenderedFieldsAsync();
    }
}
