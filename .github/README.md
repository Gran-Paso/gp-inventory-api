# GitHub Actions Setup for GP Inventory API

## Configuración de Secrets

Los workflows de la API usan los **mismos secrets** que el frontend:

### Secrets requeridos en tu repositorio:

#### `DOCKER_USERNAME`
- **Nombre**: `DOCKER_USERNAME`
- **Valor**: Tu nombre de usuario de Docker Hub

#### `DOCKER_TOKEN`
- **Nombre**: `DOCKER_TOKEN`
- **Valor**: Token de acceso de Docker Hub

## Workflows Creados para la API

### 1. `docker.yml` - Build y Push Simple
- **Trigger**: Push a `main` y PRs a `main`
- **Funcionalidad**: Build y push directo a Docker Hub
- **Imagen**: `tu-usuario/gp-inventory-api`

### 2. `ci-cd.yml` - Pipeline Completo .NET
- **Trigger**: Push a `main`/`develop` y PRs a `main`
- **Jobs**:
  1. **Test Job**: 
     - Setup .NET 8
     - Restore, build, unit tests, integration tests
     - Generación de cobertura de código
  2. **Docker Job**: 
     - Solo se ejecuta en `main` después de tests exitosos
     - Multi-arquitectura y cache optimizado

## Diferencias con el Frontend

| Aspecto | Frontend (React) | API (.NET) |
|---------|------------------|------------|
| **Runtime** | Node.js 18 + nginx | .NET 8 SDK + ASP.NET Runtime |
| **Build** | `npm run build` | `dotnet build` + `dotnet publish` |
| **Tests** | `npm run lint` + `tsc --noEmit` | `dotnet test` (unit + integration) |
| **Imagen final** | nginx:alpine | mcr.microsoft.com/dotnet/aspnet:8.0 |
| **Puerto** | 80 (nginx) | 80 (Kestrel) |
| **Optimizaciones** | Multi-stage con cache npm | Multi-stage con restore optimizado |

## Configuración de Coverage (Opcional)

Para habilitar reportes de cobertura con Codecov:

1. Ve a [codecov.io](https://codecov.io/)
2. Conecta tu repositorio
3. Agrega el token como secret `CODECOV_TOKEN` (opcional para repos públicos)

## Uso conjunto Frontend + API

### Con Docker Compose:
```yaml
version: '3.8'
services:
  api:
    image: tu-usuario/gp-inventory-api:latest
    ports:
      - "5000:80"
    environment:
      - ASPNETCORE_ENVIRONMENT=Production
  
  frontend:
    image: tu-usuario/gp-inventory:latest
    ports:
      - "80:80"
    depends_on:
      - api
```

### Con comandos separados:
```bash
# Ejecutar API
docker run -d --name gp-api -p 5000:80 tu-usuario/gp-inventory-api:latest

# Ejecutar Frontend
docker run -d --name gp-frontend -p 80:80 tu-usuario/gp-inventory:latest
```

## Deploy automático

Una vez configurados los secrets:

1. Haz commit de estos archivos al repositorio de la API
2. Haz push a la rama `main`
3. Ve a **Actions** en GitHub para ver el progreso
4. Las imágenes se publicarán automáticamente como:
   - `tu-usuario/gp-inventory-api:latest`
   - `tu-usuario/gp-inventory-api:main-<sha>`

## Comandos de testing local

```bash
# Verificar build local
dotnet build GPInventory.sln --configuration Release

# Ejecutar tests
dotnet test GPInventory.sln --configuration Release

# Build Docker
docker build -t gp-inventory-api:test .

# Ejecutar contenedor
docker run --rm -p 5000:80 gp-inventory-api:test
```
