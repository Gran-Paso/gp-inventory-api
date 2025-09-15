using GPInventory.Application.DTOs.Production;
using GPInventory.Application.Interfaces;
using GPInventory.Domain.Entities;

namespace GPInventory.Application.Services;

public class SupplyService : ISupplyService
{
    private readonly ISupplyRepository _supplyRepository;
    private readonly IFixedExpenseRepository _fixedExpenseRepository;
    private readonly IRecurrenceTypeRepository _recurrenceTypeRepository;

    public SupplyService(ISupplyRepository supplyRepository, IFixedExpenseRepository fixedExpenseRepository, IRecurrenceTypeRepository recurrenceTypeRepository)
    {
        _supplyRepository = supplyRepository;
        _fixedExpenseRepository = fixedExpenseRepository;
        _recurrenceTypeRepository = recurrenceTypeRepository;
    }

    public async Task<SupplyDto> GetSupplyByIdAsync(int id)
    {
        var supply = await _supplyRepository.GetByIdWithDetailsAsync(id);
        if (supply == null)
            throw new ArgumentException($"Supply with ID {id} not found");

        return MapToDto(supply);
    }

    public async Task<IEnumerable<SupplyDto>> GetAllSuppliesAsync()
    {
        var supplies = await _supplyRepository.GetAllAsync();
        return supplies.Select(MapToDto);
    }

    public async Task<IEnumerable<SupplyDto>> GetSuppliesByBusinessIdAsync(int businessId)
    {
        var supplies = await _supplyRepository.GetByBusinessIdAsync(businessId);
        return supplies.Select(MapToDto);
    }

    public async Task<IEnumerable<SupplyDto>> GetSuppliesByStoreIdAsync(int storeId)
    {
        var supplies = await _supplyRepository.GetByStoreIdAsync(storeId);
        return supplies.Select(MapToDto);
    }

    public async Task<IEnumerable<SupplyDto>> GetActiveSuppliesAsync(int businessId)
    {
        var supplies = await _supplyRepository.GetActiveSuppliesAsync(businessId);
        return supplies.Select(MapToDto);
    }

    public async Task<SupplyDto> CreateSupplyAsync(CreateSupplyDto createSupplyDto)
    {
        // Verificar que no existe un supply con el mismo nombre en el business
        var existingSupply = await _supplyRepository.GetByNameAsync(createSupplyDto.Name, createSupplyDto.BusinessId);
        if (existingSupply != null)
            throw new ArgumentException($"A supply with name '{createSupplyDto.Name}' already exists in this business");

        // Obtener un RecurrenceType por defecto (asumiendo que existe uno para "Una vez" o similar)
        var recurrenceTypes = await _recurrenceTypeRepository.GetAllAsync();
        var defaultRecurrenceType = recurrenceTypes.FirstOrDefault();
        if (defaultRecurrenceType == null)
            throw new InvalidOperationException("No recurrence types found in the system");

        // Crear el gasto fijo automáticamente
        var fixedExpense = new FixedExpense(
            businessId: createSupplyDto.BusinessId,
            additionalNote: $"Gasto fijo para insumo: {createSupplyDto.Name}",
            amount: createSupplyDto.FixedExpenseAmount,
            recurrenceTypeId: defaultRecurrenceType.Id,
            storeId: createSupplyDto.StoreId,
            subcategoryId: createSupplyDto.SubcategoryId,
            paymentDate: createSupplyDto.PaymentDate
        );

        var createdFixedExpense = await _fixedExpenseRepository.AddAsync(fixedExpense);

        // Crear el supply con el gasto fijo creado
        var supply = new Supply(
            name: createSupplyDto.Name,
            businessId: createSupplyDto.BusinessId,
            storeId: createSupplyDto.StoreId,
            unitMeasureId: createSupplyDto.UnitMeasureId,
            description: createSupplyDto.Description,
            fixedExpenseId: createdFixedExpense.Id,
            active: createSupplyDto.Active
        );

        var createdSupply = await _supplyRepository.AddAsync(supply);
        return MapToDto(createdSupply);
    }

