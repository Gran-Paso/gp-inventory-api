@echo off
echo Running Unit Tests with Code Coverage...
echo.

REM Clean previous test results
if exist "TestResults" rmdir /s /q "TestResults"

REM Run tests with coverage
dotnet test --collect:"XPlat Code Coverage" --results-directory:"TestResults" --logger:"trx"

REM Generate coverage report
dotnet tool install --global dotnet-reportgenerator-globaltool 2>nul

REM Find the coverage file
for /r "TestResults" %%f in (coverage.cobertura.xml) do (
    echo Generating HTML coverage report...
    reportgenerator "-reports:%%f" "-targetdir:TestResults\CoverageReport" "-reporttypes:Html"
    echo.
    echo Coverage report generated in: TestResults\CoverageReport\index.html
    echo Opening coverage report...
    start "" "TestResults\CoverageReport\index.html"
    goto :found
)

echo Coverage file not found. Please check the test execution.
:found

echo.
echo Tests completed!
pause
