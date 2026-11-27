# Historical recovery checkpoint

## Applied locally

The approved target is the existing loopback PostgreSQL database on port 55464, database `emg_restore`. The recovery dump is under the authorized backup root in `historical-p0-20261127-013718`. The archive inventory, complete SQL extraction and copied SHA-256 were verified. No test restore was performed. Original workbooks, mapping files, reconciliation figures and database dumps are private.

The office-rate pass matched the 38 project workbook hashes to existing posted batches. It corrected 34 imported rates and verified 38 matches. Commit `ab72b48` preserves this stage.

The funding schema migration and funding pass have been applied locally. The pass preserved 1,605 original schedule components, including 997 recognized allocation components. It matched all 461 pre-existing backfill receipts, captured their previous rows, corrected their dates and narratives, and inserted 132 main-schedule entries. Transfers and unsupported settlements retain separate dispositions. The project-level allocation pool does not assign a fictitious receipt or represent custody. Historical receipt dates may be null. Future operational entries retain ordinary validation.

## Replay and rollback

Use the original package with its `Index/workbooks.csv` and the Technical Lead's `07_project_mapping.json` beside the other mapping files. The tools require Python 3.11+ with openpyxl. Apply the reviewed EF migrations through `HistoricalFundingSchedules` before the funding pass. Run `office_rates.py` and then `funding.py` in `BackEnd/tools/HistoricalRecovery`, with positional arguments for the package directory and mapping JSON and the existing container name via `--container`. By default each executes a transaction ending in rollback. To persist verified changes add `--apply --backup <custom-format dump> --backup-sha256 <verified digest>`. Local transport is pinned to the existing Docker named pipe and published port. The source readers and PostgreSQL transaction scripts are reusable. A future server deployment requires its own authorized target, backup and transport.

On an execution error PostgreSQL rolls back the transaction. For recovery after commit, preserve intervening genuine activity first. For a full rollback at the backup cutoff, stop application writes, verify the archive checksum, and restore the complete pre-repair archive using PostgreSQL's restore tooling under explicit restore authorization. A full restore overwrites later activity. For a selective rollback use the rate and receipt recovery tables, inspect subsequent edits and dependencies, and reverse only unchanged recovery effects in a transaction.

## Remaining acceptance

### Work extraction and posting rehearsal

The work ledger migration has been applied on the approved local database. The verified pre-change custom-format archive and checksum are under the authorized backup root in `historical-works-20261127-021333`. PostgreSQL read the archive inventory and extracted its entire SQL stream successfully.

Run `works.py` with the same package, mapping and local container arguments as the funding pass. The rollback rehearsal validated 3,644 work lines across 36 nonempty project workbooks and linked all 2,134 earlier operational work postings by their recorded target and exact original coordinate. The extractor checked all 38 workbook identities. Two acceptance workbooks match their complete work controls. The ledger retains formula and cached-control evidence, including the known stale site control and repeated control reference. Ordinary replay checks the stored payload against the source.

### Counterparty rehearsal

Commit `aa392dc` contains the work posting mechanism. The account settlement schema is applied. A combined rollback rehearsal linked 2,047 supplier and 407 contractor work lines, preserved 279 settlement components and assigned 130 of them to evidenced parties. Unassigned payments remain outside named account balances. The separate statement workbook identifies a plot without an exact matching project code. The original account and funding schedules remain independent. The backend builds successfully.

Run `accounts.py` with the same arguments after `works.py`. Replays check the source and existing links. The historical-account routes under suppliers and contractors project the same work lines and identified settlements by project and currency. Next integrate project financials and exports, apply and replay the ledgers, and reconcile all application surfaces. Both financial rehearsals rolled back.

### Financial projections and workbook exports

Commit `cb98058` preserves the counterparty rehearsal. Final accounts now aggregate new work with the existing operational rows, use the recovered categories for linked finishing work, and report currency-specific cash, reference transfers, office balances and section allocations. The project report includes original work and settlement rows with their coordinates. Both XLSX export paths include uncapped historical detail. The standard template also populates the separate station and workmanship blocks. The backend built, and all 18 active XLSX integrity tests passed. Next apply the verified work and account passes, replay both, and compare source rows, project totals, live API, UI and exported workbooks.

### Applied work and accounts

Following `3bb9385`, the approved database committed 3,644 source work lines and 279 settlements. The pass linked 2,454 works and 130 settlements to named counterparties, created 19 supplier and 15 contractor accounts, and deactivated 43 unused legacy supplier identities. All work and account inserts and updates were zero on the second, rolled-back run. The full before/after row fingerprints matched for all 38 projects, 593 receipts, 1,605 funding lines, 2,059 site movements and 75 finishing records. There were 39 signed negative work components. The API and frontend started on their designated local ports.

Next reconcile all 38 source identities with the live projections and exported files, check both detailed cases and document open funding and counterparty evidence. Preserve the independent frontend work.

### Funding controls and export acceptance

