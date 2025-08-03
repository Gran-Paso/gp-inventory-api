@echo off
REM Script de deploy para GP Inventory API (Windows)

echo 🚀 Iniciando deploy de GP Inventory API...

REM Verificar que estamos en la rama main
for /f "tokens=*" %%i in ('git branch --show-current') do set CURRENT_BRANCH=%%i
if not "%CURRENT_BRANCH%"=="main" (
    echo ❌ Error: Debes estar en la rama 'main' para hacer deploy
    echo    Rama actual: %CURRENT_BRANCH%
    exit /b 1
)

REM Verificar que no hay cambios sin commit
git status --porcelain | findstr "." > nul
if %errorlevel% equ 0 (
    echo ❌ Error: Hay cambios sin commit. Por favor, haz commit primero.
    git status --short
    exit /b 1
)

echo ✅ Verificaciones pasaron. Iniciando build...

REM Restaurar dependencias
echo 📦 Restaurando dependencias...
call dotnet restore GPInventory.sln

REM Construir solución
echo 🏗️ Construyendo solución...
call dotnet build GPInventory.sln --configuration Release --no-restore

REM Ejecutar tests unitarios
echo 🧪 Ejecutando tests unitarios...
call dotnet test tests/GPInventory.Tests/GPInventory.Tests.csproj --configuration Release --no-build --verbosity normal

REM Ejecutar tests de integración
echo 🔗 Ejecutando tests de integración...
call dotnet test tests/GPInventory.IntegrationTests/GPInventory.IntegrationTests.csproj --configuration Release --no-build --verbosity normal

echo ✅ Tests completados exitosamente!

REM Construir imagen Docker
echo 🐳 Construyendo imagen Docker...
docker build -t gp-inventory-api:latest .

echo 🎉 Deploy completado!
echo 📋 Próximos pasos:
echo    1. Ejecutar: docker run -d --name gp-api -p 5000:80 gp-inventory-api:latest
echo    2. Verificar en: http://localhost:5000
echo    3. Swagger: http://localhost:5000/swagger
echo    4. Para publicar en Docker Hub: docker tag gp-inventory-api:latest tu-usuario/gp-inventory-api:latest ^&^& docker push tu-usuario/gp-inventory-api:latest

pause
