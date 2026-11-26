# EMG functional integration

## Workbook upload and preview

Use multipart `POST /api/universal-import/preview` with field `file`. This requires `data.migrate`. The API accepts an original XLSX and returns a read-only preview.

- `sourceProjectCode` is the plot identifier found in the source. `projectMetadata` adds nullable `clientName`, `ownerName`, `payerName` and `location`. Display empty fields for missing evidence and allow the user to enter project metadata explicitly.
- `issues[]` contains `code`, `sheetName`, `sourceCoordinate`, nullable `recordId` and an Arabic `message`. Section issues use a range. Record issues use the source coordinate and stable preview record ID. The first codes are `SHEET_MAPPING_REQUIRED`, `ROW_MAPPING_REQUIRED` and `ROW_UNRESOLVED`.
- Show `sections`, `records`, `reconciliations` and `duplicateCandidates` alongside the issues. Summary and derived records retain their original types. Financial streams and currencies remain separate. `eligibleForPosting` describes source classification and does not grant mutation authority.
- A malformed upload returns HTTP 400. Retain any existing user inputs while displaying the server message.

The existing, project-scoped review API remains under `/api/universal-import/review`. Its preview, batch, mapping and settlement routes can be presented independently of the standalone Review Center. Applying user corrections to a new-project import and confirming that import as one atomic operation still need a server contract. Keep the legacy `/api/imports/commit-new-project` separate from the universal preview because its mapping behavior differs.

## Accounting

Final Accounts currently calculates the project office fee from its total outgoing and project percentage, and subtracts office payments to produce the office balance. The consolidated statement still has a diagnostic base. A shared accounting contract needs reconciliation of those bases before replacing either total.

For supplier-linked invoices, the source link must identify the original site transaction. Supplier liabilities and payments are account dimensions. Adding either dimension to operating costs would count the same purchase twice. The cash, credit and partial-payment projections, invoice DTOs, inline supplier creation and finishing mappings are pending implementation.

## Integration boundary

The existing contractor draft, account management and password APIs retain their current contracts. Use existing endpoints until revised contracts are verified. The frontend integration will follow the Antigravity handoff.
