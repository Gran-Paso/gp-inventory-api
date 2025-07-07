# 🔐 ESTRUCTURA MEJORADA DEL JWT TOKEN

## 📋 Problema Anterior

El JWT tenía los datos desordenados en arrays separados:
```json
{
  "nameid": "1",
  "email": "pablojavierprietocepeda@gmail.com",
  "unique_name": "Pablo Prieto",
  "userId": "1",
  "role": ["Operador", "Manager"],
  "roleId": ["1", "2"],
  "businessId": ["1", "2"],
  "businessName": ["Gran Paso", "Empresa de Prueba"],
  "role:1": "Operador",
  "role:2": "Manager"
}
```

**❌ Problemas:**
- No era claro qué rol correspondía a qué negocio
- Arrays separados difíciles de correlacionar
- Acceso complejo a datos relacionados

## ✅ Nueva Estructura Mejorada

### 🎯 **Datos Agrupados por Negocio**
```json
{
  "nameid": "1",
  "email": "pablojavierprietocepeda@gmail.com", 
  "unique_name": "Pablo Prieto",
  "userId": "1",
  
  // NUEVA ESTRUCTURA AGRUPADA
  "business_0_id": "1",
  "business_0_name": "Gran Paso",
  "role_0_id": "2", 
  "role_0_name": "Manager",
  "access_0": "business:1|role:2|name:Manager",
  
  "business_1_id": "2",
  "business_1_name": "Empresa de Prueba", 
  "role_1_id": "1",
  "role_1_name": "Operador",
  "access_1": "business:2|role:1|name:Operador",
  
  // DATOS DE RESUMEN
  "total_businesses": "2",
  "primary_business_id": "1",
  "primary_business_name": "Gran Paso",
  "primary_role_id": "2", 
  "primary_role_name": "Manager",
  
  // COMPATIBILIDAD HACIA ATRÁS (se mantienen)
  "role": ["Operador", "Manager"],
  "roleId": ["1", "2"], 
  "businessId": ["1", "2"],
  "businessName": ["Gran Paso", "Empresa de Prueba"],
  "role:1": "Manager",
  "role:2": "Operador"
}
```

## 🚀 Ventajas de la Nueva Estructura

### 1. **Datos Relacionados Agrupados**
```csharp
// ANTES: Difícil de correlacionar
var businessIds = claims.Where(c => c.Type == "businessId").Select(c => c.Value);
var businessNames = claims.Where(c => c.Type == "businessName").Select(c => c.Value);
// ¿Cuál nombre corresponde a cuál ID?

// AHORA: Fácil acceso agrupado
var businessId = claims.FirstOrDefault(c => c.Type == "business_0_id")?.Value;
var businessName = claims.FirstOrDefault(c => c.Type == "business_0_name")?.Value;
```

### 2. **Métodos Helper Nuevos**
```csharp
// Obtener todos los negocios con sus roles
var businessRoles = _tokenService.GetBusinessRolesFromToken(token);

// Obtener negocio primario
var primaryBusiness = _tokenService.GetPrimaryBusinessFromToken(token);

// Verificar acceso a negocio específico
bool hasAccess = _tokenService.HasAccessToBusiness(token, businessId);

// Obtener rol en negocio específico
var role = _tokenService.GetRoleInBusiness(token, businessId);
```

### 3. **Acceso Simplificado en Controladores**
```csharp
[HttpGet("dashboard/{businessId}")]
[Authorize]
public async Task<ActionResult> GetDashboard(int businessId)
{
    var token = Request.Headers["Authorization"].ToString().Replace("Bearer ", "");
    
    // Verificación simple de acceso
    if (!_tokenService.HasAccessToBusiness(token, businessId))
    {
        return Forbid("No tienes acceso a este negocio");
    }
    
    // Obtener rol específico
    var role = _tokenService.GetRoleInBusiness(token, businessId);
    
    // Lógica del controlador...
}
```

## 📊 Estructura de la Clase BusinessRoleInfo

```csharp
public class BusinessRoleInfo
{
    public int BusinessId { get; set; }
    public string BusinessName { get; set; } = string.Empty;
    public int RoleId { get; set; }
    public string RoleName { get; set; } = string.Empty;
}
```

## 🔧 Implementación Técnica

### **TokenService Mejorado**
```csharp
// Generación agrupada de claims
foreach (var userRole in user.Roles)
{
    var businessIndex = user.Roles.IndexOf(userRole);
    
    // Claims agrupados por índice
    claims.Add(new Claim($"business_{businessIndex}_id", userRole.BusinessId.ToString()));
    claims.Add(new Claim($"business_{businessIndex}_name", userRole.BusinessName));
    claims.Add(new Claim($"role_{businessIndex}_id", userRole.Id.ToString()));
    claims.Add(new Claim($"role_{businessIndex}_name", userRole.Name));
    
    // Acceso combinado
    claims.Add(new Claim($"access_{businessIndex}", 
        $"business:{userRole.BusinessId}|role:{userRole.Id}|name:{userRole.Name}"));
}

// Claims de resumen
claims.Add(new Claim("total_businesses", user.Roles.Count.ToString()));
claims.Add(new Claim("primary_business_id", user.Roles.First().BusinessId.ToString()));
```

## 🧪 Testing

### **Endpoint de Prueba: `/api/tokeninfo/info`**
```bash
curl -X GET "http://localhost:5000/api/tokeninfo/info" \
     -H "Authorization: Bearer YOUR_JWT_TOKEN"
```

**Respuesta:**
```json
{
  "message": "Información del JWT con estructura mejorada",
  "userId": 1,
  "totalBusinesses": 2,
  "primaryBusiness": {
    "businessId": 1,
    "businessName": "Gran Paso",
    "roleId": 2,
    "roleName": "Manager"
  },
  "allBusinessRoles": [
    {
      "businessId": 1,
      "businessName": "Gran Paso", 
      "roleId": 2,
      "roleName": "Manager"
    },
    {
      "businessId": 2,
      "businessName": "Empresa de Prueba",
      "roleId": 1, 
      "roleName": "Operador"
    }
  ]
}
```

## 🔄 Compatibilidad

✅ **Mantiene compatibilidad total** con código existente  
✅ **Agrega nuevas funcionalidades** sin romper lo anterior  
✅ **Métodos helper** para acceso fácil a datos agrupados  
✅ **Validación mejorada** de acceso por negocio  

## 📈 Beneficios Empresariales

🎯 **Mejor UX**: Datos estructurados lógicamente  
⚡ **Performance**: Acceso directo a datos relacionados  
🔒 **Seguridad**: Validación granular por negocio  
🧹 **Código limpio**: Métodos helper especializados  
📊 **Analytics**: Fácil acceso a datos de contexto empresarial  

---

¡La nueva estructura del JWT está optimizada para aplicaciones multi-tenant con roles granulares por negocio! 🚀
