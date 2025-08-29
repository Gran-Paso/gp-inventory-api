using AutoMapper;
using GPInventory.Application.DTOs.Prospects;
using GPInventory.Domain.Entities;

namespace GPInventory.Application.Mappings;

public class ProspectMappingProfile : Profile
{
    public ProspectMappingProfile()
    {
        CreateMap<CreateProspectDto, Prospect>()
            .ForMember(dest => dest.Id, opt => opt.Ignore())
            .ForMember(dest => dest.CreatedAt, opt => opt.Ignore());

        CreateMap<Prospect, ProspectDto>();
    }
}
