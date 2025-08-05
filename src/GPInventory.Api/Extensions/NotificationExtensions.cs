using GPInventory.Application.Interfaces;

namespace GPInventory.Api.Extensions
{
    public static class NotificationExtensions
    {
        /// <summary>
        /// Envía notificación cuando se alcanza un hito en quick sale
        /// </summary>
        public static async Task CheckAndSendQuickSaleNotificationAsync(
            this INotificationService notificationService,
            int userId,
            decimal totalAmount,
            string businessName,
            decimal threshold = 100000m)
        {
            if (totalAmount >= threshold)
            {
                await notificationService.SendQuickSaleNotificationAsync(
                    userId,
                    totalAmount.ToString("N0"),
                    businessName
                );
            }
        }

        /// <summary>
        /// Envía notificación cuando el stock de un producto es bajo
        /// </summary>
        public static async Task CheckAndSendLowStockNotificationAsync(
            this INotificationService notificationService,
            int userId,
            string productName,
            int currentStock,
            string businessName,
            int lowStockThreshold = 5)
        {
            if (currentStock <= lowStockThreshold && currentStock > 0)
            {
                await notificationService.SendLowStockNotificationAsync(
                    userId,
                    productName,
                    currentStock,
                    businessName
                );
            }
        }

        /// <summary>
        /// Envía notificación cuando un producto se agota
        /// </summary>
        public static async Task CheckAndSendOutOfStockNotificationAsync(
            this INotificationService notificationService,
            int userId,
            string productName,
            string businessName)
        {
            var notification = await notificationService.SendNotificationToUserAsync(
                userId,
                0, // Asumiendo que el tipo "out_of_stock" tiene ID 5 según el seeder
                new Dictionary<string, string>
                {
                    ["product_name"] = productName,
                    ["business_name"] = businessName,
                    ["date"] = DateTime.Now.ToString("dd/MM/yyyy")
                }
            );
        }

        /// <summary>
        /// Envía notificación cuando se alcanza una meta diaria
        /// </summary>
        public static async Task CheckAndSendDailyGoalNotificationAsync(
            this INotificationService notificationService,
            int userId,
            string userName,
            decimal totalAmount,
            string businessName,
            decimal dailyGoal = 200000m)
        {
            if (totalAmount >= dailyGoal)
            {
                var notification = await notificationService.SendNotificationToUserAsync(
                    userId,
                    0, // Asumiendo que el tipo "daily_goal_reached" tiene ID 6 según el seeder
                    new Dictionary<string, string>
                    {
                        ["name"] = userName,
                        ["amount"] = totalAmount.ToString("N0"),
                        ["business_name"] = businessName,
                        ["date"] = DateTime.Now.ToString("dd/MM/yyyy")
                    }
                );
            }
        }
    }
}
