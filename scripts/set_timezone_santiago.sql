-- Configurar la zona horaria de Santiago, Chile (UTC-3/-4 con horario de verano)
-- Este script configura la zona horaria a nivel de sesión y globalmente

-- Configurar para la sesión actual
SET time_zone = 'America/Santiago';

-- Configurar globalmente (requiere privilegios SUPER)
SET GLOBAL time_zone = 'America/Santiago';

-- Verificar la configuración actual
SELECT @@global.time_zone AS global_timezone, 
       @@session.time_zone AS session_timezone,
       NOW() AS current_time,
       UTC_TIMESTAMP() AS utc_time;

-- Si el servidor no tiene las tablas de zona horaria cargadas, 
-- ejecuta este comando en la terminal (macOS/Linux):
-- mysql_tzinfo_to_sql /usr/share/zoneinfo | mysql -u root -p mysql

-- O en Windows, descarga las timezone tables desde:
-- https://dev.mysql.com/downloads/timezones.html
