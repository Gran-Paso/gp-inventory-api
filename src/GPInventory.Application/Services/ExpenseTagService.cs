using GPInventory.Application.DTOs.Expenses;
using GPInventory.Application.Interfaces;
using GPInventory.Domain.Entities;

namespace GPInventory.Application.Services;

public class ExpenseTagService : IExpenseTagService
{
    private readonly IExpenseTagRepository _repo;

    public ExpenseTagService(IExpenseTagRepository repo)
    {
        _repo = repo;
    }

    public async Task<IEnumerable<ExpenseTagDto>> GetByBusinessAsync(int businessId)
    {
        var tags = await _repo.GetByBusinessAsync(businessId);
        return tags.Select(Map);
    }

    public async Task<ExpenseTagDto> CreateAsync(CreateExpenseTagDto dto)
    {
        if (await _repo.ExistsByNameAsync(dto.BusinessId, dto.Name))
            throw new InvalidOperationException($"Ya existe una etiqueta con el nombre '{dto.Name}' en este negocio.");

        var tag = new ExpenseTag
        {
            BusinessId = dto.BusinessId,
            Name = dto.Name.Trim(),
            Color = dto.Color,
            CreatedAt = DateTime.UtcNow
        };

        var created = await _repo.AddAsync(tag);
        return Map(created);
    }

    public async Task<ExpenseTagDto> UpdateAsync(int id, UpdateExpenseTagDto dto)
    {
        var tag = await _repo.GetByIdAsync(id)
            ?? throw new KeyNotFoundException($"Etiqueta con id {id} no encontrada.");

        if (dto.Name is not null)
        {
            if (await _repo.ExistsByNameAsync(tag.BusinessId, dto.Name, excludeId: id))
                throw new InvalidOperationException($"Ya existe una etiqueta con el nombre '{dto.Name}' en este negocio.");
            tag.Name = dto.Name.Trim();
        }

        if (dto.Color is not null)
            tag.Color = dto.Color;

        await _repo.UpdateAsync(tag);
        return Map(tag);
    }

    public async Task DeleteAsync(int id)
    {
        var tag = await _repo.GetByIdAsync(id)
            ?? throw new KeyNotFoundException($"Etiqueta con id {id} no encontrada.");
        await _repo.DeleteAsync(id);
    }

    public async Task<IEnumerable<ExpenseTagDto>> GetTagsByExpenseAsync(int expenseId)
    {
        var tags = await _repo.GetTagsByExpenseIdAsync(expenseId);
        return tags.Select(Map);
    }

    public async Task SetTagsForExpenseAsync(int expenseId, List<int> tagIds)
    {
        await _repo.SetTagsForExpenseAsync(expenseId, tagIds);
    }

    private static ExpenseTagDto Map(ExpenseTag t) => new()
    {
        Id = t.Id,
        BusinessId = t.BusinessId,
        Name = t.Name,
        Color = t.Color,
        CreatedAt = t.CreatedAt
    };
}
