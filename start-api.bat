@echo off
echo ================================================
echo Starting GP Inventory API with In-Memory Database
echo ================================================
echo.

cd /d "c:\Users\pablo\OneDrive\Desktop\Workspace\Gran Paso\gp-inventory-api"

echo Setting environment to Development...
set ASPNETCORE_ENVIRONMENT=Development

REM Check if we're in the right directory
if not exist "src\GPInventory.Api\GPInventory.Api.csproj" (
    echo Error: GPInventory.Api.csproj not found!
    echo Make sure you're running this script from the project root directory.
    pause
    exit /b 1
)

echo Restoring packages...
dotnet restore
if %errorlevel% neq 0 (
    echo Error: Failed to restore packages
    pause
    exit /b 1
)

echo Building solution...
dotnet build
if %errorlevel% neq 0 (
    echo Error: Build failed
    pause
    exit /b 1
)

echo.
echo Starting API server...
echo The API will be available at: https://localhost:7001 and http://localhost:5001
echo Swagger UI will be available at: https://localhost:7001/swagger
echo Using In-Memory Database (Development Mode)
echo.
echo Press Ctrl+C to stop the server
echo.

dotnet run --project src/GPInventory.Api/GPInventory.Api.csproj
echo Server will be available at: http://localhost:5000
echo Swagger UI will be available at: http://localhost:5000/swagger
echo.
echo Press Ctrl+C to stop the server
echo.

cd "src\GPInventory.Api"
dotnet run --urls "http://localhost:5000"
