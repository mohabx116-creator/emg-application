using System.Globalization;
using System.Text.RegularExpressions;
using ClosedXML.Excel;

namespace EMG.Api.Modules.UniversalImports;

public sealed partial class UniversalImportPreviewService
{
    private static readonly Regex SumRange = new(
        @"^\s*=?SUM\(\s*\$?([A-Z]{1,3})\$?(\d+)\s*:\s*\$?([A-Z]{1,3})\$?(\d+)\s*\)\s*$",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(100));

    private sealed record LedgerControl(Block Block, string Currency, string Address, IReadOnlyList<IXLRange> AmountRanges);

    private static List<LedgerControl> ReadParallelControls(IXLWorksheet sheet)
    {
        var controls = new List<LedgerControl>();
        foreach (var cell in sheet.CellsUsed().Where(cell => cell.HasFormula))
        {
            var range = ReadSumRange(sheet, cell);
            if (range is null) continue;
            var row = cell.Address.RowNumber;
            var column = cell.Address.ColumnNumber;
            // A control label must be adjacent to its result. Formatting alone
            // cannot classify a monetary column (even an EGP column can show $).
            var label = string.Join(" ", new[] { column > 1 ? sheet.Cell(row, column - 1) : null,
                row > 1 ? sheet.Cell(row - 1, column) : null }
                .Where(neighbor => neighbor is { HasFormula: false, DataType: XLDataType.Text })
                .Select(neighbor => NormalizeText(SafeText(neighbor!)).Replace('إ', 'ا').Replace('أ', 'ا')));
            var isTotal = label.Contains("اجمال", StringComparison.Ordinal) || label.Contains("total", StringComparison.OrdinalIgnoreCase);
            if (!isTotal) continue;

            string stream;
            string currency;
            IReadOnlyList<IXLRange> details;
            if (range.RowCount() == 1 && range.ColumnCount() > 1 && label.Contains("دفعات", StringComparison.Ordinal))
            {
                // A horizontal total is usable only if every referenced cell
                // is itself a vertical sum for a distinct detail column.
                var subtotals = range.Cells().Select(operand => ReadSumRange(sheet, operand)).ToList();
                if (subtotals.Any(operand => operand is null || operand.ColumnCount() != 1 ||
                    operand.RowCount() < 2 || operand.RangeAddress.LastAddress.RowNumber >= range.FirstCell().Address.RowNumber))
                    continue;
                details = subtotals.OfType<IXLRange>().ToArray();
                if (details.Select(detail => detail.FirstCell().Address.ColumnNumber).Distinct().Count() != details.Count) continue;
                stream = "allocation_ledger";
                currency = "EGP";
            }
            else if (range.ColumnCount() == 1 && range.LastCell().Address.RowNumber < row)
            {
                details = [range];
                if (label.Contains("دولار", StringComparison.Ordinal) || label.Contains("USD", StringComparison.OrdinalIgnoreCase))
                {
                    stream = "unallocated_multicurrency_ledger";
                    currency = "USD";
                }
                else if (label.Contains("جنيه", StringComparison.Ordinal) || label.Contains("EGP", StringComparison.OrdinalIgnoreCase))
                {
                    stream = "unallocated_multicurrency_ledger";
                    currency = "EGP";
                }
                else if (label.Contains("دفعات", StringComparison.Ordinal) &&
                    (label.Contains("رئ", StringComparison.Ordinal) || label.Contains("ريس", StringComparison.Ordinal)))
                {
                    stream = "main_payment_ledger";
                    currency = "EGP";
                }
                else continue;
            }
            else continue;

            var firstColumn = details.Min(detail => detail.FirstCell().Address.ColumnNumber);
            var lastColumn = details.Max(detail => detail.LastCell().Address.ColumnNumber);
            var firstRow = details.Min(detail => detail.FirstCell().Address.RowNumber);
            var lastRow = details.Max(detail => detail.LastCell().Address.RowNumber);
            controls.Add(new LedgerControl(new Block(firstRow, lastRow, firstColumn, lastColumn, stream, false),
                currency, cell.Address.ToString()!, details));
        }
        // Competing controls over the same detail must stay out of recognized totals.
        return controls.Where(control => !controls.Any(other => !ReferenceEquals(other, control) &&
            control.AmountRanges.Any(first => other.AmountRanges.Any(second =>
                first.RangeAddress.Intersects(second.RangeAddress))))).ToList();
    }

    private static IXLRange? ReadSumRange(IXLWorksheet sheet, IXLCell cell)
    {
        if (!cell.HasFormula) return null;
        var match = SumRange.Match(cell.FormulaA1);
        if (!match.Success) return null;
        var range = sheet.Range($"{match.Groups[1]}{match.Groups[2]}:{match.Groups[3]}{match.Groups[4]}");
        return (long)range.RowCount() * range.ColumnCount() <= 10000 ? range : null;
    }

