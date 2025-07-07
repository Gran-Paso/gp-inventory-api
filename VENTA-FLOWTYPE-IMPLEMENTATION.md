# Implementación FlowType "Venta" para Venta Rápida

## Cambios Realizados

### 1. **Nuevo FlowType "Venta"**
- Se agregó un nuevo tipo de flujo llamado "Venta" a la base de datos
- Script SQL: `add-venta-flowtype.sql`
- Script de instalación: `add-venta-flowtype.bat`

### 2. **Modificaciones en SalesController**
- La venta rápida ahora busca automáticamente el FlowType "Venta"
- Los movimientos de stock por venta **NO especifican costo** (`Cost = null`)
- Se mantiene la cantidad negativa para representar salidas de stock

### 3. **Flujo de Venta Mejorado**
```csharp
// Antes (hardcoded)
FlowTypeId = 2, // "Salida"
Cost = product.Cost // Especificaba costo

// Ahora (dinámico)
FlowTypeId = ventaFlowType.Id, // Busca "Venta"
Cost = null // Sin costo en ventas
```

## Beneficios

### ✅ **Separación de Responsabilidades**
- **FlowType "Entrada"**: Para compras/recepciones (con costo)
- **FlowType "Salida"**: Para ajustes manuales
- **FlowType "Venta"**: Específico para ventas (sin costo)

### ✅ **Cálculos Más Precisos**
- `averageCost` solo considera entradas con costo real
- Las ventas no distorsionan los cálculos de costo promedio
- Mejor trazabilidad de movimientos

### ✅ **Flexibilidad**
- El sistema busca automáticamente el FlowType "Venta"
- No hay IDs hardcodeados en el código
- Fácil mantenimiento

## Cómo Usar

### 1. **Instalar el nuevo FlowType**
```bash
# Ejecutar el script batch
add-venta-flowtype.bat
```

### 2. **Verificar la instalación**
```sql
SELECT Id, Name FROM FlowTypes ORDER BY Id;
```

### 3. **Probar la venta rápida**
```bash
# Compilar y ejecutar
test-venta-flowtype.bat
```

### 4. **Request de venta rápida**
```json
POST /api/sales/quick-sale
{
  "businessId": 1,
  "customerName": "Cliente Test",
  "paymentMethodId": 1,
  "items": [
    {
      "productId": 1,
      "quantity": 2,
      "unitPrice": 1500
    }
  ]
}
```

### 5. **Verificar movimientos de stock**
```bash
GET /api/stock?productId=1
```

## Resultado Esperado

Los movimientos de stock generados por ventas tendrán:
- `FlowType.Name = "Venta"`
- `Amount = -cantidad` (negativo para salida)
- `Cost = null` (sin costo)
- `Notes = "Venta rápida #123"`

## Impacto en Cálculos

### **Average Cost (Costo Promedio)**
- ✅ Solo considera entradas con costo válido
- ✅ Ignora movimientos de venta (sin costo)
- ✅ Resultado más preciso

### **Average Price (Precio Promedio)**
- ✅ Basado en ventas reales
- ✅ Ponderado por cantidad vendida
- ✅ No afectado por este cambio

## Troubleshooting

### Error: "Tipo de flujo 'Venta' no encontrado"
1. Ejecutar `add-venta-flowtype.bat`
2. Verificar conexión a base de datos
3. Confirmar que el script SQL se ejecutó correctamente

### Error de compilación
1. Restaurar paquetes: `dotnet restore`
2. Limpiar build: `dotnet clean`
3. Rebuilder: `dotnet build`

## Scripts Creados

- `add-venta-flowtype.sql` - Script SQL para agregar FlowType
- `add-venta-flowtype.bat` - Script de instalación
- `test-venta-flowtype.bat` - Script de prueba completa
