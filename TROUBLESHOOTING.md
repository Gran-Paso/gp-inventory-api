# Solución de Problemas - GP Inventory API

## Problema Resuelto: "No se encuentra información del proyecto"

### Descripción del Error
```
No se encuentra información del proyecto para "C:\Users\pablo\OneDrive\Desktop\Workspace\Gran Paso\gp-inventory-api\src\GPInventory.Domain\GPInventory.Domain.csproj"
```

### Causa
El archivo `GPInventory.Domain.csproj` tenía contenido duplicado y malformado que causaba errores de compilación.

### Solución Aplicada
1. **Corrección del archivo .csproj**: Se eliminó el contenido duplicado y se configuró correctamente con .NET 8.0
2. **Eliminación de archivos innecesarios**: Se eliminaron los archivos `Class1.cs` generados automáticamente
3. **Restauración de paquetes**: Se ejecutó `dotnet restore` para asegurar que todas las dependencias estén correctas

### Comandos Ejecutados
```bash
# Limpiar la solución
dotnet clean

# Restaurar paquetes
dotnet restore

# Compilar la solución
dotnet build

# Ejecutar tests
dotnet test
```

## Otros Problemas Comunes y Soluciones

### 1. Error de Compilación
**Síntoma**: La compilación falla con errores de referencia
**Solución**:
```bash
cd "C:\Users\pablo\OneDrive\Desktop\Workspace\Gran Paso\gp-inventory-api"
dotnet clean
dotnet restore
dotnet build
```

### 2. Error de Base de Datos
**Síntoma**: Error de conexión a MySQL
**Solución**:
- Verificar que MySQL esté ejecutándose
- Actualizar la cadena de conexión en `appsettings.json`
- Verificar credenciales de base de datos

### 3. Error de JWT
**Síntoma**: Tokens JWT no funcionan
**Solución**:
- Verificar que `JwtSettings:SecretKey` tenga al menos 32 caracteres
- Configurar correctamente `Issuer` y `Audience`

### 4. Error de Tests
**Síntoma**: Los tests fallan al ejecutarse
**Solución**:
```bash
# Ejecutar tests unitarios
dotnet test tests\GPInventory.Tests\GPInventory.Tests.csproj

# Ejecutar tests de integración
dotnet test tests\GPInventory.IntegrationTests\GPInventory.IntegrationTests.csproj

# Ejecutar todos los tests
dotnet test
```

### 5. Error de Paquetes NuGet
**Síntoma**: Paquetes no se pueden restaurar
**Solución**:
```bash
# Limpiar cache de NuGet
dotnet nuget locals all --clear

# Restaurar paquetes
dotnet restore
```

## Verificación del Estado del Proyecto

### Comandos para Verificar
```bash
# Verificar que la solución compila
dotnet build

# Verificar que los tests pasan
dotnet test

# Verificar que la API inicia correctamente
dotnet run --project src\GPInventory.Api\GPInventory.Api.csproj
```

### Estructura de Archivos Esperada
```
gp-inventory-api/
├── src/
│   ├── GPInventory.Api/
│   │   ├── GPInventory.Api.csproj ✓
│   │   └── Program.cs ✓
│   ├── GPInventory.Application/
│   │   ├── GPInventory.Application.csproj ✓
│   │   └── (NO Class1.cs) ✓
│   ├── GPInventory.Domain/
│   │   ├── GPInventory.Domain.csproj ✓
│   │   └── (NO Class1.cs) ✓
│   └── GPInventory.Infrastructure/
│       ├── GPInventory.Infrastructure.csproj ✓
│       └── (NO Class1.cs) ✓
├── tests/
│   ├── GPInventory.Tests/
│   └── GPInventory.IntegrationTests/
└── GPInventory.sln ✓
```

## Contacto
Si encuentra otros problemas, puede:
1. Revisar los logs de compilación
2. Ejecutar `dotnet --info` para verificar la versión de .NET
3. Verificar que todas las dependencias estén instaladas correctamente