After the work and account commit, all 38 source hashes were reconciled. The source control investigation found four verified zero schedules, four recognized allocation-funded accounts and one conflicting worksheet owner. The other 29 use main-schedule evidence. Nine main schedules contain outstanding evidence questions affecting fourteen components. Thirty-five source work controls agree directly, with three explained differences involving a repeated reference, omitted work blocks and a stale cached total. Unassigned counterparty settlements remain open.

Run funding_controls.py using the same arguments as works.py. It verifies the workbook and portfolio ownership evidence, signed components, subtotal ranges, and the original net calculation. The verified local database has nine control records and two qualifications. A replay inserted zero rows. The receipts and allocation components were preserved. The copied payment schedule remains available as evidence. The recognized ownership and office supplement are applied only in financial projections.

The ReconcileImport tool accepts --historical-reports followed by the private mapping path and a loopback-only connection on the approved port. It reads a repeatable snapshot, renders all 38 project XLSX files in memory, checks original rows and coordinates, verifies source-backed controls, and compares final-account works, funding, cash and office cells. All passed. Twenty-three active project and XLSX tests passed, with one optional test skipped. Next verify the latest live project views and record the remaining role-session and source limitations.

### Project and section displays

The projects.view payload carries project values, category-level section figures, funding evidence status and scoped warnings. The project overview and section shells read that assigned-project payload. Detailed reports retain their reports.view permission. Project cards and ordinary dashboards render known works, payments and cash separately. The frontend type check passed. Administrator API access covered 38 projects and browser checks confirmed the reference project overview and explicit zero receipt response. The current primary database has no active engineer assignment for a positive signed-in engineer check. One funding schedule has conflicting ownership, fourteen main-schedule components require independent receipt evidence, and 149 settlements lack a named beneficiary.

### Operational Advances and consolidated reconciliation

The Advances ledger now returns actual receipts, original funding rows, independent project allocations, recorded direct owner payments and currency-specific totals. Allocation-funded projects use the same individual signed components as the canonical account. Source controls and transfers are retained as evidence. Only an existing recovery mapping links a row to a receipt. Conflicting ownership leaves the affected cash balance unknown. The consolidated statement and report classifications include recovered work, allocations, funding and identified supplier settlements.

The 38-project read-only check compares operational Advances, the consolidated statement, category projections, original source references and the actual exported project workbooks. It also compares two read-only views of one existing posted receipt and verifies its exact funding and cash delta, fixed works, allocations and office balance. No test receipt was posted. The primary database retains 593 advance rows, 997 historical allocation components and all 1,605 original funding components.

The authenticated browser showed the dated source row on the allocation-only reference project with matching funding, allocation and cash. The approved three-card header, responsive receipt card grid, navigation, spacing and entry dialog were restored after that check. The final dialog matches the previous file and TypeScript passed. The engineer permission contract passed a scoped controller test. A positive live engineer session still requires an existing project assignment. Final visual inspection of the restored cards remains outstanding after the Windows browser tool could not reliably identify its URL. Resolve the recorded ownership conflict and unsupported funding or beneficiary relationships from independent business evidence.

### Subsequent contractor payments

The payment limit now uses the remaining net entitlement under the existing serializable transaction. Two service-level regression cases settle 100 in installments of 30 and 70, reject a further 0.01 and verify unchanged works. Only a direct owner settlement contributes to project funding. Live write verification remains pending.

### Ordinary workbook import and scoped transaction tests

The approved daily new-project flow accepts a reviewed file whose SHA-256 matches the locally configured approved workbook on the development loopback target. The historical migration switch remains separate. The focused suite passed 39 cases covering file and target constraints, validation, assigned-project access, CRUD, supplier settlement, contractor installments and allocation contention. A genuine new workbook has not been committed to the operational database. This and a live assigned-engineer session remain owner acceptance items.

### Operational section and party detail

The five operational section readers expose the signed source rows and preserve existing target links. The read-only verification matched the complete original source identities and signed amounts across 190 section reads and the named supplier and contractor accounts. No traditional records were inserted. The backend build and frontend type check passed; live browser checks are tracked separately.

### Operational review navigation

Both route tables and dashboard links retire the standalone review center. Project creation and notification captions use the direct operational workflow, while historical action records remain accessible in their transaction context. TypeScript passed. A browser check of the legacy URL remains in the local acceptance queue.

### Unified project advances

The scoped ledger returns fundingEntries and allocationEntries. Recognized source components, direct owner settlements and ordinary advances retain their distinct identities. Allocations carry an actual receipt or transaction link only when recorded. Legacy list navigation resolves to project advances. Source references remain available independently. The primary report verifier read all project ledgers and matched the signed funding and allocation schedules. The working frontend type check passed.

### Primary workbook schedules

The approved template expands work, receipt and allocation rows with original references, dates, currencies and signed amounts. The independent primary table check reopened all 38 generated workbooks and matched their source work identities, signed values, receipt and allocation schedules, finishing controls and printed category totals. Twenty active XLSX integrity tests passed. One optional private legacy fixture was skipped. Disputed ownership remains excluded from recognized schedules.
