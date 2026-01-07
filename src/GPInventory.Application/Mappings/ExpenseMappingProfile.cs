using AutoMapper;
using GPInventory.Application.DTOs.Expenses;
using GPInventory.Application.DTOs.Production;
using GPInventory.Domain.Entities;
using ExpenseFixedExpenseDto = GPInventory.Application.DTOs.Expenses.FixedExpenseDto;

namespace GPInventory.Application.Mappings;

public class ExpenseMappingProfile : Profile
{
    public ExpenseMappingProfile()
    {
        // Expense mappings
        CreateMap<Expense, ExpenseDto>();
        CreateMap<CreateExpenseDto, Expense>()
            .ForMember(dest => dest.Id, opt => opt.Ignore())
            .ForMember(dest => dest.ExpenseTypeId, opt => opt.MapFrom(src => src.ExpenseTypeId));
        
        CreateMap<UpdateExpenseDto, Expense>()
            .ForAllMembers(opts => opts.Condition((src, dest, srcMember) => srcMember != null));

        CreateMap<Expense, ExpenseWithDetailsDto>()
            .ForMember(dest => dest.Subcategory, opt => opt.MapFrom(src => src.ExpenseSubcategory))
            .ForMember(dest => dest.Category, opt => opt.MapFrom(src => src.ExpenseSubcategory.ExpenseCategory))
            .ForMember(dest => dest.StoreName, opt => opt.MapFrom(src => src.Store != null ? src.Store.Name : null))
            .ForMember(dest => dest.ExpenseTypeId, opt => opt.MapFrom(src => src.ExpenseTypeId))
            .ForMember(dest => dest.Provider, opt => opt.MapFrom(src => src.Provider != null ? new ProviderDto
            {
                Id = src.Provider.Id,
                Name = src.Provider.Name,
                BusinessId = src.Provider.BusinessId,
                StoreId = src.Provider.StoreId,
                Contact = src.Provider.Contact,
                Address = src.Provider.Address,
                Mail = src.Provider.Mail,
                Prefix = src.Provider.Prefix,
                Active = src.Provider.Active,
                CreatedAt = src.Provider.CreatedAt,
                UpdatedAt = src.Provider.UpdatedAt
            } : null));

        // FixedExpense mappings
        CreateMap<FixedExpense, ExpenseFixedExpenseDto>()
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
            .ForMember(dest => dest.StartDate, opt => opt.MapFrom(src => src.PaymentDate ?? src.CreatedAt))
            .ForMember(dest => dest.Notes, opt => opt.MapFrom(src => src.AdditionalNote))
            .ForMember(dest => dest.Subcategory, opt => opt.MapFrom(src => src.Subcategory))
            .ForMember(dest => dest.Category, opt => opt.MapFrom(src => src.Subcategory!.ExpenseCategory))
            .ForMember(dest => dest.RecurrenceType, opt => opt.MapFrom(src => src.RecurrenceType))
            .ForMember(dest => dest.StoreName, opt => opt.MapFrom(src => src.Store != null ? src.Store.Name : null))
            .ForMember(dest => dest.AssociatedExpenses, opt => opt.MapFrom(src => src.GeneratedExpenses))
            .ForMember(dest => dest.ExpenseTypeId, opt => opt.MapFrom(src => src.ExpenseTypeId));

        // Category and subcategory mappings
        CreateMap<ExpenseCategory, ExpenseCategoryDto>();
        CreateMap<ExpenseSubcategory, ExpenseSubcategoryDto>();
        CreateMap<RecurrenceType, RecurrenceTypeDto>();
    }
}
