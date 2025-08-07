using GPInventory.Application.DTOs.Notifications;
using GPInventory.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace GPInventory.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class NotificationsController : ControllerBase
    {
        private readonly INotificationService _notificationService;

        public NotificationsController(INotificationService notificationService)
        {
            _notificationService = notificationService;
        }

        #region Template Management (Administrador only)

        [HttpPost("templates")]
        [Authorize(Roles = "Administrador")]
        public async Task<ActionResult<NotificationDto>> CreateTemplate([FromBody] CreateNotificationDto createDto)
        {
            try
            {
                var result = await _notificationService.CreateNotificationTemplateAsync(createDto);
                return Ok(result);
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        [HttpGet("templates")]
        [Authorize(Roles = "Administrador")]
        public async Task<ActionResult<IEnumerable<NotificationDto>>> GetTemplates()
        {
            var templates = await _notificationService.GetNotificationTemplatesAsync();
            return Ok(templates);
        }

        [HttpGet("templates/{id}")]
        [Authorize(Roles = "Administrador")]
        public async Task<ActionResult<NotificationDto>> GetTemplate(int id)
        {
            var template = await _notificationService.GetNotificationTemplateAsync(id);
            if (template == null)
                return NotFound(new { message = "Template not found" });

            return Ok(template);
        }

        [HttpPut("templates/{id}")]
        [Authorize(Roles = "Administrador")]
        public async Task<IActionResult> UpdateTemplate(int id, [FromBody] CreateNotificationDto updateDto)
        {
            try
            {
                var success = await _notificationService.UpdateNotificationTemplateAsync(id, updateDto);
                if (!success)
                    return NotFound(new { message = "Template not found" });

                return Ok(new { message = "Template updated successfully" });
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        [HttpDelete("templates/{id}")]
        [Authorize(Roles = "Administrador")]
        public async Task<IActionResult> DeleteTemplate(int id)
        {
            var success = await _notificationService.DeleteNotificationTemplateAsync(id);
            if (!success)
                return NotFound(new { message = "Template not found" });

            return Ok(new { message = "Template deleted successfully" });
        }

        #endregion

        #region User Notifications

        [HttpGet]
        public async Task<ActionResult<IEnumerable<UserNotificationDto>>> GetMyNotifications([FromQuery] bool? isRead = null)
        {
            var userId = GetCurrentUserId();
            var notifications = await _notificationService.GetUserNotificationsAsync(userId, isRead);
            return Ok(notifications);
        }

        [HttpGet("user/{userId}")]
        public async Task<ActionResult<IEnumerable<UserNotificationDto>>> GetUserNotifications(int userId, [FromQuery] bool? isRead = null)
        {
            // Verificar que el usuario solo pueda acceder a sus propias notificaciones o sea administrador
            var currentUserId = GetCurrentUserId();
            if (currentUserId != userId && !User.IsInRole("Administrador"))
            {
                return Forbid("You can only access your own notifications");
            }

            var notifications = await _notificationService.GetUserNotificationsAsync(userId, isRead);
            return Ok(notifications);
        }

        [HttpGet("user/{userId}/unread-count")]
        public async Task<ActionResult<int>> GetUserUnreadCount(int userId)
        {
            // Verificar que el usuario solo pueda acceder a sus propias notificaciones o sea administrador
            var currentUserId = GetCurrentUserId();
            if (currentUserId != userId && !User.IsInRole("Administrador"))
            {
                return Forbid("You can only access your own notifications");
            }

            var count = await _notificationService.GetUnreadCountAsync(userId);
            return Ok(new { count });
        }

        [HttpGet("unread-count")]
        public async Task<ActionResult<int>> GetUnreadCount()
        {
            var userId = GetCurrentUserId();
            var count = await _notificationService.GetUnreadCountAsync(userId);
            return Ok(new { count });
        }

        [HttpPut("{notificationId}/mark-read")]
        public async Task<IActionResult> MarkAsRead(int notificationId)
        {
            var userId = GetCurrentUserId();
            var success = await _notificationService.MarkAsReadAsync(userId, notificationId);
            
            if (!success)
                return NotFound(new { message = "Notification not found" });

            return Ok(new { message = "Notification marked as read" });
        }

        [HttpPut("mark-all-read")]
        public async Task<IActionResult> MarkAllAsRead()
        {
            var userId = GetCurrentUserId();
            await _notificationService.MarkAllAsReadAsync(userId);
            return Ok(new { message = "All notifications marked as read" });
        }

        #endregion

        #region Create User Notifications

        [HttpPost]
        public async Task<ActionResult<UserNotificationDto>> CreateUserNotification([FromBody] CreateUserNotificationDto createDto)
        {
            try
            {
                var userId = GetCurrentUserId();
                
                // Si se especifica un userId diferente, verificar permisos
                if (createDto.UserId.HasValue && createDto.UserId.Value != userId && !User.IsInRole("Administrador"))
                {
                    return Forbid("You can only create notifications for yourself");
                }

                var targetUserId = createDto.UserId ?? userId;
                
                // Por ahora usamos el método de Quick Sale como ejemplo
                await _notificationService.SendQuickSaleNotificationAsync(
                    targetUserId, 
                    "150000", // amount
                    "Gran Paso Store" // business name
                );
                
                return Ok(new { message = "Notification created successfully" });
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        [HttpPost("direct")]
        public async Task<ActionResult<UserNotificationDto>> CreateDirectNotification([FromBody] CreateDirectUserNotificationDto createDto)
        {
            try
            {
                var userId = GetCurrentUserId();
                
                // Si se especifica un userId diferente, verificar permisos
                if (createDto.UserId != userId && !User.IsInRole("Administrador"))
                {
                    return Forbid("You can only create notifications for yourself");
                }

                var notification = await _notificationService.CreateDirectUserNotificationAsync(createDto);
                return Ok(notification);
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        #endregion

        #region Administrador Methods

        [HttpPost("update-existing")]
        [Authorize(Roles = "Administrador")]
        public async Task<IActionResult> UpdateExistingNotifications()
        {
            try
            {
                var updatedCount = await _notificationService.UpdateExistingNotificationsAsync();
                return Ok(new { message = $"Updated {updatedCount} notifications successfully" });
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        #endregion

        #region Helper Methods

        private int GetCurrentUserId()
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);
            if (userIdClaim == null || !int.TryParse(userIdClaim.Value, out int userId))
            {
                throw new UnauthorizedAccessException("User ID not found in token");
            }
            return userId;
        }

        #endregion

        #region Test Endpoints (Development only)

        [HttpPost("test/quick-sale")]
        [Authorize(Roles = "Administrador,Dueño")]
        public async Task<IActionResult> TestQuickSaleNotification([FromBody] TestNotificationDto testDto)
        {
            try
            {
                var userId = GetCurrentUserId();
                await _notificationService.SendQuickSaleNotificationAsync(userId, testDto.Amount, testDto.BusinessName);
                return Ok(new { message = "Test notification sent" });
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        [HttpPost("test/low-stock")]
        [Authorize(Roles = "Administrador,Dueño")]
        public async Task<IActionResult> TestLowStockNotification([FromBody] TestLowStockDto testDto)
        {
            try
            {
                var userId = GetCurrentUserId();
                await _notificationService.SendLowStockNotificationAsync(userId, testDto.ProductName, testDto.Quantity, testDto.BusinessName);
                return Ok(new { message = "Test notification sent" });
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        [HttpPost("test/breakeven")]
        [Authorize(Roles = "Administrador,Dueño")]
        public async Task<IActionResult> TestBreakevenNotification([FromBody] TestBreakevenDto testDto)
        {
            try
            {
                await _notificationService.SendBreakevenNotificationAsync(
                    testDto.BusinessId, 
                    testDto.BusinessName, 
                    testDto.TotalRevenue, 
                    testDto.TotalCosts
                );
                return Ok(new { message = "Breakeven notification sent to business owners" });
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        #endregion
    }

    // DTOs for testing
    public class TestNotificationDto
    {
        public string Amount { get; set; } = string.Empty;
        public string BusinessName { get; set; } = string.Empty;
    }

    public class TestLowStockDto
    {
        public string ProductName { get; set; } = string.Empty;
        public int Quantity { get; set; }
        public string BusinessName { get; set; } = string.Empty;
    }

    public class TestBreakevenDto
    {
        public int BusinessId { get; set; }
        public string BusinessName { get; set; } = string.Empty;
        public decimal TotalRevenue { get; set; }
        public decimal TotalCosts { get; set; }
    }

    public class CreateUserNotificationDto
    {
        public int? UserId { get; set; }
        public int NotificationId { get; set; }
        public string? Variables { get; set; }
    }
}
