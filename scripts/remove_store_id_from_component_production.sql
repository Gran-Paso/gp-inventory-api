-- Script para eliminar completamente el campo store_id de component_production
-- Ejecutar este script en la base de datos gp-erp

USE gp_erp;

-- 1. Detectar y eliminar el foreign key constraint si existe
SET @constraint_name = (
    SELECT CONSTRAINT_NAME 
    FROM information_schema.KEY_COLUMN_USAGE 
    WHERE TABLE_SCHEMA = DATABASE()
    AND TABLE_NAME = 'component_production' 
    AND COLUMN_NAME = 'store_id' 
    AND REFERENCED_TABLE_NAME IS NOT NULL
    LIMIT 1
);

SET @sql = IF(@constraint_name IS NOT NULL,
    CONCAT('ALTER TABLE component_production DROP FOREIGN KEY ', @constraint_name),
    'SELECT "No se encontró foreign key constraint en store_id" AS mensaje'
);

PREPARE stmt FROM @sql;
EXECUTE stmt;
DEALLOCATE PREPARE stmt;

-- 2. Detectar y eliminar el índice si existe
SET @index_name = (
    SELECT INDEX_NAME 
    FROM information_schema.STATISTICS 
    WHERE TABLE_SCHEMA = DATABASE()
    AND TABLE_NAME = 'component_production' 
    AND COLUMN_NAME = 'store_id' 
    AND INDEX_NAME != 'PRIMARY'
    LIMIT 1
);

SET @sql = IF(@index_name IS NOT NULL,
    CONCAT('ALTER TABLE component_production DROP INDEX ', @index_name),
    'SELECT "No se encontró índice en store_id" AS mensaje'
);

PREPARE stmt FROM @sql;
EXECUTE stmt;
DEALLOCATE PREPARE stmt;

-- 3. Eliminar la columna store_id
ALTER TABLE component_production DROP COLUMN IF EXISTS store_id;

-- 4. Verificar que se eliminó correctamente
SELECT 
    CASE 
        WHEN COUNT(*) = 0 THEN '✅ Columna store_id eliminada exitosamente de component_production'
        ELSE '❌ La columna store_id todavía existe'
    END AS resultado
FROM information_schema.COLUMNS
WHERE TABLE_SCHEMA = DATABASE()
AND TABLE_NAME = 'component_production'
AND COLUMN_NAME = 'store_id';
