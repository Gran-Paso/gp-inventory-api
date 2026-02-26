using AutoMapper;
using GPInventory.Application.DTOs.Payments;
using GPInventory.Domain.Entities;

namespace GPInventory.Application.Mappings;

public class PaymentMappingProfile : Profile
{
    public PaymentMappingProfile()
    {
        // Catalog entities
        CreateMap<ReceiptType, ReceiptTypeDto>();
        CreateMap<PaymentType, PaymentTypeDto>();
        CreateMap<PaymentMethod, PaymentMethodDto>();
        CreateMap<BankEntity, BankEntityDto>();
        CreateMap<CreateBankEntityDto, BankEntity>()
            .ForMember(dest => dest.Id, opt => opt.Ignore());

        // PaymentPlan mappings
        CreateMap<PaymentPlan, PaymentPlanDto>()
            .ForMember(dest => dest.PaymentTypeName, opt => opt.MapFrom(src => src.PaymentType != null ? src.PaymentType.Name : null))
            .ForMember(dest => dest.BankEntityName, opt => opt.MapFrom(src => src.BankEntity != null ? src.BankEntity.Name : null));
        
        CreateMap<PaymentPlan, PaymentPlanWithInstallmentsDto>()
            .ForMember(dest => dest.PaymentTypeName, opt => opt.MapFrom(src => src.PaymentType != null ? src.PaymentType.Name : null))
            .ForMember(dest => dest.BankEntityName, opt => opt.MapFrom(src => src.BankEntity != null ? src.BankEntity.Name : null))
            .ForMember(dest => dest.Installments, opt => opt.Ignore()); // Se carga manualmente en el servicio
        
        CreateMap<CreatePaymentPlanDto, PaymentPlan>()
            .ForMember(dest => dest.Id, opt => opt.Ignore());

        // PaymentInstallment mappings
        CreateMap<PaymentInstallment, PaymentInstallmentDto>()
            .ForMember(dest => dest.Status, opt => opt.MapFrom(src => 
                src.Status.ToLower() == "pagado" ? "pagado" : "pendiente"))
            .ForMember(dest => dest.PaymentMethodName, opt => opt.MapFrom(src => src.PaymentMethod != null ? src.PaymentMethod.Name : null));
        
        CreateMap<CreateInstallmentDto, PaymentInstallment>()
            .ForMember(dest => dest.Id, opt => opt.Ignore());
        
        CreateMap<UpdateInstallmentStatusDto, PaymentInstallment>()
            .ForAllMembers(opts => opts.Condition((src, dest, srcMember) => srcMember != null));

        // InstallmentDocument mappings
        CreateMap<InstallmentDocument, InstallmentDocumentDto>();
    }
}
