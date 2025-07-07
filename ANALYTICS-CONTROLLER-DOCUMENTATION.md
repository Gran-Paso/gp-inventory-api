# Analytics Controller - Dashboard Empresarial Completo

## Descripción General
El `AnalyticsController` proporciona un análisis empresarial completo con métricas avanzadas, KPIs financieros y fórmulas de negocio para la toma de decisiones estratégicas.

## Endpoints Disponibles

### 1. Dashboard Principal de Analytics
**Endpoint:** `GET /api/analytics/dashboard/{businessId}?days=30`

#### Parámetros
- `businessId`: ID del negocio (requerido)
- `days`: Número de días para análisis (opcional, por defecto 30)

#### Respuesta Completa
```json
{
  "businessId": 1,
  "period": {
    "days": 30,
    "startDate": "2024-12-07",
    "endDate": "2025-01-06"
  },
  
  // RESUMEN EJECUTIVO
  "summary": {
    "totalRevenue": 125000.50,
    "totalTransactions": 450,
    "totalItemsSold": 1250,
    "averageOrderValue": 277.78,
    "grossProfit": 45000.25,
    "grossMargin": 36.00,
    "totalProducts": 85,
    "activeProducts": 65
  },

  // MÉTRICAS POR PERÍODO
  "sales": {
    "today": {
      "revenue": 2500.00,
      "transactions": 12,
      "items": 35
    },
    "yesterday": {
      "revenue": 2200.00,
      "transactions": 10,
      "items": 28
    },
    "week": {
      "revenue": 15000.00,
      "transactions": 68,
      "items": 185
    },
    "month": {
      "revenue": 45000.00,
      "transactions": 180,
      "items": 520
    },
    "lastMonth": {
      "revenue": 42000.00,
      "transactions": 165,
      "items": 485
    }
  },

  // ANÁLISIS DE CRECIMIENTO
  "growth": {
    "dailyGrowth": 13.64,           // +13.64% vs ayer
    "monthlyGrowth": 7.14,          // +7.14% vs mes anterior
    "revenueGrowthTrend": "Positive" // Tendencia general
  },

  // TOP 10 PRODUCTOS MÁS RENTABLES
  "topProducts": [
    {
      "productId": 15,
      "name": "Producto Premium A",
      "revenue": 8500.00,
      "unitsSold": 125,
      "margin": 45.50,
      "currentStock": 85
    }
    // ... más productos
  ],

  // PRODUCTOS CON BAJO RENDIMIENTO (Margen < 20%)
  "underperformingProducts": [
    {
      "productId": 23,
      "name": "Producto Bajo Margen",
      "revenue": 1200.00,
      "margin": 8.50,
      "currentStock": 150
    }
    // ... más productos
  ],

  // ANÁLISIS DE INVENTARIO
  "inventory": {
    "totalProducts": 85,
    "totalStock": 2500,
    "totalValue": 185000.00,
    "outOfStockProducts": 5,
    "lowStockProducts": 12,
    "averageStockPerProduct": 29.41
  },

  // KPIs FINANCIEROS AVANZADOS
  "financialKPIs": {
    // Rotación de inventario (veces por año)
    "inventoryTurnover": 2.35,
    
    // Días de inventario pendiente
    "daysInventoryOutstanding": 155.32,
    
    // Retorno sobre inventario (%)
    "returnOnInventory": 24.32,
    
    // Margen bruto (%)
    "grossMarginPercentage": 36.00,
    
    // Costo de productos vendidos
    "costOfGoodsSold": 80000.25,
    
    // Velocidad de ventas (items/día)
    "salesVelocity": 41.67,
    
    // Ingresos promedio por día
    "avgDailyRevenue": 4166.68,
    
    // Tasa de conversión de stock (%)
    "stockConversionRate": 50.00
  },

  // ANÁLISIS TEMPORAL
  "timeAnalysis": {
    "hourlyDistribution": [
      {
        "hour": 9,
        "transactions": 25,
        "revenue": 5500.00
      }
      // ... distribución por horas
    ],
    "weeklyDistribution": [
      {
        "dayOfWeek": "Monday",
        "transactions": 65,
        "revenue": 12000.00
      }
      // ... distribución semanal
    ],
    "peakHour": 14,           // 2:00 PM
    "peakDayOfWeek": "Friday" // Viernes
  },

  // TENDENCIAS SEMANALES (ÚLTIMAS 12 SEMANAS)
  "weeklyTrends": [
    {
      "year": 2024,
      "week": 50,
      "transactions": 85,
      "revenue": 18500.00,
      "items": 245
    }
    // ... más semanas
  ],

  // DATOS PARA GRÁFICOS Y VISUALIZACIONES
  "charts": {
    "dailyRevenue": {
      "type": "line",
      "data": {
        "labels": ["Jul 01", "Jul 02", "Jul 03", ...],
        "datasets": [
          {
            "label": "Ingresos Diarios",
            "data": [2500.00, 2200.00, 2800.00, ...],
            "borderColor": "rgb(75, 192, 192)",
            "backgroundColor": "rgba(75, 192, 192, 0.2)"
          },
          {
            "label": "Transacciones",
            "data": [12, 10, 15, ...],
            "borderColor": "rgb(255, 99, 132)",
            "yAxisID": "y1"
          }
        ]
      }
    },
    
    "topProductsSales": {
      "type": "bar",
      "data": {
        "labels": ["Producto A", "Producto B", ...],
        "datasets": [{
          "label": "Unidades Vendidas",
          "data": [125, 98, 87, ...]
        }]
      }
    },
    
    "marginDistribution": {
      "type": "doughnut",
      "data": {
        "labels": ["Bajo (0-20%)", "Medio (20-40%)", "Alto (40-60%)", "Excelente (60%+)"],
        "datasets": [{
          "data": [15, 35, 25, 10],
          "backgroundColor": ["rgba(255, 99, 132, 0.8)", ...]
        }]
      }
    },
    
    "hourlyDistribution": {
      "type": "radar",
      "data": {
        "labels": ["00:00", "01:00", ..., "23:00"],
        "datasets": [{
          "label": "Ingresos por Hora",
          "data": [150, 75, 50, ..., 1200]
        }]
      }
    },
    
    "weeklyDistribution": {
      "type": "polarArea",
      "data": {
        "labels": ["Lunes", "Martes", ..., "Domingo"],
        "datasets": [{
          "data": [8500, 7200, 9100, ...]
        }]
      }
    },
    
    "monthlyTrend": {
      "type": "line",
      "data": {
        "labels": ["Jan 2025", "Feb 2025", ...],
        "datasets": [{
          "label": "Ingresos Mensuales",
          "data": [45000, 48000, 52000, ...]
        }]
      }
    },
    
    "inventoryVsSales": {
      "type": "scatter",
      "data": {
        "datasets": [{
          "label": "Productos",
          "data": [
            {"x": 50, "y": 25},  // x=stock, y=ventas
            {"x": 100, "y": 85},
            ...
          ]
        }]
      }
    },
    
    "kpiGauges": {
      "grossMarginGauge": {
        "type": "gauge",
        "value": 36.5,
        "min": 0,
        "max": 100,
        "title": "Margen Bruto (%)",
        "color": "green",
        "ranges": [
          {"from": 0, "to": 15, "color": "red"},
          {"from": 15, "to": 30, "color": "orange"},
          {"from": 30, "to": 100, "color": "green"}
        ]
      },
      "inventoryTurnoverGauge": {
        "type": "gauge",
        "value": 2.35,
        "min": 0,
        "max": 12,
        "title": "Rotación de Inventario"
      },
      "roiGauge": {
        "type": "gauge",
        "value": 24.32,
        "min": 0,
        "max": 100,
        "title": "ROI Inventario (%)"
      }
    },
    
    "growthComparison": {
      "type": "bar",
      "data": {
        "labels": ["Hoy", "Ayer", "Este Mes", "Mes Anterior"],
        "datasets": [{
          "label": "Ingresos",
          "data": [2500, 2200, 45000, 42000]
        }]
      }
    }
  }
}
```

