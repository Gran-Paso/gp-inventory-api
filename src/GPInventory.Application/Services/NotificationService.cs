using System.Text.Json;
using AutoMapper;
using GPInventory.Application.DTOs.Notifications;
using GPInventory.Application.Interfaces;
using GPInventory.Domain.Entities;

namespace GPInventory.Application.Services
{
    public class NotificationService : INotificationService
    {
        private readonly INotificationRepository _notificationRepository;
        private readonly IUserRepository _userRepository;
        private readonly IMapper _mapper;

        public NotificationService(INotificationRepository notificationRepository, IUserRepository userRepository, IMapper mapper)
        {
            _notificationRepository = notificationRepository;
            _userRepository = userRepository;
            _mapper = mapper;
        }

        #region Template Management

        public async Task<NotificationDto> CreateNotificationTemplateAsync(CreateNotificationDto createDto)
        {
            var notification = _mapper.Map<Notification>(createDto);
            var created = await _notificationRepository.CreateAsync(notification);
            return _mapper.Map<NotificationDto>(created);
        }

        public async Task<IEnumerable<NotificationDto>> GetNotificationTemplatesAsync()
        {
            var notifications = await _notificationRepository.GetAllAsync();
            return _mapper.Map<IEnumerable<NotificationDto>>(notifications);
        }

        public async Task<NotificationDto?> GetNotificationTemplateAsync(int id)
        {
            var notification = await _notificationRepository.GetByIdAsync(id);
            return notification != null ? _mapper.Map<NotificationDto>(notification) : null;
        }

        public async Task<bool> UpdateNotificationTemplateAsync(int id, CreateNotificationDto updateDto)
        {
            var notification = await _notificationRepository.GetByIdAsync(id);
            if (notification == null) return false;

            _mapper.Map(updateDto, notification);
            return await _notificationRepository.UpdateAsync(notification);
        }

        public async Task<bool> DeleteNotificationTemplateAsync(int id)
        {
            return await _notificationRepository.DeleteAsync(id);
        }

        #endregion

        #region User Notifications

        public async Task<UserNotificationDto> CreateUserNotificationAsync(CreateUserNotificationDto createDto)
        {
            var notification = await _notificationRepository.GetByIdAsync(createDto.NotificationId);
            if (notification == null)
                throw new ArgumentException("Notification template not found");

            var renderedTitle = RenderTemplate(notification.TitleTemplate, createDto.Variables);
            var renderedMessage = RenderTemplate(notification.MessageTemplate, createDto.Variables);

            var userNotification = new UserNotification
            {
                UserId = createDto.UserId,
                NotificationId = createDto.NotificationId,
                RenderedTitle = renderedTitle,
                RenderedMessage = renderedMessage,
                Variables = JsonSerializer.Serialize(createDto.Variables),
                CreatedAt = DateTime.UtcNow
            };

            var created = await _notificationRepository.CreateUserNotificationAsync(userNotification);
            var result = _mapper.Map<UserNotificationDto>(created);
            result.Type = notification.Type;
            return result;
        }

        public async Task<UserNotificationDto> CreateDirectUserNotificationAsync(CreateDirectUserNotificationDto createDto)
        {
            // Buscar o crear un template genérico
            var genericTemplate = await _notificationRepository.GetByTypeAsync("direct_message");
            if (genericTemplate == null)
            {
                // Crear un template genérico si no existe
                genericTemplate = new Notification
                {
                    TitleTemplate = "{{title}}", // Template que será reemplazado
                    MessageTemplate = "{{message}}", // Template que será reemplazado
                    Type = "direct_message",
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow
                };
                genericTemplate = await _notificationRepository.CreateAsync(genericTemplate);
            }

            // Crear la notificación con el mensaje completo renderizado
            var userNotification = new UserNotification
            {
                UserId = createDto.UserId,
                NotificationId = genericTemplate.Id,
                RenderedTitle = createDto.Title, // Mensaje final sin variables
                RenderedMessage = createDto.Message, // Mensaje final sin variables
                Variables = JsonSerializer.Serialize(new { title = createDto.Title, message = createDto.Message }), // Para auditoría
                CreatedAt = DateTime.UtcNow
            };

            var created = await _notificationRepository.CreateUserNotificationAsync(userNotification);
            var result = _mapper.Map<UserNotificationDto>(created);
            result.Type = createDto.Type;
            return result;
        }

