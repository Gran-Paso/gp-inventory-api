using GPInventory.Application.DTOs.Production;
using GPInventory.Application.DTOs.Expenses;
using GPInventory.Application.Interfaces;
using GPInventory.Domain.Entities;

namespace GPInventory.Application.Services;

public class SupplyEntryService : ISupplyEntryService
{
    private readonly ISupplyEntryRepository _repository;
    private readonly ISupplyRepository _supplyRepository;
    private readonly IUnitMeasureRepository _unitMeasureRepository;
    private readonly IExpenseService _expenseService;
    private readonly IExpenseSubcategoryRepository _subcategoryRepository;

    public SupplyEntryService(
        ISupplyEntryRepository repository,
        ISupplyRepository supplyRepository,
        IUnitMeasureRepository unitMeasureRepository,
        IExpenseService expenseService,
        IExpenseSubcategoryRepository subcategoryRepository)
    {
        _repository = repository;
        _supplyRepository = supplyRepository;
        _unitMeasureRepository = unitMeasureRepository;
        _expenseService = expenseService;
        _subcategoryRepository = subcategoryRepository;
    }

    public async Task<IEnumerable<SupplyEntryDto>> GetAllAsync()
    {
        var supplyEntries = await _repository.GetAllWithDetailsAsync();
        return supplyEntries.Select(MapToDto);
    }

    public async Task<SupplyEntryDto?> GetByIdAsync(int id)
    {
        var supplyEntry = await _repository.GetByIdAsync(id);
        return supplyEntry != null ? MapToDto(supplyEntry) : null;
    }

    public async Task<IEnumerable<SupplyEntryDto>> GetBySupplyIdAsync(int supplyId)
    {
        var supplyEntries = await _repository.GetBySupplyIdAsync(supplyId);
        return supplyEntries.Select(MapToDto);
    }

    public async Task<IEnumerable<SupplyEntryDto>> GetByProcessDoneIdAsync(int processDoneId)
    {
        var supplyEntries = await _repository.GetByProcessDoneIdAsync(processDoneId);
        return supplyEntries.Select(MapToDto);
    }

    public async Task<SupplyStockDto?> GetSupplyStockAsync(int supplyId)
    {
        // Load supply first
        var supply = await _supplyRepository.GetByIdAsync(supplyId);
        if (supply == null) return null;

        // Load supply entries
        var supplyEntries = await _repository.GetBySupplyIdAsync(supplyId);
        
        // Calculate stock based on ProcessDoneId
        // Entradas: process_done_id IS NULL (incoming stock) - amounts positivos
        // Salidas: process_done_id IS NOT NULL (outgoing stock used in processes) - amounts negativos
        var totalIncoming = supplyEntries.Where(se => se.ProcessDoneId == null).Sum(se => (decimal)se.Amount);
        var totalOutgoing = supplyEntries.Where(se => se.ProcessDoneId != null).Sum(se => (decimal)se.Amount);
        var currentStock = totalIncoming + totalOutgoing; // totalOutgoing ya incluye valores negativos
        
        // Get unit measure separately to avoid EF Core auto-detection issues
        UnitMeasure? unitMeasure = null;
        try
        {
            // Load the unit measure separately without triggering EF Core relationship detection
            var unitMeasureId = supply.UnitMeasureId;
            unitMeasure = await _unitMeasureRepository.GetByIdAsync(unitMeasureId);
        }
        catch
        {
            // If there's any issue getting the unit measure, just continue without it
            unitMeasure = null;
        }

        return new SupplyStockDto
        {
            SupplyId = supply.Id,
            SupplyName = supply.Name,
            CurrentStock = currentStock,
            UnitMeasureName = unitMeasure?.Name ?? "Unknown",
            UnitMeasureSymbol = unitMeasure?.Symbol,
            TotalIncoming = totalIncoming,
            TotalOutgoing = totalOutgoing
        };
    }

    public async Task<IEnumerable<SupplyStockDto>> GetAllSupplyStocksAsync(int? businessId = null)
    {
        return await _supplyRepository.GetAllSupplyStocksAsync(businessId);
    }

