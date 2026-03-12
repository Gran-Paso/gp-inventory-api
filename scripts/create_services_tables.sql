-- Active: 1769994214503@@143.198.232.23@3306@gp_inventory
-- ============================================================
-- GP Services - Tablas de base de datos
-- Fecha: 2026-03-06
-- Una venta (service_sale) puede contener N servicios (service_sale_item).
-- Al completarse, genera expenses por los costos de cada servicio vendido.
-- ============================================================
use `gp-erp`;
describe store;
-- Categorías de servicios (por negocio)
CREATE TABLE service_category (
    id INT AUTO_INCREMENT PRIMARY KEY,
    name VARCHAR(100) NOT NULL,
    description TEXT,
    business_id INT NOT NULL,
    active TINYINT(1) DEFAULT 1,
    created_at DATETIME DEFAULT CURRENT_TIMESTAMP,
    FOREIGN KEY (business_id) REFERENCES business(id)
);

-- Servicios
CREATE TABLE service (
    id INT AUTO_INCREMENT PRIMARY KEY,
    name VARCHAR(200) NOT NULL,
    description TEXT,
    category_id INT,
    business_id INT NOT NULL,
    store_id INT,
    base_price DECIMAL(12,2) NOT NULL DEFAULT 0,
    duration_minutes INT,          -- duración estimada en minutos
    active TINYINT(1) DEFAULT 1,
    created_at DATETIME DEFAULT CURRENT_TIMESTAMP,
    updated_at DATETIME DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
    FOREIGN KEY (category_id) REFERENCES service_category(id),
    FOREIGN KEY (business_id) REFERENCES business(id),
    FOREIGN KEY (store_id) REFERENCES store(id)
);

-- Desglose de costos de un servicio (línea de costos)
-- Cada ítem puede ser un material, mano de obra, proveedor externo, etc.
CREATE TABLE service_cost_item (
    id INT AUTO_INCREMENT PRIMARY KEY,
    service_id INT NOT NULL,
    name VARCHAR(200) NOT NULL,
    description TEXT,
    cost_type ENUM('material', 'labor', 'external', 'overhead', 'other') DEFAULT 'other',
    amount DECIMAL(12,2) NOT NULL DEFAULT 0,  -- costo unitario
    quantity DECIMAL(10,3) DEFAULT 1,
    unit VARCHAR(50),                          -- 'hrs', 'unidades', 'kg', etc.
    is_externalized TINYINT(1) DEFAULT 0,      -- es proveedor externo?
    provider_id   INT NULL,                    -- FK a provider.id (proveedor registrado)
    provider_name VARCHAR(200),                -- nombre libre si no hay provider_id
    receipt_type_id TINYINT NULL,              -- 1=Boleta 2=Factura Exenta 3=Factura Afecta 4=Sin Documento
    sort_order INT DEFAULT 0,
    created_at DATETIME DEFAULT CURRENT_TIMESTAMP,
    FOREIGN KEY (service_id) REFERENCES service(id) ON DELETE CASCADE
);