        public async Task<IEnumerable<UserNotificationDto>> GetUserNotificationsAsync(int userId, bool? isRead = null)
        {
            var userNotifications = await _notificationRepository.GetUserNotificationsAsync(userId, isRead);
            var result = new List<UserNotificationDto>();

            foreach (var userNotification in userNotifications)
            {
                // Renderizar título y mensaje dinámicamente
                if (userNotification.Notification != null)
                {
                    var variables = new Dictionary<string, string>();
                    if (!string.IsNullOrEmpty(userNotification.Variables))
                    {
                        try
                        {
                            variables = JsonSerializer.Deserialize<Dictionary<string, string>>(userNotification.Variables) ?? new Dictionary<string, string>();
                        }
                        catch
                        {
                            variables = new Dictionary<string, string>();
                        }
                    }

                    userNotification.RenderedTitle = RenderTemplate(userNotification.Notification.TitleTemplate, variables);
                    userNotification.RenderedMessage = RenderTemplate(userNotification.Notification.MessageTemplate, variables);
                }

                var dto = _mapper.Map<UserNotificationDto>(userNotification);
                result.Add(dto);
            }

            return result;
        }

        public async Task<int> GetUnreadCountAsync(int userId)
        {
            return await _notificationRepository.GetUnreadCountAsync(userId);
        }

        public async Task<bool> MarkAsReadAsync(int userId, int notificationId)
        {
            return await _notificationRepository.MarkAsReadAsync(userId, notificationId);
        }

        public async Task<bool> MarkAllAsReadAsync(int userId)
        {
            return await _notificationRepository.MarkAllAsReadAsync(userId);
        }

        public async Task<UserNotificationDto> SendNotificationToUserAsync(int userId, int notificationTemplateId, Dictionary<string, string> variables)
        {
            var createDto = new CreateUserNotificationDto
            {
                UserId = userId,
                NotificationId = notificationTemplateId,
                Variables = variables
            };

            return await CreateUserNotificationAsync(createDto);
        }

        #endregion

        #region Convenience Methods

        public async Task SendQuickSaleNotificationAsync(int userId, string amount, string businessName)
        {
            // Buscar o crear template específico para ventas
            var template = await _notificationRepository.GetByTypeAsync("quick_sale_milestone");
            if (template == null)
            {
                template = new Notification
                {
                    TitleTemplate = "¡Felicitaciones, {{name}}!",
                    MessageTemplate = "Has alcanzado los ${{amount}} en ventas hoy en {{business_name}}. ¡Excelente trabajo!",
                    Type = "quick_sale_milestone",
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow
                };
                template = await _notificationRepository.CreateAsync(template);
            }

            // Obtener el nombre real del usuario
            var userName = await GetUserNameAsync(userId);

            // Variables para reemplazar en el template
            var variables = new Dictionary<string, string>
            {
                ["name"] = userName, // Nombre real del usuario
                ["amount"] = amount,
                ["business_name"] = businessName
            };

            // Renderizar el mensaje final
            var renderedTitle = RenderTemplate(template.TitleTemplate, variables);
            var renderedMessage = RenderTemplate(template.MessageTemplate, variables);

            // Crear la notificación con mensajes finales
            var userNotification = new UserNotification
            {
                UserId = userId,
                NotificationId = template.Id,
                RenderedTitle = renderedTitle, // "¡Felicitaciones, Juan!"
                RenderedMessage = renderedMessage, // "Has alcanzado los $150000 en ventas hoy en Gran Paso Store. ¡Excelente trabajo!"
                Variables = JsonSerializer.Serialize(variables),
                CreatedAt = DateTime.UtcNow
            };

            await _notificationRepository.CreateUserNotificationAsync(userNotification);
        }

