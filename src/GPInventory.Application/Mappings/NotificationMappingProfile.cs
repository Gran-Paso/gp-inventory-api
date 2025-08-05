using AutoMapper;
using GPInventory.Application.DTOs.Notifications;
using GPInventory.Domain.Entities;

namespace GPInventory.Application.Mappings
{
    public class NotificationMappingProfile : Profile
    {
        public NotificationMappingProfile()
        {
            // Notification mappings
            CreateMap<Notification, NotificationDto>();
            CreateMap<CreateNotificationDto, Notification>()
                .ForMember(dest => dest.Id, opt => opt.Ignore())
                .ForMember(dest => dest.CreatedAt, opt => opt.Ignore())
                .ForMember(dest => dest.UserNotifications, opt => opt.Ignore());

            // UserNotification mappings
            CreateMap<UserNotification, UserNotificationDto>()
                .ForMember(dest => dest.Type, opt => opt.MapFrom(src => src.Notification.Type));
            
            CreateMap<CreateUserNotificationDto, UserNotification>()
                .ForMember(dest => dest.Id, opt => opt.Ignore())
                .ForMember(dest => dest.RenderedTitle, opt => opt.Ignore())
                .ForMember(dest => dest.RenderedMessage, opt => opt.Ignore())
                .ForMember(dest => dest.IsRead, opt => opt.Ignore())
                .ForMember(dest => dest.ReadAt, opt => opt.Ignore())
                .ForMember(dest => dest.CreatedAt, opt => opt.Ignore())
                .ForMember(dest => dest.Variables, opt => opt.Ignore())
                .ForMember(dest => dest.User, opt => opt.Ignore())
                .ForMember(dest => dest.Notification, opt => opt.Ignore());
        }
    }
}