    public async Task<SupplyDto> UpdateSupplyAsync(int id, UpdateSupplyDto updateSupplyDto)
    {
        var supply = await _supplyRepository.GetByIdAsync(id);
        if (supply == null)
            throw new ArgumentException($"Supply with ID {id} not found");

        // Verificar que no existe otro supply con el mismo nombre en el business
        var existingSupply = await _supplyRepository.GetByNameAsync(updateSupplyDto.Name, supply.BusinessId);
        if (existingSupply != null && existingSupply.Id != id)
            throw new ArgumentException($"A supply with name '{updateSupplyDto.Name}' already exists in this business");

        // Actualizar el gasto fijo asociado si existe
        if (supply.FixedExpenseId.HasValue)
        {
            var fixedExpense = await _fixedExpenseRepository.GetByIdAsync(supply.FixedExpenseId.Value);
            if (fixedExpense != null)
            {
                fixedExpense.AdditionalNote = $"Gasto fijo para insumo: {updateSupplyDto.Name}";
                fixedExpense.Amount = updateSupplyDto.FixedExpenseAmount;
                fixedExpense.SubcategoryId = updateSupplyDto.SubcategoryId;
                fixedExpense.PaymentDate = updateSupplyDto.PaymentDate;
                fixedExpense.StoreId = updateSupplyDto.StoreId;
                
                await _fixedExpenseRepository.UpdateAsync(fixedExpense);
            }
        }

        // Actualizar el supply
        supply.Name = updateSupplyDto.Name;
        supply.Description = updateSupplyDto.Description;
        supply.UnitMeasureId = updateSupplyDto.UnitMeasureId;
        supply.Active = updateSupplyDto.Active;
        supply.StoreId = updateSupplyDto.StoreId;

        await _supplyRepository.UpdateAsync(supply);
        return MapToDto(supply);
    }

    public async Task DeleteSupplyAsync(int id)
    {
        var exists = await _supplyRepository.ExistsAsync(id);
        if (!exists)
            throw new ArgumentException($"Supply with ID {id} not found");

        await _supplyRepository.DeleteAsync(id);
    }

    public async Task<IEnumerable<SupplyDto>> GetSuppliesWithDetailsAsync(int[]? businessIds = null)
    {
        var supplies = await _supplyRepository.GetSuppliesWithDetailsAsync(businessIds);
        return supplies.Select(MapToDto);
    }

    private static SupplyDto MapToDto(Supply supply)
    {
        return new SupplyDto
        {
            Id = supply.Id,
            Name = supply.Name,
            Description = supply.Description,
            UnitMeasureId = supply.UnitMeasureId,
            FixedExpenseId = supply.FixedExpenseId,
            Active = supply.Active,
            BusinessId = supply.BusinessId,
            StoreId = supply.StoreId,
            CreatedAt = supply.CreatedAt,
            UpdatedAt = supply.UpdatedAt,
            
            // Calculate current stock from SupplyEntries
            CurrentStock = supply.SupplyEntries?.Sum(se => se.Amount) ?? 0,
            
            // Navigation properties - only include if loaded
            UnitMeasure = supply.UnitMeasure != null ? new UnitMeasureDto
            {
                Id = supply.UnitMeasure.Id,
                Name = supply.UnitMeasure.Name,
                Symbol = supply.UnitMeasure.Symbol
            } : null,
            FixedExpense = supply.FixedExpense != null ? new FixedExpenseDto
            {
                Id = supply.FixedExpense.Id,
                AdditionalNote = supply.FixedExpense.AdditionalNote,
                Amount = supply.FixedExpense.Amount
            } : null,
            Business = supply.Business != null ? new BusinessDto
            {
                Id = supply.Business.Id,
                Name = supply.Business.CompanyName
            } : null,
            Store = supply.Store != null ? new StoreDto
            {
                Id = supply.Store.Id,
                Name = supply.Store.Name ?? string.Empty
            } : null,
            // SupplyEntries - map without circular reference to Supply
            SupplyEntries = supply.SupplyEntries?.Select(se => new SupplyEntryDto
            {
                Id = se.Id,
                UnitCost = se.UnitCost,
                Amount = (decimal)se.Amount,
                ProviderId = se.ProviderId,
                SupplyId = se.SupplyId,
                ProcessDoneId = se.ProcessDoneId,
                ReferenceToSupplyEntry = se.ReferenceToSupplyEntry, // ⭐ Agregar mapeo
                CreatedAt = se.CreatedAt,
                UpdatedAt = se.UpdatedAt,
                // Don't include Supply to avoid circular reference
                Provider = se.Provider != null ? new ProviderDto
                {
                    Id = se.Provider.Id,
                    Name = se.Provider.Name,
                    StoreId = se.Provider.StoreId
                } : null
            }).ToList() ?? new List<SupplyEntryDto>()
        };
    }
}
