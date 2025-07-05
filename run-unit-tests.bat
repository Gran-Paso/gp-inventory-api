@echo off
echo Running Only Unit Tests (Fast)...
echo.

dotnet test tests\GPInventory.Tests\GPInventory.Tests.csproj --verbosity:normal

echo.
echo Unit tests completed!
pause
