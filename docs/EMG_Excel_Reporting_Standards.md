# EMG Excel Reporting Standards

## Scope and evidence

This is a read-only reverse-engineering specification for future XLSX exports. No source workbook, database record, accounting rule, Backend or Frontend file was changed.

The exact consolidated reference was found at `<local-project-path>`.

- SHA-256: `1C3928C31F10C188293A300CA03CC2DAD593AE6CA7F44556B3C10290EE31AEEB`
- Worksheet order: one worksheet, `Sheet1`.
- Used range: `A1:U31`.
- The requested exact filename `191(2).xlsx` was not found. The available local reference `191.xlsx` was inspected at `<local-project-path>`; it is not asserted to be byte-identical to the unavailable `191(2).xlsx`.
- Available `191.xlsx` SHA-256: `9765E1BF00EFF4E10273C7FB67F2B3BB996B87DBBD3B2A8B9076D33570CDF335`.

The machine had no usable Python interpreter or `openpyxl` installation. An initial Excel COM read-only attempt hung while opening a workbook and was stopped. Final extraction used direct read-only parsing of the XLSX package XML, preserving formulas, cached values, style IDs, merged ranges, worksheet dimensions, RTL metadata and freeze-pane metadata. This limitation is material for theme-to-RGB conversion and must be rechecked with `openpyxl` or Excel in the export implementation CI environment.

## Consolidated statement standard

### Worksheet and print structure

| Evidence | Observed standard |
|---|---|
| `Sheet1`, `A1:U31` | One consolidated worksheet, not one sheet per project. |
| `A1:U2` merged | Top title/header band. |
| `S3:U3` merged | Right-side project/report heading band. |
| `E4:E5`, `F4:U5` merged | Column-heading structure starts at row 4 and occupies two rows. |
| `E6:U6` | Detail-table headers. The logical report columns are E through U. |
| `E7:U30` | Project/detail rows are available before the final row. |
| RTL flag | `rightToLeft=1`. Preserve RTL worksheet presentation. |
| Freeze pane | No pane coordinate was present in the extracted sheet metadata. Do not invent one. |

### Exact E:U headers at row 6

| Column | Source heading | Meaning to preserve in presentation | Status for future dynamic exports |
|---|---|---|---|
| E | `مسلسل` | Sequence | Exact label. |
| F | `اسم القطعه` | Project/plot name or code | Exact label; use project identity from the reporting DTO. |
| G | `اجمالى اعمال الموقع` | Site works aggregate | Preserve as a distinct category. |
| H | `اجمالى الخرسانة` | Concrete aggregate/diagnostic | Preserve label, but show an overlap warning; do not include automatically in an approved grand total. |
| I | `اجمالى حساب العرب` | Excavation/Arab account | Preserve established terminology and map to excavation only when classification evidence supports it. |
| J | `اجمالى حساب المقاول` | Contractor workmanship | Distinct from station/material supply. |
| K | `اجمالى حساب المحطه` | Concrete station | Distinct from contractor concrete. |
| L | `اجمالى حساب الحديد` | Steel | Distinct category. |
| M | `اجمالى اعمال المبانى` | Buildings | Distinct finishing category. |
| N | `اجمالى حساب المحاره` | Plaster | Distinct finishing category. |
| O | `اجمالى حساب العزل` | Insulation | Distinct finishing category. |
| P | `اجمالى حساب الكهرباء` | Electrical | Distinct finishing category. |
| Q | `اجمالى حساب السباكه` | Plumbing | Distinct finishing category. |
| R | `اجمالى حساب المكتب` | Office account/payment presentation | Keep separate from accrued office fee. |
| S | `اجمالى  الدفعات` | Advances/owner funding presentation | Keep receipts, allocations and cached historical value distinct. |
| T | `صافى المعاملة` | Net transaction diagnostic | Unapproved; do not present as an official balance. |
| U | `ملاحظات` | Notes | Preserve for warnings, provenance and accountant review. |

### Formula and cache evidence

The source row for `ق 191` is row 7. Examples from the source XML include:

- `G7`: formula reference to the advances sheet site value, cached `211375`.
- `H7`: formula combining a prior site-work sheet value, cached `1278550`.
- `I7`: excavation reference, cached `442500`.
- `J7`: contractor reference, cached `760298`.
- `K7`: station reference, cached `1278550`.
- `L7`: steel reference, cached `1850502.5`.
- `O7`: insulation reference, cached `64600`.
- `R7`: office reference, cached `100000`.
- `S7`: formula combining a payment value and a funding value, cached `3306802`.
- `T7`: a structured-table formula subtracting G through R from S, cached `-2679573.5`.

These formulas and cached values are evidence of the workbook's presentation behavior, not approval of its accounting meaning. External-link references and stale caches must not be copied into a new export.

### Consolidated visual language

The consolidated workbook contains 11 fonts, 6 fills, 3 borders and 21 cell formats. Important observed style IDs:

- Header cells `E6:U6` use style 9: Arial, 26pt, bold, white/theme text, theme-based fill, centered, wrapped, bordered.
- Detail sequence `E7` uses style 7: Arial, 11pt, bold, centered, numeric format 1, bordered.
- The workbook uses theme fills and theme fonts rather than only literal RGB values. The implementation must preserve theme references when cloning the template instead of approximating them.
- The source contains an explicit green fill `FFC6EFCE` in one style and theme-based fills for the main header/section hierarchy. Exact theme tint values are recorded in the template mapping document.

## Detailed project workbook standard

