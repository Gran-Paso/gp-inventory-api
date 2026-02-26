-- ============================================================
-- Bank integration tables for Fintoc
-- Run this script against the gp_inventory database
-- ============================================================

-- Bank connections (one per linked bank account)
CREATE TABLE IF NOT EXISTS bank_connections (
    id               INT AUTO_INCREMENT PRIMARY KEY,
    business_id      INT           NOT NULL,
    link_token       VARCHAR(500)  NOT NULL COMMENT 'Fintoc link_token from widget OAuth',
    account_id       VARCHAR(200)  NULL     COMMENT 'Fintoc account id',
    bank_entity_id   INT           NULL     COMMENT 'FK to bank_entities table',
    label            VARCHAR(200)  NULL     COMMENT 'Human-readable label',
    last_sync_at     DATETIME      NULL,
    is_active        TINYINT(1)    NOT NULL DEFAULT 1,
    created_at       DATETIME      NOT NULL DEFAULT CURRENT_TIMESTAMP,
    updated_at       DATETIME      NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
    CONSTRAINT fk_bc_business    FOREIGN KEY (business_id)  REFERENCES business(id)      ON DELETE CASCADE,
    CONSTRAINT fk_bc_bank_entity FOREIGN KEY (bank_entity_id) REFERENCES bank_entities(id) ON DELETE SET NULL
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;

-- Bank transactions imported from Fintoc
CREATE TABLE IF NOT EXISTS bank_transactions (
    id                      INT AUTO_INCREMENT PRIMARY KEY,
    bank_connection_id      INT             NOT NULL,
    business_id             INT             NOT NULL,
    fintoc_id               VARCHAR(200)    NOT NULL  COMMENT 'Unique Fintoc movement id',
    amount                  DECIMAL(18,2)   NOT NULL,
    description             VARCHAR(500)    NULL,
    transaction_date        DATETIME        NOT NULL,
    transaction_type        VARCHAR(50)     NULL      COMMENT 'debit | credit',
    status                  VARCHAR(50)     NOT NULL DEFAULT 'pending'
                                            COMMENT 'pending | confirmed | dismissed',
    expense_id              INT             NULL      COMMENT 'Set after user confirms as expense',
    suggested_subcategory_id INT            NULL,
    is_active               TINYINT(1)      NOT NULL DEFAULT 1,
    created_at              DATETIME        NOT NULL DEFAULT CURRENT_TIMESTAMP,
    updated_at              DATETIME        NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
    CONSTRAINT uq_bt_fintoc_id UNIQUE (fintoc_id),
    CONSTRAINT fk_bt_connection FOREIGN KEY (bank_connection_id) REFERENCES bank_connections(id) ON DELETE CASCADE,
    CONSTRAINT fk_bt_business   FOREIGN KEY (business_id)        REFERENCES business(id) ON DELETE CASCADE,
    CONSTRAINT fk_bt_expense    FOREIGN KEY (expense_id)          REFERENCES expenses(id) ON DELETE SET NULL
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;

-- Index for fast "get pending transactions for business" query
CREATE INDEX idx_bt_business_status
    ON bank_transactions (business_id, status);
