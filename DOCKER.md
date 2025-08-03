# Docker Configuration for GP Inventory API

## Archivos creados

### 1. `Dockerfile`
- **Basado en**: .NET 8 SDK para build y ASP.NET Core Runtime para producción
- **Multi-stage build**: Optimizado para tamaño mínimo en producción
- **Características**:
  - Restauración de dependencias optimizada
  - Compilación en modo Release
  - Usuario no-root para seguridad
  - Soporte para localización es-ES
  - Configuración de variables de entorno

### 2. `.dockerignore`
- Excluye archivos de build (`bin/`, `obj/`)
- Excluye archivos de IDE y temporales
- Optimiza el contexto de build

### 3. GitHub Actions Workflows

#### `docker.yml` - Build simple
- Se ejecuta en push a `main` y pull requests
- Build y push directo a Docker Hub

#### `ci-cd.yml` - Pipeline completo
- **Test Job**: 
  - Restore, build y tests unitarios/integración
  - Generación de reportes de cobertura
- **Docker Job**: 
  - Solo se ejecuta en `main` después de tests exitosos
  - Multi-arquitectura (AMD64, ARM64)

## Configuración requerida

### Secrets de GitHub (mismos que para el frontend):
- `DOCKER_USERNAME`: Usuario de Docker Hub
- `DOCKER_TOKEN`: Access token de Docker Hub

## Comandos de uso

### Build local
```bash
# Desde la carpeta gp-inventory-api
docker build -t gp-inventory-api:latest .
```

### Ejecutar localmente
```bash
# Ejecutar la API en puerto 5000
docker run -d --name gp-api -p 5000:80 gp-inventory-api:latest

# Con variables de entorno personalizadas
docker run -d --name gp-api -p 5000:80 \
  -e ASPNETCORE_ENVIRONMENT=Development \
  -e ConnectionStrings__DefaultConnection="tu-connection-string" \
  gp-inventory-api:latest
```

### Desde Docker Hub (después del deploy)
```bash
# Descargar y ejecutar desde Docker Hub
docker run -d --name gp-api -p 5000:80 tu-usuario/gp-inventory-api:latest

# Acceder a la API
# http://localhost:5000
# Swagger: http://localhost:5000/swagger
```

## Configuración de producción

### Variables de entorno importantes:
```bash
ASPNETCORE_ENVIRONMENT=Production
ASPNETCORE_URLS=http://+:80
ConnectionStrings__DefaultConnection=tu-connection-string
JwtSettings__SecretKey=tu-secret-key
JwtSettings__Issuer=tu-issuer
JwtSettings__Audience=tu-audience
```

### Docker Compose ejemplo:
```yaml
version: '3.8'
services:
  api:
    image: tu-usuario/gp-inventory-api:latest
    ports:
      - "5000:80"
    environment:
      - ASPNETCORE_ENVIRONMENT=Production
      - ConnectionStrings__DefaultConnection=Server=db;Database=GPInventory;User=sa;Password=YourPassword;TrustServerCertificate=true;
    depends_on:
      - db
  
  frontend:
    image: tu-usuario/gp-inventory:latest
    ports:
      - "80:80"
    depends_on:
      - api
    environment:
      # Frontend se servirá en inventory.granpasochile.cl
      - VIRTUAL_HOST=inventory.granpasochile.cl
  
  db:
    image: mcr.microsoft.com/mssql/server:2022-latest
    environment:
      - ACCEPT_EULA=Y
      - SA_PASSWORD=YourStrongPassword
    volumes:
      - sqldata:/var/opt/mssql

volumes:
  sqldata:
```

## Estructura de tags en Docker Hub

Una vez configurado, las imágenes se publicarán como:
- `tu-usuario/gp-inventory-api:latest` (última versión estable)
- `tu-usuario/gp-inventory-api:main-<sha>` (por commit)
- `tu-usuario/gp-inventory-api:YYYYMMDD-HHmmss` (por timestamp)

## Notas de seguridad

- La imagen usa usuario no-root (`appuser`)
- Solo se expone el puerto 80
- No incluye archivos de configuración sensibles
- Variables de entorno para configuración en runtime

## Testing del Dockerfile

```bash
# Verificar que compila correctamente
dotnet build GPInventory.sln --configuration Release

# Probar el Dockerfile localmente
docker build -t gp-inventory-api:test .
docker run --rm -p 5000:80 gp-inventory-api:test
```
