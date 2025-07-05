@echo off
echo Running Only Integration Tests...
echo.

dotnet test tests\GPInventory.IntegrationTests\GPInventory.IntegrationTests.csproj --verbosity:normal

echo.
echo Integration tests completed!
pause
