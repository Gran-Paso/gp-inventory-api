namespace GPInventory.Application.DTOs.Services;

// ── Response ─────────────────────────────────────────────────────────────────

public class ServiceSessionExpenseDto
{
    public int    Id                   { get; set; }
    public int    BusinessId           { get; set; }
    public int?   StoreId              { get; set; }
    public int    ServiceSessionId     { get; set; }
    public int?   ServiceCostItemId    { get; set; }
    public string Description          { get; set; } = string.Empty;
    public decimal Amount              { get; set; }
    public string Status               { get; set; } = "pending";  // pending | paid | cancelled
    public string? PayeeType           { get; set; }               // employee | external
    public int?   PayeeEmployeeId      { get; set; }
    public string? PayeeEmployeeName   { get; set; }
    public string? PayeeExternalName   { get; set; }
    public int?   ExpenseId            { get; set; }
    public DateTime? PaidAt            { get; set; }
    public int?   PaidByUserId         { get; set; }
    public string? Notes               { get; set; }
    public DateTime CreatedAt          { get; set; }
    public DateTime UpdatedAt          { get; set; }

    // Denormalized extras
    public string? SessionDate         { get; set; }  // "YYYY-MM-DD"
    public string? ServiceName         { get; set; }
    public string? CostItemDescription { get; set; }
}

// ── Requests ─────────────────────────────────────────────────────────────────

/// <summary>Asignar destinatario del pago (empleado o persona externa).</summary>
public class AssignPayeeDto
{
    /// <summary>employee | external</summary>
    public string PayeeType          { get; set; } = string.Empty;
    public int?   PayeeEmployeeId    { get; set; }
    public string? PayeeEmployeeName { get; set; }
    public string? PayeeExternalName { get; set; }
    public string? Notes             { get; set; }
}

/// <summary>Marcar como pagado y (opcionalmente) crear en tabla expenses.</summary>
public class MarkSessionExpensePaidDto
{
    public DateTime? PaidAt            { get; set; }
    /// <summary>Si se debe crear el registro en la tabla `expenses` de gp-expenses.</summary>
    public bool CreateExpenseRecord    { get; set; } = true;
    /// <summary>ID de subcategoría de gasto requerido cuando CreateExpenseRecord=true.</summary>
    public int? ExpenseSubcategoryId   { get; set; }
    /// <summary>Método de pago para el expense.</summary>
    public int? PaymentMethodId        { get; set; }
    public string? Notes               { get; set; }
}

/// <summary>Crear manualmente un ítem de gasto pendiente para una sesión.</summary>
public class CreateSessionExpenseManualDto
{
    public int    ServiceSessionId  { get; set; }
    public string Description       { get; set; } = string.Empty;
    public decimal Amount           { get; set; }
    public string? PayeeType        { get; set; }
    public int?   PayeeEmployeeId   { get; set; }
    public string? PayeeEmployeeName { get; set; }
    public string? PayeeExternalName { get; set; }
    public string? Notes            { get; set; }
}
