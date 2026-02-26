-- ============================================================================
-- Agregar columna system_role a la tabla user
-- ============================================================================

USE gp_inventory;

-- Agregar la columna system_role como ENUM
ALTER TABLE `user` 
ADD COLUMN `system_role` ENUM('super_admin', 'admin', 'none') NOT NULL DEFAULT 'none' 
AFTER `active`;

-- Verificar la estructura de la tabla
DESCRIBE `user`;

-- Mostrar todos los usuarios con su system_role
SELECT id, email, name, last_name, system_role, active 
FROM `user`
ORDER BY id;

-- ============================================================================
-- OPCIONAL: Si quieres asignar super_admin a un usuario específico
-- ============================================================================
-- UPDATE `user` SET system_role = 'super_admin' WHERE email = 'tu@email.com';
