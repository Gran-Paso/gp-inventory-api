# Swagger con JWT Token - GP Inventory API

## 🎯 Configuración Completada

He configurado Swagger para que permita agregar tokens JWT de manera fácil y visual.

## 🚀 Cómo Usar Swagger con JWT

### Paso 1: Iniciar la API
```bash
cd "c:\Users\pablo\OneDrive\Desktop\Workspace\Gran Paso\gp-inventory-api"
call swagger-with-jwt.bat
```

O manualmente:
```bash
dotnet run --project src/GPInventory.Api --urls "https://localhost:7237;http://localhost:5000"
```

### Paso 2: Abrir Swagger
Navega a: **https://localhost:7237/swagger**

### Paso 3: Obtener Token JWT

1. Busca el endpoint `/api/auth/login` en Swagger
2. Haz clic en **"Try it out"**
3. Usa estas credenciales en el body:
   ```json
   {
     "email": "pablojavierprietocepeda@gmail.com",
     "password": "admin123"
   }
   ```
4. Haz clic en **"Execute"**
5. **Copia el token** de la respuesta (el valor después de `"token":`)

### Paso 4: Autorizar en Swagger

1. Haz clic en el botón **"Authorize"** 🔒 (parte superior derecha)
2. En el campo que aparece, escribe: `Bearer TU_TOKEN_AQUI`
   - Ejemplo: `Bearer eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...`
3. Haz clic en **"Authorize"**
4. Haz clic en **"Close"**

### Paso 5: Probar Endpoints Protegidos

Ahora puedes usar todos los endpoints que requieren autenticación:

- ✅ **GET** `/api/products/types` - Obtener categorías
- ✅ **POST** `/api/products/types` - Crear nueva categoría

## 📋 Endpoints Disponibles

### Sin Autenticación
| Método | Endpoint | Descripción |
|--------|----------|-------------|
| GET | `/api/products/types/public` | Obtener categorías (público) |

### Con Autenticación (requiere Bearer token)
| Método | Endpoint | Descripción |
|--------|----------|-------------|
| GET | `/api/products/types` | Obtener todas las categorías |
| POST | `/api/products/types` | Crear nueva categoría |

## 🎨 Características de Swagger Configurado

- ✅ **Botón Authorize** para agregar tokens JWT
- ✅ **Documentación completa** de cada endpoint
- ✅ **Ejemplos de request/response**
- ✅ **Códigos de estado HTTP** documentados
- ✅ **Modelos de datos** con descripciones
- ✅ **Interfaz amigable** para testing

## 🔧 Configuración Técnica Agregada

```csharp
// En Program.cs
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo 
    { 
        Title = "GP Inventory API", 
        Version = "v1",
        Description = "API para el sistema de inventario Gran Paso"
    });

    // JWT Authentication para Swagger
    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Description = "JWT Authorization header using the Bearer scheme.",
        Name = "Authorization",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.ApiKey,
        Scheme = "Bearer"
    });

    c.AddSecurityRequirement(new OpenApiSecurityRequirement { ... });
});
```

## 🎯 Ejemplo Completo de Uso

1. **Abrir**: https://localhost:7237/swagger
2. **Login**: POST `/api/auth/login` con credenciales
3. **Autorizar**: Botón "Authorize" con `Bearer {token}`
4. **Crear categoría**: POST `/api/products/types` con `{"name": "Nueva Categoria"}`
5. **Listar categorías**: GET `/api/products/types`

## 🚀 Script de Inicio Rápido

```bash
call swagger-with-jwt.bat
```

¡Ahora tienes una interfaz completa para probar la API con JWT de manera visual y fácil! 🎉
