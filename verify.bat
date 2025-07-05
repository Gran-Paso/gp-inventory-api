@echo off
echo ===========================================
echo   GP INVENTORY API - VERIFICATION SCRIPT
echo ===========================================
echo.

echo [1/6] Checking project structure...
if not exist "src\GPInventory.Api\GPInventory.Api.csproj" (
    echo ❌ Error: API project not found!
    goto :error
)
if not exist "src\GPInventory.Domain\GPInventory.Domain.csproj" (
    echo ❌ Error: Domain project not found!
    goto :error
)
if not exist "src\GPInventory.Application\GPInventory.Application.csproj" (
    echo ❌ Error: Application project not found!
    goto :error
)
if not exist "src\GPInventory.Infrastructure\GPInventory.Infrastructure.csproj" (
    echo ❌ Error: Infrastructure project not found!
    goto :error
)
echo ✅ Project structure OK

echo.
echo [2/6] Restoring NuGet packages...
dotnet restore >nul 2>&1
if %errorlevel% neq 0 (
    echo ❌ Error: Failed to restore packages
    goto :error
)
echo ✅ Package restoration OK

echo.
echo [3/6] Building solution...
dotnet build >nul 2>&1
if %errorlevel% neq 0 (
    echo ❌ Error: Build failed
    echo Running build with details...
    dotnet build
    goto :error
)
echo ✅ Build OK

echo.
echo [4/6] Running unit tests...
dotnet test tests\GPInventory.Tests\GPInventory.Tests.csproj --verbosity quiet >nul 2>&1
if %errorlevel% neq 0 (
    echo ❌ Error: Unit tests failed
    echo Running tests with details...
    dotnet test tests\GPInventory.Tests\GPInventory.Tests.csproj --verbosity normal
    goto :error
)
echo ✅ Unit tests OK

echo.
echo [5/6] Running integration tests...
dotnet test tests\GPInventory.IntegrationTests\GPInventory.IntegrationTests.csproj --verbosity quiet >nul 2>&1
if %errorlevel% neq 0 (
    echo ❌ Error: Integration tests failed
    echo Running tests with details...
    dotnet test tests\GPInventory.IntegrationTests\GPInventory.IntegrationTests.csproj --verbosity normal
    goto :error
)
echo ✅ Integration tests OK

echo.
echo [6/6] Checking API startup...
echo Testing API startup (this may take a few seconds)...
cd "src\GPInventory.Api"
timeout 8 dotnet run --urls "http://localhost:5000" --no-build >nul 2>&1
cd ..\..
echo ✅ API startup OK

echo.
echo ===========================================
echo ✅ ALL VERIFICATIONS PASSED!
echo ===========================================
echo.
echo Your GP Inventory API is ready to use!
echo.
echo Available scripts:
echo   • start-api.bat                - Start the API server
echo   • run-tests.bat               - Run all tests
echo   • run-tests-coverage.bat      - Run tests with coverage
echo   • run-unit-tests.bat          - Run only unit tests
echo   • run-integration-tests.bat   - Run only integration tests
echo.
echo API Endpoints:
echo   • http://localhost:5000/swagger - Swagger UI
echo   • POST /api/auth/register      - User registration
echo   • POST /api/auth/login         - User login
echo   • POST /api/auth/validate-token - Token validation
echo   • GET  /api/auth/me            - Get current user
echo.
echo Database connection:
echo   • Update connection string in appsettings.json
echo   • Current: Server=143.198.232.23;Database=gp_inventory;Uid=root;Pwd=...
echo.
goto :end

:error
echo.
echo ===========================================
echo ❌ VERIFICATION FAILED!
echo ===========================================
echo.
echo Please check the error messages above and fix any issues.
echo For help, see TROUBLESHOOTING.md
echo.
exit /b 1

:end
echo Press any key to exit...
pause >nul