-- Sub-servicios: servicios de la misma empresa que componen un servicio padre
-- (no externalizados — los externalizados van como service_cost_item con is_externalized=1)
CREATE TABLE service_sub_service (
    id INT AUTO_INCREMENT PRIMARY KEY,
    parent_service_id INT NOT NULL,
    child_service_id INT NOT NULL,
    quantity DECIMAL(10,3) DEFAULT 1,
    additional_cost DECIMAL(12,2) DEFAULT 0,   -- costo adicional al incluirlo
    notes TEXT,
    sort_order INT DEFAULT 0,
    created_at DATETIME DEFAULT CURRENT_TIMESTAMP,
    FOREIGN KEY (parent_service_id) REFERENCES service(id) ON DELETE CASCADE,
    FOREIGN KEY (child_service_id) REFERENCES service(id),
    UNIQUE KEY uq_parent_child (parent_service_id, child_service_id)
);
use `gp-erp`;
-- Orden de venta de servicios (puede tener N servicios)
-- Equivalente a la tabla "sales" de gp-inventory
CREATE TABLE service_sale (
    id INT AUTO_INCREMENT PRIMARY KEY,
    business_id INT NOT NULL,
    store_id INT,
    -- Cliente con cuenta registrada en Gran Paso (opcional)
    user_id INT NULL,                          -- FK a user.id si tiene cuenta
    -- Datos manuales para clientes sin cuenta (o para sobreescribir los del user)
    client_name VARCHAR(200),
    client_rut VARCHAR(20),
    client_email VARCHAR(200),
    client_phone VARCHAR(50),
    total_amount DECIMAL(12,2) NOT NULL DEFAULT 0,  -- suma de todos los items
    status ENUM('pending', 'in_progress', 'completed', 'cancelled') DEFAULT 'pending',
    date DATE NOT NULL,
    scheduled_date DATETIME,                   -- fecha/hora agendada de ejecución
    completed_date DATETIME,                   -- fecha/hora de término real
    notes TEXT,
    created_by INT,                            -- id del usuario que creó la venta
    -- Documento tributario
    document_type ENUM('none','boleta','factura') NOT NULL DEFAULT 'none' COMMENT 'Tipo de documento emitido',
    -- Forma de pago (se registra al completar la venta)
    payment_type       TINYINT NOT NULL DEFAULT 1 COMMENT '1=Contado  2=Cuotas/Crédito',
    installments_count TINYINT NOT NULL DEFAULT 1 COMMENT 'Nº de cuotas (1 = pago único)',
    payment_start_date DATE    NULL     COMMENT 'Fecha del primer pago (aplica si hay cuotas)',
    created_at DATETIME DEFAULT CURRENT_TIMESTAMP,
    updated_at DATETIME DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
    FOREIGN KEY (business_id) REFERENCES business(id),
    FOREIGN KEY (user_id) REFERENCES `user`(id) ON DELETE SET NULL,
    FOREIGN KEY (store_id) REFERENCES store(id)
);
select * from user;
-- Ítems de la venta de servicios (cada servicio dentro de la orden)
-- Equivalente a "sales_detail" de gp-inventory
CREATE TABLE service_sale_item (
    id INT AUTO_INCREMENT PRIMARY KEY,
    sale_id INT NOT NULL,
    service_id INT NOT NULL,
    quantity DECIMAL(10,3) NOT NULL DEFAULT 1,
    unit_price DECIMAL(12,2) NOT NULL,          -- precio acordado al momento de venta
    subtotal DECIMAL(12,2) NOT NULL,            -- quantity * unit_price
    notes TEXT,
    is_completed TINYINT(1) DEFAULT 0,          -- servicio individual completado en el proceso
    created_at DATETIME DEFAULT CURRENT_TIMESTAMP,
    FOREIGN KEY (sale_id)    REFERENCES service_sale(id) ON DELETE CASCADE,
    FOREIGN KEY (service_id) REFERENCES service(id)
);

-- Índices para búsquedas frecuentes
CREATE INDEX idx_service_business         ON service(business_id);
CREATE INDEX idx_service_category         ON service(category_id);
CREATE INDEX idx_service_cost_item_svc    ON service_cost_item(service_id);
CREATE INDEX idx_service_sub_svc_parent   ON service_sub_service(parent_service_id);
CREATE INDEX idx_service_sale_business    ON service_sale(business_id);
CREATE INDEX idx_service_sale_user        ON service_sale(user_id);
CREATE INDEX idx_service_sale_status      ON service_sale(status);
CREATE INDEX idx_service_sale_date        ON service_sale(date);
CREATE INDEX idx_service_sale_item_sale   ON service_sale_item(sale_id);
CREATE INDEX idx_service_sale_item_svc    ON service_sale_item(service_id);

-- ============================================================
-- CONEXIÓN CON GP-EXPENSES
-- Agrega service_sale_id a la tabla expenses para vincular
-- los costos generados al registrar una venta de servicio.
-- ============================================================

