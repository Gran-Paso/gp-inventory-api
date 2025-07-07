# Métricas de Ventas en Endpoint de Inventario

## Objetivo
Agregar información de ventas diarias y mensuales al endpoint de inventario para proporcionar métricas completas del dashboard.

## Implementación

### Endpoint Modificado
`GET /api/stock/inventory/{businessId}`

### Nuevas Métricas por Producto
Cada producto en el inventario ahora incluye:

```json
{
  "id": 1,
  "name": "Producto Ejemplo",
  "currentStock": 50,
  "averagePrice": 25.50,
  "averageCost": 20.00,
  // ... otros campos existentes ...
  
  // NUEVAS MÉTRICAS CON PORCENTAJES DE CAMBIO
  "todaySales": {
    "amount": 150.00,        // Monto total vendido hoy
    "quantity": 6,           // Cantidad total vendida hoy
    "changePercent": 25.5    // % cambio vs mismo día mes anterior
  },
  "monthSales": {
    "amount": 1250.00,       // Monto total vendido este mes
    "quantity": 50,          // Cantidad total vendida este mes
    "changePercent": -10.2   // % cambio vs mes anterior
  }
}
```

### Resumen del Negocio
La respuesta ahora incluye un resumen general con porcentajes de cambio:

```json
{
  "businessId": 1,
  "summary": {
    "totalProducts": 25,
    "totalStock": 500,
    "todaySales": {
      "amount": 850.00,
      "transactions": 15,
      "changePercent": 18.5    // % cambio vs mismo día mes anterior
    },
    "monthSales": {
      "amount": 12500.00,
      "transactions": 120,
      "changePercent": -5.2    // % cambio vs mes anterior
    }
  },
  "products": [...]
}
```

## Beneficios

1. **Dashboard Completo**: Una sola llamada proporciona toda la información necesaria
2. **Performance Optimizada**: Se evitan múltiples consultas separadas
3. **Métricas en Tiempo Real**: Ventas del día actual y acumulado mensual
4. **Contexto Comercial**: Información de transacciones además de montos
5. **Análisis de Tendencias**: Porcentajes de cambio para evaluar crecimiento
6. **Comparación Temporal**: Datos históricos para toma de decisiones

## Cálculo de Porcentajes de Cambio

### Ventas del Día
- **Comparación**: Día actual vs mismo día del mes anterior
- **Fórmula**: `((ventas_hoy - ventas_mismo_dia_mes_anterior) / ventas_mismo_dia_mes_anterior) * 100`
- **Valores**:
  - `null`: Si no hay datos del mismo día mes anterior
  - Positivo: Crecimiento
  - Negativo: Decrecimiento

### Ventas del Mes
- **Comparación**: Mes actual vs mes anterior completo
- **Fórmula**: `((ventas_mes_actual - ventas_mes_anterior) / ventas_mes_anterior) * 100`
- **Valores**:
  - `null`: Si no hay datos del mes anterior
  - Positivo: Crecimiento
  - Negativo: Decrecimiento

## Uso en Frontend

```javascript
// Obtener inventario con métricas
const response = await fetch('/api/stock/inventory/1', {
  headers: { 'Authorization': `Bearer ${token}` }
});

const data = await response.json();

// Usar resumen del negocio
console.log(`Ventas de hoy: $${data.summary.todaySales.amount}`);
console.log(`Ventas del mes: $${data.summary.monthSales.amount}`);

// Usar métricas por producto con análisis de tendencias
data.products.forEach(product => {
  const todayTrend = product.todaySales.changePercent;
  const monthTrend = product.monthSales.changePercent;
  
  console.log(`${product.name}: 
    Stock: ${product.currentStock}
    Vendido hoy: ${product.todaySales.quantity} unidades
    Ingresos hoy: $${product.todaySales.amount}
    Tendencia diaria: ${todayTrend ? (todayTrend > 0 ? '+' : '') + todayTrend.toFixed(1) + '%' : 'N/A'}
    
    Ventas del mes: $${product.monthSales.amount}
    Tendencia mensual: ${monthTrend ? (monthTrend > 0 ? '+' : '') + monthTrend.toFixed(1) + '%' : 'N/A'}`);
});

// Usar resumen del negocio con tendencias
const summary = data.summary;
console.log(`Resumen del Negocio:
  Ventas de hoy: $${summary.todaySales.amount} (${summary.todaySales.changePercent?.toFixed(1) || 'N/A'}%)
  Ventas del mes: $${summary.monthSales.amount} (${summary.monthSales.changePercent?.toFixed(1) || 'N/A'}%)`);
```

## Notas Técnicas

- Las fechas se calculan en la zona horaria del servidor
- Los errores de parsing de cantidades se registran pero no interrumpen la operación
- Se mantiene compatibilidad con la estructura anterior
- Las métricas incluyen manejo de errores robusto

## Scripts de Prueba

- `test-inventory-with-sales-metrics.bat`: Prueba completa del endpoint
- `test-dashboard-performance.bat`: Prueba de performance (existente)