The available `191.xlsx` contains 11 worksheets, all marked RTL. They are not visually interchangeable and must remain separate template families:

1. `اعمال الحفر` — `D2:K29`, excavation ledger.
2. `اعمال توريد الحديد اولاد شرف` — `A2:H55`, steel supply ledger.
3. `اعمال الخرسانات` — `A2:W36`, contractor/station concrete worksheet.
4. `خاص باعمال الموقع` — `A1:K51`, site-work aggregate and detail ledger.
5. `دفعات تحت الحساب` — `A1:Q28`, owner advances and category allocations.
6. `كشف حساب نهائي` — `D4:K13`, final-account summary.
7. `اعمال المباني` — `A1:K45`, buildings contractor ledger.
8. `اعمال المحاره` — `A1:K45`, plaster contractor ledger.
9. `اعمال العزل` — `A1:K45`, insulation ledger.
10. `اعمال الكهرباء` — `A1:K45`, electrical ledger.
11. `اعمال السباكه` — `A1:K45`, plumbing ledger.

### Shared detailed ledger pattern

The finishing sheets share a visible header/summary pattern:

- Title band near row 2.
- Client/project identity around rows 5–7.
- Summary cells in J:K around rows 6–8: total payments, total work and amount due.
- Detail headings at row 9, including `رقم البند`, `التاريخ`, `نوع الاعمال`, `اسم المورد`, `الوحده`, `السعر بالجنيه`, previous/current quantities and payment columns.
- RTL layout, centered cells, wrapped title/header cells, bordered tables and colored summary cells.

The observed common style families include theme-based blue/teal fills, green `FF92D050` status/total fill, yellow `FFFFFF00` inputs, and light theme-tinted fills. The exact style IDs are workbook-specific and must be cloned from the selected template, not hard-coded globally.

### Detailed-sheet exceptions

- Excavation begins at column D and uses `D2:K29`; it has a different header geometry and a compact summary at J:K.
- Steel uses `A2:H55`, with weight, size, price, cost, notes and payments. Its cost rows use formulas such as `=SUM(E7*C7)` and the source contains a bottom summary area.
- Concrete uses `A2:W36`, extensive merged cells, separate contractor and station sections, and summary cells near rows 30–31. It must not be flattened into the finishing-sheet layout.
- The site sheet uses `A1:K51`, a combined site-work view with `اجمالي المقاول`, `اجمالي الخرسانه`, `اجمالي اعمال الموقع` and `اجمالي التكاليف` summary lines.
- The advances sheet uses category columns B:L and separate payment/date columns N:O. It is a funding/allocation schedule, not an operational-cost ledger.
- The final-account sheet uses E:K with `صادر` and `وارد` rows and a total column. It is a summary presentation and must not be used as the sole source for dynamic calculations.

## Reusable standards

1. **Template shell**: copy a verified template layout and preserve worksheet names, order, merges, dimensions, RTL, fills, borders, print configuration and title areas.
2. **Data projection**: populate values only after the canonical reporting query has resolved authorization, category, financial dimension, snapshot and provenance.
3. **Calculation separation**: exported values are final values supplied by the reporting engine. Do not retain source formulas, external links or stale calculation chains.
4. **Warning channel**: use notes and a dedicated audit/warning area for unresolved classification, H overlap, T unapproved status, office-fee ambiguity and historical-source differences.
5. **Currency and numbers**: preserve numeric cell types and the source number-format family. Never write localized currency strings into numeric cells.
6. **Dynamic rows**: expand only within safe regions after detecting reserved summary rows and merged cells. If a selected template cannot represent all rows safely, fail with a structured capacity error rather than truncating.
7. **Source references**: retain source sheet and row/cell provenance in a details/audit sheet or notes area, without importing external links.

## Exact requirements versus approved adaptations

### Exact-template requirements

- Consolidated E:U order and Arabic headings.
- RTL sheet presentation.
- Header/section color hierarchy and centered wrapped headers.
- Merged title and section bands.
- Separate detailed worksheets for site, excavation, concrete, steel, advances, final account and each finishing trade when the detailed preset is selected.
- Numeric values remain numeric and totals remain visually distinct.

### Dynamic adaptations requiring explicit implementation

- Row expansion beyond the fixed source rows.
- Multi-project reports and custom filters.
- Custom groupings/columns that do not fit E:U.
- Additional unresolved/provenance diagnostics.
- Page breaks and print areas after dynamic expansion.
- New supplier/payment sections not present in the historical detailed workbook.

These adaptations should be added as clearly labeled sections or additional detail worksheets. They must not silently overwrite template summary rows or force a custom report into the consolidated fixed-column layout.

## Acceptance criteria

1. Every exported worksheet has the expected name/order, RTL flag, title placement, merged ranges, column order and section labels.
2. Style comparison checks fonts, theme/RGB fills, borders, alignment, wrapping, number formats, row heights and column widths against the selected verified template.
3. Formula scan confirms no external links, `#REF!`, stale calculation chain or formulas copied from the source workbook.
4. Boundary fixtures at the last safe detail row pass; beyond-capacity fixtures fail clearly with no incomplete workbook returned.
5. A visual review in Excel confirms no clipped headings, overwritten subtotal rows, broken merges, `####`, missing colors or unreadable RTL layout.
6. Reconciliation checks prove each included record appears once, supplier supplies/payments remain separate, owner funding/allocations remain separate, H is not added to K, and T is labeled unapproved.
7. Authorized-project filtering is enforced server-side before export.
