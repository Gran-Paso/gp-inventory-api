@echo off
echo ========================================
echo  GP Inventory API - Swagger con JWT
echo ========================================

echo.
echo Iniciando la API con Swagger configurado...
cd "c:\Users\pablo\OneDrive\Desktop\Workspace\Gran Paso\gp-inventory-api"

echo.
echo Compilando proyecto...
dotnet build src/GPInventory.Api --configuration Release

if %errorlevel% neq 0 (
    echo ERROR: Falló la compilación
    pause
    exit /b 1
)

echo.
echo Iniciando API en modo desarrollo...
start dotnet run --project src/GPInventory.Api --urls "https://localhost:7237;http://localhost:5000"

echo.
echo Esperando que la API inicie...
timeout /t 10 /nobreak >nul

echo.
echo ========================================
echo  INSTRUCCIONES PARA USAR SWAGGER
echo ========================================
echo.
echo 1. Abre tu navegador en: https://localhost:7237/swagger
echo.
echo 2. Para autenticarte:
echo    a) Busca el endpoint /api/auth/login
echo    b) Haz clic en "Try it out"
echo    c) Usa estas credenciales:
echo       {
echo         "email": "pablojavierprietocepeda@gmail.com",
echo         "password": "admin123"
echo       }
echo    d) Ejecuta y copia el token de la respuesta
echo.
echo 3. Para autorizar todas las peticiones:
echo    a) Haz clic en el botón "Authorize" (candado) en la parte superior
echo    b) Ingresa: Bearer [TU_TOKEN_AQUI]
echo    c) Haz clic en "Authorize"
echo.
echo 4. Ahora puedes probar los endpoints protegidos como:
echo    - GET /api/products/types
echo    - POST /api/products/types
echo.
echo ========================================
echo  ENDPOINTS DISPONIBLES
echo ========================================
echo.
echo SIN AUTENTICACIÓN:
echo - GET /api/products/types/public
echo.
echo CON AUTENTICACIÓN (requiere Bearer token):
echo - GET /api/products/types
echo - POST /api/products/types
echo.
echo ========================================

echo.
echo Abriendo Swagger en el navegador...
start https://localhost:7237/swagger

echo.
echo La API está corriendo. Presiona cualquier tecla para detenerla...
pause >nul

echo.
echo Deteniendo la API...
taskkill /f /im dotnet.exe 2>nul
echo.
echo ¡Listo!
