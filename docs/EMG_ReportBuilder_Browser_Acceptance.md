# EMG Financial Report Builder — Browser Acceptance & QA

Date: 2026-11-15
Workspace: `<local-project-path>`

## Executive result

**Overall: BLOCKED for acceptance of the current implementation in a real browser.**

The available browser instance was safe but was built from an older QA image. Its `/api/reports/builder/preview` route returned `404`, and the frontend redirected the browser to the mandatory password-rotation page. The current canonical source was therefore not exercised through a real authenticated browser/API session.

No source correction was made during this QA pass. No permanent database was changed.

## Safety gate

### Official launcher

`<local-project-path>` was inspected but **not executed**.

Configured ports from the launcher:

- Frontend: `http://localhost:3100`
- Backend: `http://localhost:5121`
- Frontend proxy target: `http://localhost:5121`

The launcher starts `dotnet run --launch-profile http` from `BackEnd\src\EMG.WebApi` and `npm run start -- --host 127.0.0.1` from `FrontEnd`.

### Startup write risk

`BackEnd/src/EMG.WebApi/Program.cs` calls `await app.SeedSecurityAsync()` before serving requests. `SecuritySeeder` can add permissions, create/update roles, create the administrator through the bootstrap path, add role permissions, and call `SaveChangesAsync()`.

Because the launcher uses the local configuration and its normal database target is not proven to be disposable, starting it against the permanent EMG environment would violate the database-safety gate. It was not started.

### Existing sessions and containers

At inspection time:

- No listener existed on the launcher ports `3100` or `5121`.
- Existing safe QA frontend: `localhost:3185`.
- Existing safe QA backend: `emg-auth-v2-local-backend`.
- Existing safe QA database: `emg-auth-v2-cutover-restore-postgres`, database `emg_restore`.
- QA PostgreSQL had no published host port.
- QA PostgreSQL used volume `emg-auth-v2-cutover-restore-pgdata`.
- Permanent PostgreSQL was separate: `emg-postgres`, published on `5432`, volume `emg-application_emg_postgres_data`, network `emg-application_default`.

No duplicate EMG processes were found and none were terminated. No visible launcher terminals were opened because the safety gate prevented launcher execution.

## Actual browser/API evidence

Browser URL tested:

`http://localhost:3185/emg/reports/builder`

Observed result:

- Browser redirected to `http://localhost:3185/account/password-change`.
- The page displayed the mandatory password-rotation screen.
- The available QA backend returned `404` for `POST http://localhost:3185/api/reports/builder/preview`.
- Container inspection showed the backend was connected to `restore-postgres`, database `emg_restore`, not the permanent database.

This is evidence that the running QA image predates the Report Builder endpoint. It is not evidence of a defect in the current canonical source.

## Scenario results

| Scenario | Result | Evidence / blocker |
|---|---|---|
| Single-project detailed report | BLOCKED | Current browser instance is old and forced password rotation. |
| Multiple-project consolidated report | BLOCKED | Current Report Builder endpoint was not present in the running image. |
| All accessible projects | BLOCKED | No authenticated current-code browser session. |
| One category across projects | BLOCKED | Current endpoint unavailable in safe running instance. |
| One contractor/supplier across projects | BLOCKED | Current endpoint unavailable in safe running instance. |
| Date-range filtering | BLOCKED | Not executable against current UI/API. |
| Imported vs current snapshot | BLOCKED | Not executable against current UI/API. |
| Multiple simultaneous filters | BLOCKED | Not executable against current UI/API. |
| Grouping dimensions | BLOCKED | Not executable against current UI/API. |
| Column selection/hiding/reordering/sorting | BLOCKED | Not executable against current UI/API. |
| Summary drill-down/source reference | BLOCKED | Not executable against current UI/API. |
| Preset switching | BLOCKED | Not executable against current UI/API. |

## Financial and authorization checks

## Rerun: canonical runtime with disposable QA (2026-11-15)

The earlier blocked result above was superseded by this controlled rerun. The canonical launcher was used after replacing only its parent-process environment for the disposable QA database; the launcher file itself was not changed.

### Runtime and isolation

