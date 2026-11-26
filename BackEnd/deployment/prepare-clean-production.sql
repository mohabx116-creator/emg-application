-- EMG clean-production preparation.
-- Dry-run is the default. Execute only with: psql -v execute=true -f ...
-- This script never drops schemas, resets sequences, truncates with CASCADE,
-- changes passwords, or deletes security_audit_logs / roles / permissions.

\set ON_ERROR_STOP on
\if :{?execute}
\else
\set execute false
\endif

BEGIN;

CREATE TEMP TABLE _emg_retained_users(email text PRIMARY KEY) ON COMMIT DROP;
INSERT INTO _emg_retained_users(email) VALUES
  ('mohab@emg.com'),
  ('acc@emg.com'),
  ('ibrahim@emg.com'),
  ('mohand7@emg.com');

DO $$
BEGIN
  IF (SELECT count(*) FROM users u JOIN _emg_retained_users r ON lower(u."Email") = r.email) <> 4 THEN
    RAISE EXCEPTION 'Protected user precondition failed: all four retained accounts must exist';
  END IF;
END $$;

-- Counts are emitted in both dry-run and execute mode for the review record.
SELECT 'projects' AS table_name, count(*) AS rows FROM projects
UNION ALL SELECT 'suppliers', count(*) FROM suppliers
UNION ALL SELECT 'contractors', count(*) FROM contractors
UNION ALL SELECT 'site_movements', count(*) FROM site_movements
UNION ALL SELECT 'excavation_records', count(*) FROM excavation_records
UNION ALL SELECT 'concrete_records', count(*) FROM concrete_records
UNION ALL SELECT 'steel_records', count(*) FROM steel_records
UNION ALL SELECT 'finishing_records', count(*) FROM finishing_records
UNION ALL SELECT 'supplier_supplies', count(*) FROM supplier_supplies
UNION ALL SELECT 'supplier_payments', count(*) FROM supplier_payments
UNION ALL SELECT 'advance_payments', count(*) FROM advance_payments
UNION ALL SELECT 'advance_allocations', count(*) FROM advance_allocations
UNION ALL SELECT 'contractor_contracts', count(*) FROM contractor_contracts
UNION ALL SELECT 'contractor_contract_items', count(*) FROM contractor_contract_items
UNION ALL SELECT 'contractor_statements', count(*) FROM contractor_statements
UNION ALL SELECT 'contractor_statement_lines', count(*) FROM contractor_statement_lines
UNION ALL SELECT 'historical_import_batches', count(*) FROM historical_import_batches
UNION ALL SELECT 'historical_import_records', count(*) FROM historical_import_records
UNION ALL SELECT 'historical_posted_entries', count(*) FROM historical_posted_entries
UNION ALL SELECT 'attachments', count(*) FROM attachments
UNION ALL SELECT 'workflow_actions', count(*) FROM workflow_actions
UNION ALL SELECT 'attendance_records', count(*) FROM attendance_records
UNION ALL SELECT 'notifications', count(*) FROM notifications
UNION ALL SELECT 'auth_sessions', count(*) FROM auth_sessions
UNION ALL SELECT 'password_reset_tokens', count(*) FROM password_reset_tokens
UNION ALL SELECT 'users_to_remove', count(*) FROM users u
  WHERE NOT EXISTS (SELECT 1 FROM _emg_retained_users r WHERE lower(u."Email") = r.email)
ORDER BY 1;

-- Explicit deletion order follows the FK graph. No CASCADE is used.
\if :execute
DELETE FROM attachments;
DELETE FROM historical_posted_entries;
DELETE FROM historical_import_audit_events;
DELETE FROM historical_import_records;
DELETE FROM historical_import_batches;
DELETE FROM contractor_statement_lines;
DELETE FROM contractor_statements;
DELETE FROM contractor_contract_items;
DELETE FROM contractor_contracts;
DELETE FROM advance_allocations;
DELETE FROM advance_payments;
DELETE FROM supplier_payments;
DELETE FROM supplier_supplies;
DELETE FROM site_movements;
DELETE FROM excavation_records;
DELETE FROM concrete_records;
DELETE FROM steel_records;
DELETE FROM finishing_records;
DELETE FROM attendance_records;
DELETE FROM workflow_actions;
DELETE FROM notifications;
DELETE FROM auth_sessions;
DELETE FROM password_reset_tokens;
DELETE FROM user_project_assignments;
DELETE FROM contractors;
DELETE FROM suppliers;
DELETE FROM projects;
DELETE FROM users
 WHERE NOT EXISTS (SELECT 1 FROM _emg_retained_users r WHERE lower("Email") = r.email);

-- Security audit history is deliberately preserved. UserId is configured
-- with SET NULL for removed users; retained users and all role/permission
-- rows are untouched.
SELECT 'retained_users' AS verification, count(*) AS rows
  FROM users u JOIN _emg_retained_users r ON lower(u."Email") = r.email;
COMMIT;
\else
ROLLBACK;
\endif