### 2. Análisis de Rentabilidad por Producto
**Endpoint:** `GET /api/analytics/profitability/{businessId}?days=30`

#### Respuesta
```json
{
  "businessId": 1,
  "period": {
    "days": 30,
    "startDate": "2024-12-07",
    "endDate": "2025-01-06"
  },
  "totalRevenue": 125000.50,
  "totalCost": 80000.25,
  "totalProfit": 45000.25,
  "averageMargin": 36.00,
  "products": [
    {
      "productId": 15,
      "name": "Producto Premium A",
      "sku": "PRE-001",
      "revenue": 8500.00,
      "cost": 4675.00,
      "profit": 3825.00,
      "margin": 45.00,
      "unitsSold": 125,
      "avgSellingPrice": 68.00
    }
    // ... ordenados por rentabilidad
  ]
}
```

## Fórmulas Empresariales Implementadas

### 1. **Cost of Goods Sold (COGS)**
```
COGS = Σ(Costo Promedio del Producto × Unidades Vendidas)
```
- Calcula el costo real de los productos vendidos
- Usa el costo promedio ponderado de entradas de stock

### 2. **Margen Bruto**
```
Margen Bruto (%) = ((Ingresos - COGS) / Ingresos) × 100
```
- Indica la rentabilidad antes de gastos operativos
- Métrica clave para evaluar eficiencia de precios

