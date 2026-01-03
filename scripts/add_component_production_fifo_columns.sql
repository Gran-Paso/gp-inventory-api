-- Migración: Agregar columnas para FIFO y auditoría en component_production
-- Fecha: 2026-01-03
-- Descripción: Agrega component_production_id (auto-referencia FIFO), created_at y updated_at

USE gp_inventory;

-- 1. Agregar columna component_production_id (auto-referencia para FIFO)
ALTER TABLE component_production
ADD COLUMN component_production_id INT NULL
COMMENT 'Referencia al component_production padre (FIFO)';

-- 2. Agregar columnas de auditoría
ALTER TABLE component_production
ADD COLUMN created_at DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP
COMMENT 'Fecha de creación del registro',
ADD COLUMN updated_at DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP
COMMENT 'Fecha de última actualización';

-- 3. Agregar foreign key para component_production_id
ALTER TABLE component_production
ADD CONSTRAINT fk_component_production_parent 
    FOREIGN KEY (component_production_id) 
    REFERENCES component_production(id) 
    ON DELETE SET NULL;

-- 4. Crear índice para mejorar el rendimiento de queries FIFO
CREATE INDEX idx_component_production_parent 
ON component_production(component_production_id);

-- 5. Crear índice compuesto para queries de stock disponible
CREATE INDEX idx_component_stock_lookup 
ON component_production(component_id, is_active, component_production_id);

-- 6. Verificar el resultado
SELECT 
    COLUMN_NAME, 
    DATA_TYPE, 
    IS_NULLABLE, 
    COLUMN_DEFAULT,
    COLUMN_COMMENT
FROM INFORMATION_SCHEMA.COLUMNS
WHERE TABLE_SCHEMA = 'gp_inventory'
AND TABLE_NAME = 'component_production'
AND COLUMN_NAME IN ('component_production_id', 'created_at', 'updated_at')
ORDER BY ORDINAL_POSITION;

-- Resultado esperado:
-- component_production_id | int | YES | NULL | Referencia al component_production padre (FIFO)
-- created_at | datetime | NO | CURRENT_TIMESTAMP | Fecha de creación del registro
-- updated_at | datetime | NO | CURRENT_TIMESTAMP on update CURRENT_TIMESTAMP | Fecha de última actualización