    private static List<UniversalRecordPreview> ExtractControlledLedger(string sha, IXLWorksheet sheet, string sectionId, LedgerControl control)
    {
        var rows = new List<UniversalRecordPreview>();
        foreach (var cell in control.AmountRanges.SelectMany(range => range.CellsUsed()))
        {
            var value = cell.HasFormula ? cell.CachedValue : cell.Value;
            if (value.IsBlank) continue;
            var amount = value.IsNumber ? (decimal?)value.GetNumber() : null;
            if (amount == 0) continue;
            var type = control.Block.StreamKey == "allocation_ledger" ? UniversalRecordType.Allocation : UniversalRecordType.Payment;
            // An individual formula may be a subtotal or a repeated amount.
            // Preserve formula provenance, but retain a formula-backed detail
            // row when the row itself has an explicit date and narrative. This
            // covers legitimate workbook calculations such as 4*6700 inside
            // an allocation row without admitting summary formulas.
            if (cell.HasFormula && !HasDetailEvidence(sheet, cell) || amount is null) type = UniversalRecordType.Unresolved;
            else if (amount < 0) type = UniversalRecordType.Adjustment;
            var address = cell.Address.ToString()!;
            rows.Add(new UniversalRecordPreview(StableId(sha, sheet.Name, address, control.Block.StreamKey), sectionId,
                sheet.Name, $"{sheet.Name}!{address}", [address], type, control.Block.StreamKey, control.Currency,
                amount, cell.HasFormula, false, false, type == UniversalRecordType.Unresolved ? "LOW" : "MEDIUM", ""));
        }
        return rows;
    }

    private static bool HasDetailEvidence(IXLWorksheet sheet, IXLCell amountCell)
    {
        var row = sheet.Row(amountCell.Address.RowNumber);
        var hasDate = row.CellsUsed().Any(cell => !cell.HasFormula && cell.DataType == XLDataType.DateTime);
        var hasNarrative = row.CellsUsed().Any(cell => !cell.HasFormula && cell.DataType == XLDataType.Text &&
            !string.IsNullOrWhiteSpace(cell.GetString()));
        return hasDate && hasNarrative;
    }

    private static IReadOnlyList<UniversalReconciliationPreview> ReconcileControlledLedger(IXLWorksheet sheet, string sectionId,
        LedgerControl control, IReadOnlyList<UniversalRecordPreview> records)
    {
        var total = records.Where(row => row.RecordType != UniversalRecordType.Unresolved).Sum(row => row.Amount ?? 0);
        var cached = sheet.Cell(control.Address).CachedValue;
        decimal? source = cached.IsNumber ? decimal.Round((decimal)cached.GetNumber(), 2) : null;
        var difference = source - total;
        return [new UniversalReconciliationPreview(sectionId, control.Block.StreamKey, control.Currency, total,
            source, difference, source is null ? "NO_CACHED_SOURCE_CONTROL" : difference == 0 ? "MATCHED" : "DIFFERENCE",
            $"{sheet.Name}!{control.Address}")];
    }

    private static List<UniversalRecordPreview> ExtractParallelRemainder(string sha, IXLWorksheet sheet, string sectionId,
        List<LedgerControl> controls)
    {
        var assigned = controls.SelectMany(control => control.AmountRanges.SelectMany(range => range.CellsUsed())
            .Select(cell => cell.Address.ToString()!)).Concat(controls.Select(control => control.Address)).ToHashSet();
        var output = new List<UniversalRecordPreview>();
        var firstControlRow = controls.Min(control => sheet.Cell(control.Address).Address.RowNumber);
        foreach (var cell in sheet.CellsUsed())
        {
            var address = cell.Address.ToString()!;
            if (assigned.Contains(address)) continue;
            var number = cell.HasFormula ? cell.CachedValue : cell.Value;
            if (!number.IsNumber || number.GetNumber() == 0) continue;
            var summary = cell.HasFormula && cell.Address.RowNumber >= firstControlRow;
            var type = summary ? UniversalRecordType.DerivedValue : UniversalRecordType.Unresolved;
            output.Add(new UniversalRecordPreview(StableId(sha, sheet.Name, address, "parallel_reference"), sectionId,
                sheet.Name, $"{sheet.Name}!{address}", [address], type, "parallel_reference", "UNKNOWN",
                (decimal)number.GetNumber(), cell.HasFormula, summary, false, "LOW", ""));
        }
        return output;
    }
}
