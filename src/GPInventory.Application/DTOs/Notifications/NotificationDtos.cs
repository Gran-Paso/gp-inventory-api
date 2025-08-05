namespace GPInventory.Application.DTOs.Notifications
{
    public class NotificationDto
    {
        public int Id { get; set; }
        public string TitleTemplate { get; set; } = string.Empty;
        public string MessageTemplate { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
        public bool IsActive { get; set; }
    }

    public class CreateNotificationDto
    {
        public string TitleTemplate { get; set; } = string.Empty;
        public string MessageTemplate { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty;
        public bool IsActive { get; set; } = true;
    }

    public class UserNotificationDto
    {
        public int Id { get; set; }
        public int UserId { get; set; }
        public int NotificationId { get; set; }
        public string? RenderedTitle { get; set; } = string.Empty;
        public string? RenderedMessage { get; set; } = string.Empty;
        public bool? IsRead { get; set; }
        public DateTime? ReadAt { get; set; }
        public DateTime? CreatedAt { get; set; }
        public string Type { get; set; } = string.Empty; // Del notification
    }

    public class CreateUserNotificationDto
    {
        public int UserId { get; set; }
        public int NotificationId { get; set; }
        public Dictionary<string, string> Variables { get; set; } = new();
    }

    public class CreateDirectUserNotificationDto
    {
        public int UserId { get; set; }
        public string Title { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
        public string Type { get; set; } = "info";
    }

    public class MarkAsReadDto
    {
        public int NotificationId { get; set; }
    }

    public class NotificationVariables
    {
        public string Name { get; set; } = string.Empty;
        public string OrderNumber { get; set; } = string.Empty;
        public string Amount { get; set; } = string.Empty;
        public string BusinessName { get; set; } = string.Empty;
        public string Date { get; set; } = string.Empty;
        public string ProductName { get; set; } = string.Empty;
        public string Quantity { get; set; } = string.Empty;
        
        // Método para convertir a dictionary
        public Dictionary<string, string> ToDictionary()
        {
            return new Dictionary<string, string>
            {
                ["name"] = Name,
                ["order_number"] = OrderNumber,
                ["amount"] = Amount,
                ["business_name"] = BusinessName,
                ["date"] = Date,
                ["product_name"] = ProductName,
                ["quantity"] = Quantity
            };
        }
    }
}
