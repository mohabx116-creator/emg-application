# Historical recovery checkpoint

## Applied locally

The approved target is the existing loopback PostgreSQL database on port 55464, database `emg_restore`. The recovery dump is under the authorized backup root in `historical-p0-20261127-013718`. The archive inventory, complete SQL extraction and copied SHA-256 were verified. No test restore was performed. Original workbooks, mapping files, reconciliation figures and database dumps are private.

The office-rate pass matched the 38 project workbook hashes to existing posted batches. It corrected 34 imported rates and verified 38 matches. Commit `ab72b48` preserves this stage.

The funding schema migration and funding pass have been applied locally. The pass preserved 1,605 original schedule components, including 997 recognized allocation components. It matched all 461 pre-existing backfill receipts, captured their previous rows, corrected their dates and narratives, and inserted 132 main-schedule entries. Transfers and unsupported settlements retain separate dispositions. The project-level allocation pool does not assign a fictitious receipt or represent custody. Historical receipt dates may be null. Future operational entries retain ordinary validation.

## Replay and rollback

Use the original package with its `Index/workbooks.csv` and the Technical Lead's `07_project_mapping.json` beside the other mapping files. The tools require Python 3.11+ with openpyxl. Apply the reviewed EF migrations through `HistoricalFundingSchedules` before the funding pass. Run `office_rates.py` and then `funding.py` in `BackEnd/tools/HistoricalRecovery`, with positional arguments for the package directory and mapping JSON and the existing container name via `--container`. By default each executes a transaction ending in rollback. To persist verified changes add `--apply --backup <custom-format dump> --backup-sha256 <verified digest>`. Local transport is pinned to the existing Docker named pipe and published port. The source readers and PostgreSQL transaction scripts are reusable. A future server deployment requires its own authorized target, backup and transport.

On an execution error PostgreSQL rolls back the transaction. For recovery after commit, preserve intervening genuine activity first. For a full rollback at the backup cutoff, stop application writes, verify the archive checksum, and restore the complete pre-repair archive using PostgreSQL's restore tooling under explicit restore authorization. A full restore overwrites later activity. For a selective rollback use the rate and receipt recovery tables, inspect subsequent edits and dependencies, and reverse only unchanged recovery effects in a transaction.

## Remaining acceptance

Complete work-family posting, counterparty obligations and settlements, categorized exports and 38-project source/DB/API/UI/XLSX reconciliation. Review unresolved funding bases individually, including allocation-only workbooks. The operational historical balances have not passed acceptance. Keep the existing project identities and the independent visual-design changes. The final-account API exposes per-section works, allocated funding and their difference by currency.
