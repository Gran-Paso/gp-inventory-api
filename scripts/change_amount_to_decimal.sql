-- Migration: Change Amount columns from INT to DECIMAL(18,2)
-- This allows storing larger monetary values with decimal precision

-- Update expenses table
ALTER TABLE expenses 
MODIFY COLUMN amount DECIMAL(18,2) NOT NULL;

-- Update fixed_expenses table
ALTER TABLE fixed_expenses 
MODIFY COLUMN amount DECIMAL(18,2) NOT NULL;

-- Note: Run this script against your database to apply the changes
-- Make sure to backup your database before running this migration