- Launcher: `<local-project-path>`
- Frontend: `http://localhost:3100`
- Backend: `http://localhost:5121`
- QA PostgreSQL: `127.0.0.1:5433`, database `emg_builder`, container `emg-report-builder-qa-postgres`
- QA volume: `emg-report-builder-qa-pgdata`
- Permanent `emg-postgres` remained separate and was not queried or modified.
- The launcher opened visible split terminals: frontend above, backend below.
- Only the intended QA frontend/backend listeners were used on 3100/5121. The pre-existing old QA resources on 3184/3185 were left untouched.
- Startup migrations and security bootstrap were applied only to the disposable QA database before browser testing.

### Reproduced defect and scoped correction

The first browser login failed because the backend CORS policy did not allow credentials while the frontend sends cookie/CSRF requests with credentials. The direct API login worked, but the browser could not retain the authenticated session. The minimal correction was:

- `BackEnd/src/EMG.WebApi/Program.cs`: add `.AllowCredentials()` to the existing localhost CORS policy.

This is an immediate browser/API integration defect; it does not change permissions, financial rules, or database schema.

### Actual browser/API results

| Scenario | Result | Evidence |
|---|---|---|
| Fresh admin login | PASS | Browser reached `/dashboard/default` after login against port 5121. |
| Mandatory password rotation gate | PASS | Initial login reached `/account/password-change`; operational screens were not exposed. |
| Successful rotation and fresh login | PASS | Official change-password API in QA returned success; database flag became false; a new browser session reached the dashboard with the new credential. |
| Report Builder route | PASS | Browser opened `/emg/reports/builder` and rendered the Arabic RTL builder UI. |
| Empty preview | PASS | `POST /api/reports/builder/preview` returned 200 with two summary rows after synthetic projects were created; no mutation occurred from preview. |
| Synthetic project creation | PASS | Two QA-only projects were created through `POST /api/projects`; IDs were 1 and 2 in the disposable database. |
| Workflow transition | PASS | One synthetic site movement was created, then submitted, approved, and posted through the existing workflow API. All three responses were 200. |
| Current snapshot | PASS | Preview returned one posted Alpha movement with current amount 1,250; Beta remained 0. |
| Category filter | PASS | Filtering by the canonical category key `SiteExpense` returned one summary and one transaction with current amount 1,250. |
| Multi-project/date/dimension filters | PASS | Query over both projects, November date range, and `operational-cost` returned one matching transaction and current amount 1,250. |
| Grouping and warnings | PASS | UI displayed project/category controls and the explicit H-overlap, T-unapproved, and office-dimension warnings. |
| Unresolved records visibility | PASS | UI defaulted to showing unresolved records; the API contract returned unresolved amount/count fields separately. No unresolved synthetic rows existed. |
| H/K overlap protection | PASS | UI/API warnings state that H is diagnostic and no combined official total is produced. No overlapping financial total was asserted. |
| T handling | PASS | UI/API warnings state that T is unapproved and is not shown as an official balance. |
| Office payments/accruals | PASS | UI/API warnings keep office dimensions separate; no synthetic office rows were present. |
| Supplier/contractor party filters | BLOCKED | No supplier/contractor fixture was created in this QA pass, so no positive party result was claimed. |
| Imported vs current snapshots | BLOCKED | Synthetic records were live-only; no workbook import fixture was loaded into QA. |
| Column hide/reorder/sort | PASS (UI contract) | Builder rendered independent column controls and the ordering instruction. A persisted reorder was not asserted because the UI control is action-based and no data fixture required it. |
| Drill-down/source reference | BLOCKED | No imported source rows were available in QA; no source reference was fabricated. |
| Unauthorized project isolation | BLOCKED | Admin has global access. A restricted synthetic user was not provisioned, so a positive cross-account denial was not claimed. Server-side authorization remains enforced by the controller/service path. |
| Refresh/logout/back-forward/error states | PARTIAL | Login/forced-rotation redirect and direct Report Builder navigation passed. Full logout/back-forward/error matrix was not completed in this run. |

### Financial and database safety result

- Preview requests were read-only; no financial rows were modified by report queries.
- Synthetic writes were limited to the disposable QA database and consisted of two projects, two site movements, and one workflow transition chain.
- No permanent PostgreSQL connection, migration, seed, or account update was used.
- The canonical source is not a Git repository at the root, so no commit SHA is available. The only source file changed in this QA correction was `BackEnd/src/EMG.WebApi/Program.cs`.

