@echo off
echo ========================================
echo   AGREGANDO TIPO DE FLUJO "VENTA"
echo ========================================
echo.

REM Ejecutar el script SQL para agregar el tipo de flujo "Venta"
echo Ejecutando script SQL para agregar FlowType "Venta"...
sqlcmd -S (localdb)\MSSQLLocalDB -d GPInventoryDB -i add-venta-flowtype.sql

if %errorlevel% equ 0 (
    echo.
    echo ✅ Tipo de flujo "Venta" agregado exitosamente
    echo.
    echo Ahora puedes usar la venta rápida que creará movimientos de stock
    echo con FlowTypeId = 3 (Venta) sin especificar costo.
    echo.
) else (
    echo.
    echo ❌ Error al agregar el tipo de flujo "Venta"
    echo Verifica la conexión a la base de datos.
    echo.
)

pause
