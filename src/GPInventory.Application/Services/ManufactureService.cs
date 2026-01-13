using GPInventory.Application.DTOs.Production;
using GPInventory.Application.Interfaces;
using GPInventory.Domain.Entities;
using System.Data.Common;

namespace GPInventory.Application.Services;

public class ManufactureService : IManufactureService
{
    private readonly IManufactureRepository _manufactureRepository;
    private readonly IProcessDoneRepository _processDoneRepository;
    private readonly IProductRepository _productRepository;
    private readonly IUserRepository _userRepository;

    public ManufactureService(
        IManufactureRepository manufactureRepository,
        IProcessDoneRepository processDoneRepository,
        IProductRepository productRepository,
        IUserRepository userRepository)
    {
        _manufactureRepository = manufactureRepository;
        _processDoneRepository = processDoneRepository;
        _productRepository = productRepository;
        _userRepository = userRepository;
    }

    public async Task<ManufactureDto> GetByIdAsync(int id)
    {
        var manufacture = await _manufactureRepository.GetByIdAsync(id);
        if (manufacture == null)
            throw new ArgumentException($"Manufacture with ID {id} not found");

        return MapToDto(manufacture);
    }

    public async Task<IEnumerable<ManufactureDto>> GetAllAsync()
    {
        var manufactures = await _manufactureRepository.GetAllAsync();
        return manufactures.Select(MapToDto);
    }

    public async Task<IEnumerable<ManufactureDto>> GetByBusinessIdAsync(int businessId)
    {
        var manufactures = await _manufactureRepository.GetByBusinessIdAsync(businessId);
        var dtos = manufactures.Select(MapToDto).ToList();
        
        // Cargar nombres de usuarios
        await EnrichWithUserNamesAsync(dtos);
        
        return dtos;
    }

    public async Task<IEnumerable<ManufactureDto>> GetByProcessDoneIdAsync(int processDoneId)
    {
        var manufactures = await _manufactureRepository.GetByProcessDoneIdAsync(processDoneId);
        return manufactures.Select(MapToDto);
    }

    public async Task<IEnumerable<ManufactureDto>> GetPendingAsync(int businessId)
    {
        var manufactures = await _manufactureRepository.GetPendingAsync(businessId);
        return manufactures.Select(MapToDto);
    }

    public async Task<IEnumerable<ProcessDoneSummaryDto>> GetProcessDoneSummariesAsync(int businessId)
    {
        // Obtener todos los manufactures del negocio
        var manufactures = await _manufactureRepository.GetByBusinessIdAsync(businessId);
        var allDtos = manufactures.Select(MapToDto).ToList();
        
        // Cargar nombres de usuarios para TODOS los manufactures
        await EnrichWithUserNamesAsync(allDtos);
        
        // Agrupar por ProcessDoneId
        var grouped = allDtos
            .GroupBy(m => m.ProcessDoneId)
            .Select(g => new ProcessDoneSummaryDto
            {
                ProcessDoneId = g.Key,
                ProcessId = g.First().ProcessDone?.ProcessId ?? 0,
                ProcessName = g.First().ProcessDone?.Process?.Name ?? "Unknown",
                CompletedAt = g.First().ProcessDone?.CompletedAt ?? DateTime.MinValue,
                Notes = g.First().ProcessDone?.Notes,
                TotalBatches = g.Count(),
                TotalAmount = g.Sum(m => m.Amount),
                Batches = g.ToList()
            })
            .OrderByDescending(s => s.CompletedAt)
            .ToList();

        return grouped;
    }

    public async Task<ManufactureDto> CreateAsync(CreateManufactureDto createDto)
    {
        // Verificar que el producto existe
        var product = await _productRepository.GetByIdAsync(createDto.ProductId);
        if (product == null)
            throw new ArgumentException($"Product with ID {createDto.ProductId} not found");

        // Verificar que el process done existe
        var processDone = await _processDoneRepository.GetByIdAsync(createDto.ProcessDoneId);
        if (processDone == null)
            throw new ArgumentException($"ProcessDone with ID {createDto.ProcessDoneId} not found");

        var manufacture = new Manufacture(
            productId: createDto.ProductId,
            processDoneId: createDto.ProcessDoneId,
            businessId: createDto.BusinessId,
            amount: createDto.Amount,
            cost: createDto.Cost,
            notes: createDto.Notes,
            expirationDate: createDto.ExpirationDate
        );

        var created = await _manufactureRepository.AddAsync(manufacture);
        return MapToDto(created);
    }

