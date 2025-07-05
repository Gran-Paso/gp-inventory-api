@echo off
echo Building GP Inventory API...
dotnet build

if %errorlevel% neq 0 (
    echo Build failed!
    pause
    exit /b 1
)

echo Build successful!
echo.
echo Starting API on http://localhost:5000
echo Swagger UI available at http://localhost:5000/swagger
echo.
echo Press Ctrl+C to stop the API
echo.

cd "src\GPInventory.Api"
dotnet run --urls "http://localhost:5000"
