# EMG functional integration

## Workbook upload and preview

Use multipart `POST /api/universal-import/preview` with field `file`. This requires `data.migrate`. The API accepts an original XLSX and returns a read-only preview.

- `sourceProjectCode` is the plot identifier found in the source. `projectMetadata` adds nullable `clientName`, `ownerName`, `payerName` and `location`. Display empty fields for missing evidence and allow the user to enter project metadata explicitly.
- `issues[]` contains `code`, `sheetName`, `sourceCoordinate`, nullable `recordId` and an Arabic `message`. Section issues use a range. Record issues use the source coordinate and stable preview record ID. The first codes are `SHEET_MAPPING_REQUIRED`, `ROW_MAPPING_REQUIRED` and `ROW_UNRESOLVED`.
- Show `sections`, `records`, `reconciliations` and `duplicateCandidates` alongside the issues. Summary and derived records retain their original types. Financial streams and currencies remain separate. `eligibleForPosting` describes source classification and does not grant mutation authority.
- A malformed upload returns HTTP 400. Retain any existing user inputs while displaying the server message.

The existing, project-scoped review API remains under `/api/universal-import/review`. Its preview, batch, mapping and settlement routes can be presented independently of the standalone Review Center. Applying user corrections to a new-project import and confirming that import as one atomic operation still need a server contract. Keep the legacy `/api/imports/commit-new-project` separate from the universal preview because its mapping behavior differs.

## Accounting

Final Accounts and the consolidated statement use a shared calculation for the office fee and outstanding fee. In the consolidated response, use `officeAccrual.officeFee` and `officeAccrual.officeOutstanding` when `isOfficial` is true. The base includes each recognized operational source row once. Owner funding, funding allocations, supplier liabilities and supplier payments are separate dimensions. With unresolved operational classification, the official values are null and only the diagnostic amount is available. The net-transaction calculation remains subject to source reconciliation.

For supplier-linked invoices, the source link must identify the original site transaction. Supplier liabilities and payments are account dimensions. Adding either dimension to operating costs would count the same purchase twice. Supplier invoices are currently represented as one or more `SupplierSupply` lines sharing an invoice number; when the caller omits the number, the backend generates an `AUTO-<project>-<supplier>-<unique>` number. Authorized users can create a supplier and its first direct-posted line atomically through `POST /api/suppliers/inline-supply` with the `suppliers.supplies-create` permission. Linked supplier payments re-check the remaining payable inside a serializable transaction, so concurrent payments cannot over-settle a supply. A canonical link to a site transaction, multi-attachment invoice metadata and finishing-specific supplier linkage still require separate verified contracts.

## Integration boundary

The existing contractor draft, account management and password APIs retain their current contracts. Use existing endpoints until revised contracts are verified. The frontend integration will follow the Antigravity handoff.
