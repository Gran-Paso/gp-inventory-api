-- Check user roles in database
SELECT 
    u.id as user_id,
    u.mail as user_email,
    u.name as user_name,
    ub.id_user,
    ub.id_business,
    ub.id_role,
    r.name as role_name,
    b.company_name as business_name
FROM user u
LEFT JOIN user_has_business ub ON u.id = ub.id_user
LEFT JOIN role r ON ub.id_role = r.id
LEFT JOIN business b ON ub.id_business = b.id
WHERE u.mail = 'pablojavierprietocepeda@gmail.com';

-- Check if user exists
SELECT 'User exists' as status, id, mail, name, lastname, active 
FROM user 
WHERE mail = 'pablojavierprietocepeda@gmail.com';

-- Check if there are any roles in the system
SELECT 'Available roles' as status, id, name FROM role;

-- Check if there are any businesses in the system
SELECT 'Available businesses' as status, id, company_name FROM business;

-- Check if there are any user-business-role associations
SELECT 'User-Business-Role associations' as status, id_user, id_business, id_role 
FROM user_has_business LIMIT 5;
