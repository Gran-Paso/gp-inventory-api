# JWT con Roles de Usuario - Implementación Completa

## Resumen de la Implementación

La funcionalidad para incluir roles de usuario (con ID y nombre) en el JWT token **ya está completamente implementada** en el proyecto.

## Componentes Implementados

### 1. DTOs Actualizados (AuthDtos.cs)
- **UserDto**: Incluye lista de roles del usuario
- **UserRoleDto**: Contiene ID, nombre, businessId, y businessName del rol

### 2. TokenService (TokenService.cs)
El servicio de tokens genera claims con información completa de roles:
- `ClaimTypes.Role`: Nombre del rol
- `"roleId"`: ID del rol
- `"businessId"`: ID del negocio
- `"businessName"`: Nombre del negocio
- `"role:{businessId}"`: Rol específico para cada negocio

### 3. UserRepository (UserRepository.cs)
- Método `GetByEmailWithRolesAsync()`: Obtiene usuario con roles y negocios asociados
- Incluye entidades relacionadas: `UserBusinesses`, `Role`, `Business`

### 4. AuthService (AuthService.cs)
- Usa `GetByEmailWithRolesAsync()` para login
- Mapea automáticamente roles usando AutoMapper

### 5. AutoMapper Profile (AuthMappingProfile.cs)
- Mapea `User.UserBusinesses` a `UserDto.Roles`
- Convierte entidades de dominio a DTOs correctamente

## Estructura del JWT Token

El token JWT incluye los siguientes claims por cada rol del usuario:

```json
{
  "sub": "1",
  "email": "user@example.com",
  "name": "Usuario Ejemplo",
  "userId": "1",
  "role": "Administrator",
  "roleId": "1",
  "businessId": "1",
  "businessName": "Mi Negocio",
  "role:1": "Administrator",
  "exp": 1735228800,
  "iss": "GPInventory",
  "aud": "GPInventory"
}
```

## Cómo Usar

### 1. Login
```http
POST /api/auth/login
Content-Type: application/json

{
  "email": "user@example.com",
  "password": "password123"
}
```

### 2. Respuesta
```json
{
  "token": "eyJ0eXAiOiJKV1QiLCJhbGciOiJIUzI1NiJ9...",
  "user": {
    "id": 1,
    "email": "user@example.com",
    "name": "Usuario",
    "lastName": "Ejemplo",
    "roles": [
      {
        "id": 1,
        "name": "Administrator",
        "businessId": 1,
        "businessName": "Mi Negocio"
      }
    ]
  }
}
```

### 3. Decodificar Token
Puedes usar [jwt.io](https://jwt.io) para decodificar el token y ver todos los claims incluyendo los roles.

## Scripts de Prueba

- `test-jwt-token-content.bat`: Prueba completa con API real
- `test-jwt-roles-simple.bat`: Prueba simple con endpoint de test
- `quick-verify.bat`: Verificación rápida del proyecto

## Verificación

Para verificar que funciona correctamente:

1. Ejecutar `test-jwt-roles-simple.bat`
2. O iniciar la API y hacer login con credenciales válidas
3. Copiar el token JWT y pegarlo en jwt.io
4. Verificar que aparecen los claims: `role`, `roleId`, `businessId`, `businessName`

## Estado

✅ **COMPLETAMENTE IMPLEMENTADO Y FUNCIONAL**

Todos los componentes están en su lugar y la funcionalidad está lista para usar.
