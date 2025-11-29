using GPInventory.Application.DTOs.Budgets;
using GPInventory.Application.Interfaces;
using Microsoft.Extensions.Logging;

namespace GPInventory.Infrastructure.Services;

public class BudgetService : IBudgetService
{
    private readonly IBudgetRepository _budgetRepository;
    private readonly ILogger<BudgetService> _logger;

    public BudgetService(IBudgetRepository budgetRepository, ILogger<BudgetService> logger)
    {
        _budgetRepository = budgetRepository;
        _logger = logger;
    }

    public async Task<List<BudgetDto>> GetBudgetsAsync(int? storeId, int? businessId, int? year, string? status)
    {
        try
        {
            return await _budgetRepository.GetBudgetsAsync(storeId, businessId, year, status);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching budgets");
            throw;
        }
    }

    public async Task<BudgetDto> GetBudgetByIdAsync(int id)
    {
        try
        {
            var budget = await _budgetRepository.GetBudgetByIdAsync(id);
            if (budget == null)
            {
                throw new KeyNotFoundException($"Presupuesto con ID {id} no encontrado");
            }
            return budget;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching budget {Id}", id);
            throw;
        }
    }

    public async Task<BudgetDto> CreateBudgetAsync(CreateBudgetDto createDto)
    {
        try
        {
            // Validations
            ValidateCreateBudget(createDto);

            var budgetId = await _budgetRepository.CreateBudgetAsync(createDto);
            return await GetBudgetByIdAsync(budgetId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating budget");
            throw;
        }
    }

    public async Task<BudgetDto> UpdateBudgetAsync(int id, UpdateBudgetDto updateDto)
    {
        try
        {
            // Verify budget exists
            await GetBudgetByIdAsync(id);

            var success = await _budgetRepository.UpdateBudgetAsync(id, updateDto);
            if (!success)
            {
                throw new Exception("Error al actualizar el presupuesto");
            }

            return await GetBudgetByIdAsync(id);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating budget {Id}", id);
            throw;
        }
    }

    public async Task DeleteBudgetAsync(int id)
    {
        try
        {
            // Verify budget exists
            await GetBudgetByIdAsync(id);

            var success = await _budgetRepository.DeleteBudgetAsync(id);
            if (!success)
            {
                throw new Exception("Error al eliminar el presupuesto");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting budget {Id}", id);
            throw;
        }
    }

    public async Task<BudgetSummaryDto> GetBudgetSummaryAsync(int id)
    {
        try
        {
            var summary = await _budgetRepository.GetBudgetSummaryAsync(id);
            if (summary == null)
            {
                throw new KeyNotFoundException($"Presupuesto con ID {id} no encontrado");
            }
            return summary;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching budget summary {Id}", id);
            throw;
        }
    }

    public async Task<List<BudgetAllocationDto>> GetBudgetAllocationsAsync(int budgetId)
    {
        try
        {
            return await _budgetRepository.GetBudgetAllocationsAsync(budgetId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching budget allocations for budget {Id}", budgetId);
            throw;
        }
    }

    public async Task<List<MonthlyDistributionDto>> GetMonthlyDistributionAsync(int budgetId)
    {
        try
        {
            return await _budgetRepository.GetMonthlyDistributionAsync(budgetId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching monthly distribution for budget {Id}", budgetId);
            throw;
        }
    }

    private void ValidateCreateBudget(CreateBudgetDto createDto)
    {
        if (string.IsNullOrWhiteSpace(createDto.Name))
        {
            throw new ArgumentException("El nombre del presupuesto es requerido");
        }

        if (createDto.Year < 2020 || createDto.Year > 2100)
        {
            throw new ArgumentException("El año debe estar entre 2020 y 2100");
        }

        if (createDto.TotalAmount <= 0)
        {
            throw new ArgumentException("El monto total debe ser mayor a 0");
        }

        // Validate allocations
        if (createDto.Allocations != null && createDto.Allocations.Count > 0)
        {
            var totalPercentage = 0m;

            foreach (var allocation in createDto.Allocations)
            {
                var percentage = allocation.GetPercentage();
                var hasPercentage = percentage.HasValue;
                var hasFixedAmount = allocation.FixedAmount.HasValue;

                if (hasPercentage && hasFixedAmount)
                {
                    throw new ArgumentException("No se puede tener porcentaje y monto fijo al mismo tiempo en una asignación");
                }

                if (!hasPercentage && !hasFixedAmount)
                {
                    throw new ArgumentException("Debe especificar porcentaje o monto fijo para cada asignación");
                }

                if (hasPercentage)
                {
                    if (percentage <= 0 || percentage > 100)
                    {
                        throw new ArgumentException("El porcentaje debe estar entre 0 y 100");
                    }
                    totalPercentage += percentage ?? 0;
                }

                if (hasFixedAmount && allocation.FixedAmount <= 0)
                {
                    throw new ArgumentException("El monto fijo debe ser mayor a 0");
                }
            }

            if (totalPercentage > 100)
            {
                throw new ArgumentException($"La suma de los porcentajes ({totalPercentage}%) no puede exceder el 100%");
            }
        }

        // Validate monthly distributions
        if (createDto.MonthlyDistributions != null && createDto.MonthlyDistributions.Count > 0)
        {
            var totalPercentage = 0m;
            var months = new HashSet<int>();

            foreach (var distribution in createDto.MonthlyDistributions)
            {
                if (distribution.Month < 1 || distribution.Month > 12)
                {
                    throw new ArgumentException("El mes debe estar entre 1 y 12");
                }

                if (!months.Add(distribution.Month))
                {
                    throw new ArgumentException($"El mes {distribution.Month} está duplicado");
                }

                var percentage = distribution.GetPercentage();
                var hasPercentage = percentage.HasValue;
                var hasFixedAmount = distribution.FixedAmount.HasValue;

                if (hasPercentage && hasFixedAmount)
                {
                    throw new ArgumentException("No se puede tener porcentaje y monto fijo al mismo tiempo en una distribución mensual");
                }

                if (!hasPercentage && !hasFixedAmount)
                {
                    throw new ArgumentException("Debe especificar porcentaje o monto fijo para cada distribución mensual");
                }

                if (hasPercentage)
                {
                    if (percentage <= 0 || percentage > 100)
                    {
                        throw new ArgumentException("El porcentaje mensual debe estar entre 0 y 100");
                    }
                    totalPercentage += percentage ?? 0;
                }

                if (hasFixedAmount && distribution.FixedAmount <= 0)
                {
                    throw new ArgumentException("El monto fijo mensual debe ser mayor a 0");
                }
            }

            // Only validate if all distributions use percentage
            if (createDto.MonthlyDistributions.All(d => d.GetPercentage().HasValue) && totalPercentage > 100)
            {
                throw new ArgumentException($"La suma de los porcentajes mensuales ({totalPercentage}%) no puede exceder el 100%");
            }
        }
    }
}