### Verification commands

- `dotnet build EMG.slnx --no-restore --verbosity minimal` — PASS, 0 errors.
- `dotnet test tests/EMG.WebApi.Tests/EMG.WebApi.Tests.csproj --no-restore --verbosity minimal` — PASS, 24/24.
- `npx tsc --noEmit` — PASS.
- `npm run build` — PASS.
- `npm run test:password-rotation` — PASS, 24 assertions.
- Targeted ESLint on `src/Modules/EMG/ReportBuilder/index.tsx` — PASS with 0 errors and 55 existing Prettier warnings.

### Remaining blockers

The Report Builder is now verified through the canonical runtime for authenticated live-only synthetic data. Full acceptance remains **PARTIAL**, not PASS, until QA fixtures cover supplier/contractor parties, imported historical provenance, unresolved classifications, a restricted user assignment, and the remaining browser navigation/error matrix.

## Follow-up acceptance attempt (2026-11-15)

### CORS verification

`Program.cs` uses an explicit `WithOrigins(...)` allow-list and `.AllowCredentials()`. It does not use `AllowAnyOrigin()` or reflect arbitrary origins. The existing cookie/CSRF flow remains compatible with these explicit frontend origins. No CORS change was required in this attempt.

### Isolation gate result

**HARD STOP / BLOCKED.** A new disposable QA PostgreSQL container, volume, and network were created and migrations were applied before application startup. The official launcher was reused on ports 3100/5121 with QA settings supplied in the same CMD session. However, the running backend could not be proven to authenticate against the QA database: the synthetic QA account was present in the QA database, while the application login did not accept the known QA credential. Because the effective connection target could not be conclusively established, no further fixtures or browser assertions were run.

The temporary QA processes and resources were stopped/removed. The permanent `emg-postgres` container was not stopped or modified. No permanent database writes were performed.

### Current consolidated status

**PARTIAL** — the prior authenticated live-only synthetic acceptance remains valid, but the newly requested supplier/contractor, imported-provenance, restricted-user, unauthorized-filter, and full browser interaction scenarios remain unverified. They are not reported as PASS.

## QA runtime isolation investigation (2026-11-15)

### Configuration precedence

The effective database key is `ConnectionStrings:EmgDb`, supplied by the environment as `ConnectionStrings__EmgDb`.

Observed precedence for this application is:

1. `appsettings.json` (`ConnectionStrings:EmgDb` is empty).
2. `appsettings.Development.json` (local fallback points to the permanent 5432 target).
3. environment variables (`ConnectionStrings__EmgDb`) — highest priority when they reach the `dotnet` process.
4. `launchSettings.json` contributes `ASPNETCORE_ENVIRONMENT=Development` and the HTTP URL; it does not define or override the database connection.

`Program.cs` calls `AddInfrastructure(configuration)`, which reads `GetConnectionString("EmgDb")`, and then calls `SeedSecurityAsync()` before serving requests. Therefore the connection must be proven before startup seeding is allowed.

### Root cause

The original uncertainty had two separate causes:

1. The QA database migration command was attempted before the disposable PostgreSQL instance was fully ready, leaving the QA database without tables. The backend did reach QA, then exited during `SecuritySeeder` with `42P01: relation "permissions" does not exist`. The login failure was therefore not proof of a wrong database.
2. `run-emg.bat` launches its panes through `wt.exe`. A parent CMD environment override is not a reliable way to configure a newly created/reused Windows Terminal pane. The attempted inline forwarding also exposed a command-line parsing hazard because PostgreSQL connection strings contain semicolons, which Windows Terminal treats as command separators in this construction.

### Conclusive isolation evidence

After waiting for `pg_isready` and applying all EF migrations to the disposable QA database:

- QA database identity: `emg_builder`, user `qa_builder`, 25 public tables.
- The manually started backend with the exact same application configuration reached the QA schema, completed security seeding, and listened on 5121.
- `pg_stat_activity` showed backend sessions for `emg_builder`/`qa_builder` with client address `172.23.0.1`.
- Windows TCP inspection for the backend PID showed an established connection to `127.0.0.1:5433` and no 5432 connection.
- Permanent `emg-postgres` remained separate on host port 5432 and was not selected or modified.

This proves the application can be isolated safely once the QA database is migrated before startup.

### Source change and remaining launcher limitation

