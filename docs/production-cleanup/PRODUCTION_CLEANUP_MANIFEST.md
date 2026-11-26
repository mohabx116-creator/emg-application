# EMG disposable business-data cleanup manifest

Status: preparation only. No production database was modified by this change.

## Protected exactly

- The four owner-approved accounts: `mohab@emg.com`, `acc@emg.com`, `ibrahim@emg.com`, and `mohand7@emg.com`.
- Their existing password hashes, profile/account fields, active state and user IDs.
- Roles, permissions, all `role_permissions` rows (including scope and amount limits), and all retained `user_roles` rows.
- `security_audit_logs`, including rows referencing removed users (the FK is `SET NULL`).
- Database schema and migration history.
- Data Protection keys, application templates, and required static assets. Physical files are not deleted by the SQL script.

## Disposable records targeted

The script targets all rows in the business and test-operational tables: projects, suppliers, contractors, site movements, excavation, concrete, steel, finishing, supplier supplies/payments, advances/allocations, contractor contracts/items/statements/lines, historical import batches/records/events/posted entries, workflow actions, attendance, notifications, sessions, reset tokens, attachments, user-project assignments, and non-retained users.

The deletion order is explicit and follows the current EF foreign-key graph. It does not use `DROP DATABASE`, `TRUNCATE`, `TRUNCATE ... CASCADE`, sequence resets, password changes, or automatic reseeding.

## Execution contract

`BackEnd/deployment/prepare-clean-production.sql` defaults to dry-run. It prints table counts, validates that all four protected accounts exist, and rolls back. Actual execution requires a separately reviewed command using `psql -v execute=true`, a fresh verified encrypted backup, maintenance mode, and explicit owner authorization.

The script does not delete physical uploads/profile files. Those require a separate manifest of exact paths, archive verification, and owner approval.

Before any commit of a production cleanup, the script now fails closed unless it can prove that the four protected account IDs, password-hash fingerprints, role rows, permission rows, scope/amount-limit mappings, and retained user-role mappings are unchanged. It also checks every intended business table for zero rows after deletion. If a supported financial-period table exists in the final schema, it is preserved and must already contain zero disposable rows; the script never drops its schema.

## Post-cleanup acceptance

On a disposable PostgreSQL clone, verify:

1. Four retained users and their password-hash fingerprints are unchanged.
2. Role, permission, scope, amount-limit, user-role and security-audit fingerprints are unchanged.
3. All targeted business tables are empty and foreign keys validate.
4. Migration history, Data Protection keys, templates and required assets remain present.
5. Login, project creation, direct posting, reports and XLSX export work with synthetic records.
6. No historical pending record is auto-posted and no production database is touched.

## Isolated smoke-test gate

The cleanup rehearsal proves database preservation and emptiness. Before production execution, run the application smoke gate against the same cleaned disposable clone with a valid synthetic password hash: login and `/api/account/me`, project creation, direct site-work posting, authorized correction, cancellation, report read, and XLSX export. The gate must also assert that a cancelled transaction has no remaining active financial effect and that the protected fingerprints still match.

The current repository already contains focused coverage for these operations (`DirectPostingTests`, project creation tests, authentication regression tests, and XLSX/report integrity tests). Production execution remains blocked until the isolated authenticated API smoke run and a fresh verified encrypted backup are both recorded.
