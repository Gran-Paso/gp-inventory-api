-- Active: 1769994214503@@143.198.232.23@3306@gp_inventory
-- ============================================================
-- GP-HR: Script de creación de tablas de Recursos Humanos
-- Conecta con las tablas existentes: `user`, `user_has_business`
-- Integración con gp-services: `hr_position.hourly_rate`
-- ============================================================

-- ─────────────────────────────────────────────────────────────
-- 1. DEPARTAMENTOS
-- ─────────────────────────────────────────────────────────────
CREATE TABLE IF NOT EXISTS hr_department (
    id                  INT          NOT NULL AUTO_INCREMENT PRIMARY KEY,
    business_id         INT          NOT NULL,
    name                VARCHAR(120) NOT NULL,
    description         TEXT         NULL,
    manager_employee_id INT          NULL,      -- FK → hr_employee.id (circular, se agrega luego)
    active              TINYINT(1)   NOT NULL DEFAULT 1,
    created_at          DATETIME     NOT NULL DEFAULT CURRENT_TIMESTAMP,
    updated_at          DATETIME     NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
    INDEX idx_hd_business (business_id)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

-- ─────────────────────────────────────────────────────────────
-- 2. CARGOS / POSICIONES
--    hourly_rate → punto de integración con gp-services
--    (costo de un cargo en una orden de servicio)
-- ─────────────────────────────────────────────────────────────
CREATE TABLE IF NOT EXISTS hr_position (
    id              INT            NOT NULL AUTO_INCREMENT PRIMARY KEY,
    business_id     INT            NOT NULL,
    department_id   INT            NULL,
    name            VARCHAR(120)   NOT NULL,
    description     TEXT           NULL,
    schedule_type   ENUM('full_time','part_time','by_hours','by_project')
                                   NOT NULL DEFAULT 'full_time',
    monthly_salary  DECIMAL(12,2)  NOT NULL DEFAULT 0.00,
    hourly_rate     DECIMAL(10,4)  NOT NULL DEFAULT 0.0000,  -- para gp-services
    active          TINYINT(1)     NOT NULL DEFAULT 1,
    created_at      DATETIME       NOT NULL DEFAULT CURRENT_TIMESTAMP,
    updated_at      DATETIME       NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
    INDEX idx_hp_business   (business_id),
    INDEX idx_hp_department (department_id),
    FOREIGN KEY (department_id) REFERENCES hr_department(id) ON DELETE SET NULL
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

-- ─────────────────────────────────────────────────────────────
-- 3. EMPLEADOS
--    user_id → FK opcional hacia `user.id` (gp-auth)
-- ─────────────────────────────────────────────────────────────
CREATE TABLE IF NOT EXISTS hr_employee (
    id               INT          NOT NULL AUTO_INCREMENT PRIMARY KEY,
    business_id      INT          NOT NULL,
    user_id          INT          NULL,           -- FK → user.id (puede ser NULL si no tiene cuenta)
    first_name       VARCHAR(80)  NOT NULL,
    last_name        VARCHAR(80)  NOT NULL,
    email            VARCHAR(150) NULL,
    phone            VARCHAR(30)  NULL,
    rut              VARCHAR(20)  NULL,            -- RUT chileno u otro identificador
    birth_date       DATE         NULL,
    hire_date        DATE         NOT NULL,
    termination_date DATE         NULL,
    position_id      INT          NULL,
    department_id    INT          NULL,
    contract_type    ENUM('indefinido','plazo_fijo','honorarios','otro')
                                  NOT NULL DEFAULT 'indefinido',
    status           ENUM('active','inactive','on_leave')
                                  NOT NULL DEFAULT 'active',
    current_salary   DECIMAL(12,2) NOT NULL DEFAULT 0.00,
    notes            TEXT         NULL,
    active           TINYINT(1)   NOT NULL DEFAULT 1,
    created_at       DATETIME     NOT NULL DEFAULT CURRENT_TIMESTAMP,
    updated_at       DATETIME     NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
    INDEX idx_he_business    (business_id),
    INDEX idx_he_user        (user_id),
    INDEX idx_he_position    (position_id),
    INDEX idx_he_department  (department_id),
    INDEX idx_he_status      (status),
    FOREIGN KEY (position_id)   REFERENCES hr_position(id)   ON DELETE SET NULL,
    FOREIGN KEY (department_id) REFERENCES hr_department(id) ON DELETE SET NULL
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

-- Ahora sí se puede agregar la FK circular de departamento → manager
ALTER TABLE hr_department
    ADD CONSTRAINT fk_hd_manager
    FOREIGN KEY (manager_employee_id) REFERENCES hr_employee(id) ON DELETE SET NULL;

-- ─────────────────────────────────────────────────────────────
-- 4. LIQUIDACIONES DE SUELDO
-- ─────────────────────────────────────────────────────────────
CREATE TABLE IF NOT EXISTS hr_payroll (
    id               INT           NOT NULL AUTO_INCREMENT PRIMARY KEY,
    employee_id      INT           NOT NULL,
    business_id      INT           NOT NULL,
    period_year      SMALLINT      NOT NULL,
    period_month     TINYINT       NOT NULL,        -- 1-12
    gross_salary     DECIMAL(12,2) NOT NULL DEFAULT 0.00,
    total_deductions DECIMAL(12,2) NOT NULL DEFAULT 0.00,
    net_salary       DECIMAL(12,2) NOT NULL DEFAULT 0.00,
    payment_date     DATE          NULL,
    status           ENUM('draft','approved','paid','cancelled')
                                   NOT NULL DEFAULT 'draft',
    notes            TEXT          NULL,
    created_at       DATETIME      NOT NULL DEFAULT CURRENT_TIMESTAMP,
    updated_at       DATETIME      NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
    INDEX idx_hpay_employee (employee_id),
    INDEX idx_hpay_business (business_id),
    INDEX idx_hpay_period   (period_year, period_month),
    UNIQUE KEY uq_hpay_emp_period (employee_id, period_year, period_month),
    FOREIGN KEY (employee_id) REFERENCES hr_employee(id) ON DELETE CASCADE
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

-- ─────────────────────────────────────────────────────────────
-- 5. DEDUCCIONES DE LIQUIDACIÓN
-- ─────────────────────────────────────────────────────────────
CREATE TABLE IF NOT EXISTS hr_payroll_deduction (
    id          INT            NOT NULL AUTO_INCREMENT PRIMARY KEY,
    payroll_id  INT            NOT NULL,
    type        ENUM('afp','salud','seguro_cesantia','impuesto','otro')
                               NOT NULL DEFAULT 'otro',
    name        VARCHAR(100)   NOT NULL,
    amount      DECIMAL(12,2)  NOT NULL DEFAULT 0.00,
    percentage  DECIMAL(6,4)   NULL,     -- porcentaje aplicado, referencial
    FOREIGN KEY (payroll_id) REFERENCES hr_payroll(id) ON DELETE CASCADE
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

-- ─────────────────────────────────────────────────────────────
-- 6. TIPOS DE PERMISO
-- ─────────────────────────────────────────────────────────────
CREATE TABLE IF NOT EXISTS hr_leave_type (
    id            INT          NOT NULL AUTO_INCREMENT PRIMARY KEY,
    business_id   INT          NOT NULL,
    name          VARCHAR(100) NOT NULL,
    days_per_year INT          NOT NULL DEFAULT 15,
    paid          TINYINT(1)   NOT NULL DEFAULT 1,
    active        TINYINT(1)   NOT NULL DEFAULT 1,
    created_at    DATETIME     NOT NULL DEFAULT CURRENT_TIMESTAMP,
    INDEX idx_hlt_business (business_id)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

-- ─────────────────────────────────────────────────────────────
-- 7. SOLICITUDES DE PERMISO
-- ─────────────────────────────────────────────────────────────
CREATE TABLE IF NOT EXISTS hr_leave_request (
    id              INT          NOT NULL AUTO_INCREMENT PRIMARY KEY,
    employee_id     INT          NOT NULL,
    leave_type_id   INT          NOT NULL,
    start_date      DATE         NOT NULL,
    end_date        DATE         NOT NULL,
    days_requested  INT          NOT NULL DEFAULT 1,
    reason          TEXT         NULL,
    status          ENUM('pending','approved','rejected')
                                 NOT NULL DEFAULT 'pending',
    reviewed_by     INT          NULL,    -- user.id del revisor
    reviewed_at     DATETIME     NULL,
    created_at      DATETIME     NOT NULL DEFAULT CURRENT_TIMESTAMP,
    updated_at      DATETIME     NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
    INDEX idx_hlr_employee   (employee_id),
    INDEX idx_hlr_leave_type (leave_type_id),
    INDEX idx_hlr_status     (status),
    FOREIGN KEY (employee_id)   REFERENCES hr_employee(id)   ON DELETE CASCADE,
    FOREIGN KEY (leave_type_id) REFERENCES hr_leave_type(id) ON DELETE CASCADE
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

-- ─────────────────────────────────────────────────────────────
-- 8. ROLES DE NEGOCIO PARA HR
--    Permite definir roles con permisos granulares por negocio
-- ─────────────────────────────────────────────────────────────
CREATE TABLE IF NOT EXISTS hr_business_role (
    id          INT          NOT NULL AUTO_INCREMENT PRIMARY KEY,
    business_id INT          NOT NULL,
    name        VARCHAR(100) NOT NULL,
    description TEXT         NULL,
    permissions JSON         NULL,    -- {"view_employees": true, "manage_payroll": false, ...}
    active      TINYINT(1)   NOT NULL DEFAULT 1,
    created_at  DATETIME     NOT NULL DEFAULT CURRENT_TIMESTAMP,
    updated_at  DATETIME     NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
    INDEX idx_hbr_business (business_id)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

-- ─────────────────────────────────────────────────────────────
-- 9. EXTENDER user_has_business con rol de HR
--    Conecta el usuario del negocio con su rol en el módulo HR
-- ─────────────────────────────────────────────────────────────
ALTER TABLE user_has_business
    ADD COLUMN hr_business_role_id INT NULL AFTER id_business,
    ADD CONSTRAINT fk_uhb_hr_role
        FOREIGN KEY (hr_business_role_id)
        REFERENCES hr_business_role(id)
        ON DELETE SET NULL;

-- ─────────────────────────────────────────────────────────────
-- 10. INTEGRACIÓN CON GP-SERVICES
--     Agrega position_id a service_cost_item para vincular
--     el costo de un ítem de servicio a un cargo/posición HR.
--     El costo se calcula: hourly_rate × horas estimadas.
--     El gasto queda absorbido por la liquidación del empleado.
-- ─────────────────────────────────────────────────────────────
ALTER TABLE service_cost_item
    ADD COLUMN position_id INT NULL AFTER cost_type,
    ADD CONSTRAINT fk_sci_hr_position
        FOREIGN KEY (position_id)
        REFERENCES hr_position(id)
        ON DELETE SET NULL;

-- ─────────────────────────────────────────────────────────────
-- 11. MIGRACIÓN: position_type → schedule_type en hr_position
--     Ejecutar solo si la tabla ya fue creada con position_type
-- ─────────────────────────────────────────────────────────────
ALTER TABLE hr_position
    CHANGE COLUMN position_type schedule_type
        ENUM('full_time','part_time','by_hours','by_project')
        NOT NULL DEFAULT 'full_time';

ALTER TABLE hr_payroll ADD COLUMN expense_id INT NULL COMMENT 'ID del gasto vinculado en GP Expenses';

-- Ampliar ENUM de deducciones para los tipos legales chilenos
ALTER TABLE hr_payroll_deduction
    MODIFY COLUMN type ENUM('afp','salud','seguro_cesantia','impuesto','otro')
    NOT NULL DEFAULT 'otro';

-- ─────────────────────────────────────────────────────────────
-- 12. DATOS INICIALES: tipos de permiso comunes en Chile
-- ─────────────────────────────────────────────────────────────
-- (Descomentar y adaptar el business_id según corresponda)
--
-- INSERT INTO hr_leave_type (business_id, name, days_per_year, paid) VALUES
--   (1, 'Vacaciones legales',       15, 1),
--   (1, 'Licencia médica',          30, 1),
--   (1, 'Permiso por fallecimiento', 3, 1),
--   (1, 'Permiso por matrimonio',    5, 1),
--   (1, 'Permiso sin goce de sueldo', 15, 0);

-- ─────────────────────────────────────────────────────────────
-- FIN DEL SCRIPT
-- ─────────────────────────────────────────────────────────────
