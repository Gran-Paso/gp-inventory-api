-- ============================================================================
-- Script para vaciar datos transaccionales de gp_inventory
-- Mantiene: Usuarios, Datos estáticos (roles, categorías, tipos, etc.)
-- Elimina: Negocios y todos sus datos relacionados (ventas, stock, gastos, etc.)
-- ============================================================================

SET FOREIGN_KEY_CHECKS = 0;

-- ============================================================================
-- NIVEL 1: Tablas más dependientes (sin dependencias externas)
-- ============================================================================

-- Notificaciones de usuarios
TRUNCATE TABLE user_notifications;

-- Logs de productos
TRUNCATE TABLE product_log;

-- Detalles de ventas
TRUNCATE TABLE sales_detail;

-- Componentes de procesos
TRUNCATE TABLE process_components;

-- Suministros de procesos
TRUNCATE TABLE process_supplies;

-- Suministros de componentes
TRUNCATE TABLE component_supplies;

-- Producción de componentes
TRUNCATE TABLE component_production;

-- Cuotas de pago
TRUNCATE TABLE payment_installment;

-- Distribución mensual de presupuestos
TRUNCATE TABLE budget_monthly_distribution;

-- Asignaciones de presupuesto
TRUNCATE TABLE budget_allocations;

-- ============================================================================
-- NIVEL 2: Tablas con dependencias de nivel 1
-- ============================================================================

-- Ventas
TRUNCATE TABLE sales;

-- Stock de productos
TRUNCATE TABLE stock;

-- Entradas de suministros
TRUNCATE TABLE supply_entry;

-- Procesos completados
TRUNCATE TABLE process_done;

-- Gastos fijos
TRUNCATE TABLE fixed_expense;

-- Documentos de cuotas
TRUNCATE TABLE installment_document;

-- Planes de pago
TRUNCATE TABLE payment_plan;

-- Gastos
TRUNCATE TABLE expenses;

-- Presupuestos
TRUNCATE TABLE budgets;

-- Notificaciones generales
TRUNCATE TABLE notifications;

-- ============================================================================
-- NIVEL 3: Tablas con dependencias de nivel 2
-- ============================================================================

-- Productos
TRUNCATE TABLE product;

-- Suministros
TRUNCATE TABLE supplies;

-- Componentes
TRUNCATE TABLE components;

-- Procesos
TRUNCATE TABLE processes;

-- Manufactura
TRUNCATE TABLE manufacture;

-- Prospectos
TRUNCATE TABLE prospect;

-- Proveedores
TRUNCATE TABLE provider;

-- ============================================================================
-- NIVEL 4: Tablas de relación usuario-negocio y tiendas
-- ============================================================================

-- Relación usuario-negocio (IMPORTANTE: eliminar pero mantener usuarios)
TRUNCATE TABLE user_has_business;

-- Tiendas
TRUNCATE TABLE store;

-- ============================================================================
-- NIVEL 5: Negocios (tabla raíz de casi todo)
-- ============================================================================

-- Negocios (esto eliminará la referencia principal)
TRUNCATE TABLE business;

SET FOREIGN_KEY_CHECKS = 1;

-- ============================================================================
-- VERIFICACIÓN: Contar registros en tablas principales
-- ============================================================================

SELECT 'Verificación de limpieza:' AS status;

SELECT 'business' AS tabla, COUNT(*) AS registros FROM business
UNION ALL
SELECT 'user' AS tabla, COUNT(*) AS registros FROM user
UNION ALL
SELECT 'store' AS tabla, COUNT(*) AS registros FROM store
UNION ALL
SELECT 'product' AS tabla, COUNT(*) AS registros FROM product
UNION ALL
SELECT 'sales' AS tabla, COUNT(*) AS registros FROM sales
UNION ALL
SELECT 'expenses' AS tabla, COUNT(*) AS registros FROM expenses
UNION ALL
SELECT 'stock' AS tabla, COUNT(*) AS registros FROM stock
UNION ALL
SELECT 'components' AS tabla, COUNT(*) AS registros FROM components
UNION ALL
SELECT 'supplies' AS tabla, COUNT(*) AS registros FROM supplies
UNION ALL
SELECT 'processes' AS tabla, COUNT(*) AS registros FROM processes
UNION ALL
SELECT 'user_has_business' AS tabla, COUNT(*) AS registros FROM user_has_business
UNION ALL
SELECT 'role' AS tabla, COUNT(*) AS registros FROM role
UNION ALL
SELECT 'expense_category' AS tabla, COUNT(*) AS registros FROM expense_category
UNION ALL
SELECT 'expense_subcategory' AS tabla, COUNT(*) AS registros FROM expense_subcategory;

-- ============================================================================
-- TABLAS QUE NO SE TOCAN (datos estáticos):
-- ============================================================================
-- ✓ user (usuarios se mantienen)
-- ✓ role (roles del sistema)
-- ✓ payment_methods (métodos de pago)
-- ✓ payment_types (tipos de pago)
-- ✓ expense_types (tipos de gastos)
-- ✓ expense_category (categorías de gastos)
-- ✓ expense_subcategory (subcategorías de gastos)
-- ✓ flow_type (tipos de flujo)
-- ✓ receipt_types (tipos de recibos)
-- ✓ recurrence_type (tipos de recurrencia)
-- ✓ time_units (unidades de tiempo)
-- ✓ unit_measures (unidades de medida)
-- ✓ supply_categories (categorías de suministros)
-- ✓ bank_entities (entidades bancarias)
-- ✓ product_type (tipos de productos)
-- ============================================================================
