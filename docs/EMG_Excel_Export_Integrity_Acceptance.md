# EMG P2A-0 — Excel Desktop Repair Acceptance

## Final status

**PASS for the two regenerated QA workbooks in Excel 2013.** Both opened without a current Repair dialog, Removed Records warning, Repaired Records warning, or `[Repaired]` title marker. The Product Activation Wizard overlay is an Office-environment limitation and is unrelated to workbook repair.

## Smallest reproduced defect

The previously fixed formula-cache defect remains covered. `ClearRow` in `BackEnd/src/EMG.WebApi/Modules/Reports/Services/XlsxBuilder.cs` must preserve formula cells in the template's formula columns. Otherwise a text formula cache becomes an implicitly numeric cell:

```xml
<!-- before -->
<c r="D46" s="9"><f>+D45</f><v>الكهربائى</v></c>
<!-- after -->
<c r="D46" s="9" t="str"><f>+D45</f><v>الكهربائى</v></c>
```

The same correction preserves `sheet9!D11` with cached value `عمر عزل`. Numeric formula caches are not blanket-converted to strings.

The remaining Repair was isolated to trailing whitespace in the table header cells. The old exporter wrote the template captions as inline strings without `xml:space="preserve"`. The saved table column names include the trailing spaces. The exporter now retains the template's shared-string cells and their caption text.

Before the correction, `sheet4!K9` contained:

```xml
<c r="K9" s="85" t="inlineStr"><is><t>ملاحظات </t></is></c>
```

The corrected exporter leaves the template header cell intact:

```xml
<c r="K9" s="85" t="s"><v>9</v></c>
```

The exact shared-string index is template-owned and is intentionally not regenerated. This preserves the accountant-approved Arabic labels, styles, filters, merges, and table relationships.

## Delta-debugging evidence

| Variant | Result in Excel 2013 |
|---|---|
| Original `ProjectReportTemplate.xlsx` | No Repair |
| Generated workbook before header correction | Repair involving `table2.xml`–`table7.xml` |
| Generated workbook with table calculated formulas removed only | Repair remained |
| Generated workbook with only table header rows restored from the template | No Repair |
| Corrected populated workbook | No Repair |
| Corrected near-capacity workbook | No Repair |

The fresh pre-header-correction log `<local-project-path>` listed only table repairs (`table2.xml` through `table7.xml`), with no worksheet-cell repair. That confirmed the earlier `D46`/`D11` correction and narrowed the remaining issue to the header-cell representation. No table metadata was changed.

## Automated regression coverage

Changed test coverage in `BackEnd/tests/EMG.WebApi.Tests/XlsxExportIntegrityTests.cs` verifies:

- `sheet4!D46` and `sheet9!D11` retain `t="str"` and their Arabic formula-cache values.
- Non-numeric formula caches are never emitted as implicit numeric cells.
- All six affected table sheets (`sheet4`, `sheet7`, `sheet8`, `sheet9`, `sheet10`, `sheet11`) retain 11 shared-string header cells in row 9.
- Existing capacity, summary-cell, table, financial-value, and workbook-structure tests continue to pass.

Results:

- Targeted XLSX tests: **14/14 PASS**, including the final all-six-sheet assertion.
- Full backend tests: **35/35 PASS**.
- Backend build: **PASS, 0 errors**.
- Existing non-blocking warnings: NU1903 package advisory, existing `ISystemClock` deprecation, and existing xUnit analyzer warning.

## Desktop evidence

QA files:

| Fixture | Path | SHA-256 |
|---|---|---|
| Populated | `<local-project-path>` | `7D1D1A666B33CEC1A92F6D9F2FE76330D40960A9077A7E5CE09A67D63B069CEA` |
| Near capacity | `<local-project-path>` | `4D4E365410FC11863D86819F096F0094FA308B3329F9A15632C5ACDAB9FBB219` |

Both files were opened directly in Microsoft Excel 2013 after the final regeneration. The window titles were the normal filenames with `Product Activation Failed`; neither included `[Repaired]`. The visible Excel UI exposed all 11 worksheets, including the generated populated detail rows. No current Repair or Removed/Repaired Records dialog appeared. The activation and historical Document Recovery panes were pre-existing Excel UI state and were not treated as workbook errors.

XML checks also confirmed the corrected formula-cache types and shared-string table headers in both artifacts. The QA workbooks remain outside the source repository for human review.

## Exact source changes

- `BackEnd/src/EMG.WebApi/Modules/Reports/Services/XlsxBuilder.cs`
  - Preserve template formula-cache cells during row clearing.
  - Stop rewriting table header rows as `inlineStr`; retain template shared-string cells.
- `BackEnd/tests/EMG.WebApi.Tests/XlsxExportIntegrityTests.cs`
  - Add formula-cache regressions and all affected table-header representation checks.
- `docs/EMG_Excel_Export_Integrity_Acceptance.md`
  - Record the reproducible defect, correction, artifacts, and Desktop acceptance.

No accounting rules, source templates, PostgreSQL data, Auth, Frontend, or application servers were changed. No source Git repository exists at the canonical root, so no commit or push was performed.

## Cleanup and limitations

Excel was closed after Desktop verification. Temporary inspection scripts and the temporary generator source were removed. The two QA workbooks were intentionally retained at the paths above for manual acceptance. The only Desktop limitation is the unactivated Office installation; it did not prevent opening or observing the workbook structure and did not produce a Repair dialog.

## November 16 recheck

The preserved before-fix log named Tables 2–7 and predated the two original accepted fixture hashes. Two controls made one byte-level cell replacement at `sheet4!K9` in a known-good export. All other package parts were byte-identical.

| K9 control | Excel 2013 result |
|---|---|
| Legacy inline caption with trailing space and no `xml:space` | Repair prompt. Excel reported only `/xl/tables/table2.xml`. |
| Same caption and inline representation with `<t xml:space="preserve">` | Opened normally in read-only mode. |
| Original shared-string cell | Opened normally in read-only mode. |

The current exporter already preserves the heading cells. This recheck updates the explanatory comment and strengthens `TableHeadersPreserveTemplateSharedStringCells` to check the decoded worksheet heading against the corresponding table column name for every affected header.

Fresh artifacts and the one-cell repair log are under `<local-project-path>`.

| Fresh export | SHA-256 | Excel Desktop |
|---|---|---|
| `emg-p2a0-recheck-populated.xlsx` | `747952a5aee3ccea7db5362f04838a11c1f39fa9a63e9e4dedcca6d160f6cb58` | Opened read-only without Repair. |
| `emg-p2a0-recheck-near-capacity.xlsx` | `732a0579898a0d10ddf48ef641cf233f24c31d6d2628818a30030584f92fed95` | Opened read-only without Repair. The Arabic site table filter listed the actual fixture values. |

Both contain the 11 original sheets and the two supplier sheets. XML verification matched the template's styles, seven table parts, column widths, tab colors, views, and merge ranges. Site detail sums were 300 and 4,200. Final-account expenses were 1,400 and 32,400. Owner funding was 40,000 in each fixture. At the template's saved zoom, some dates showed `####` in the inherited column width. Their underlying cells retain numeric date serials.

Final verification after the strengthened regression passed 14 of 14 XLSX tests and 35 of 35 backend tests. The backend build succeeded with zero errors. The optional PostgreSQL-backed test path was disabled. The build used a separate temporary output because the normal executable was in use.
