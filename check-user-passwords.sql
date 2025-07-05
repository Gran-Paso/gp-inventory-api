-- Script to check user passwords in database
SELECT 
    id,
    mail,
    name,
    lastname,
    LEFT(password, 20) as password_start,
    LENGTH(password) as password_length,
    LEFT(salt, 20) as salt_start,
    LENGTH(salt) as salt_length,
    active
FROM user 
WHERE mail = 'pablojavierprietocepeda@gmail.com';

-- Check if password looks like BCrypt format
SELECT 
    id,
    mail,
    CASE 
        WHEN password LIKE '$2%' THEN 'BCrypt format'
        WHEN LENGTH(password) = 60 THEN 'Possible BCrypt'
        WHEN LENGTH(password) < 30 THEN 'Plain text or simple hash'
        ELSE 'Unknown format'
    END as password_format,
    LENGTH(password) as password_length
FROM user 
WHERE mail = 'pablojavierprietocepeda@gmail.com';
