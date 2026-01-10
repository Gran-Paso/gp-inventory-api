@echo off
echo ===============================================
echo Correccion de Errores de Redondeo en Cuotas
echo ===============================================
echo.
echo Este script corregira los errores de redondeo en las cuotas
echo de los planes de pago, ajustando la ultima cuota para que
echo el total sea exactamente igual al monto del gasto.
echo.
echo IMPORTANTE: Este script requiere acceso a SQL Server.
echo.
pause
echo.

sqlcmd -S localhost -d GPInventoryDB -i scripts\fix_installment_rounding_error.sql

echo.
echo ===============================================
echo Proceso completado
echo ===============================================
pause