### 3. **Rotación de Inventario (Inventory Turnover)**
```
Rotación = COGS / Valor Promedio del Inventario
```
- Mide cuántas veces se vende el inventario en un período
- Valores altos indican eficiencia en gestión de stock

### 4. **Días de Inventario Pendiente (DIO)**
```
DIO = 365 / Rotación de Inventario
```
- Número promedio de días para vender el inventario
- Menor valor = mejor flujo de caja

### 5. **Retorno sobre Inventario (ROI)**
```
ROI (%) = ((Ingresos - COGS) / Valor del Inventario) × 100
```
- Rentabilidad generada por cada peso invertido en inventario
- KPI crítico para evaluar eficiencia de capital

### 6. **Velocidad de Ventas**
```
Velocidad = Total Items Vendidos / Días del Período
```
- Items promedio vendidos por día
- Útil para proyecciones y planificación

### 7. **Tasa de Conversión de Stock**
```
Conversión (%) = (Items Vendidos / Stock Total) × 100
```
- Porcentaje del inventario convertido en ventas
- Indica eficiencia de rotación

### 8. **Crecimiento Periódico**
```
Crecimiento (%) = ((Período Actual - Período Anterior) / Período Anterior) × 100
```
- Aplicado a ventas diarias, semanales y mensuales
- Tendencias positivas/negativas del negocio

## Casos de Uso Empresariales

### 1. **Análisis de Rentabilidad**
- Identificar productos más y menos rentables
- Optimizar mix de productos
- Decidir discontinuación de productos

### 2. **Gestión de Inventario**
- Detectar productos con exceso de stock
- Identificar productos agotados
- Optimizar niveles de inventario

### 3. **Análisis de Tendencias**
- Detectar patrones estacionales
- Identificar horas/días pico de ventas
- Planificar staffing y promociones

### 4. **KPIs Financieros**
- Monitorear salud financiera del negocio
- Comparar performance con objetivos
- Generar reportes para stakeholders

### 5. **Toma de Decisiones Estratégicas**
- Evaluar nuevas líneas de productos
- Optimizar precios basado en márgenes
- Mejorar eficiencia operativa

## Características Técnicas

### ✅ **Optimización de Performance**
- Consultas SQL optimizadas
- Cálculos en memoria para métricas complejas
- Manejo robusto de errores

### ✅ **Precisión de Datos**
- Uso de tipos decimales para cálculos financieros
- Redondeo consistente a 2 decimales
- Validación de datos de entrada

### ✅ **Escalabilidad**
- Parámetros configurables de período
- Estructuras de datos eficientes
- Logging detallado para debugging

### ✅ **Flexibilidad**
- Múltiples períodos de análisis
- Métricas personalizables
- Fácil extensión para nuevos KPIs

## Integración con Frontend

El Analytics Controller está diseñado para alimentar dashboards empresariales completos con:
- Gráficos de tendencias temporales
- Tablas de productos por rendimiento
- KPIs en tiempo real
- Alertas de inventario
- Reportes de rentabilidad

Cada endpoint proporciona datos estructurados listos para visualización en frameworks como Chart.js, D3.js, o bibliotecas de dashboards empresariales.

## Datos para Gráficos y Visualizaciones

El Analytics Controller ahora incluye una sección `charts` con datos estructurados específicamente para crear gráficos interactivos. Cada tipo de gráfico está optimizado para bibliotecas populares como **Chart.js**, **D3.js**, **ApexCharts**, y **Plotly**.

