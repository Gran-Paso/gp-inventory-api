using GPInventory.Application.Interfaces;
using GPInventory.Domain.Entities;
using GPInventory.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace GPInventory.Infrastructure.Repositories
{
    public class NotificationRepository : INotificationRepository
    {
        private readonly ApplicationDbContext _context;

        public NotificationRepository(ApplicationDbContext context)
        {
            _context = context;
        }

        #region Notifications (Templates)

        public async Task<Notification> CreateAsync(Notification notification)
        {
            _context.Notifications.Add(notification);
            await _context.SaveChangesAsync();
            return notification;
        }

        public async Task<IEnumerable<Notification>> GetAllAsync()
        {
            return await _context.Notifications
                .Where(n => n.IsActive)
                .OrderBy(n => n.CreatedAt)
                .ToListAsync();
        }

        public async Task<Notification?> GetByIdAsync(int id)
        {
            return await _context.Notifications
                .FirstOrDefaultAsync(n => n.Id == id);
        }

        public async Task<Notification?> GetByTypeAsync(string type)
        {
            return await _context.Notifications
                .FirstOrDefaultAsync(n => n.Type == type && n.IsActive);
        }

        public async Task<bool> UpdateAsync(Notification notification)
        {
            _context.Notifications.Update(notification);
            return await _context.SaveChangesAsync() > 0;
        }

        public async Task<bool> DeleteAsync(int id)
        {
            var notification = await GetByIdAsync(id);
            if (notification == null) return false;

            notification.IsActive = false; // Soft delete
            return await UpdateAsync(notification);
        }

        #endregion

        #region User Notifications

        public async Task<UserNotification> CreateUserNotificationAsync(UserNotification userNotification)
        {
            _context.UserNotifications.Add(userNotification);
            await _context.SaveChangesAsync();
            return userNotification;
        }

        public async Task<IEnumerable<UserNotification>> GetUserNotificationsAsync(int userId, bool? isRead = null)
        {
            var query = _context.UserNotifications
                .Include(un => un.Notification)
                .Where(un => un.UserId == userId);

            if (isRead.HasValue)
            {
                query = query.Where(un => un.IsRead == isRead.Value);
            }

            return await query
                .OrderByDescending(un => un.CreatedAt ?? DateTime.MinValue)
                .ThenByDescending(un => un.Id)
                .ToListAsync();
        }

        public async Task<UserNotification?> GetUserNotificationAsync(int userId, int notificationId)
        {
            return await _context.UserNotifications
                .Include(un => un.Notification)
                .FirstOrDefaultAsync(un => un.UserId == userId && un.Id == notificationId);
        }

        public async Task<int> GetUnreadCountAsync(int userId)
        {
            return await _context.UserNotifications
                .CountAsync(un => un.UserId == userId && (un.IsRead == null || un.IsRead == false));
        }

        public async Task<bool> MarkAsReadAsync(int userId, int notificationId)
        {
            var userNotification = await GetUserNotificationAsync(userId, notificationId);
            if (userNotification == null || userNotification.IsRead == true) return false;

            userNotification.IsRead = true;
            userNotification.ReadAt = DateTime.UtcNow;

            return await UpdateUserNotificationAsync(userNotification);
        }

        public async Task<bool> MarkAllAsReadAsync(int userId)
        {
            var unreadNotifications = await _context.UserNotifications
                .Where(un => un.UserId == userId && (un.IsRead == null || un.IsRead == false))
                .ToListAsync();

            if (!unreadNotifications.Any()) return true;

            var readAt = DateTime.UtcNow;
            foreach (var notification in unreadNotifications)
            {
                notification.IsRead = true;
                notification.ReadAt = readAt;
            }

            return await _context.SaveChangesAsync() > 0;
        }

        public async Task<bool> UpdateUserNotificationAsync(UserNotification userNotification)
        {
            _context.UserNotifications.Update(userNotification);
            return await _context.SaveChangesAsync() > 0;
        }

        public async Task<IEnumerable<UserNotification>> GetUserNotificationsWithNullRenderedFieldsAsync()
        {
            return await _context.UserNotifications
                .Include(un => un.Notification)
                .Where(un => un.RenderedTitle == null || un.RenderedMessage == null)
                .ToListAsync();
        }

        #endregion
    }
}
