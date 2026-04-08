using GPInventory.Application.DTOs.Services;
using GPInventory.Application.Interfaces;
using GPInventory.Domain.Entities;
using GPInventory.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace GPInventory.Application.Services;

public class ServiceClientService : IServiceClientService
{
    private readonly ApplicationDbContext _context;

    public ServiceClientService(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<ServiceClientDto> GetByIdAsync(int id)
    {
        var client = await _context.ServiceClients
            .Include(c => c.SubClients)
                .ThenInclude(s => s.RelationshipType)
            .Include(c => c.ParentClient)
            .Include(c => c.RelationshipType)
            .FirstOrDefaultAsync(c => c.Id == id);

        if (client == null)
            throw new InvalidOperationException($"ServiceClient with id {id} not found");

        return await MapToDtoAsync(client);
    }

    public async Task<IEnumerable<ServiceClientDto>> GetAllAsync(int businessId)
    {
        var clients = await _context.ServiceClients
            .Include(c => c.SubClients)
                .ThenInclude(s => s.RelationshipType)
            .Include(c => c.ParentClient)
            .Include(c => c.RelationshipType)
            .Where(c => c.BusinessId == businessId)
            .OrderBy(c => c.Name)
            .ToListAsync();

        var dtos = new List<ServiceClientDto>();
        foreach (var client in clients)
            dtos.Add(await MapToDtoAsync(client));

        return dtos;
    }

    public async Task<IEnumerable<ServiceClientDto>> GetByStoreIdAsync(int storeId)
    {
        var clients = await _context.ServiceClients
            .Include(c => c.SubClients)
                .ThenInclude(s => s.RelationshipType)
            .Include(c => c.ParentClient)
            .Include(c => c.RelationshipType)
            .Where(c => c.StoreId == storeId)
            .OrderBy(c => c.Name)
            .ToListAsync();

        var dtos = new List<ServiceClientDto>();
        foreach (var client in clients)
            dtos.Add(await MapToDtoAsync(client));

        return dtos;
    }

    public async Task<IEnumerable<ServiceClientDto>> GetActiveClientsAsync(int businessId)
    {
        var clients = await _context.ServiceClients
            .Include(c => c.SubClients)
                .ThenInclude(s => s.RelationshipType)
            .Include(c => c.ParentClient)
            .Include(c => c.RelationshipType)
            .Where(c => c.BusinessId == businessId && c.Active)
            .OrderBy(c => c.Name)
            .ToListAsync();

        var dtos = new List<ServiceClientDto>();
        foreach (var client in clients)
            dtos.Add(await MapToDtoAsync(client));

        return dtos;
    }

    public async Task<ServiceClientDto> CreateAsync(CreateServiceClientDto dto)
    {
        var client = new ServiceClient
        {
            BusinessId = dto.BusinessId,
            StoreId = dto.StoreId,
            Name = dto.Name,
            Rut = dto.Rut,
            Email = dto.Email,
            Phone = dto.Phone,
            Address = dto.Address,
            City = dto.City,
            ContactPerson = dto.ContactPerson,
            ClientType = dto.ClientType,
            Segment = dto.Segment,
            Tags = dto.Tags,
            Notes = dto.Notes,
            Active = true,
            CreatedByUserId = dto.CreatedByUserId,
            ParentClientId = dto.ParentClientId,
            RelationshipTypeId = dto.RelationshipTypeId,
            BirthDate = dto.BirthDate
        };

        _context.ServiceClients.Add(client);
        await _context.SaveChangesAsync();

        // Recargar con navegación para devolver DTO completo
        return await GetByIdAsync(client.Id);
    }

    public async Task<ServiceClientDto> UpdateAsync(int id, UpdateServiceClientDto dto)
    {
        var client = await _context.ServiceClients.FindAsync(id);

        if (client == null)
            throw new InvalidOperationException($"ServiceClient with id {id} not found");

        client.Name = dto.Name;
        client.Rut = dto.Rut;
        client.Email = dto.Email;
        client.Phone = dto.Phone;
        client.Address = dto.Address;
        client.City = dto.City;
        client.ContactPerson = dto.ContactPerson;
        client.ClientType = dto.ClientType;
        client.Segment = dto.Segment;
        client.Tags = dto.Tags;
        client.Notes = dto.Notes;
        client.Active = dto.Active;
        client.RelationshipTypeId = dto.RelationshipTypeId;
        client.BirthDate = dto.BirthDate;
        client.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        return await GetByIdAsync(client.Id);
    }

    public async Task DeleteAsync(int id)
    {
        var client = await _context.ServiceClients.FindAsync(id);

        if (client == null)
            throw new InvalidOperationException($"ServiceClient with id {id} not found");

        // Verificar si tiene ventas asociadas
        var hasSales = await _context.ServiceSales.AnyAsync(s => s.ServiceClientId == id);

        if (hasSales)
        {
            // Soft delete: marcar como inactivo en lugar de eliminar
            client.Active = false;
            client.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();
        }
        else
        {
            // Hard delete si no tiene ventas
            _context.ServiceClients.Remove(client);
            await _context.SaveChangesAsync();
        }
    }

    public async Task<IEnumerable<ServiceClientDto>> GetSubClientsAsync(int parentClientId)
    {
        var subClients = await _context.ServiceClients
            .Include(c => c.SubClients)
                .ThenInclude(s => s.RelationshipType)
            .Include(c => c.RelationshipType)
            .Where(c => c.ParentClientId == parentClientId)
            .OrderBy(c => c.Name)
            .ToListAsync();

        var dtos = new List<ServiceClientDto>();
        foreach (var client in subClients)
            dtos.Add(await MapToDtoAsync(client));

        return dtos;
    }

    public async Task<IEnumerable<RelationshipTypeDto>> GetRelationshipTypesAsync()
    {
        return await _context.ServiceClientRelationshipTypes
            .Where(r => r.Active)
            .OrderBy(r => r.SortOrder)
            .Select(r => new RelationshipTypeDto
            {
                Id = r.Id,
                Code = r.Code,
                Label = r.Label,
                Description = r.Description,
                SortOrder = r.SortOrder
            })
            .ToListAsync();
    }

    public async Task<IEnumerable<ServiceSaleDto>> GetClientHistoryAsync(int clientId)
    {
        var sales = await _context.ServiceSales
            .Include(s => s.Items).ThenInclude(i => i.Service)
            .Where(s => s.ServiceClientId == clientId)
            .OrderByDescending(s => s.Date)
            .ToListAsync();

        return sales.Select(s => new ServiceSaleDto
        {
            Id = s.Id,
            BusinessId = s.BusinessId,
            StoreId = s.StoreId,
            UserId = s.UserId,
            ServiceClientId = s.ServiceClientId,
            ClientName = s.ClientName,
            ClientRut = s.ClientRut,
            ClientEmail = s.ClientEmail,
            ClientPhone = s.ClientPhone,
            EnrollmentCount = s.EnrollmentCount,
            AmountNet = s.AmountNet,
            AmountIva = s.AmountIva,
            TotalAmount = s.TotalAmount,
            Status = s.Status,
            Date = s.Date,
            ScheduledDate = s.ScheduledDate,
            CompletedDate = s.CompletedDate,
            DocumentType = s.DocumentType,
            PaymentType = (GPInventory.Domain.Enums.PaymentType)s.PaymentType,
            InstallmentsCount = s.InstallmentsCount,
            PaymentStartDate = s.PaymentStartDate,
            CreatedAt = s.CreatedAt,
            UpdatedAt = s.UpdatedAt,
            Items = s.Items.Select(i => new ServiceSaleItemDto
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
                    Active = i.Service.Active ?? false,
                    CreatedAt = i.Service.CreatedAt ?? DateTime.MinValue,
                    UpdatedAt = i.Service.UpdatedAt ?? DateTime.MinValue
                } : null
            }).ToList()
        });
    }

    private async Task<ServiceClientDto> MapToDtoAsync(ServiceClient client)
    {
        // Calcular estadísticas del cliente
        var salesStats = await _context.ServiceSales
            .Where(s => s.ServiceClientId == client.Id && s.Status == Domain.Enums.ServiceSaleStatus.Completed)
            .Select(s => new
            {
                s.TotalAmount,
                s.Date
            })
            .ToListAsync();

        var totalPurchases = salesStats.Count;
        var totalRevenue = salesStats.Sum(s => s.TotalAmount ?? 0);
        var lastPurchaseDate = salesStats.MaxBy(s => s.Date)?.Date;

        // Sub-clientes ya cargados por Include (si no, cargar desde DB)
        var subClientList = client.SubClients?.ToList()
            ?? await _context.ServiceClients
                .Include(c => c.RelationshipType)
                .Where(c => c.ParentClientId == client.Id)
                .OrderBy(c => c.Name)
                .ToListAsync();

        return new ServiceClientDto
        {
            Id = client.Id,
            BusinessId = client.BusinessId,
            StoreId = client.StoreId,
            Name = client.Name,
            Rut = client.Rut,
            Email = client.Email,
            Phone = client.Phone,
            Address = client.Address,
            City = client.City,
            ContactPerson = client.ContactPerson,
            ClientType = client.ClientType,
            Segment = client.Segment,
            Tags = client.Tags,
            Notes = client.Notes,
            Active = client.Active,
            CreatedAt = client.CreatedAt,
            UpdatedAt = client.UpdatedAt,
            CreatedByUserId = client.CreatedByUserId,
            TotalPurchases = totalPurchases,
            TotalRevenue = totalRevenue,
            LastPurchaseDate = lastPurchaseDate,
            // Sub-cliente
            ParentClientId = client.ParentClientId,
            ParentClientName = client.ParentClient?.Name,
            RelationshipTypeId = client.RelationshipTypeId,
            RelationshipLabel = client.RelationshipType?.Label,
            BirthDate = client.BirthDate,
            SubClients = subClientList
                .Select(s => new SubClientSummaryDto
                {
                    Id = s.Id,
                    Name = s.Name,
                    RelationshipTypeId = s.RelationshipTypeId,
                    RelationshipLabel = s.RelationshipType?.Label,
                    BirthDate = s.BirthDate,
                    Active = s.Active,
                    Rut = s.Rut,
                    Phone = s.Phone,
                    Notes = s.Notes
                }).ToList()
        };
    }
}