`run-emg.bat` was minimally updated to accept an optional `EMG_CONNECTION_STRING` and inject it into the backend pane. This change preserves the normal fallback when the variable is absent, but the direct semicolon-bearing value still needs a safer launcher transport (for example, a non-project external connection file read by the pane) before it can be certified as the standard QA mechanism. No production run was attempted with the override.

The QA backend/frontend processes and disposable QA resources were stopped/removed after the proof. No browser matrix was continued because the official launcher transport was not yet certified end-to-end.

### Final outcome of this investigation

**QA DATABASE ISOLATION: PROVEN for the manually started diagnostic backend.**

**OFFICIAL LAUNCHER QA OVERRIDE: NOT YET CERTIFIED.** The launcher still starts the frontend pane but the backend pane exits when the optional connection transport is supplied. The visible diagnostic backend command was therefore used only to capture the actual startup error and prove the target connection; it was not used to continue browser acceptance.

The remaining launcher defect is confined to Windows Terminal command transport/redirection around the optional override. It is not a database or authentication defect. The official default launcher behavior was not used against the permanent database during this investigation.

### Independently verified from isolated tests/source

- Contractor concrete and station concrete are distinct classifications in the current classification service.
- H is returned as a diagnostic aggregate and is not used as an official grand total.
- T remains null/unapproved in the consolidated projection.
- Office payments, advance allocations, supplier supplies, and supplier payments are represented as separate dimensions.
- Unresolved records remain visible when `IncludeUnresolved=true` and are marked incomplete.
- Project visibility is filtered server-side before classification/report projection.
- SQLite report-builder tests verified unauthorized project exclusion, composed filters, snapshot dimensions, unresolved rows, and no database mutation.

These checks are not a substitute for live browser acceptance.

### Not verified in this pass

- Real current-database H/K/T reconciliation through the Report Builder endpoint.
- Real browser permission denial for unauthorized project IDs.
- Live drill-down/source reference behavior.
- Live responsive layout, sticky headers, and horizontal scrolling.
- Live reconciliation of all current project totals.

## Automated verification

Executed against isolated test data/source:

- Backend tests: **24 passed, 0 failed**.
- Backend build: **PASS**, 0 errors.
- Frontend TypeScript: **PASS**.
- Frontend production build: **PASS**.
- Password-rotation contract: **24 assertions passed**.
- Targeted Report Builder ESLint: **0 errors, 55 formatting warnings**. Warnings are Prettier formatting diagnostics; no functional lint errors were reported.

The tests used SQLite fixtures and did not write to `emg-postgres`.

## Files modified by the implementation before this QA pass

The acceptance pass itself made no source changes. The Report Builder implementation under review consists of:

- `BackEnd/src/EMG.WebApi/Modules/ReportBuilder/DependencyInjection.cs`
- `BackEnd/src/EMG.WebApi/Modules/ReportBuilder/Dtos/ReportBuilderDtos.cs`
- `BackEnd/src/EMG.WebApi/Modules/ReportBuilder/Services/IReportBuilderService.cs`
- `BackEnd/src/EMG.WebApi/Modules/ReportBuilder/Services/ReportBuilderService.cs`
- `BackEnd/src/EMG.WebApi/Modules/ReportBuilder/Controllers/ReportBuilderController.cs`
- `BackEnd/src/EMG.WebApi/Program.cs`
- `BackEnd/tests/EMG.WebApi.Tests/ReportBuilderTests.cs`
- `FrontEnd/src/Modules/EMG/ReportBuilder/index.tsx`
- `FrontEnd/src/api/emg.ts`
- `FrontEnd/src/routes/MainRoutes.tsx`
- `FrontEnd/src/menu-items/emg.ts`

## Remaining blockers and next safe action

1. Build a disposable QA backend image from the current canonical source and attach it only to the existing isolated `emg-auth-v2-cutover-restore-net`, or use a separately verified disposable QA database.
2. Serve the current frontend through the official launcher configuration only after the database connection is explicitly pointed at that disposable QA database and the startup seeder write scope is accepted for QA.
3. Run the authenticated Playwright/browser matrix and capture sanitized screenshots.
4. Do not run the current `run-emg.bat` against the permanent EMG database until startup seeding is disabled or the database target is proven disposable.

## Final runtime report (updated rerun)

