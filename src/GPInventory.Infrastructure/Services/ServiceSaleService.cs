using GPInventory.Application.DTOs.Services;
using GPInventory.Application.Helpers;
using GPInventory.Application.Interfaces;
using GPInventory.Domain.Entities;
using GPInventory.Domain.Enums;
using GPInventory.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace GPInventory.Application.Services;

public class ServiceSaleService : IServiceSaleService
{
    private readonly ApplicationDbContext _context;

    public ServiceSaleService(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<ServiceSaleDto> GetByIdAsync(int id)
    {
        var sale = await _context.ServiceSales
            .Include(s => s.ServiceClient)
            .Include(s => s.Items).ThenInclude(i => i.Service).ThenInclude(s => s!.Category)
            .Include(s => s.Supplies).ThenInclude(s => s.Supply)
            .FirstOrDefaultAsync(s => s.Id == id);

        if (sale == null)
            throw new InvalidOperationException($"ServiceSale with id {id} not found");

        return MapToDto(sale);
    }

    public async Task<IEnumerable<ServiceSaleDto>> GetAllAsync(int businessId)
    {
        var sales = await _context.ServiceSales
            .Include(s => s.ServiceClient)
            .Include(s => s.Items).ThenInclude(i => i.Service)
            .Where(s => s.BusinessId == businessId)
            .OrderByDescending(s => s.Date)
            .ToListAsync();

        return sales.Select(MapToDto);
    }

    public async Task<IEnumerable<ServiceSaleDto>> GetByStoreIdAsync(int storeId)
    {
        var sales = await _context.ServiceSales
            .Include(s => s.ServiceClient)
            .Include(s => s.Items).ThenInclude(i => i.Service)
            .Where(s => s.StoreId == storeId)
            .OrderByDescending(s => s.Date)
            .ToListAsync();

        return sales.Select(MapToDto);
    }

    public async Task<IEnumerable<ServiceSaleDto>> GetPendingSalesAsync(int businessId)
    {
        var sales = await _context.ServiceSales
            .Include(s => s.ServiceClient)
            .Include(s => s.Items).ThenInclude(i => i.Service)
            .Where(s => s.BusinessId == businessId && s.Status == ServiceSaleStatus.Pending)
            .OrderBy(s => s.ScheduledDate)
            .ToListAsync();

        return sales.Select(MapToDto);
    }

    public async Task<IEnumerable<ServiceSaleDto>> GetSalesByClientIdAsync(int clientId)
    {
        var sales = await _context.ServiceSales
            .Include(s => s.ServiceClient)
            .Include(s => s.Items).ThenInclude(i => i.Service)
            .Where(s => s.ServiceClientId == clientId)
            .OrderByDescending(s => s.Date)
            .ToListAsync();

        return sales.Select(MapToDto);
    }

    public async Task<ServiceSaleDto> CreateAsync(CreateServiceSaleDto dto)
    {
        var sale = new ServiceSale
        {
            BusinessId = dto.BusinessId,
            StoreId = dto.StoreId,
            UserId = dto.UserId,
            ServiceClientId = dto.ServiceClientId,
            ClientName = dto.ClientName,
            ClientRut = dto.ClientRut,
            ClientEmail = dto.ClientEmail,
            ClientPhone = dto.ClientPhone,
            Date = dto.Date,
            ScheduledDate = dto.ScheduledDate,
            DocumentType = dto.DocumentType,
            PaymentType = (int)dto.PaymentType,
            InstallmentsCount = dto.InstallmentsCount,
            PaymentStartDate = dto.PaymentStartDate,
            Status = ServiceSaleStatus.Pending,
            Items = new List<ServiceSaleItem>()
        };

        // Agregar items
        foreach (var itemDto in dto.Items)
        {
            sale.Items.Add(new ServiceSaleItem
            {
                ServiceId = itemDto.ServiceId,
                Price = itemDto.Price,
                Notes = itemDto.Notes
            });
        }

        _context.ServiceSales.Add(sale);
        await _context.SaveChangesAsync();

        return await GetByIdAsync(sale.Id);
    }

    public async Task<ServiceSaleDto> CompleteAsync(int id, CompleteServiceSaleDto dto)
    {
        var sale = await _context.ServiceSales
            .Include(s => s.Items).ThenInclude(i => i.Service)
            .FirstOrDefaultAsync(s => s.Id == id);

        if (sale == null)
            throw new InvalidOperationException($"ServiceSale with id {id} not found");

        if (sale.Status == ServiceSaleStatus.Completed)
            throw new InvalidOperationException($"ServiceSale {id} is already completed");

        if (sale.Status == ServiceSaleStatus.Cancelled)
            throw new InvalidOperationException($"ServiceSale {id} is cancelled");

        // Validar que hay al menos un item
        if (!sale.Items.Any())
            throw new InvalidOperationException($"ServiceSale {id} has no items");

        // Obtener el servicio principal (primer item)
        var firstItem = sale.Items.First();
        var service = await _context.Services.FindAsync(firstItem.ServiceId);

        if (service == null)
            throw new InvalidOperationException($"Service with id {firstItem.ServiceId} not found");

        // Calcular montos usando ServiceCalculator
        var (amountNet, amountIva, totalAmount) = ServiceCalculator.CalculateAmounts(
            service.BasePrice,
            service.PricingType,
            service.IsTaxable,
            dto.EnrollmentCount
        );

        // Actualizar venta
        sale.EnrollmentCount = dto.EnrollmentCount;
        sale.AmountNet = amountNet;
        sale.AmountIva = amountIva;
        sale.TotalAmount = totalAmount;
        sale.Status = ServiceSaleStatus.Completed;
        sale.CompletedDate = dto.CompletedDate ?? DateTime.UtcNow;
        sale.UpdatedAt = DateTime.UtcNow;

        // Agregar supplies si se proporcionaron
        if (dto.Supplies != null && dto.Supplies.Any())
        {
            foreach (var supplyDto in dto.Supplies)
            {
                var saleSupply = new ServiceSaleSupply
                {
                    ServiceSaleId = id,
                    SupplyId = supplyDto.SupplyId,
                    Quantity = supplyDto.Quantity,
                    Notes = supplyDto.Notes
                };

                _context.ServiceSaleSupplies.Add(saleSupply);

                // Aquí se debería descontar del inventario (supply_entry)
                // TODO: Implementar descuento de inventario
            }
        }

        // Generar expense automáticamente
        var expense = new Expense
        {
            Date = sale.CompletedDate.Value,
            SubcategoryId = 1, // TODO: Determinar categoría correcta basada en configuración
            Amount = totalAmount,
            AmountNet = amountNet,
            AmountIva = amountIva,
            AmountTotal = totalAmount,
            ReceiptTypeId = (int)sale.DocumentType + 1, // Mapeo básico
            Description = $"Venta de servicio #{sale.Id} - {sale.ClientName ?? "Cliente"}",
            BusinessId = sale.BusinessId,
            StoreId = sale.StoreId,
            IsFixed = false,
            ServiceSaleId = sale.Id
        };

        _context.Expenses.Add(expense);

        await _context.SaveChangesAsync();

        return await GetByIdAsync(id);
    }

    public async Task<ServiceSaleDto> StartAsync(int id)
    {
        var sale = await _context.ServiceSales.FindAsync(id);

        if (sale == null)
            throw new InvalidOperationException($"ServiceSale with id {id} not found");

        if (sale.Status != ServiceSaleStatus.Pending)
            throw new InvalidOperationException($"ServiceSale {id} is not pending");

        sale.Status = ServiceSaleStatus.InProgress;
        sale.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        return await GetByIdAsync(id);
    }

    public async Task<ServiceSaleDto> CancelAsync(int id)
    {
        var sale = await _context.ServiceSales.FindAsync(id);

        if (sale == null)
            throw new InvalidOperationException($"ServiceSale with id {id} not found");

        if (sale.Status == ServiceSaleStatus.Completed)
            throw new InvalidOperationException($"ServiceSale {id} is already completed and cannot be cancelled");

        sale.Status = ServiceSaleStatus.Cancelled;
        sale.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        return await GetByIdAsync(id);
    }

    public async Task DeleteAsync(int id)
    {
        var sale = await _context.ServiceSales.FindAsync(id);

        if (sale == null)
            throw new InvalidOperationException($"ServiceSale with id {id} not found");

        if (sale.Status == ServiceSaleStatus.Completed)
            throw new InvalidOperationException($"Cannot delete completed ServiceSale {id}");

        _context.ServiceSales.Remove(sale);
        await _context.SaveChangesAsync();
    }

    private static ServiceSaleDto MapToDto(ServiceSale sale)
    {
        return new ServiceSaleDto
        {
            Id = sale.Id,
            BusinessId = sale.BusinessId,
            StoreId = sale.StoreId,
            UserId = sale.UserId,
            ServiceClientId = sale.ServiceClientId,
            ClientName = sale.ClientName,
            ClientRut = sale.ClientRut,
            ClientEmail = sale.ClientEmail,
            ClientPhone = sale.ClientPhone,
            EnrollmentCount = sale.EnrollmentCount,
            AmountNet = sale.AmountNet,
            AmountIva = sale.AmountIva,
            TotalAmount = sale.TotalAmount,
            Status = sale.Status,
            Date = sale.Date,
            ScheduledDate = sale.ScheduledDate,
            CompletedDate = sale.CompletedDate,
            DocumentType = sale.DocumentType,
            PaymentType = (GPInventory.Domain.Enums.PaymentType)sale.PaymentType,
            InstallmentsCount = sale.InstallmentsCount,
            PaymentStartDate = sale.PaymentStartDate,
            CreatedAt = sale.CreatedAt,
            UpdatedAt = sale.UpdatedAt,
            ServiceClient = sale.ServiceClient != null ? new ServiceClientDto
            {
                Id = sale.ServiceClient.Id,
                BusinessId = sale.ServiceClient.BusinessId,
                StoreId = sale.ServiceClient.StoreId,
                Name = sale.ServiceClient.Name,
                Rut = sale.ServiceClient.Rut,
                Email = sale.ServiceClient.Email,
                Phone = sale.ServiceClient.Phone,
                Address = sale.ServiceClient.Address,
                City = sale.ServiceClient.City,
                ContactPerson = sale.ServiceClient.ContactPerson,
                ClientType = sale.ServiceClient.ClientType,
                Segment = sale.ServiceClient.Segment,
                Tags = sale.ServiceClient.Tags,
                Notes = sale.ServiceClient.Notes,
                Active = sale.ServiceClient.Active,
                CreatedAt = sale.ServiceClient.CreatedAt,
                UpdatedAt = sale.ServiceClient.UpdatedAt,
                CreatedByUserId = sale.ServiceClient.CreatedByUserId
            } : null,
            Items = sale.Items?.Select(i => new ServiceSaleItemDto
            {
                Id = i.Id,
                ServiceSaleId = i.ServiceSaleId,
                ServiceId = i.ServiceId,
                Price = i.Price,
                IsCompleted = i.IsCompleted,
                Notes = i.Notes,
                CreatedAt = i.CreatedAt,
                UpdatedAt = i.UpdatedAt,
                Service = i.Service != null ? new ServiceDto
                {
                    Id = i.Service.Id,
                    Name = i.Service.Name,
                    CategoryId = i.Service.CategoryId,
                    BusinessId = i.Service.BusinessId,
                    StoreId = i.Service.StoreId,
                    BasePrice = i.Service.BasePrice,
                    DurationMinutes = i.Service.DurationMinutes,
                    Description = i.Service.Description,
                    PricingType = i.Service.PricingType,
                    IsTaxable = i.Service.IsTaxable,
                    Active = i.Service.Active,
                    CreatedAt = i.Service.CreatedAt,
                    UpdatedAt = i.Service.UpdatedAt
                } : null
            }).ToList() ?? new List<ServiceSaleItemDto>(),
            Supplies = sale.Supplies?.Select(s => new ServiceSaleSupplyDto
            {
                SupplyId = s.SupplyId,
                Quantity = s.Quantity,
                Notes = s.Notes
            }).ToList() ?? new List<ServiceSaleSupplyDto>()
        };
    }
}