    public async Task<ManufactureDto> UpdateAsync(int id, UpdateManufactureDto updateDto)
    {
        var manufacture = await _manufactureRepository.GetByIdAsync(id);
        if (manufacture == null)
            throw new ArgumentException($"Manufacture with ID {id} not found");

        // Check if we're transitioning to sent status with a store
        bool shouldCreateStock = updateDto.StoreId.HasValue && 
                                updateDto.Status == "sent" && 
                                manufacture.Status != "sent" &&
                                !manufacture.StockId.HasValue;

        if (updateDto.Amount.HasValue)
            manufacture.Amount = updateDto.Amount.Value;

        if (updateDto.Cost.HasValue)
            manufacture.Cost = updateDto.Cost.Value;

        if (updateDto.Notes != null)
            manufacture.Notes = updateDto.Notes;

        if (updateDto.ExpirationDate.HasValue)
            manufacture.ExpirationDate = updateDto.ExpirationDate;

        if (updateDto.Status != null)
            manufacture.Status = updateDto.Status;

        if (updateDto.StoreId.HasValue)
            manufacture.StoreId = updateDto.StoreId;

        // Si debemos crear stock, hacerlo antes de actualizar el manufacture
        if (shouldCreateStock)
        {
            var stockId = await CreateStockForManufactureAsync(manufacture);
            manufacture.StockId = stockId;
            // Cuando se envía, desactivar el manufacture (active = 0)
            manufacture.IsActive = false;
        }

        var updated = await _manufactureRepository.UpdateAsync(manufacture);
        return MapToDto(updated);
    }

    private async Task<int> CreateStockForManufactureAsync(Manufacture manufacture)
    {
        if (!manufacture.StoreId.HasValue)
            throw new InvalidOperationException("StoreId is required to create stock");

        // Get database connection
        var connection = await _manufactureRepository.GetDbConnectionAsync();
        if (connection.State != System.Data.ConnectionState.Open)
        {
            await connection.OpenAsync();
        }

        // Get default provider for the business using raw SQL
        int? providerId = null;
        using (var command = connection.CreateCommand())
        {
            command.CommandText = @"
                SELECT id 
                FROM provider 
                WHERE id_business = @businessId 
                  AND active = 1 
                ORDER BY id ASC 
                LIMIT 1";
            
            var param = command.CreateParameter();
            param.ParameterName = "@businessId";
            param.Value = manufacture.BusinessId;
            command.Parameters.Add(param);
            
            var result = await command.ExecuteScalarAsync();
            if (result != null && result != DBNull.Value)
            {
                providerId = Convert.ToInt32(result);
            }
        }

        if (!providerId.HasValue)
            throw new InvalidOperationException($"No active provider found for business {manufacture.BusinessId}");

        // Create Stock entry using raw SQL (FlowTypeId = 1 for incoming)
        int stockId;
        using (var insertCommand = connection.CreateCommand())
        {
            insertCommand.CommandText = @"
                INSERT INTO stock (product, date, flow, amount, cost, provider, notes, id_store, expiration_date, active, created_at)
                VALUES (@product, @date, @flow, @amount, @cost, @provider, @notes, @storeId, @expirationDate, 1, @createdAt);
                SELECT LAST_INSERT_ID();";
            
            var productParam = insertCommand.CreateParameter();
            productParam.ParameterName = "@product";
            productParam.Value = manufacture.ProductId;
            insertCommand.Parameters.Add(productParam);
            
            var dateParam = insertCommand.CreateParameter();
            dateParam.ParameterName = "@date";
            dateParam.Value = DateTime.UtcNow;
            insertCommand.Parameters.Add(dateParam);
            
            var flowParam = insertCommand.CreateParameter();
            flowParam.ParameterName = "@flow";
            flowParam.Value = 1; // FlowType "Entrada" / "Incoming"
            insertCommand.Parameters.Add(flowParam);
            
            var amountParam = insertCommand.CreateParameter();
            amountParam.ParameterName = "@amount";
            amountParam.Value = manufacture.Amount;
            insertCommand.Parameters.Add(amountParam);
            
            var costParam = insertCommand.CreateParameter();
            costParam.ParameterName = "@cost";
            costParam.Value = (object?)manufacture.Cost ?? DBNull.Value;
            insertCommand.Parameters.Add(costParam);
            
            var providerParam = insertCommand.CreateParameter();
            providerParam.ParameterName = "@provider";
            providerParam.Value = providerId.Value;
            insertCommand.Parameters.Add(providerParam);
            
            var notesParam = insertCommand.CreateParameter();
            notesParam.ParameterName = "@notes";
            notesParam.Value = $"Stock from manufacture batch #{manufacture.Id}";
            insertCommand.Parameters.Add(notesParam);
            
            var storeIdParam = insertCommand.CreateParameter();
            storeIdParam.ParameterName = "@storeId";
            storeIdParam.Value = manufacture.StoreId.Value;
            insertCommand.Parameters.Add(storeIdParam);
            
            var expirationParam = insertCommand.CreateParameter();
            expirationParam.ParameterName = "@expirationDate";
            expirationParam.Value = (object?)manufacture.ExpirationDate ?? DBNull.Value;
            insertCommand.Parameters.Add(expirationParam);
            
            var createdAtParam = insertCommand.CreateParameter();
            createdAtParam.ParameterName = "@createdAt";
            createdAtParam.Value = DateTime.UtcNow;
            insertCommand.Parameters.Add(createdAtParam);
            
            var result = await insertCommand.ExecuteScalarAsync();
            stockId = Convert.ToInt32(result);
        }

        // Update manufacture with stock_id
        using (var updateCommand = connection.CreateCommand())
        {
            updateCommand.CommandText = @"
                UPDATE manufacture 
                SET stock_id = @stockId 
                WHERE id = @manufactureId";
            
            var stockIdParam = updateCommand.CreateParameter();
            stockIdParam.ParameterName = "@stockId";
            stockIdParam.Value = stockId;
            updateCommand.Parameters.Add(stockIdParam);
            
            var manufactureIdParam = updateCommand.CreateParameter();
            manufactureIdParam.ParameterName = "@manufactureId";
            manufactureIdParam.Value = manufacture.Id;
            updateCommand.Parameters.Add(manufactureIdParam);
            
            await updateCommand.ExecuteNonQueryAsync();
        }

        return stockId;
    }

