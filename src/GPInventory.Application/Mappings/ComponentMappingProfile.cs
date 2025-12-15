using AutoMapper;
using GPInventory.Application.DTOs.Components;
using GPInventory.Domain.Entities;

namespace GPInventory.Application.Mappings;

public class ComponentMappingProfile : Profile
{
    public ComponentMappingProfile()
    {
        // Component mappings
        CreateMap<Component, ComponentDto>();
        
        CreateMap<Component, ComponentWithSuppliesDto>()
            .ForMember(dest => dest.Supplies, opt => opt.MapFrom(src => src.Supplies));
        
        CreateMap<CreateComponentDto, Component>()
            .ForMember(dest => dest.Id, opt => opt.Ignore())
            .ForMember(dest => dest.Active, opt => opt.Ignore())
            .ForMember(dest => dest.CreatedAt, opt => opt.Ignore())
            .ForMember(dest => dest.UpdatedAt, opt => opt.Ignore())
            .ForMember(dest => dest.Supplies, opt => opt.Ignore())
            .ForMember(dest => dest.UsedInComponents, opt => opt.Ignore())
            .ForMember(dest => dest.Productions, opt => opt.Ignore());
        
        CreateMap<UpdateComponentDto, Component>()
            .ForMember(dest => dest.Id, opt => opt.Ignore())
            .ForMember(dest => dest.BusinessId, opt => opt.Ignore())
            .ForMember(dest => dest.Active, opt => opt.Ignore())
            .ForMember(dest => dest.CreatedAt, opt => opt.Ignore())
            .ForMember(dest => dest.UpdatedAt, opt => opt.Ignore())
            .ForMember(dest => dest.Supplies, opt => opt.Ignore())
            .ForMember(dest => dest.UsedInComponents, opt => opt.Ignore())
            .ForMember(dest => dest.Productions, opt => opt.Ignore());

        // ComponentSupply mappings
        CreateMap<ComponentSupply, ComponentSupplyDto>()
            .ForMember(dest => dest.SupplyName, opt => opt.MapFrom(src => src.Supply != null ? src.Supply.Name : null))
            .ForMember(dest => dest.SupplyUnitSymbol, opt => opt.MapFrom(src => src.Supply != null && src.Supply.UnitMeasure != null ? src.Supply.UnitMeasure.Symbol : null))
            .ForMember(dest => dest.SubComponentName, opt => opt.MapFrom(src => src.SubComponent != null ? src.SubComponent.Name : null))
            .ForMember(dest => dest.SubComponentUnitSymbol, opt => opt.MapFrom(src => src.SubComponent != null ? src.SubComponent.UnitMeasureSymbol : null));
        
        CreateMap<CreateComponentSupplyDto, ComponentSupply>()
            .ForMember(dest => dest.Id, opt => opt.Ignore())
            .ForMember(dest => dest.ComponentId, opt => opt.Ignore())
            .ForMember(dest => dest.Component, opt => opt.Ignore())
            .ForMember(dest => dest.Supply, opt => opt.Ignore())
            .ForMember(dest => dest.SubComponent, opt => opt.Ignore());

        // ComponentProduction mappings
        CreateMap<ComponentProduction, ComponentProductionDto>();
        
        CreateMap<CreateComponentProductionDto, ComponentProduction>()
            .ForMember(dest => dest.Id, opt => opt.Ignore())
            .ForMember(dest => dest.IsActive, opt => opt.Ignore())
            .ForMember(dest => dest.CreatedAt, opt => opt.Ignore())
            .ForMember(dest => dest.Component, opt => opt.Ignore())
            .ForMember(dest => dest.ProcessDone, opt => opt.Ignore())
            .ForMember(dest => dest.Business, opt => opt.Ignore())
            .ForMember(dest => dest.Store, opt => opt.Ignore());
        
        CreateMap<UpdateComponentProductionDto, ComponentProduction>()
            .ForMember(dest => dest.Id, opt => opt.Ignore())
            .ForMember(dest => dest.ComponentId, opt => opt.Ignore())
            .ForMember(dest => dest.ProductionDate, opt => opt.Ignore())
            .ForMember(dest => dest.BatchNumber, opt => opt.Ignore())
            .ForMember(dest => dest.IsActive, opt => opt.Ignore())
            .ForMember(dest => dest.CreatedAt, opt => opt.Ignore())
            .ForMember(dest => dest.Component, opt => opt.Ignore())
            .ForMember(dest => dest.ProcessDone, opt => opt.Ignore())
            .ForMember(dest => dest.Business, opt => opt.Ignore())
            .ForMember(dest => dest.Store, opt => opt.Ignore());
    }
}
