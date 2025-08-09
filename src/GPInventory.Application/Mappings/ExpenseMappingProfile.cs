using AutoMapper;
using GPInventory.Application.DTOs.Expenses;
using GPInventory.Domain.Entities;

namespace GPInventory.Application.Mappings;

public class ExpenseMappingProfile : Profile
{
    public ExpenseMappingProfile()
    {
        // Expense mappings
        CreateMap<Expense, ExpenseDto>();
        CreateMap<CreateExpenseDto, Expense>()
            .ForMember(dest => dest.Id, opt => opt.Ignore());
        
        CreateMap<UpdateExpenseDto, Expense>()
            .ForAllMembers(opts => opts.Condition((src, dest, srcMember) => srcMember != null));

        CreateMap<Expense, ExpenseWithDetailsDto>()
            .ForMember(dest => dest.Subcategory, opt => opt.MapFrom(src => src.ExpenseSubcategory))
            .ForMember(dest => dest.Category, opt => opt.MapFrom(src => src.ExpenseSubcategory.ExpenseCategory))
            .ForMember(dest => dest.StoreName, opt => opt.MapFrom(src => src.Store != null ? src.Store.Name : null));

        // FixedExpense mappings
        CreateMap<FixedExpense, FixedExpenseDto>()
            .ForMember(dest => dest.Description, opt => opt.MapFrom(src => src.AdditionalNote))
            .ForMember(dest => dest.StartDate, opt => opt.MapFrom(src => src.PaymentDate ?? DateTime.UtcNow))
            .ForMember(dest => dest.Notes, opt => opt.MapFrom(src => src.AdditionalNote));
            
        CreateMap<CreateFixedExpenseDto, FixedExpense>()
            .ForMember(dest => dest.Id, opt => opt.Ignore())
            .ForMember(dest => dest.AdditionalNote, opt => opt.MapFrom(src => src.Description))
            .ForMember(dest => dest.PaymentDate, opt => opt.MapFrom(src => src.StartDate))
            .ForMember(dest => dest.IsActive, opt => opt.MapFrom(src => true))
            .ForMember(dest => dest.CreatedAt, opt => opt.MapFrom(src => DateTime.UtcNow))
            .ForMember(dest => dest.UpdatedAt, opt => opt.MapFrom(src => DateTime.UtcNow));
        
        CreateMap<UpdateFixedExpenseDto, FixedExpense>()
            .ForAllMembers(opts => opts.Condition((src, dest, srcMember) => srcMember != null));

        CreateMap<FixedExpense, FixedExpenseWithDetailsDto>()
            .ForMember(dest => dest.Description, opt => opt.MapFrom(src => src.AdditionalNote))
            .ForMember(dest => dest.StartDate, opt => opt.MapFrom(src => src.PaymentDate ?? DateTime.UtcNow))
            .ForMember(dest => dest.Notes, opt => opt.MapFrom(src => src.AdditionalNote))
            .ForMember(dest => dest.Subcategory, opt => opt.MapFrom(src => src.Subcategory))
            .ForMember(dest => dest.Category, opt => opt.MapFrom(src => src.Subcategory!.ExpenseCategory))
            .ForMember(dest => dest.RecurrenceType, opt => opt.MapFrom(src => src.RecurrenceType))
            .ForMember(dest => dest.StoreName, opt => opt.MapFrom(src => src.Store != null ? src.Store.Name : null));

        // Category and subcategory mappings
        CreateMap<ExpenseCategory, ExpenseCategoryDto>();
        CreateMap<ExpenseSubcategory, ExpenseSubcategoryDto>();
        CreateMap<RecurrenceType, RecurrenceTypeDto>();
    }
}