    public async Task DeleteAsync(int id)
    {
        var exists = await _manufactureRepository.ExistsAsync(id);
        if (!exists)
            throw new ArgumentException($"Manufacture with ID {id} not found");

        await _manufactureRepository.DeleteAsync(id);
    }

    private async Task EnrichWithUserNamesAsync(List<ManufactureDto> dtos)
    {
        var userIds = dtos
            .Where(d => d.CreatedByUserId.HasValue)
            .Select(d => d.CreatedByUserId!.Value)
            .Distinct()
            .ToList();
        
        Console.WriteLine($"🔍 ManufactureService: Found {userIds.Count} unique user IDs to load: {string.Join(", ", userIds)}");
        
        if (!userIds.Any()) return;
        
        var userNames = await _userRepository.GetUserNamesByIdsAsync(userIds);
        
        Console.WriteLine($"🔍 ManufactureService: Received {userNames.Count} user names from repository");
        
        foreach (var dto in dtos)
        {
            if (dto.CreatedByUserId.HasValue && userNames.TryGetValue(dto.CreatedByUserId.Value, out var userName))
            {
                dto.CreatedByUserName = userName;
                Console.WriteLine($"✅ Set user name for manufacture {dto.Id}: {userName}");
            }
            else if (dto.CreatedByUserId.HasValue)
            {
                Console.WriteLine($"⚠️ No user name found for manufacture {dto.Id} with userId {dto.CreatedByUserId.Value}");
            }
        }
    }

    private static ManufactureDto MapToDto(Manufacture manufacture)
    {
        return new ManufactureDto
        {
            Id = manufacture.Id,
            ProductId = manufacture.ProductId,
            Date = manufacture.Date,
            Amount = manufacture.Amount,
            Cost = manufacture.Cost,
            Notes = manufacture.Notes,
            StoreId = manufacture.StoreId,
            StockId = manufacture.StockId,
            ExpirationDate = manufacture.ExpirationDate,
            ProcessDoneId = manufacture.ProcessDoneId,
            BusinessId = manufacture.BusinessId,
            Status = manufacture.Status,
            CreatedAt = manufacture.CreatedAt,
            UpdatedAt = manufacture.UpdatedAt,
            CreatedByUserId = manufacture.CreatedByUserId,
            Product = manufacture.Product != null ? new ProductDto
            {
                Id = manufacture.Product.Id,
                Name = manufacture.Product.Name,
                Price = manufacture.Product.Price,
                Cost = manufacture.Product.Cost
            } : null,
            ProcessDone = manufacture.ProcessDone != null ? new ProcessDoneDto
            {
                Id = manufacture.ProcessDone.Id,
                ProcessId = manufacture.ProcessDone.ProcessId,
                CompletedAt = manufacture.ProcessDone.CompletedAt,
                Notes = manufacture.ProcessDone.Notes,
                Stage = 1,
                StartDate = manufacture.ProcessDone.CompletedAt,
                EndDate = manufacture.ProcessDone.CompletedAt,
                StockId = null,
                Amount = 0,
                CreatedAt = manufacture.ProcessDone.CompletedAt,
                UpdatedAt = manufacture.ProcessDone.CompletedAt,
                IsActive = true,
                Process = manufacture.ProcessDone.Process != null ? new ProcessDto
                {
                    Id = manufacture.ProcessDone.Process.Id,
                    Name = manufacture.ProcessDone.Process.Name,
                } : null
            } : null,
            Store = manufacture.Store != null ? new StoreDto
            {
                Id = manufacture.Store.Id,
                Name = manufacture.Store.Name ?? string.Empty
            } : null
        };
    }
}
