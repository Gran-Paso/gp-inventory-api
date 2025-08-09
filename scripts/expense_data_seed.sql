-- Script de migración y datos iniciales para el sistema de gastos
-- Ejecutar después de crear las tablas de expense_category, expense_subcategory, recurrence_type, expenses, fixed_expense

-- Insertar tipos de recurrencia
INSERT INTO recurrence_type (id, name, description, is_active) VALUES
(1, 'Diario', 'Se repite cada día', 1),
(2, 'Semanal', 'Se repite cada semana', 1),
(3, 'Quincenal', 'Se repite cada 15 días', 1),
(4, 'Mensual', 'Se repite cada mes', 1),
(5, 'Trimestral', 'Se repite cada 3 meses', 1),
(6, 'Anual', 'Se repite cada año', 1)
ON DUPLICATE KEY UPDATE name = VALUES(name), description = VALUES(description);

-- Insertar categorías de gastos (globales - sin business_id)
INSERT INTO expense_category (id, name, description, business_id) VALUES
(1, 'Servicios Básicos', 'Gastos de servicios esenciales como electricidad, agua, gas', NULL),
(2, 'Telecomunicaciones', 'Gastos de comunicación y conectividad', NULL),
(3, 'Transporte', 'Gastos relacionados con transporte y vehículos', NULL),
(4, 'Alimentación', 'Gastos de comida y bebidas', NULL),
(5, 'Mantenimiento', 'Gastos de mantenimiento y reparaciones', NULL),
(6, 'Seguros', 'Gastos de seguros y protecciones', NULL),
(7, 'Marketing', 'Gastos de publicidad y marketing', NULL),
(8, 'Capacitación', 'Gastos de educación y desarrollo', NULL),
(9, 'Administrativos', 'Gastos administrativos y de oficina', NULL)
ON DUPLICATE KEY UPDATE name = VALUES(name), description = VALUES(description);

-- Insertar subcategorías de gastos (globales - sin business_id)
INSERT INTO expense_subcategory (id, name, description, expense_category_id, business_id) VALUES
-- Servicios Básicos (1)
(1, 'Electricidad', 'Consumo de energía eléctrica', 1, NULL),
(2, 'Agua', 'Servicio de agua potable', 1, NULL),
(3, 'Gas', 'Servicio de gas natural o LP', 1, NULL),
(4, 'Recolección de basura', 'Servicio de recolección de residuos', 1, NULL),

-- Telecomunicaciones (2)
(5, 'Internet', 'Servicio de conexión a internet', 2, NULL),
(6, 'Teléfono fijo', 'Servicio de telefonía fija', 2, NULL),
(7, 'Teléfono móvil', 'Servicio de telefonía móvil', 2, NULL),
(8, 'Hosting/Dominios', 'Servicios de alojamiento web y dominios', 2, NULL),
(9, 'Software/Licencias', 'Licencias de software y aplicaciones', 2, NULL),

-- Transporte (3)
(10, 'Combustible', 'Gasolina, diésel y otros combustibles', 3, NULL),
(11, 'Mantenimiento vehicular', 'Servicio y reparación de vehículos', 3, NULL),
(12, 'Seguros vehiculares', 'Seguros de automóviles', 3, NULL),
(13, 'Transporte público', 'Gastos en transporte público', 3, NULL),
(14, 'Estacionamiento', 'Gastos de estacionamiento y parquímetros', 3, NULL),

-- Alimentación (4)
(15, 'Comida personal', 'Gastos de alimentación del personal', 4, NULL),
(16, 'Cafetería/Restaurante', 'Gastos en restaurantes y cafeterías', 4, NULL),
(17, 'Eventos y reuniones', 'Comida para eventos de trabajo', 4, NULL),

-- Mantenimiento (5)
(18, 'Limpieza', 'Servicios y productos de limpieza', 5, NULL),
(19, 'Reparaciones menores', 'Reparaciones y mantenimiento menor', 5, NULL),
(20, 'Equipos y herramientas', 'Compra y mantenimiento de herramientas', 5, NULL),
(21, 'Jardinería', 'Mantenimiento de áreas verdes', 5, NULL),

-- Seguros (6)
(22, 'Seguro de responsabilidad civil', 'Seguros de responsabilidad', 6, NULL),
(23, 'Seguro de equipos', 'Seguros para equipos y maquinaria', 6, NULL),
(24, 'Seguro de local', 'Seguros para el local comercial', 6, NULL),

-- Marketing (7)
(25, 'Publicidad digital', 'Gastos en publicidad online', 7, NULL),
(26, 'Publicidad impresa', 'Gastos en publicidad tradicional', 7, NULL),
(27, 'Redes sociales', 'Gastos en marketing de redes sociales', 7, NULL),
(28, 'Eventos promocionales', 'Gastos en eventos y promociones', 7, NULL),
(29, 'Material promocional', 'Folletos, tarjetas, merchandising', 7, NULL),

-- Capacitación (8)
(30, 'Cursos online', 'Capacitación y educación online', 8, NULL),
(31, 'Seminarios/Conferencias', 'Asistencia a eventos educativos', 8, NULL),
(32, 'Libros y material educativo', 'Compra de material de aprendizaje', 8, NULL),
(33, 'Certificaciones', 'Gastos en certificaciones profesionales', 8, NULL),

-- Administrativos (9)
(34, 'Papelería y oficina', 'Materiales de oficina y papelería', 9, NULL),
(35, 'Servicios profesionales', 'Contadores, abogados, consultores', 9, NULL),
(36, 'Gastos bancarios', 'Comisiones y gastos bancarios', 9, NULL),
(37, 'Impuestos y tasas', 'Gastos en impuestos y tasas municipales', 9, NULL),
(38, 'Alquiler', 'Gastos de alquiler de local o equipos', 9, NULL),
(39, 'Servicios de mensajería', 'Envíos y paquetería', 9, NULL),
(40, 'Suscripciones', 'Revistas, periódicos, servicios premium', 9, NULL),
(41, 'Gastos legales', 'Trámites y gestiones legales', 9, NULL),
(42, 'Depreciación', 'Depreciación de activos', 9, NULL),
(43, 'Donaciones', 'Donaciones y contribuciones sociales', 9, NULL),
(44, 'Gastos varios', 'Gastos menores no clasificados', 9, NULL),
(45, 'Pérdidas y faltantes', 'Pérdidas por robo, daño o faltantes', 9, NULL),
(46, 'Intereses y financieros', 'Intereses de préstamos y gastos financieros', 9, NULL),
(47, 'Multas y penalizaciones', 'Multas de tránsito, penalizaciones', 9, NULL),
(48, 'Gastos de representación', 'Gastos para atender clientes o proveedores', 9, NULL)
ON DUPLICATE KEY UPDATE name = VALUES(name), description = VALUES(description), expense_category_id = VALUES(expense_category_id);

-- Nota: Los registros en las tablas 'expenses' y 'fixed_expense' se crearán a través de la aplicación
-- ya que requieren datos específicos del negocio como business_id, store_id, etc.

-- Verificar la inserción de datos
SELECT 
    'Tipos de Recurrencia' as tabla,
    COUNT(*) as total_registros
FROM recurrence_type
WHERE is_active = 1
UNION ALL
SELECT 
    'Categorías de Gastos' as tabla,
    COUNT(*) as total_registros
FROM expense_category
WHERE business_id IS NULL
UNION ALL
SELECT 
    'Subcategorías de Gastos' as tabla,
    COUNT(*) as total_registros
FROM expense_subcategory
WHERE business_id IS NULL;
