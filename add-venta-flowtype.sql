-- Script para agregar el tipo de flujo "Venta"
-- Ejecutar este script en la base de datos para agregar el nuevo FlowType

-- Verificar si ya existe el tipo de flujo "Venta"
IF NOT EXISTS (SELECT 1 FROM FlowTypes WHERE Name = 'Venta')
BEGIN
    INSERT INTO FlowTypes (Name) 
    VALUES ('Venta');
    PRINT 'Tipo de flujo "Venta" agregado exitosamente.';
END
ELSE
BEGIN
    PRINT 'El tipo de flujo "Venta" ya existe.';
END

-- Mostrar todos los tipos de flujo disponibles
SELECT Id, Name FROM FlowTypes ORDER BY Id;
