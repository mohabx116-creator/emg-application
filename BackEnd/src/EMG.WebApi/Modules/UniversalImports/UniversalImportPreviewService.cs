using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using ClosedXML.Excel;
using EMG.Api.Modules.Imports.Services;

namespace EMG.Api.Modules.UniversalImports;

public interface IUniversalImportPreviewService
{
    Task<UniversalPreviewResponse> PreviewAsync(IFormFile file, CancellationToken cancellationToken);
}

public sealed partial class UniversalImportPreviewService(UniversalImportMappingRegistry mappings) : IUniversalImportPreviewService
{
    private const long MaxWorkbookBytes = 20 * 1024 * 1024;

    public async Task<UniversalPreviewResponse> PreviewAsync(IFormFile file, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(file);
        if (file.Length <= 0 || file.Length > MaxWorkbookBytes)
            throw new InvalidOperationException("The workbook must be between 1 byte and 20 MB.");

        await using var input = file.OpenReadStream();
        await using var memory = new MemoryStream();
        await input.CopyToAsync(memory, cancellationToken);
        memory.Position = 0;
        var sha = Convert.ToHexString(await SHA256.HashDataAsync(memory, cancellationToken));
        memory.Position = 0;

        using var workbook = new XLWorkbook(memory);
        var workbookCurrencies = WorkbookCurrencyDeclarations.OfWorkbook(workbook).Select(item => item.Code).Distinct().ToArray();
        var sections = new List<UniversalSectionPreview>();
        var records = new List<UniversalRecordPreview>();
        var reconciliations = new List<UniversalReconciliationPreview>();
        var exceptions = new List<string>();

        foreach (var sheet in workbook.Worksheets)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var used = sheet.RangeUsed();
            if (used is null) continue;
            var evidence = string.Join(' ', used.CellsUsed().Take(40).Select(SafeText));
            var titleMapping = mappings.Resolve(sheet.Name, "");
            var mapping = titleMapping ?? mappings.Resolve(sheet.Name, evidence);
            // F06 is a business family, but only a worksheet with explicit
            // advance-ledger evidence is split into parallel streams. A word
            // such as "payment" in an ordinary work sheet is not enough.
            var isParallel = mapping?.FamilyKey == "F06_ADVANCE_PARALLEL" &&
                !HasLabel(Key(sheet.Name), "owner funding", "owner advances", "تمويل المالك", "دفعات المالك");
            var controlled = isParallel ? ReadParallelControls(sheet) : [];
            var groups = controlled.Count > 0 ? controlled.Select(control => control.Block).ToList() : BuildGroups(sheet);
            var layouts = groups.ToDictionary(group => group, group => ReadOperationalLayouts(sheet, group));
            var firstHeader = layouts.Values.SelectMany(items => items).Select(layout => layout.HeaderRow)
                .DefaultIfEmpty(20).Min();
            var currencies = workbookCurrencies.Concat(WorkbookCurrencyDeclarations.OfSheet(sheet, firstHeader).Select(item => item.Code))
                .Distinct().ToArray();
            foreach (var group in groups)
            {
                var sectionId = StableId(sha, sheet.Name, group.Range, mapping?.FamilyKey ?? "MAPPING_REQUIRED");
                var control = controlled.SingleOrDefault(item => item.Block == group);
                var sectionRecords = control is null
                    ? ExtractRecords(sha, sheet, group, sectionId, titleMapping, layouts[group], currencies, allowOperationalMapping: !isParallel)
                    : ExtractControlledLedger(sha, sheet, sectionId, control);
                var requires = mapping is null || isParallel || sectionRecords.Any(item =>
                    item.RecordType == UniversalRecordType.Unresolved || item.OperationalEvidence?.RequiresManualMapping == true);
                var sectionExceptions = new List<string>();
                if (mapping is null) sectionExceptions.Add("MAPPING_REQUIRED");
                if (group.IsSummary) sectionExceptions.Add("Summary/formula rows excluded from posting totals.");
                if (group.StreamKey == "allocation_ledger") sectionExceptions.Add("Allocation ledger is independent and is not linked to a payment in preview.");
                if (group.StreamKey == "unallocated_multicurrency_ledger") sectionExceptions.Add("Currencies remain separate; no implicit FX conversion.");

                sections.Add(new UniversalSectionPreview(sectionId, sheet.Name, group.Range, group.StreamKey,
                    mapping?.FamilyKey ?? "MAPPING_REQUIRED", UniversalImportMappingRegistry.Version,
                    requires ? "LOW" : mapping is null ? "NONE" : "MEDIUM", requires,
                    !requires && sectionRecords.Any(item => item.EligibleForPosting), group.SourceRowCount,
                    sectionRecords.Count, sectionExceptions));
                records.AddRange(sectionRecords);
                reconciliations.AddRange(control is null
                    ? BuildReconciliations(sheet, sectionId, group, sectionRecords)
                    : ReconcileControlledLedger(sheet, sectionId, control, sectionRecords));
                if (requires) exceptions.Add($"{sheet.Name}!{group.Range}: MAPPING_REQUIRED or unresolved source rows.");
            }
            if (controlled.Count > 0)
            {
                var range = used.RangeAddress.ToStringRelative();
                var sectionId = StableId(sha, sheet.Name, range, "parallel_reference");
                var remaining = ExtractParallelRemainder(sha, sheet, sectionId, controlled);
                if (remaining.Count > 0)
                {
                    sections.Add(new UniversalSectionPreview(sectionId, sheet.Name, range, "parallel_reference", mapping!.FamilyKey,
                        UniversalImportMappingRegistry.Version, "LOW", true, false, remaining.Select(item => item.SourceCoordinate).Distinct().Count(),
                        remaining.Count, ["Unassigned numeric cells and balance controls are retained as review evidence."]));
                    records.AddRange(remaining);
                }
            }
        }

