using AutoMapper;
using GPInventory.Application.DTOs.Auth;
using GPInventory.Domain.Entities;

namespace GPInventory.Application.Mappings;

public class AuthMappingProfile : Profile
{
    public AuthMappingProfile()
    {
        CreateMap<User, UserDto>()
            .ForMember(dest => dest.Email, opt => opt.MapFrom(src => src.Mail))
            .ForMember(dest => dest.Gender, opt => opt.MapFrom(src => src.Gender))
            .ForMember(dest => dest.BirthDate, opt => opt.MapFrom(src => src.BirthDate))
            .ForMember(dest => dest.Phone, opt => opt.MapFrom(src => src.Phone))
            .ForMember(dest => dest.SystemRole, opt => opt.MapFrom(src => src.SystemRole))
            .ForMember(dest => dest.Roles, opt => opt.MapFrom(src => src.UserBusinesses.Select(ub => new UserRoleDto
            {
                Id = ub.RoleId, // Cambiado de ub.Role.Id a ub.RoleId para obtener el ID correcto del rol (1=Cofundador, 2=Dueño, etc)
                Name = ub.Role.Name,
                BusinessId = ub.Business.Id,
                BusinessName = ub.Business.CompanyName
            })))
            .ForMember(dest => dest.BusinessRoles, opt => opt.MapFrom(src => src.UserBusinesses.Select(ub => new BusinessRoleInfo
            {
                BusinessId = ub.Business.Id,
                BusinessName = ub.Business.CompanyName,
                RoleId = ub.RoleId,
                RoleName = ub.Role.Name
            })));
        
        CreateMap<RegisterDto, User>()
            .ForMember(dest => dest.Mail, opt => opt.MapFrom(src => src.Email));
    }
}
