using AutoMapper;
using GPInventory.Application.DTOs.Expenses;
using GPInventory.Application.Interfaces;
using GPInventory.Domain.Entities;

namespace GPInventory.Application.Services;

public class ExpenseCategoryService : IExpenseCategoryService
{
    private readonly IExpenseCategoryRepository _categoryRepository;
    private readonly IExpenseSubcategoryRepository _subcategoryRepository;
    private readonly IMapper _mapper;

    public ExpenseCategoryService(
        IExpenseCategoryRepository categoryRepository,
        IExpenseSubcategoryRepository subcategoryRepository,
        IMapper mapper)
    {
        _categoryRepository = categoryRepository;
        _subcategoryRepository = subcategoryRepository;
        _mapper = mapper;
    }

    public async Task<IEnumerable<ExpenseCategoryDto>> GetAllAsync()
    {
        var categories = await _categoryRepository.GetCategoriesWithSubcategoriesAsync();
        return _mapper.Map<IEnumerable<ExpenseCategoryDto>>(categories);
    }

    public async Task<ExpenseCategoryDto?> GetByIdAsync(int id)
    {
        var category = await _categoryRepository.GetCategoryWithSubcategoriesAsync(id);
        return category != null ? _mapper.Map<ExpenseCategoryDto>(category) : null;
    }

    public async Task<ExpenseCategoryDto> CreateAsync(CreateExpenseCategoryDto createDto)
    {
        // Verificar si ya existe una categoría con el mismo nombre
        var exists = await _categoryRepository.ExistsByNameAsync(createDto.Name);
        if (exists)
            throw new ArgumentException("Ya existe una categoría con ese nombre");

        var category = _mapper.Map<ExpenseCategory>(createDto);
        var createdCategory = await _categoryRepository.AddAsync(category);

        return _mapper.Map<ExpenseCategoryDto>(createdCategory);
    }

    public async Task<ExpenseCategoryDto> UpdateAsync(int id, UpdateExpenseCategoryDto updateDto)
    {
        var existingCategory = await _categoryRepository.GetByIdAsync(id);
        if (existingCategory == null)
            throw new ArgumentException("Categoría no encontrada");

        // Verificar si ya existe una categoría con el mismo nombre (excluyendo la actual)
        if (!string.IsNullOrEmpty(updateDto.Name))
        {
            var exists = await _categoryRepository.ExistsByNameAsync(updateDto.Name, id);
            if (exists)
                throw new ArgumentException("Ya existe una categoría con ese nombre");
        }

        _mapper.Map(updateDto, existingCategory);
        await _categoryRepository.UpdateAsync(existingCategory);

        return _mapper.Map<ExpenseCategoryDto>(existingCategory);
    }

    public async Task<bool> DeleteAsync(int id)
    {
        var category = await _categoryRepository.GetByIdAsync(id);
        if (category == null)
            return false;

        // Verificar si la categoría tiene subcategorías
        var subcategories = await _subcategoryRepository.GetSubcategoriesByCategoryAsync(id);
        if (subcategories.Any())
            throw new InvalidOperationException("No se puede eliminar una categoría que tiene subcategorías");

        await _categoryRepository.DeleteAsync(category.Id);
        return true;
    }

    public async Task<IEnumerable<ExpenseCategoryDto>> GetCategoriesWithSubcategoriesAsync()
    {
        var categories = await _categoryRepository.GetCategoriesWithSubcategoriesAsync();
        return _mapper.Map<IEnumerable<ExpenseCategoryDto>>(categories);
    }

    public async Task<IEnumerable<ExpenseSubcategoryDto>> GetSubcategoriesByCategoryAsync(int categoryId)
    {
        var subcategories = await _subcategoryRepository.GetSubcategoriesByCategoryAsync(categoryId);
        return _mapper.Map<IEnumerable<ExpenseSubcategoryDto>>(subcategories);
    }
}
