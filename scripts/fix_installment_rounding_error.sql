-- Script para corregir errores de redondeo en las cuotas de pagos
-- Este script ajusta la última cuota de cada plan de pago para que el total
-- de las cuotas sea exactamente igual al monto del gasto

-- Mostrar información antes de la corrección
PRINT '=== ANÁLISIS DE CUOTAS CON PROBLEMAS DE REDONDEO ==='
PRINT ''

SELECT 
    e.Id AS ExpenseId,
    e.Description,
    e.Amount AS ExpenseAmount,
    pp.Id AS PaymentPlanId,
    pp.InstallmentsCount,
    SUM(pi.AmountClp) AS TotalInstallments,
    e.Amount - SUM(pi.AmountClp) AS Difference
FROM Expenses e
INNER JOIN PaymentPlans pp ON e.Id = pp.ExpenseId
INNER JOIN PaymentInstallments pi ON pp.Id = pi.PaymentPlanId
GROUP BY e.Id, e.Description, e.Amount, pp.Id, pp.InstallmentsCount
HAVING ABS(e.Amount - SUM(pi.AmountClp)) > 0.01
ORDER BY ABS(e.Amount - SUM(pi.AmountClp)) DESC;

PRINT ''
PRINT '=== INICIANDO CORRECCIÓN ==='
PRINT ''

-- Corregir cada plan de pago con diferencia
DECLARE @ExpenseId INT;
DECLARE @PaymentPlanId INT;
DECLARE @ExpenseAmount DECIMAL(18,2);
DECLARE @TotalInstallments DECIMAL(18,2);
DECLARE @Difference DECIMAL(18,2);
DECLARE @LastInstallmentId INT;
DECLARE @LastInstallmentAmount DECIMAL(18,2);
DECLARE @NewLastInstallmentAmount DECIMAL(18,2);

DECLARE correction_cursor CURSOR FOR
SELECT 
    e.Id,
    pp.Id,
    e.Amount,
    SUM(pi.AmountClp),
    e.Amount - SUM(pi.AmountClp)
FROM Expenses e
INNER JOIN PaymentPlans pp ON e.Id = pp.ExpenseId
INNER JOIN PaymentInstallments pi ON pp.Id = pi.PaymentPlanId
GROUP BY e.Id, pp.Id, e.Amount
HAVING ABS(e.Amount - SUM(pi.AmountClp)) > 0.01;

OPEN correction_cursor;

FETCH NEXT FROM correction_cursor INTO @ExpenseId, @PaymentPlanId, @ExpenseAmount, @TotalInstallments, @Difference;

WHILE @@FETCH_STATUS = 0
BEGIN
    -- Obtener la última cuota del plan de pago
    SELECT TOP 1 
        @LastInstallmentId = Id,
        @LastInstallmentAmount = AmountClp
    FROM PaymentInstallments
    WHERE PaymentPlanId = @PaymentPlanId
    ORDER BY InstallmentNumber DESC;
    
    -- Calcular el nuevo monto de la última cuota
    SET @NewLastInstallmentAmount = @LastInstallmentAmount + @Difference;
    
    -- Actualizar la última cuota
    UPDATE PaymentInstallments
    SET AmountClp = @NewLastInstallmentAmount
    WHERE Id = @LastInstallmentId;
    
    PRINT 'Corregido - Expense ID: ' + CAST(@ExpenseId AS VARCHAR) + 
          ', PaymentPlan ID: ' + CAST(@PaymentPlanId AS VARCHAR) +
          ', Diferencia: ' + CAST(@Difference AS VARCHAR) +
          ', Última cuota actualizada de ' + CAST(@LastInstallmentAmount AS VARCHAR) +
          ' a ' + CAST(@NewLastInstallmentAmount AS VARCHAR);
    
    FETCH NEXT FROM correction_cursor INTO @ExpenseId, @PaymentPlanId, @ExpenseAmount, @TotalInstallments, @Difference;
END;

CLOSE correction_cursor;
DEALLOCATE correction_cursor;

PRINT ''
PRINT '=== CORRECCIÓN COMPLETADA ==='
PRINT ''

-- Verificar que ya no hay diferencias
PRINT '=== VERIFICACIÓN POST-CORRECCIÓN ==='
PRINT ''

SELECT 
    e.Id AS ExpenseId,
    e.Description,
    e.Amount AS ExpenseAmount,
    pp.Id AS PaymentPlanId,
    pp.InstallmentsCount,
    SUM(pi.AmountClp) AS TotalInstallments,
    e.Amount - SUM(pi.AmountClp) AS Difference
FROM Expenses e
INNER JOIN PaymentPlans pp ON e.Id = pp.ExpenseId
INNER JOIN PaymentInstallments pi ON pp.Id = pi.PaymentPlanId
GROUP BY e.Id, e.Description, e.Amount, pp.Id, pp.InstallmentsCount
HAVING ABS(e.Amount - SUM(pi.AmountClp)) > 0.01;

PRINT ''
PRINT 'Si no se muestran registros, la corrección fue exitosa.'
PRINT ''