### Estructura de Respuesta con Charts
```json
{
  // ... otras secciones del dashboard ...
  
  "charts": {
    "dailyRevenue": {
      "type": "line",
      "data": {
        "labels": ["Jul 01", "Jul 02", "Jul 03", ...],
        "datasets": [
          {
            "label": "Ingresos Diarios",
            "data": [2500.00, 2200.00, 2800.00, ...],
            "borderColor": "rgb(75, 192, 192)",
            "backgroundColor": "rgba(75, 192, 192, 0.2)"
          },
          {
            "label": "Transacciones",
            "data": [12, 10, 15, ...],
            "borderColor": "rgb(255, 99, 132)",
            "yAxisID": "y1"
          }
        ]
      }
    },
    
    "topProductsSales": {
      "type": "bar",
      "data": {
        "labels": ["Producto A", "Producto B", ...],
        "datasets": [{
          "label": "Unidades Vendidas",
          "data": [125, 98, 87, ...]
        }]
      }
    },
    
    "marginDistribution": {
      "type": "doughnut",
      "data": {
        "labels": ["Bajo (0-20%)", "Medio (20-40%)", "Alto (40-60%)", "Excelente (60%+)"],
        "datasets": [{
          "data": [15, 35, 25, 10],
          "backgroundColor": ["rgba(255, 99, 132, 0.8)", ...]
        }]
      }
    },
    
    "hourlyDistribution": {
      "type": "radar",
      "data": {
        "labels": ["00:00", "01:00", ..., "23:00"],
        "datasets": [{
          "label": "Ingresos por Hora",
          "data": [150, 75, 50, ..., 1200]
        }]
      }
    },
    
    "weeklyDistribution": {
      "type": "polarArea",
      "data": {
        "labels": ["Lunes", "Martes", ..., "Domingo"],
        "datasets": [{
          "data": [8500, 7200, 9100, ...]
        }]
      }
    },
    
    "monthlyTrend": {
      "type": "line",
      "data": {
        "labels": ["Jan 2025", "Feb 2025", ...],
        "datasets": [{
          "label": "Ingresos Mensuales",
          "data": [45000, 48000, 52000, ...]
        }]
      }
    },
    
    "inventoryVsSales": {
      "type": "scatter",
      "data": {
        "datasets": [{
          "label": "Productos",
          "data": [
            {"x": 50, "y": 25},  // x=stock, y=ventas
            {"x": 100, "y": 85},
            ...
          ]
        }]
      }
    },
    
    "kpiGauges": {
      "grossMarginGauge": {
        "type": "gauge",
        "value": 36.5,
        "min": 0,
        "max": 100,
        "title": "Margen Bruto (%)",
        "color": "green",
        "ranges": [
          {"from": 0, "to": 15, "color": "red"},
          {"from": 15, "to": 30, "color": "orange"},
          {"from": 30, "to": 100, "color": "green"}
        ]
      },
      "inventoryTurnoverGauge": {
        "type": "gauge",
        "value": 2.35,
        "min": 0,
        "max": 12,
        "title": "Rotación de Inventario"
      },
      "roiGauge": {
        "type": "gauge",
        "value": 24.32,
        "min": 0,
        "max": 100,
        "title": "ROI Inventario (%)"
      }
    },
    
    "growthComparison": {
      "type": "bar",
      "data": {
        "labels": ["Hoy", "Ayer", "Este Mes", "Mes Anterior"],
        "datasets": [{
          "label": "Ingresos",
          "data": [2500, 2200, 45000, 42000]
        }]
      }
    }
  }
}
```

### Tipos de Gráficos Incluidos

#### 1. **📈 Gráfico de Ingresos Diarios (Line Chart)**
- **Propósito**: Mostrar evolución de ingresos y transacciones día a día
- **Biblioteca recomendada**: Chart.js, ApexCharts
- **Datos**: Últimos 30 días con doble eje Y
- **Casos de uso**: Tendencias diarias, detección de patrones

#### 2. **📊 Top Productos Más Vendidos (Bar Chart)**
- **Propósito**: Ranking de productos por unidades vendidas
- **Biblioteca recomendada**: Chart.js, D3.js
- **Datos**: Top 10 productos con nombres truncados
- **Casos de uso**: Análisis de productos estrella

#### 3. **🍩 Distribución de Márgenes (Doughnut Chart)**
- **Propósito**: Categorización de productos por rango de margen
- **Biblioteca recomendada**: Chart.js, ApexCharts
- **Datos**: 4 categorías con colores semaforizados
- **Casos de uso**: Análisis de rentabilidad por categorías

#### 4. **🎯 Ventas por Hora (Radar Chart)**
- **Propósito**: Patrón de ventas durante el día (24 horas)
- **Biblioteca recomendada**: Chart.js, Plotly
- **Datos**: Ingresos por cada hora del día
- **Casos de uso**: Optimización de horarios, staffing

#### 5. **🌟 Ventas por Día de Semana (Polar Area Chart)**
- **Propósito**: Distribución de ventas semanales
- **Biblioteca recomendada**: Chart.js, D3.js
- **Datos**: 7 días con colores distintivos
- **Casos de uso**: Planificación de promociones semanales

