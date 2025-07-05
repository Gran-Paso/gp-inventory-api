@echo off
echo Running Tests with Detailed Code Coverage...
echo.

REM Clean previous test results
if exist "TestResults" rmdir /s /q "TestResults"

REM Run tests with coverage collection
dotnet test --collect:"XPlat Code Coverage" --results-directory:"TestResults" --logger:"trx" --verbosity:normal

REM Install reportgenerator if not already installed
dotnet tool install --global dotnet-reportgenerator-globaltool 2>nul

REM Generate coverage report
echo.
echo Generating coverage reports...

REM Find all coverage files
for /r "TestResults" %%f in (coverage.cobertura.xml) do (
    echo Found coverage file: %%f
    echo Generating HTML report...
    reportgenerator "-reports:%%f" "-targetdir:TestResults\CoverageReport" "-reporttypes:Html;TextSummary;Cobertura"
    
    echo.
    echo === COVERAGE SUMMARY ===
    if exist "TestResults\CoverageReport\Summary.txt" (
        type "TestResults\CoverageReport\Summary.txt"
    )
    echo ========================
    echo.
    
    echo HTML Coverage report: TestResults\CoverageReport\index.html
    echo Cobertura report: TestResults\CoverageReport\Cobertura.xml
    echo.
    
    echo Opening coverage report...
    start "" "TestResults\CoverageReport\index.html"
    goto :found
)

echo Coverage file not found. Please check the test execution.
:found

echo.
echo ==========================================
echo Tests and coverage analysis completed!
echo ==========================================
pause
