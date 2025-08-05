using GPInventory.Domain.Entities;
using GPInventory.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace GPInventory.Infrastructure.Data.Seeders
{
    public static class NotificationSeeder
    {
        public static async Task SeedNotificationsAsync(ApplicationDbContext context)
        {
            // Verificar si ya existen notificaciones
            if (await context.Notifications.AnyAsync())
                return;

            var notifications = new List<Notification>
            {
                new()
                {
                    TitleTemplate = "🎉 ¡Felicitaciones, {{name}}!",
                    MessageTemplate = "Has alcanzado los ${{amount}} en ventas hoy en {{business_name}}. ¡Excelente trabajo!",
                    Type = "quick_sale_milestone",
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow
                },
                new()
                {
                    TitleTemplate = "⚠️ Stock Bajo",
                    MessageTemplate = "El producto {{product_name}} tiene solo {{quantity}} unidades en stock en {{business_name}}. Considera hacer un nuevo pedido.",
                    Type = "low_stock",
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow
                },
                new()
                {
                    TitleTemplate = "✅ Pedido Completado",
                    MessageTemplate = "El pedido #{{order_number}} por ${{amount}} ha sido completado exitosamente el {{date}}.",
                    Type = "order_completed",
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow
                },
                new()
                {
                    TitleTemplate = "📦 Nuevo Producto Agregado",
                    MessageTemplate = "El producto {{product_name}} ha sido agregado al inventario de {{business_name}} con {{quantity}} unidades.",
                    Type = "product_added",
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow
                },
                new()
                {
                    TitleTemplate = "🚨 Stock Agotado",
                    MessageTemplate = "¡ATENCIÓN! El producto {{product_name}} se ha agotado en {{business_name}}. Restock urgente requerido.",
                    Type = "out_of_stock",
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow
                },
                new()
                {
                    TitleTemplate = "📈 Meta Diaria Alcanzada",
                    MessageTemplate = "¡Increíble, {{name}}! Has superado la meta de ventas diaria con ${{amount}} en {{business_name}}.",
                    Type = "daily_goal_reached",
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow
                },
                new()
                {
                    TitleTemplate = "🔄 Actualización de Inventario",
                    MessageTemplate = "El inventario de {{product_name}} ha sido actualizado. Nueva cantidad: {{quantity}} unidades en {{business_name}}.",
                    Type = "inventory_update",
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow
                }
            };

            await context.Notifications.AddRangeAsync(notifications);
            await context.SaveChangesAsync();
        }
    }
}
