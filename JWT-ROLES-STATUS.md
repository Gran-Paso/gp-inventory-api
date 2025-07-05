# JWT con Roles de Usuario - Estado Actual

## ✅ IMPLEMENTACIÓN COMPLETADA

### 1. **UserDto con Roles**
- **UserDto**: Incluye propiedad `Roles` (List<UserRoleDto>)
- **UserRoleDto**: Contiene:
  - `Id`: ID del rol
  - `Name`: Nombre del rol
  - `BusinessId`: ID del negocio
  - `BusinessName`: Nombre del negocio

### 2. **AutoMapper Profile**
- **AuthMappingProfile**: Mapea `User.UserBusinesses` a `UserDto.Roles`
- **Mapeo automático**: Incluye rol y información del negocio

### 3. **UserRepository**
- **GetByEmailWithRolesAsync()**: Obtiene usuario con roles y negocios
- **Include**: UserBusinesses, Role, Business

### 4. **AuthService**
- **LoginAsync()**: Usa `GetByEmailWithRolesAsync()`
- **Mapeo automático**: User → UserDto (con roles)

### 5. **TokenService**
- **GenerateToken()**: Incluye claims de roles:
  - `ClaimTypes.Role`: Nombre del rol
  - `"roleId"`: ID del rol
  - `"businessId"`: ID del negocio
  - `"businessName"`: Nombre del negocio
  - `"role:{businessId}"`: Rol específico por negocio

## 📋 RESPUESTA DE LOGIN ESPERADA

```json
{
  "token": "eyJ0eXAiOiJKV1QiLCJhbGciOiJIUzI1NiJ9...",
  "user": {
    "id": 1,
    "email": "pablojavierprietocepeda@gmail.com",
    "name": "Pablo",
    "lastName": "Prieto",
    "active": true,
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

## 🔑 CLAIMS EN EL JWT TOKEN

Al decodificar el JWT en [jwt.io](https://jwt.io), verás:

```json
{
  "sub": "1",
  "email": "pablojavierprietocepeda@gmail.com",
  "name": "Pablo Prieto",
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

## 🧪 SCRIPTS DE PRUEBA

- **`test-complete-jwt-roles.bat`**: Prueba completa del login con roles
- **`test-password-diagnosis.bat`**: Diagnóstico de password y login
- **`emergency-password-fix.bat`**: Corrección de password si es necesario

## 🚀 CÓMO PROBAR

1. **Ejecutar**: `test-complete-jwt-roles.bat`
2. **Copiar**: El token JWT del resultado
3. **Decodificar**: En https://jwt.io
4. **Verificar**: Claims de roles y business

## 🎯 PRÓXIMO PASO

Ejecutar `test-complete-jwt-roles.bat` para verificar que todo funciona correctamente.
