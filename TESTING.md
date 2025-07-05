# Test Coverage Configuration

## Para ejecutar tests con cobertura de código:

### 1. Instalar herramientas necesarias:
```bash
dotnet tool install --global dotnet-reportgenerator-globaltool
```

### 2. Ejecutar todos los tests con cobertura:
```bash
run-tests-coverage.bat
```

### 3. Ejecutar solo tests unitarios:
```bash
run-unit-tests.bat
```

### 4. Ejecutar solo tests de integración:
```bash
run-integration-tests.bat
```

### 5. Ejecutar tests con cobertura desde línea de comandos:
```bash
dotnet test --collect:"XPlat Code Coverage" --results-directory:"TestResults"
```

### 6. Generar reporte HTML:
```bash
reportgenerator "-reports:TestResults/**/coverage.cobertura.xml" "-targetdir:TestResults/CoverageReport" "-reporttypes:Html"
```

## Estructura de Tests:

### Unit Tests (`GPInventory.Tests`)
- **Domain Tests**: Entidades y lógica de negocio
- **Application Tests**: Servicios y DTOs
- **Infrastructure Tests**: Repositories y servicios externos
- **Api Tests**: Controllers y endpoints

### Integration Tests (`GPInventory.IntegrationTests`)
- **Auth Controller Integration Tests**: Pruebas end-to-end del flujo completo de autenticación

## Métricas de Cobertura Objetivo:

- **Line Coverage**: > 80%
- **Branch Coverage**: > 75%
- **Method Coverage**: > 85%

## Herramientas Utilizadas:

- **xUnit**: Framework de testing
- **FluentAssertions**: Assertions más legibles
- **Moq**: Mocking framework
- **Coverlet**: Colección de métricas de cobertura
- **ReportGenerator**: Generación de reportes HTML
- **Microsoft.AspNetCore.Mvc.Testing**: Tests de integración
- **EntityFramework InMemory**: Base de datos en memoria para tests

## Comandos Útiles:

### Ejecutar tests específicos:
```bash
dotnet test --filter "FullyQualifiedName~AuthServiceTests"
```

### Ejecutar tests con filtro por categoría:
```bash
dotnet test --filter "Category=Unit"
```

### Ejecutar tests con output detallado:
```bash
dotnet test --verbosity detailed
```

### Generar reporte de cobertura en formato XML:
```bash
dotnet test --collect:"XPlat Code Coverage" --results-directory:"TestResults" --logger:"trx"
```
