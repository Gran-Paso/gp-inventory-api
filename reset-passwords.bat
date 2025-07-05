@echo off
echo ========================================
echo Resetting User Passwords with BCrypt
echo ========================================

echo.
echo Building project...
dotnet build src/GPInventory.Api --configuration Release

if %errorlevel% neq 0 (
    echo Build failed!
    pause
    exit /b 1
)

echo.
echo Starting API to reset passwords...
start /b dotnet run --project src/GPInventory.Api --urls "http://localhost:5000"

echo Waiting for API to start...
timeout /t 8 /nobreak >nul

echo.
echo Resetting password for pablojavierprietocepeda@gmail.com...
curl -X POST "http://localhost:5000/api/auth/reset-password" ^
     -H "Content-Type: application/json" ^
     -d "{\"email\":\"pablojavierprietocepeda@gmail.com\",\"newPassword\":\"admin123\"}" ^
     -w "\nHTTP Status: %%{http_code}\n"

echo.
echo Testing login with new password...
curl -X POST "http://localhost:5000/api/auth/login" ^
     -H "Content-Type: application/json" ^
     -d "{\"email\":\"pablojavierprietocepeda@gmail.com\",\"password\":\"admin123\"}" ^
     -w "\nHTTP Status: %%{http_code}\n"

echo.
echo Stopping API...
taskkill /f /im dotnet.exe >nul 2>&1

echo.
echo Done! If you see a JWT token above, the password reset worked.
pause