        var totals = records.Where(item => item.Amount.HasValue && !item.IsSummary &&
                item.RecordType != UniversalRecordType.Unresolved)
            .GroupBy(item => (item.StreamKey, item.Currency), StringTupleComparer.Instance)
            .Select(group => new UniversalStreamTotal(group.Key.StreamKey, group.Key.Currency,
                group.Sum(item => item.Amount!.Value), group.Count(), group.Count(item => item.EligibleForPosting)))
            .OrderBy(item => item.StreamKey).ThenBy(item => item.Currency).ToList();
        var duplicates = records.Where(item => item.ValueFingerprint.Length > 0)
            .GroupBy(item => item.ValueFingerprint, StringComparer.Ordinal)
            .Where(group => group.Count() > 1)
            .Select(group => new UniversalDuplicateCandidate(group.Key, group.Select(item => item.RecordId).ToList(), group.Count()))
            .ToList();
        return new UniversalPreviewResponse(Path.GetFileName(file.FileName), file.Length, sha, UniversalImportMappingRegistry.Version,
            sections.Any(item => item.RequiresMapping), sections, records, totals, reconciliations, duplicates,
            exceptions.Distinct().ToList(), WorkbookProjectCode(workbook))
        {
            ProjectMetadata = new(
                WorkbookImportService.ExtractClient(workbook),
                WorkbookImportService.ExtractLabeledValue(workbook, "اسم المالك", exactLabel: true),
                WorkbookImportService.ExtractLabeledValue(workbook, "الدافع", exactLabel: true),
                WorkbookImportService.ExtractLabeledValue(workbook, "الموقع", exactLabel: true)),
            Issues = BuildIssues(sections, records)
        };
    }

    private static IReadOnlyList<UniversalImportIssue> BuildIssues(
        IReadOnlyList<UniversalSectionPreview> sections, IReadOnlyList<UniversalRecordPreview> records)
    {
        var issues = sections.Where(section => section.MappingFamily == "MAPPING_REQUIRED")
            .Select(section => new UniversalImportIssue("SHEET_MAPPING_REQUIRED", section.SheetName,
                $"{section.SheetName}!{section.SourceRange}", null, "يحتاج هذا النطاق إلى تحديد نوع البيانات من المصدر."))
            .ToList();
        foreach (var record in records.Where(record => !record.IsSummary && !record.EligibleForPosting))
        {
            var code = record.OperationalEvidence?.RequiresManualMapping == true
                ? "ROW_MAPPING_REQUIRED"
                : record.RecordType == UniversalRecordType.Unresolved ? "ROW_UNRESOLVED" : null;
            if (code is null) continue;
            var message = code == "ROW_MAPPING_REQUIRED"
                ? "تحتاج الحركة إلى تأكيد المطابقة مع نوع العملية في النظام."
                : "لم يمكن إثبات المعنى المحاسبي من بيانات الصف.";
            issues.Add(new(code, record.SheetName, record.SourceCoordinate, record.RecordId, message));
        }
        return issues;
    }

    private static List<Block> BuildGroups(IXLWorksheet sheet)
    {
        var used = sheet.RangeUsed()!;
        var rows = Enumerable.Range(used.RangeAddress.FirstAddress.RowNumber, used.RowCount())
            .Where(row => sheet.Row(row).Cells(used.RangeAddress.FirstAddress.ColumnNumber, used.RangeAddress.LastAddress.ColumnNumber).Any(cell => !string.IsNullOrWhiteSpace(SafeText(cell))))
            .ToList();
        var blocks = new List<(int First, int Last)>();
        for (var index = 0; index < rows.Count; index++)
        {
            var first = rows[index]; var last = first;
            // A fully blank row is a structural boundary. This keeps parallel
            // tables and separated business sections independently reviewable.
            while (index + 1 < rows.Count && rows[index + 1] - rows[index] == 1) { last = rows[++index]; }
            blocks.Add((first, last));
        }
        return blocks.Select(block => new Block(block.First, block.Last, used.RangeAddress.FirstAddress.ColumnNumber,
            used.RangeAddress.LastAddress.ColumnNumber, "workbook_stream", false)).ToList();
    }

    private static List<UniversalRecordPreview> ExtractRecords(string sha, IXLWorksheet sheet, Block block, string sectionId,
        UniversalMappingDefinition? mapping, List<OperationalLayout> layouts, string[] currencies, bool allowOperationalMapping)
    {
        var output = new List<UniversalRecordPreview>();
        int? sourceCurrencyColumn = null;
        int? sourceHeaderRow = null;
        for (var row = block.FirstRow; row <= block.LastRow; row++)
        {
            var cells = sheet.Row(row).Cells(block.FirstColumn, block.LastColumn).Where(cell => !string.IsNullOrWhiteSpace(SafeText(cell))).ToList();
            if (cells.Count == 0) continue;
            var layout = layouts.LastOrDefault(item => item.HeaderRow <= row);
            if (layout?.HeaderRow == row)
            {
                sourceCurrencyColumn = null;
                sourceHeaderRow = row;
            }
            var currencyHeadings = cells.Where(cell => !cell.HasFormula && cell.DataType == XLDataType.Text &&
                WorkbookCurrencyDeclarations.IsCurrencyColumn(cell.GetString())).ToArray();
            if (currencyHeadings.Length > 0)
            {
                sourceCurrencyColumn = currencyHeadings.Length == 1 ? currencyHeadings[0].Address.ColumnNumber : -1;
                sourceHeaderRow = row;
            }
            var hasMeaningfulValue = cells.Any(cell => !cell.HasFormula &&
                ((cell.DataType == XLDataType.Text && !string.IsNullOrWhiteSpace(cell.GetString())) ||
                 cell.TryGetValue<decimal>(out var number) && number != 0 || IsDateCell(cell)));
            if (!hasMeaningfulValue && cells.All(cell => cell.HasFormula ||
                !cell.TryGetValue<decimal>(out var number) || number == 0))
                continue;
            var formula = cells.Any(cell => cell.HasFormula);
            var numeric = cells.Where(cell => !IsDateCell(cell) && cell.TryGetValue<decimal>(out _)).ToList();
            var text = string.Join(' ', cells.Select(SafeText));
            var summary = block.IsSummary || IsSummary(text, false, row);
            var type = summary ? UniversalRecordType.Summary : UniversalRecordType.Unresolved;
            string RowCurrency(IXLCell? amountCell)
            {
                if (sourceCurrencyColumn == -1) return "UNKNOWN";
                var currencyCell = sourceCurrencyColumn is > 0 ? sheet.Cell(row, sourceCurrencyColumn.Value) : null;
                var amountHeading = sourceHeaderRow is int headerRow && amountCell is not null
                    ? SafeText(sheet.Cell(headerRow, amountCell.Address.ColumnNumber)) : string.Empty;
                var rateHeading = layout is not null && layout.HeaderRow == sourceHeaderRow && layout.Columns.TryGetValue("rate", out var rate)
                    ? sheet.Cell(layout.HeaderRow, rate).GetString() : string.Empty;
                return WorkbookSourceCurrency.ResolveWithDeclarations(currencies, currencyCell, amountCell, sheet.Name, amountHeading, rateHeading);
            }
            var mapped = allowOperationalMapping ? ReadOperationalEvidence(sheet, row, mapping?.FamilyKey, layout) : [];
            if (mapped.Count > 0)
            {
                foreach (var (evidence, amount, sourceCells) in mapped)
                {
                    var currency = RowCurrency(sheet.Cell(evidence.AmountCell));
                    var typeOfRecord = IsSignedAdjustment(evidence.Description, evidence.Quantity, amount)
                        ? UniversalRecordType.Adjustment
                        : evidence.Target switch
                        {
                            "SupplierSupply" => UniversalRecordType.Liability,
                            "AdvancePayment" => UniversalRecordType.Funding,
                            _ => evidence.RequiresManualMapping ? UniversalRecordType.Unresolved : UniversalRecordType.Expense
                        };
                    var recordId = StableId(sha, sheet.Name, evidence.AmountCell, evidence.Target);
                    var fingerprint = StableId(evidence.Target, currency, evidence.Date.ToString("O"), NormalizeText(evidence.Description).ToUpperInvariant(),
                        amount.ToString("G29", CultureInfo.InvariantCulture), evidence.Party?.ToUpperInvariant() ?? "", evidence.Reference?.ToUpperInvariant() ?? "");
                    output.Add(new UniversalRecordPreview(recordId, sectionId, sheet.Name, $"{sheet.Name}!{evidence.AmountCell}",
                        sourceCells, typeOfRecord, block.StreamKey, currency, amount, sheet.Cell(evidence.AmountCell).HasFormula,
                        false, currency == "EGP" && !evidence.RequiresManualMapping, evidence.RequiresManualMapping ? "LOW" : "HIGH", fingerprint, evidence));
                }
                continue;
            }
            foreach (var amountCell in new[] { numeric.LastOrDefault() })
            {
                decimal? amount = amountCell is not null && amountCell.TryGetValue<decimal>(out var parsed) ? parsed : null;
                var currency = RowCurrency(amountCell);
                var recordId = StableId(sha, sheet.Name, sectionId, row.ToString(CultureInfo.InvariantCulture), amountCell?.Address.ToString() ?? "");
                output.Add(new UniversalRecordPreview(recordId, sectionId, sheet.Name,
                    $"{sheet.Name}!{ColumnName(block.FirstColumn)}{row}:{ColumnName(block.LastColumn)}{row}",
                    cells.Select(cell => cell.Address.ToString() ?? string.Empty).ToList(), type, block.StreamKey, currency, amount,
                    formula, summary, false,
                    summary ? "HIGH" : "LOW", ""));
            }
        }
        return output;
    }

    private static bool IsDateCell(IXLCell cell) => cell.DataType == XLDataType.DateTime;

    private static IReadOnlyList<UniversalReconciliationPreview> BuildReconciliations(IXLWorksheet sheet, string sectionId,
        Block block, IReadOnlyList<UniversalRecordPreview> records)
    {
        return records.Where(item => item.Amount.HasValue && !item.IsSummary && item.RecordType != UniversalRecordType.Unresolved)
            .GroupBy(item => item.Currency)
            .Select(group =>
            {
                var amount = group.Sum(item => item.Amount!.Value);
                var controls = OperationalControls(sheet, group).Take(2).ToArray();
                var control = controls.Length == 1 && records.Where(item => item.OperationalEvidence is not null)
                    .All(item => item.Currency == group.Key) ? controls[0] : null;
                if (control is null)
                    return new UniversalReconciliationPreview(sectionId, block.StreamKey, group.Key, amount,
                        null, null, "NO_VERIFIED_SOURCE_CONTROL", "");
                var source = decimal.Round((decimal)control.CachedValue.GetNumber(), 2);
                var difference = source - amount;
                return new UniversalReconciliationPreview(sectionId, block.StreamKey, group.Key, amount,
                    source, difference, difference == 0 ? "MATCHED" : "DIFFERENCE", $"{sheet.Name}!{control.Address}");
            }).ToList();
    }

    private static bool IsSummary(string text, bool formula, int row) => formula || NormalizeText(text).Split(' ').Any(term => term is "اجمالي" or "الإجمالي" or "المتبقي" or "الرصيد" or "المستحق" or "summary" or "total");

    private static string SafeText(IXLCell cell) => cell.HasFormula ? cell.CachedValue.ToString(CultureInfo.InvariantCulture).Trim() : cell.GetFormattedString().Trim();
    private static string NormalizeText(string value) => string.Join(' ', value.Trim().Replace('ـ', ' ').Split(' ', StringSplitOptions.RemoveEmptyEntries));
    private static string StableId(params string[] parts) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(string.Join("\u001f", parts)))).ToLowerInvariant()[..24];
    private static string ColumnName(int column) { var result = ""; while (column > 0) { column--; result = (char)('A' + column % 26) + result; column /= 26; } return result; }

    private sealed record Block(int FirstRow, int LastRow, int FirstColumn, int LastColumn, string StreamKey, bool IsSummary)
    {
        public int SourceRowCount => LastRow - FirstRow + 1;
        public string Range => $"{ColumnName(FirstColumn)}{FirstRow}:{ColumnName(LastColumn)}{LastRow}";
    }

    private sealed class StringTupleComparer : IEqualityComparer<(string StreamKey, string Currency)>
    {
        public static readonly StringTupleComparer Instance = new();
        public bool Equals((string StreamKey, string Currency) x, (string StreamKey, string Currency) y) => StringComparer.Ordinal.Equals(x.StreamKey, y.StreamKey) && StringComparer.Ordinal.Equals(x.Currency, y.Currency);
        public int GetHashCode((string StreamKey, string Currency) obj) => HashCode.Combine(StringComparer.Ordinal.GetHashCode(obj.StreamKey), StringComparer.Ordinal.GetHashCode(obj.Currency));
    }
}