    public async Task<SupplyEntryDto> CreateAsync(CreateSupplyEntryDto createDto)
    {
        // Get the supply to check its unit measure and business info
        var supply = await _supplyRepository.GetByIdAsync(createDto.SupplyId);
        if (supply == null)
            throw new InvalidOperationException($"Supply with id {createDto.SupplyId} not found");

        var unitCost = createDto.UnitCost;
        
        // If the supply is measured in grams (UnitMeasureId = 2), adjust the unit cost
        // Since items are bought by kilogram but stored in grams, divide the cost by 1000
        if (supply.UnitMeasureId == 2) // Grams
        {
            unitCost = createDto.UnitCost / 1000;
        }

        var supplyEntry = new SupplyEntry(
            unitCost,
            createDto.Amount,
            createDto.ProviderId,
            createDto.SupplyId,
            createDto.ProcessDoneId
        );
        
        // Asignar Tag si está presente
        if (!string.IsNullOrEmpty(createDto.Tag))
        {
            supplyEntry.Tag = createDto.Tag;
        }
        
        // Si se especifica una referencia, asignarla después de la creación
        if (createDto.ReferenceToSupplyEntry.HasValue)
        {
            supplyEntry.ReferenceToSupplyEntry = createDto.ReferenceToSupplyEntry.Value;
        }

        var created = await _repository.CreateAsync(supplyEntry);

        // Si es una entrada de stock (no tiene ProcessDoneId), crear un expense automáticamente
        if (createDto.ProcessDoneId == null && supply.FixedExpenseId.HasValue)
        {
            try
            {
                // Para el expense, usar el costo real de la compra
                decimal totalAmount;
                if (supply.UnitMeasureId == 2) // Grams
                {
                    // Si está en gramos, el costo unitario está en kilos y la cantidad en gramos
                    // Convertir gramos a kilos para calcular el costo total real
                    var amountInKilos = createDto.Amount / 1000m;
                    totalAmount = createDto.UnitCost * amountInKilos;
                }
                else
                {
                    // Para otras unidades, usar directamente
                    totalAmount = createDto.UnitCost * createDto.Amount;
                }
                
                var subcategoryId = await GetDefaultSubcategoryForSupplyAsync();
                
                var expenseDto = new CreateExpenseDto
                {
                    SubcategoryId = subcategoryId,
                    Amount = totalAmount,
                    Description = $"Compra de insumo: {supply.Name} - {createDto.Amount} unidades",
                    Date = DateTime.UtcNow,
                    BusinessId = supply.BusinessId,
                    StoreId = supply.StoreId,
                    IsFixed = true,
                    FixedExpenseId = supply.FixedExpenseId.Value,
                    ProviderId = createDto.ProviderId,
                    ExpenseTypeId = 2, // Costos - compra de insumos
                    
                    // Payment Plan data (if financing)
                    PaymentTypeId = createDto.PaymentTypeId,
                    InstallmentsCount = createDto.InstallmentsCount,
                    ExpressedInUf = createDto.ExpressedInUf ?? false,
                    BankEntityId = createDto.BankEntityId,
                    PaymentStartDate = createDto.PaymentStartDate
                };

                await _expenseService.CreateExpenseAsync(expenseDto);
                Console.WriteLine($"✅ Expense created automatically for supply entry {created.Id}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"⚠️ Warning: Could not create expense for supply entry {created.Id}: {ex.Message}");
                // No lanzamos excepción para no fallar la creación del supply entry
            }
        }
        
        // Return a simple DTO without navigation properties for creation
        return new SupplyEntryDto
        {
            Id = created.Id,
            UnitCost = created.UnitCost,
            Amount = (decimal)created.Amount, // Convert int to decimal
            ProviderId = created.ProviderId,
            SupplyId = created.SupplyId,
            ProcessDoneId = created.ProcessDoneId,
            CreatedAt = created.CreatedAt
        };
    }

    private async Task<int> GetDefaultSubcategoryForSupplyAsync()
    {
        try
        {
            // Buscar una subcategoría que contenga "insumo", "compra", "materia prima" o similar
            var subcategories = await _subcategoryRepository.GetAllAsync();
            var supplySubcategory = subcategories.FirstOrDefault(s => 
                s.Name.ToLower().Contains("insumo") ||
                s.Name.ToLower().Contains("compra") ||
                s.Name.ToLower().Contains("materia") ||
                s.Name.ToLower().Contains("material") ||
                s.Name.ToLower().Contains("supply")
            );

            if (supplySubcategory != null)
            {
                return supplySubcategory.Id;
            }

            // Si no encuentra una específica, usar la primera disponible
            var firstSubcategory = subcategories.FirstOrDefault();
            if (firstSubcategory != null)
            {
                return firstSubcategory.Id;
            }

            // Fallback: usar ID 1
            return 1;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"⚠️ Warning: Could not find subcategory for supply expense: {ex.Message}");
            return 1; // Fallback
        }
    }