- Batch file used: `<local-project-path>`, executed with temporary process-level QA connection settings; the file was not edited.
- Backend port: `5121`.
- Frontend port: `3100`.
- Existing sessions found: old QA Docker services on `3184/3185`; no prior listener on the canonical launcher ports.
- Duplicate/stale sessions closed: only the temporary canonical QA launcher instance was stopped after verification; unrelated services and the old QA containers were not touched.
- Visible terminal windows used: yes, the official launcher split frontend/backend terminals.
- Browser URL tested: `http://localhost:3100/emg/reports/builder`.
- Database safety: `emg-report-builder-qa-postgres` on `5433` was the only database used by the canonical QA process; permanent `emg-postgres` on `5432` was not accessed or modified.
- Final application process status: canonical QA frontend/backend were stopped after evidence capture; the disposable QA PostgreSQL container, volume, and network were removed. Pre-existing old QA containers were left untouched. Permanent `emg-postgres` remained running with its original volume.

## Final canonical launcher QA certification (2026-11-15)

### Isolation and launcher correction

- **Status: PASS for launcher/database isolation.** The remaining transport defect was in Windows Terminal pane argument propagation: passing the connection string or PowerShell arguments through `wt.exe` was not reliable, and semicolons could be parsed as command separators.
- `run-emg.bat` now keeps normal mode unchanged and, only when `EMG_QA_MODE=1`, calls an external temporary bootstrap `.cmd` path. The bootstrap reads an ACL-restricted JSON file outside the repository, validates the target, and sets `ConnectionStrings__EmgDb` inside the helper process before starting the backend. No raw connection string is placed in the `wt.exe` command line.
- Official launcher ports: frontend `3100`, backend `5121`.
- Official-launcher backend PID during proof: `23356`.
- TCP evidence: the backend had an established connection to `127.0.0.1:5433`; no connection to `5432` was present.
- PostgreSQL evidence: `current_database=emg_builder`, `current_user=qa_builder`, public table count `25`.
- QA topology: disposable container `emg-report-builder-qa-postgres`, dedicated network `emg-report-builder-qa-net`, dedicated volume, host binding only on `127.0.0.1:5433`. Permanent `emg-postgres` remained outside the QA process and unchanged.
- Fail-closed helper checks: missing configuration exited `10`; invalid JSON exited `11`; neither test emitted secret values.
- CORS review: `Program.cs` uses explicit localhost origins and `.AllowCredentials()`; no wildcard or arbitrary-origin reflection was found. CSRF and cookie authentication remained active during the browser test.

### Browser evidence

| Scenario | Result | Evidence |
|---|---|---|
| QA admin login with initial bootstrap credential | PASS | Browser reached `/account/password-change`. |
| Mandatory password-rotation gate | PASS | Operational screens were not shown before rotation. |
| Password rotation and session replacement | PASS | API sequence: old session became unusable after change; fresh login with rotated QA credential returned `200`; `/api/account/me` returned `mustChangePassword=false`. |
| Dashboard after fresh login | PASS | Browser reached `/dashboard/default`. |
| Direct Report Builder URL | PASS | Browser reached `/emg/reports/builder`. |
| Read-only preview | PASS | Preview completed with explicit warnings, `0` totals and `0` movements; no error UI appeared. |
| Permanent database protection | PASS | Runtime TCP and PostgreSQL evidence showed QA only; no `5432` connection from the backend. |
| Supplier/contractor, multi-project, restricted-user, unresolved, provenance and rich subtotal scenarios | NOT TESTED | The isolated database used for this certification contained only the synthetic bootstrap account and schema, not the required business fixtures. No production values were used or fabricated. |

### Current status

**PARTIAL.** Launcher/database isolation and the authentication-gated browser smoke path are certified. The remaining data-rich Report Builder matrix still requires controlled QA fixtures for multiple projects, suppliers/contractors, imported provenance, unresolved classifications, and permission boundaries. This report does not claim those scenarios passed.

### Cleanup and boundaries

- Temporary helper/configuration files were outside the project and are removed after QA cleanup.
- No permanent PostgreSQL migration, seed, account update, financial write, deployment, or source clone was performed.
- The only canonical source change in this task is the scoped QA transport/fail-closed path in `run-emg.bat`; Report Builder accounting logic was not changed.
