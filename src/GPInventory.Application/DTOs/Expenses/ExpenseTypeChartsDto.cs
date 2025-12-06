namespace GPInventory.Application.DTOs.Expenses;

/// <summary>
/// DTO para las visualizaciones estratégicas de expenses por tipo
/// Incluye datos optimizados para gráficos de torta, línea, barras e indicadores
/// </summary>
public class ExpenseTypeChartsDto
{
    /// <summary>
    /// Datos para el gráfico de torta - Distribución por categoría
    /// </summary>
    public List<CategoryChartDataDto> CategoryDistribution { get; set; } = new();

    /// <summary>
    /// Datos para el gráfico de línea - Evolución mensual acumulada
    /// </summary>
    public List<MonthlyChartDataDto> MonthlyTrend { get; set; } = new();

    /// <summary>
    /// Datos para indicadores de estado - Activas vs Finalizadas
    /// </summary>
    public StatusIndicatorDto StatusIndicator { get; set; } = new();

    /// <summary>
    /// Datos para gráfico de barras - Presupuesto ejecutado por categoría (Pro)
    /// </summary>
    public List<BudgetExecutionDto> BudgetExecution { get; set; } = new();
}

/// <summary>
/// Datos de categoría para gráfico de torta
/// </summary>
public class CategoryChartDataDto
{
    public int CategoryId { get; set; }
    public string CategoryName { get; set; } = string.Empty;
    public decimal TotalAmount { get; set; }
    public int Count { get; set; }
    public decimal Percentage { get; set; }
}

/// <summary>
/// Datos mensuales para gráfico de línea acumulada
/// </summary>
public class MonthlyChartDataDto
{
    public int Month { get; set; }
    public string MonthName { get; set; } = string.Empty;
    public decimal MonthlyAmount { get; set; }
    public decimal AccumulatedAmount { get; set; }
}

/// <summary>
/// Indicador de estado activas vs finalizadas
/// </summary>
public class StatusIndicatorDto
{
    public int ActiveCount { get; set; }
    public int CompletedCount { get; set; }
    public int TotalCount { get; set; }
    public decimal ActivePercentage { get; set; }
    public decimal CompletedPercentage { get; set; }
}

/// <summary>
/// Ejecución de presupuesto por categoría
/// </summary>
public class BudgetExecutionDto
{
    public int CategoryId { get; set; }
    public string CategoryName { get; set; } = string.Empty;
    public decimal BudgetAmount { get; set; }
    public decimal ExecutedAmount { get; set; }
    public decimal ExecutionPercentage { get; set; }
    public bool IsOverBudget { get; set; }
}
