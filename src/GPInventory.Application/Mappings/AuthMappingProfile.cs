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
                Id = ub.Role.Id,
                Name = ub.Role.Name,
                BusinessId = ub.Business.Id,
                BusinessName = ub.Business.CompanyName
            })));
        
        CreateMap<RegisterDto, User>()
            .ForMember(dest => dest.Mail, opt => opt.MapFrom(src => src.Email));
    }
}