        public async Task SendLowStockNotificationAsync(int userId, string productName, int quantity, string businessName)
        {
            // Buscar o crear template específico para stock bajo
            var template = await _notificationRepository.GetByTypeAsync("low_stock");
            if (template == null)
            {
                template = new Notification
                {
                    TitleTemplate = "⚠️ Stock Bajo",
                    MessageTemplate = "El producto '{{product_name}}' tiene solo {{quantity}} unidades en {{business_name}}. Considera reabastecer.",
                    Type = "low_stock",
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow
                };
                template = await _notificationRepository.CreateAsync(template);
            }

            // Variables para reemplazar en el template
            var variables = new Dictionary<string, string>
            {
                ["product_name"] = productName,
                ["quantity"] = quantity.ToString(),
                ["business_name"] = businessName
            };

            // Renderizar el mensaje final
            var renderedTitle = RenderTemplate(template.TitleTemplate, variables);
            var renderedMessage = RenderTemplate(template.MessageTemplate, variables);

            // Crear la notificación con mensajes finales
            var userNotification = new UserNotification
            {
                UserId = userId,
                NotificationId = template.Id,
                RenderedTitle = renderedTitle, // "⚠️ Stock Bajo"
                RenderedMessage = renderedMessage, // "El producto 'Coca Cola' tiene solo 5 unidades en Gran Paso Store. Considera reabastecer."
                Variables = JsonSerializer.Serialize(variables),
                CreatedAt = DateTime.UtcNow
            };

            await _notificationRepository.CreateUserNotificationAsync(userNotification);
        }

        public async Task SendOrderNotificationAsync(int userId, string orderNumber, string amount)
        {
            // Buscar o crear template específico para pedidos
            var template = await _notificationRepository.GetByTypeAsync("order_completed");
            if (template == null)
            {
                template = new Notification
                {
                    TitleTemplate = "✅ Pedido Completado",
                    MessageTemplate = "Tu pedido #{{order_number}} por ${{amount}} ha sido procesado exitosamente.",
                    Type = "order_completed",
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow
                };
                template = await _notificationRepository.CreateAsync(template);
            }

            // Variables para reemplazar en el template
            var variables = new Dictionary<string, string>
            {
                ["order_number"] = orderNumber,
                ["amount"] = amount
            };

            // Renderizar el mensaje final
            var renderedTitle = RenderTemplate(template.TitleTemplate, variables);
            var renderedMessage = RenderTemplate(template.MessageTemplate, variables);

            // Crear la notificación con mensajes finales
            var userNotification = new UserNotification
            {
                UserId = userId,
                NotificationId = template.Id,
                RenderedTitle = renderedTitle, // "✅ Pedido Completado"
                RenderedMessage = renderedMessage, // "Tu pedido #12345 por $250 ha sido procesado exitosamente."
                Variables = JsonSerializer.Serialize(variables),
                CreatedAt = DateTime.UtcNow
            };

            await _notificationRepository.CreateUserNotificationAsync(userNotification);
        }