ALTER TABLE expenses
    ADD COLUMN service_sale_id INT NULL COMMENT 'Venta de servicio que originó este gasto',
    ADD CONSTRAINT fk_expense_service_sale
        FOREIGN KEY (service_sale_id) REFERENCES service_sale(id) ON DELETE SET NULL;

CREATE INDEX idx_expenses_service_sale ON expenses(service_sale_id);

-- ============================================================
-- SEED: Nueva categoría y subcategorías para costos de servicios
-- Estos se usan cuando el backend crea automáticamente los gastos
-- al registrar una service_sale (uno por service_cost_item).
-- ============================================================
use `gp-erp`;
describe expense_category;
select * from expense_category;
INSERT INTO expense_category (id, name)
VALUES (10, 'Costos de Servicios')
ON DUPLICATE KEY UPDATE name = VALUES(name);

describe expense_subcategory;
use `gp-erp`;
INSERT INTO expense_subcategory (id, name, expense_category_id)
VALUES
    (49, 'Materiales de servicio',   10),
    (50, 'Mano de obra',             10),
    (51, 'Subcontratación',          10),
    (52, 'Costo general de servicio',10)
ON DUPLICATE KEY UPDATE
    name = VALUES(name),
    expense_category_id = VALUES(expense_category_id);

-- ============================================================
-- MIGRACIÓN: Columnas de forma de pago en service_sale
-- ============================================================
ALTER TABLE service_sale
    ADD COLUMN IF NOT EXISTS document_type      ENUM('none','boleta','factura') NOT NULL DEFAULT 'none'
        COMMENT 'Tipo de documento emitido',
    ADD COLUMN IF NOT EXISTS payment_type       TINYINT NOT NULL DEFAULT 1
        COMMENT '1=Contado  2=Cuotas/Crédito',
    ADD COLUMN IF NOT EXISTS installments_count TINYINT NOT NULL DEFAULT 1
        COMMENT 'Nº de cuotas (1 = pago único)',
    ADD COLUMN IF NOT EXISTS payment_start_date DATE NULL
        COMMENT 'Fecha del primer pago (aplica si hay cuotas)';

-- ============================================================
-- MIGRACIÓN: Columnas de proveedor y tipo de documento en service_cost_item
-- ============================================================
ALTER TABLE service_cost_item
    ADD COLUMN IF NOT EXISTS provider_id    INT NULL
        COMMENT 'FK a provider.id (proveedor registrado)',
    ADD COLUMN IF NOT EXISTS receipt_type_id TINYINT NULL
        COMMENT '1=Boleta 2=Factura Exenta 3=Factura Afecta 4=Sin Documento';

-- ============================================================
-- MIGRACIÓN: Columna is_completed en service_sale_item
-- ============================================================
ALTER TABLE service_sale_item
    ADD COLUMN is_completed TINYINT(1) DEFAULT 0
        COMMENT 'Servicio individual completado en el proceso';

use `gp-erp`;

ALTER TABLE fixed_expense MODIFY COLUMN store_id INT NULL;
ALTER TABLE supplies MODIFY COLUMN store_id INT NULL;

-- ============================================================
-- Tabla de insumos consumidos en una venta de servicios
-- ============================================================
CREATE TABLE IF NOT EXISTS service_sale_supply (
    id INT AUTO_INCREMENT PRIMARY KEY,
    sale_id INT NOT NULL,
    supply_id INT NOT NULL,
    quantity DECIMAL(10,3) NOT NULL DEFAULT 1,
    unit_cost DECIMAL(12,2) NOT NULL DEFAULT 0,
    notes TEXT,
    created_at DATETIME DEFAULT CURRENT_TIMESTAMP,
    FOREIGN KEY (sale_id) REFERENCES service_sale(id) ON DELETE CASCADE,
    FOREIGN KEY (supply_id) REFERENCES supplies(id) ON DELETE RESTRICT,
    INDEX idx_sale_supply (sale_id, supply_id)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;
