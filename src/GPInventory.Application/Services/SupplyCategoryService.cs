using GPInventory.Application.DTOs.Production;
using GPInventory.Application.Interfaces;
using GPInventory.Domain.Entities;

namespace GPInventory.Application.Services;

public interface ISupplyCategoryService
{
    Task<IEnumerable<SupplyCategoryDto>> GetAllByBusinessIdAsync(int businessId, bool activeOnly = false);
    Task<SupplyCategoryDto?> GetByIdAsync(int id);
    Task<SupplyCategoryDto> CreateAsync(CreateSupplyCategoryDto dto);
    Task<SupplyCategoryDto> UpdateAsync(int id, CreateSupplyCategoryDto dto);
    Task<bool> ToggleActiveAsync(int id);
    Task<bool> DeleteAsync(int id);
}

public class SupplyCategoryService : ISupplyCategoryService
{
    private readonly ISupplyCategoryRepository _repository;

    public SupplyCategoryService(ISupplyCategoryRepository repository)
    {
        _repository = repository;
    }

    public async Task<IEnumerable<SupplyCategoryDto>> GetAllByBusinessIdAsync(int businessId, bool activeOnly = false)
    {
        var categories = await _repository.GetAllByBusinessIdAsync(businessId, activeOnly);
        return categories.Select(MapToDto);
    }

    public async Task<SupplyCategoryDto?> GetByIdAsync(int id)
    {
        var category = await _repository.GetByIdAsync(id);
        return category == null ? null : MapToDto(category);
    }

    public async Task<SupplyCategoryDto> CreateAsync(CreateSupplyCategoryDto dto)
    {
        // Validar nombre único
        if (await _repository.NameExistsAsync(dto.Name, dto.BusinessId))
        {
            throw new InvalidOperationException($"Ya existe una categoría con el nombre '{dto.Name}'");
        }

        var category = new SupplyCategory(
            name: dto.Name,
            businessId: dto.BusinessId,
            description: dto.Description,
            active: dto.Active
        );

        var created = await _repository.CreateAsync(category);
        return MapToDto(created);
    }

    public async Task<SupplyCategoryDto> UpdateAsync(int id, CreateSupplyCategoryDto dto)
    {
        var category = await _repository.GetByIdAsync(id);
        if (category == null)
        {
            throw new KeyNotFoundException($"Categoría con ID {id} no encontrada");
        }

        // Validar nombre único (excluyendo la categoría actual)
        if (await _repository.NameExistsAsync(dto.Name, dto.BusinessId, id))
        {
            throw new InvalidOperationException($"Ya existe otra categoría con el nombre '{dto.Name}'");
        }

        category.Name = dto.Name;
        category.Description = dto.Description;
        category.Active = dto.Active;

        var updated = await _repository.UpdateAsync(category);
        return MapToDto(updated);
    }

    public async Task<bool> ToggleActiveAsync(int id)
    {
        var category = await _repository.GetByIdAsync(id);
        if (category == null)
        {
            throw new KeyNotFoundException($"Categoría con ID {id} no encontrada");
        }

        category.Active = !category.Active;
        await _repository.UpdateAsync(category);
        return true;
    }

    public async Task<bool> DeleteAsync(int id)
    {
        var category = await _repository.GetByIdAsync(id);
        if (category == null)
        {
            return false;
        }

        // Verificar si hay supplies o components usando esta categoría
        if (category.Supplies.Any() || category.Components.Any())
        {
            throw new InvalidOperationException("No se puede eliminar una categoría que está en uso");
        }

        return await _repository.DeleteAsync(id);
    }

    private static SupplyCategoryDto MapToDto(SupplyCategory category)
    {
        return new SupplyCategoryDto
        {
            Id = category.Id,
            Name = category.Name,
            Description = category.Description,
            Active = category.Active
        };
    }
}