        public async Task SendBreakevenNotificationAsync(int businessId, string businessName, decimal totalRevenue, decimal totalCosts)
        {
            // Buscar o crear template específico para breakeven
            var template = await _notificationRepository.GetByTypeAsync("breakeven_achievement");
            if (template == null)
            {
                template = new Notification
                {
                    TitleTemplate = "🎉 ¡Punto de Equilibrio Diario Alcanzado!",
                    MessageTemplate = "¡Excelente! {{business_name}} ha cubierto todos los costos del día (${{total_costs}}) con ingresos de ${{total_revenue}}. ¡De aquí en adelante toda venta de hoy es ganancia pura! | Negocio: {{business_name}}",
                    Type = "breakeven_achievement",
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow
                };
                template = await _notificationRepository.CreateAsync(template);
            }

            // Variables para reemplazar en el template
            var variables = new Dictionary<string, string>
            {
                ["business_name"] = businessName,
                ["total_revenue"] = totalRevenue.ToString("N0"),
                ["total_costs"] = totalCosts.ToString("N0"),
                ["business_id"] = businessId.ToString()
            };

            // Renderizar el mensaje final
            var renderedTitle = RenderTemplate(template.TitleTemplate, variables);
            var renderedMessage = RenderTemplate(template.MessageTemplate, variables);

            // Buscar todos los usuarios con roles Manager y Operador en este negocio
            var targetUsers = await GetBusinessUsersWithRolesAsync(businessId, new[] { "Manager", "Operador" });

            // Crear notificación para cada usuario elegible
            foreach (var user in targetUsers)
            {
                var userNotification = new UserNotification
                {
                    UserId = user.UserId,
                    NotificationId = template.Id,
                    RenderedTitle = renderedTitle, // "🎉 ¡Punto de Equilibrio Alcanzado!"
                    RenderedMessage = renderedMessage, // "Gran Paso Store ha cubierto todos los costos ($50000) con ingresos de $52000. ¡De aquí en adelante todo es ganancia pura!"
                    Variables = JsonSerializer.Serialize(variables),
                    CreatedAt = DateTime.UtcNow
                };

                await _notificationRepository.CreateUserNotificationAsync(userNotification);
            }
        }

        public async Task<int> UpdateExistingNotificationsAsync()
        {
            var notificationsToUpdate = await _notificationRepository.GetUserNotificationsWithNullRenderedFieldsAsync();
            int updatedCount = 0;

            foreach (var userNotification in notificationsToUpdate)
            {
                if (!string.IsNullOrEmpty(userNotification.Variables))
                {
                    try
                    {
                        var variables = JsonSerializer.Deserialize<Dictionary<string, string>>(userNotification.Variables) ?? new();
                        
                        userNotification.RenderedTitle = RenderTemplate(userNotification.Notification.TitleTemplate, variables);
                        userNotification.RenderedMessage = RenderTemplate(userNotification.Notification.MessageTemplate, variables);
                        
                        await _notificationRepository.UpdateUserNotificationAsync(userNotification);
                        updatedCount++;
                    }
                    catch (Exception)
                    {
                        // Si hay error al deserializar, usar el template original
                        userNotification.RenderedTitle = userNotification.Notification.TitleTemplate;
                        userNotification.RenderedMessage = userNotification.Notification.MessageTemplate;
                        await _notificationRepository.UpdateUserNotificationAsync(userNotification);
                        updatedCount++;
                    }
                }
                else
                {
                    // Si no hay variables, usar el template original
                    userNotification.RenderedTitle = userNotification.Notification.TitleTemplate;
                    userNotification.RenderedMessage = userNotification.Notification.MessageTemplate;
                    await _notificationRepository.UpdateUserNotificationAsync(userNotification);
                    updatedCount++;
                }
            }

            return updatedCount;
        }

        #endregion

        #region Private Methods

        private static string RenderTemplate(string template, Dictionary<string, string> variables)
        {
            var result = template;
            foreach (var variable in variables)
            {
                result = result.Replace($"{{{{{variable.Key}}}}}", variable.Value);
            }
            return result;
        }

        private async Task<string> GetUserNameAsync(int userId)
        {
            try
            {
                var user = await _userRepository.GetByIdAsync(userId);
                return user?.Name ?? "Usuario";
            }
            catch
            {
                return "Usuario";
            }
        }

        private async Task<List<(int UserId, string UserName, string RoleName)>> GetBusinessUsersWithRolesAsync(int businessId, string[] targetRoles)
        {
            try
            {
                // Usar el UserRepository para obtener usuarios con roles específicos
                return await _userRepository.GetBusinessUsersWithRolesAsync(businessId, targetRoles);
            }
            catch
            {
                return new List<(int UserId, string UserName, string RoleName)>();
            }
        }

        #endregion
    }
}
