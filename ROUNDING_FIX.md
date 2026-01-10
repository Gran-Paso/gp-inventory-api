# Corrección de Errores de Redondeo en KPIs de Inversiones

## Problema Identificado

Al dividir el monto total de una inversión en múltiples cuotas, se producía un error de redondeo que causaba que el "Monto Pendiente" no coincidiera con el "Monto Total".

### Ejemplo del Error

- **Monto Total de Inversión**: $160.000.000
- **Número de Cuotas**: 300
- **Cálculo Anterior**: 
  - Monto por cuota = $160.000.000 ÷ 300 = $533.333,333...
  - Con redondeo: $533.333 por cuota
  - Total de 300 cuotas: $533.333 × 300 = **$159.999.900** ❌
- **Diferencia**: Faltaban **$100**

## Solución Implementada

### 1. Cambios en el Backend

#### A. Creación de Cuotas con Ajuste (ExpenseService.cs)

**Archivo**: `src/GPInventory.Application/Services/ExpenseService.cs` - Método `CreateExpenseAsync`

Se modificó la lógica de creación de cuotas para que:

1. Las primeras N-1 cuotas usan el monto base redondeado hacia abajo
2. La **última cuota ajusta** para absorber cualquier diferencia de redondeo
3. Esto garantiza que: `suma(cuotas) = monto_total`

#### Código Anterior
```csharp
decimal installmentAmount = createdExpense.Amount / createExpenseDto.InstallmentsCount.Value;

for (int i = 1; i <= createExpenseDto.InstallmentsCount.Value; i++)
{
    var installment = new PaymentInstallment
    {
        AmountClp = installmentAmount, // ❌ Todas las cuotas iguales
        // ...
    };
}
```

#### Código Nuevo
```csharp
decimal baseInstallmentAmount = Math.Floor(createdExpense.Amount / createExpenseDto.InstallmentsCount.Value);
decimal totalAssigned = 0;

for (int i = 1; i <= createExpenseDto.InstallmentsCount.Value; i++)
{
    decimal installmentAmount;
    
    // La última cuota ajusta para absorber cualquier diferencia
    if (i == createExpenseDto.InstallmentsCount.Value)
    {
        installmentAmount = createdExpense.Amount - totalAssigned; // ✅ Ajuste exacto
    }
    else
    {
        installmentAmount = baseInstallmentAmount;
        totalAssigned += installmentAmount;
    }
    
    var installment = new PaymentInstallment
    {
        AmountClp = installmentAmount,
        // ...
    };
}
```

#### B. Cálculo de KPIs Basado en Monto Original (ExpenseService.cs)

**Archivo**: `src/GPInventory.Application/Services/ExpenseService.cs` - Método `GetExpenseTypeKPIsAsync`

**Problema identificado**: El endpoint `/api/expenses/type-kpis` calculaba el monto pendiente sumando las cuotas individuales, lo que amplificaba los errores de redondeo.

**Solución**: Calcular el monto pendiente basándose en el monto original del expense:

```csharp
// ✅ Correcto: Usar el monto original del expense
decimal pendingForThisExpense = expense.Amount - paidForThisExpense;
pendingAmount += pendingForThisExpense;

// ❌ Anterior: Sumar cuotas individuales (amplifica errores de redondeo)
// pendingAmount += installment.AmountClp;
```

Esta corrección garantiza que:
- El monto pendiente siempre sea exacto
- No se acumulen errores de redondeo al sumar múltiples cuotas
- Los KPIs coincidan con el monto total de las inversiones

### 2. Script de Corrección para Datos Existentes (Opcional)

**Archivo**: `scripts/fix_installment_rounding_error.sql`

**Nota**: Con la corrección del método `GetExpenseTypeKPIsAsync`, este script SQL ya no es estrictamente necesario, ya que el KPI ahora calcula el monto pendiente basándose en el monto original del expense, no en la suma de cuotas.

Sin embargo, si deseas mantener la consistencia perfecta en la base de datos (para auditorías o reportes directos de SQL), puedes ejecutar este script que:
- Identifica todos los planes de pago con diferencias de redondeo
- Ajusta la última cuota de cada plan para corregir la diferencia
- Verifica que la corrección fue exitosa

### 3. Script Batch para Ejecución (Opcional)

**Archivo**: `fix-rounding-errors.bat`

Script para ejecutar la corrección de manera sencilla en Windows.

## Cómo Usar

### Corrección Automática (Recomendado)

La corrección principal está en el código del backend. Solo necesitas:

1. **Reiniciar la API** para que tome los cambios:
   ```cmd
   cd gp-inventory-api
   dotnet run --project src/GPInventory.Api
   ```

2. **Verificar** que el endpoint de KPIs muestre el monto correcto:
   ```
   http://localhost:5000/api/expenses/type-kpis?businessId=4&expenseTypeId=3
   ```

### Corrección Manual en Base de Datos (Opcional)

Si deseas corregir las cuotas existentes en la base de datos por consistencia:

1. Asegúrate de tener acceso a SQL Server
2. Ejecuta el script batch:
   ```cmd
   fix-rounding-errors.bat
   ```
   O ejecuta directamente el SQL:
   ```cmd
   sqlcmd -S localhost -d GPInventoryDB -i scripts\fix_installment_rounding_error.sql
   ```

### Para Nuevas Inversiones

La corrección en el código ya está implementada. Todas las nuevas inversiones con cuotas se crearán correctamente sin errores de redondeo.

## Verificación

Después de ejecutar la corrección, los KPIs deberían mostrar:

- ✅ **Monto Total**: $160.000.000
- ✅ **Monto Pendiente**: $160.000.000 (0/300 cuotas pagadas)
- ✅ **Diferencia**: $0

## Archivos Modificados

1. ✅ `src/GPInventory.Application/Services/ExpenseService.cs`
   - Método `CreateExpenseAsync`: Lógica de creación de cuotas con ajuste
   - Método `GetExpenseTypeKPIsAsync`: Cálculo de monto pendiente basado en monto original
2. ✅ `scripts/fix_installment_rounding_error.sql` - Script de corrección (opcional)
3. ✅ `fix-rounding-errors.bat` - Script batch de ejecución (opcional)
4. ✅ `ROUNDING_FIX.md` - Esta documentación

## Notas Técnicas

- El ajuste en la última cuota típicamente será muy pequeño (centavos o pesos)
- En el caso de 300 cuotas de $160.000.000:
  - Cuotas 1-299: $533.333 cada una
  - Cuota 300: $533.433 (absorbe los $100 faltantes)
- Esta es una práctica estándar en sistemas financieros para mantener precisión contable