    public async Task<SupplyEntryDto> UpdateAsync(int id, UpdateSupplyEntryDto updateDto)
    {
        var supplyEntry = await _repository.GetByIdAsync(id);
        if (supplyEntry == null)
            throw new InvalidOperationException($"SupplyEntry with id {id} not found");

        supplyEntry.UnitCost = updateDto.UnitCost;
        supplyEntry.Amount = (int)updateDto.Amount; // Convert decimal to int
        supplyEntry.ProviderId = updateDto.ProviderId;
        
        // Actualizar Tag si está presente en el DTO
        if (!string.IsNullOrEmpty(updateDto.Tag))
        {
            supplyEntry.Tag = updateDto.Tag;
        }

        var updated = await _repository.UpdateAsync(supplyEntry);
        return MapToDto(updated);
    }

    public async Task DeleteAsync(int id)
    {
        await _repository.DeleteAsync(id);
    }

    public async Task<IEnumerable<SupplyEntryDto>> GetSupplyHistoryAsync(int supplyEntryId, int supplyId)
    {
        var supplyEntries = await _repository.GetSupplyHistoryAsync(supplyEntryId,supplyId);
        return supplyEntries.Select(MapToDto);
    }

    public async Task<SupplyEntry?> GetFirstEntryBySupplyIdAsync(int supplyId)
    {
        return await _repository.GetFirstEntryBySupplyIdAsync(supplyId);
    }

    private static SupplyEntryDto MapToDto(SupplyEntry supplyEntry)
    {
        return new SupplyEntryDto
        {
            Id = supplyEntry.Id,
            UnitCost = supplyEntry.UnitCost,
            Amount = (decimal)supplyEntry.Amount, // Convert int to decimal
            Tag = supplyEntry.Tag,
            ProviderId = supplyEntry.ProviderId,
            SupplyId = supplyEntry.SupplyId,
            ProcessDoneId = supplyEntry.ProcessDoneId,
            ReferenceToSupplyEntry = supplyEntry.ReferenceToSupplyEntry,
            IsActive = supplyEntry.IsActive,
            CreatedAt = supplyEntry.CreatedAt,
            UpdatedAt = supplyEntry.UpdatedAt,
            Provider = supplyEntry.Provider != null ? new ProviderDto
            {
                Id = supplyEntry.Provider.Id,
                Name = supplyEntry.Provider.Name,
                StoreId = supplyEntry.Provider.StoreId
            } : null,
            Supply = supplyEntry.Supply != null ? new SupplyDto
            {
                Id = supplyEntry.Supply.Id,
                Name = supplyEntry.Supply.Name,
                Description = supplyEntry.Supply.Description,
                UnitMeasureId = supplyEntry.Supply.UnitMeasureId,
                UnitMeasure = supplyEntry.Supply.UnitMeasure != null ? new UnitMeasureDto
                {
                    Id = supplyEntry.Supply.UnitMeasure.Id,
                    Name = supplyEntry.Supply.UnitMeasure.Name,
                    Symbol = supplyEntry.Supply.UnitMeasure.Symbol
                } : null,
                FixedExpenseId = supplyEntry.Supply.FixedExpenseId,
                Active = supplyEntry.Supply.Active,
                BusinessId = supplyEntry.Supply.BusinessId,
                StoreId = supplyEntry.Supply.StoreId,
                CreatedAt = supplyEntry.Supply.CreatedAt,
                UpdatedAt = supplyEntry.Supply.UpdatedAt
            } : null,
            ProcessDone = supplyEntry.ProcessDone != null ? new ProcessDoneDto
            {
                Id = supplyEntry.ProcessDone.Id,
                ProcessId = supplyEntry.ProcessDone.ProcessId,
                CompletedAt = supplyEntry.ProcessDone.CompletedAt,
                Notes = supplyEntry.ProcessDone.Notes,
                // Solo mapear los campos que realmente existen en ProcessDone
                Stage = 1, // Default value for completed process
                StartDate = supplyEntry.ProcessDone.CompletedAt,
                EndDate = supplyEntry.ProcessDone.CompletedAt,
                StockId = null, // Nullable int
                Amount = 0, // Default value
                CreatedAt = supplyEntry.ProcessDone.CompletedAt,
                UpdatedAt = supplyEntry.ProcessDone.CompletedAt,
                IsActive = true,
                // No incluir Process ya que no está disponible en el query
                Process = null
            } : null
        };
    }
}