#### 6. **📈 Evolución Mensual (Line Chart)**
- **Propósito**: Tendencia de crecimiento mensual
- **Biblioteca recomendada**: Chart.js, ApexCharts
- **Datos**: Últimos 12 meses
- **Casos de uso**: Análisis de crecimiento a largo plazo

#### 7. **💎 Inventario vs Ventas (Scatter Chart)**
- **Propósito**: Correlación entre stock y performance de ventas
- **Biblioteca recomendada**: Chart.js, Plotly, D3.js
- **Datos**: Productos con coordenadas X=stock, Y=ventas
- **Casos de uso**: Optimización de inventario, detección de excesos

#### 8. **⚡ KPI Gauges (Gauge Charts)**
- **Propósito**: Indicadores clave con rangos semaforizados
- **Biblioteca recomendada**: ApexCharts, Plotly, AmCharts
- **Datos**: 3 KPIs principales con rangos de color
- **Casos de uso**: Dashboard ejecutivo, monitoreo en tiempo real

#### 9. **📊 Comparación de Crecimiento (Bar Chart)**
- **Propósito**: Comparativo de períodos clave
- **Biblioteca recomendada**: Chart.js, ApexCharts
- **Datos**: 4 períodos comparativos
- **Casos de uso**: Análisis de performance inmediata

### Implementación Frontend

#### Chart.js (Recomendado)
```javascript
// Ejemplo: Gráfico de Ingresos Diarios
const chartData = analyticsData.charts.dailyRevenue;
const ctx = document.getElementById('dailyRevenueChart').getContext('2d');

new Chart(ctx, {
  type: chartData.type,
  data: chartData.data,
  options: chartData.options
});
```

#### ApexCharts
```javascript
// Ejemplo: KPI Gauge
const gaugeData = analyticsData.charts.kpiGauges.grossMarginGauge;

const options = {
  series: [gaugeData.value],
  chart: { type: 'radialBar' },
  plotOptions: {
    radialBar: {
      startAngle: -90,
      endAngle: 90,
      dataLabels: {
        name: { value: gaugeData.title },
        value: { formatter: (val) => `${val}%` }
      }
    }
  }
};

const chart = new ApexCharts(document.querySelector("#gauge"), options);
chart.render();
```

#### D3.js (Avanzado)
```javascript
// Ejemplo: Scatter Plot Inventario vs Ventas
const scatterData = analyticsData.charts.inventoryVsSales.data.datasets[0].data;

const svg = d3.select("#scatter-chart")
  .append("svg")
  .attr("width", 500)
  .attr("height", 400);

svg.selectAll("circle")
  .data(scatterData)
  .enter()
  .append("circle")
  .attr("cx", d => xScale(d.x))
  .attr("cy", d => yScale(d.y))
  .attr("r", 5);
```

### Personalización y Extensión

#### Colores Personalizados
Todos los gráficos incluyen esquemas de colores predefinidos que pueden ser personalizados:
- **Verde**: Indicadores positivos, productos de alto rendimiento
- **Naranja**: Indicadores de advertencia, productos de rendimiento medio
- **Rojo**: Indicadores críticos, productos de bajo rendimiento
- **Azul**: Datos informativos, métricas de transacciones

#### Responsividad
Todos los gráficos están configurados con `responsive: true` para adaptarse automáticamente a diferentes tamaños de pantalla.

#### Interactividad
Los datos están estructurados para soportar:
- **Tooltips personalizados**
- **Zoom y pan**
- **Filtros dinámicos**
- **Drill-down por producto**
- **Exportación a PDF/imagen**

### Dashboard Recomendado

#### Layout Sugerido para Dashboard Ejecutivo:
```
┌─────────────────┬─────────────────┬─────────────────┐
│   KPI Gauges    │  Growth Comparison │ Margin Distribution │
│   (3 gauges)    │   (Bar Chart)   │  (Doughnut)     │
├─────────────────┴─────────────────┴─────────────────┤
│              Daily Revenue Trend                    │
│                (Line Chart)                         │
├─────────────────┬─────────────────┬─────────────────┤
│ Top Products    │ Hourly Pattern  │ Weekly Pattern  │
│  (Bar Chart)    │ (Radar Chart)   │ (Polar Area)    │
├─────────────────┴─────────────────┴─────────────────┤
│           Inventory vs Sales Correlation           │
│                (Scatter Plot)                       │
└─────────────────────────────────────────────────────┘
```

Esta estructura proporciona datos listos para crear dashboards profesionales e interactivos que pueden competir con soluciones empresariales como Tableau, Power BI o Looker.
