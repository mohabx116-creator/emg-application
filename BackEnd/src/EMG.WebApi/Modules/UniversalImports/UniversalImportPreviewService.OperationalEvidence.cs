using System.Globalization;
using ClosedXML.Excel;
using EMG.Api.Modules.Imports.Services;

namespace EMG.Api.Modules.UniversalImports;

public sealed partial class UniversalImportPreviewService
{
    private static readonly Dictionary<string, string[]> ColumnLabels = new(StringComparer.Ordinal)
    {
        ["date"] = ["date", "التاريخ", "تاريخ", "تاريخ العمل", "تاريخ الدفع", "تاريخ التوريد"],
        ["description"] = ["description", "item", "item name", "البيان", "بيان الاعمال", "بيان بالاعمال", "الوصف", "وصف العمل", "نوع الاعمال", "البند", "اسم البند", "الصنف"],
        ["amount"] = ["amount", "total amount", "amount egp", "expense amount", "قيمة المصروف", "مبلغ المصروف", "المبلغ", "اجمالي المبلغ", "اجمالي القيمة", "القيمة الاجمالية", "الاجمالي", "الاجمال", "القيمة بالجنيه", "اجمالي تكلفة العمل الاعمال", "صافي قيمة الاعمال بالجنيه"],
        ["quantity"] = ["quantity", "current quantity", "الكمية", "كمية", "الكمية الحالية", "خلال الفتره", "الوزن", "الكمية طن", "الكميه(م3)"],
        ["previous"] = ["previous quantity", "مقدار الاعمال السابق", "الكمية السابقة"],
        ["cumulative"] = ["cumulative quantity", "مجموع مقدار لاعمال", "اجمالي الكمية"],
        ["unit"] = ["unit", "الوحدة", "وحدة", "الوحد"],
        ["rate"] = ["unit price", "rate", "unit rate", "سعر الوحدة", "السعر", "السعر بالجنيه", "سعر الطن", "سعر المتر", "السعر بالمتر مكعب"],
        ["party"] = ["supplier", "supplier name", "payer", "اسم المورد", "المورد", "الدافع", "اسم الدافع"],
        ["reference"] = ["invoice", "invoice number", "reference", "المرجع", "رقم الفاتورة", "رقم الايصال"],
        ["paid"] = ["paid", "paid amount", "المدفوع", "المبلغ المدفوع", "الدفعة", "الدفعات", "المتبقي", "الخصم", "الرصيد السابق"],
        ["currency"] = ["currency", "العملة"]
    };

    private sealed record OperationalLayout(int HeaderRow, IReadOnlyDictionary<string, int> Columns, string AmountLabel);

    private static string Key(string value) => string.Join(' ', value.Trim().ToLowerInvariant()
        .Replace('أ', 'ا').Replace('إ', 'ا').Replace('آ', 'ا').Replace('ه', 'ة').Replace('ى', 'ي').Replace('ـ', ' ')
        .Split(' ', StringSplitOptions.RemoveEmptyEntries));

    private static List<OperationalLayout> ReadOperationalLayouts(IXLWorksheet sheet, Block block)
    {
        var layouts = new List<OperationalLayout>();
        for (var row = block.FirstRow; row <= block.LastRow; row++)
        {
            var headings = sheet.Row(row).Cells(block.FirstColumn, block.LastColumn)
                .Where(cell => !cell.HasFormula && cell.DataType == XLDataType.Text)
                .Select(cell => (Label: Key(cell.GetString()), Column: cell.Address.ColumnNumber)).ToArray();
            // A parallel table needs its own explicit schema. An ambiguous
            // repeated label cannot borrow the adjacent table's date or rate.
            var columns = new Dictionary<string, int>(StringComparer.Ordinal);
            foreach (var (field, aliases) in ColumnLabels)
            {
                var matches = headings.Where(column => aliases.Any(alias => Key(alias) == column.Label)).ToArray();
                if (matches.Length == 1) columns[field] = matches[0].Column;
                else if (matches.Length > 1) columns[field] = -1;
            }
            if (columns.Values.Any(column => column == -1) ||
                !columns.ContainsKey("date") || !columns.TryGetValue("amount", out var amountColumn) ||
                !columns.ContainsKey("description")) continue;
            layouts.Add(new OperationalLayout(row, columns, headings.Single(cell => cell.Column == amountColumn).Label));
        }
        return layouts;
    }

    private static List<(UniversalOperationalEvidence Evidence, decimal Amount, IReadOnlyList<string> Cells, string Currency)> ReadOperationalEvidence(
        IXLWorksheet sheet, int row, string? family, List<OperationalLayout> layouts)
    {
        var layout = layouts.LastOrDefault(item => item.HeaderRow < row);
        if (layout is null || layouts.Any(item => item.HeaderRow == row)) return [];
        var title = Key(sheet.Name);
        var target = family switch
        {
            "F01_SITE_EXPENSE" => "SiteMovement",
            "F02_EXCAVATION" => "ExcavationRecord",
            "F03_CONCRETE" => "ConcreteRecord",
            "F04_STEEL_SUPPLY" => "SteelRecord",
            "F05_FINISHING" => "FinishingRecord",
            _ => null
        };
        if (HasLabel(title, "supplier receipts", "supplier supplies", "توريدات الموردين", "استلام الموردين"))
            target = "SupplierSupply";
        if (HasLabel(title, "owner funding", "owner advances", "تمويل المالك", "دفعات المالك"))
            target = "AdvancePayment";
        // An unclassified worksheet with explicit expense headings can be
        // mapped with a separately recorded review decision.
        var manual = target is null && family is null &&
            HasLabel(layout.AmountLabel, "expense amount", "قيمة المصروف", "مبلغ المصروف");
        if (manual) target = "SiteMovement";
        if (target is null) return [];

        IXLCell? Cell(string field) => layout.Columns.TryGetValue(field, out var column) ? sheet.Cell(row, column) : null;
        static string? TextValue(IXLCell? cell)
        {
            if (cell is null || cell.IsEmpty()) return null;
            if (!cell.HasFormula)
                return cell.DataType == XLDataType.Text ? cell.GetString().Trim() : null;

            // Some approved workbooks carry reusable labels such as the unit
            // through a direct reference (=+E43). Read only that safe,
            // cached text result. Do not turn arbitrary formulas into source
            // evidence.
            var formula = cell.FormulaA1.Replace("$", "", StringComparison.Ordinal)
                .Trim().TrimStart('=').TrimStart('+');
            if (formula.Length < 2 || formula.Any(char.IsWhiteSpace)) return null;
            var digitStart = formula.Length;
            while (digitStart > 0 && char.IsDigit(formula[digitStart - 1])) digitStart--;
            if (digitStart == formula.Length || digitStart == 0 ||
                formula[..digitStart].Any(character => character is < 'A' or > 'Z')) return null;
            return cell.CachedValue.IsText ? cell.CachedValue.GetText().Trim() : null;
        }

        string? Text(string field) => TextValue(Cell(field));
        static decimal? Number(IXLCell? cell) => cell is { DataType: XLDataType.Number, HasFormula: false } value &&
            value.TryGetValue<decimal>(out var number) ? number : null;

        var description = Text("description");
        var date = Cell("date");
        var parsedDate = ReadDate(date);
        if (string.IsNullOrWhiteSpace(description) || description.Length > 250 || IsSummary(description, false, row) ||
            parsedDate is null || parsedDate >= DateOnly.FromDateTime(DateTime.UtcNow))
            return [];
        // A finishing sheet may carry a prior payment beside the work row.
        // That payment is evidence only. Keep the measured work in the
        // FinishingRecord stream and do not manufacture a payment entry.
        if (target != "FinishingRecord" && Cell("paid") is { } payment && Number(payment) is { } paid && paid != 0) return [];
        // A blank "previous quantity" means no prior quantity was entered.
        // It is not evidence of an unresolved prior balance. Treat only an
        // explicit non-zero numeric value as a reason to keep the row out of
        // the operational stream.
        if (Cell("previous") is { } previous && Number(previous) is { } previousValue && previousValue != 0) return [];

        var valueCell = Cell("amount")!;
        var quantity = Number(Cell("quantity"));
        var rate = Number(Cell("rate"));
        var unit = Text("unit");
        var party = Text("party");
        var reference = Text("reference");
        var amount = Number(valueCell);
        if (valueCell.HasFormula && Cell("quantity") is { } quantityCell && Cell("rate") is { } rateCell)
        {
            var formula = SimpleFormula(valueCell);
            var isProduct = IsOperation(formula, quantityCell, '*', rateCell);
            if (!isProduct && Cell("previous") is { } prior && Cell("cumulative") is { HasFormula: true } cumulative &&
                (prior.IsEmpty() || Number(prior) == 0) && IsOperation(SimpleFormula(cumulative), quantityCell, '+', prior))
                isProduct = IsOperation(formula, cumulative, '*', rateCell);
            if (isProduct)
            {
                // Excel normally persists a numeric cached result. Some
                // valid workbooks (including files produced by compatible
                // spreadsheet writers) carry the evaluated result in Value
                // while leaving CachedValue blank. The formula structure has
                // already been proven against quantity/rate above, so use
                // that evaluated value without treating arbitrary formulas
                // as posting evidence.
                if (valueCell.CachedValue.IsNumber)
                    amount = (decimal)valueCell.CachedValue.GetNumber();
                else if (valueCell.TryGetValue<decimal>(out var evaluatedAmount))
                    amount = evaluatedAmount;
            }
        }

        var signedAdjustment = IsSignedAdjustment(description, quantity, amount);
        if (amount is null or 0m || Math.Abs(amount.Value) > 9999999999999999.99m || decimal.Round(amount.Value, 2) != amount ||
            !signedAdjustment && amount < 0 ||
            party?.Length > 200 || reference?.Length > 150 || unit?.Length > 40) return [];
        var measured = quantity.HasValue || rate.HasValue || target is not ("SiteMovement" or "AdvancePayment");
        var quantityIsValid = signedAdjustment ? quantity is < 0m : quantity is > 0m;
        if (measured && (!quantityIsValid || quantity is null || Math.Abs(quantity.Value) > 999999999999999.999m || decimal.Round(quantity.Value, 3) != quantity ||
            rate is not > 0 or > 9999999999999999.99m || decimal.Round(rate.Value, 2) != rate ||
            rate > 9999999999999999.99m / Math.Abs(quantity.Value) || decimal.Round(quantity.Value * rate.Value, 2) != amount ||
            string.IsNullOrWhiteSpace(unit))) return [];
        if ((target is "SupplierSupply" or "AdvancePayment") &&
            (string.IsNullOrWhiteSpace(party) || string.IsNullOrWhiteSpace(reference))) return [];
        // Steel already contributes to project accounting. A row explicitly
        // describing a supplier receipt must take that route alone.
        if (target == "SteelRecord" && !string.IsNullOrWhiteSpace(party) && !string.IsNullOrWhiteSpace(reference))
            target = "SupplierSupply";

        var currencyLabel = Text("currency");
        var currency = currencyLabel is null
            ? DetectCurrency($"{title} {layout.AmountLabel} {valueCell.Style.NumberFormat.Format}")
            : ParseCurrency(currencyLabel);
        var evidence = new UniversalOperationalEvidence(target, parsedDate.Value, description,
            quantity, unit, rate, party, reference, valueCell.Address.ToString()!, manual);
        var coordinates = layout.Columns.Values.Select(column => sheet.Cell(row, column))
            .Where(cell => !cell.IsEmpty()).Select(cell => cell.Address.ToString()!).ToArray();
        return [(evidence, amount.Value, coordinates, currency)];
    }

    private static DateOnly? ReadDate(IXLCell? date)
    {
        if (date is null || date.HasFormula) return null;
        if (date.DataType == XLDataType.DateTime) return DateOnly.FromDateTime(date.GetDateTime());
        return date.DataType == XLDataType.Text && DateOnly.TryParseExact(date.GetString().Trim(), ["d/M/yyyy", "yyyy-MM-dd"],
            CultureInfo.InvariantCulture, DateTimeStyles.None, out var value) ? value : null;
    }

    private static string SimpleFormula(IXLCell cell)
    {
        var formula = cell.FormulaA1.Replace(" ", "").Replace("$", "").TrimStart('=');
        return formula.StartsWith("SUM(", StringComparison.OrdinalIgnoreCase) && formula.EndsWith(')') ? formula[4..^1] : formula;
    }

    private static bool IsOperation(string formula, IXLCell first, char operation, IXLCell second) =>
        formula.Equals($"{first.Address}{operation}{second.Address}", StringComparison.OrdinalIgnoreCase) ||
        formula.Equals($"{second.Address}{operation}{first.Address}", StringComparison.OrdinalIgnoreCase);

    private static IEnumerable<IXLCell> OperationalControls(IXLWorksheet sheet, IEnumerable<UniversalRecordPreview> group)
    {
        var cells = group.Select(item => sheet.Cell(item.OperationalEvidence!.AmountCell)).ToArray();
        var column = cells[0].Address.ColumnNumber;
        if (cells.Any(cell => cell.Address.ColumnNumber != column)) yield break;
        var first = cells.Min(cell => cell.Address.RowNumber);
        var last = cells.Max(cell => cell.Address.RowNumber);
        foreach (var control in sheet.CellsUsed().Where(cell => cell.HasFormula && cell.CachedValue.IsNumber))
        {
            if (control.Address.RowNumber >= first && control.Address.RowNumber <= last) continue;
            var range = ReadSumRange(sheet, control);
            if (range is null && control.FormulaA1.TrimStart('=').Trim() is { } formula &&
                formula.StartsWith("SUM(", StringComparison.OrdinalIgnoreCase) && formula.EndsWith(')'))
            {
                var reference = formula[4..^1].Trim();
                foreach (var table in sheet.Tables)
                {
                    var field = table.Fields.FirstOrDefault(field =>
                        reference == $"{table.Name}[{field.Name}]" || reference == $"{table.Name}[[{field.Name}]]");
                    if (field is not null && table.DataRange is { } data)
                    {
                        var fieldColumn = field.Column.ColumnNumber();
                        range = sheet.Range(data.FirstCell().Address.RowNumber, fieldColumn, data.LastCell().Address.RowNumber, fieldColumn);
                        break;
                    }
                }
            }
            if (range is not null && range.ColumnCount() == 1 && range.FirstCell().Address.ColumnNumber == column &&
                range.FirstCell().Address.RowNumber <= first && range.LastCell().Address.RowNumber >= last)
                yield return control;
        }
    }

    private static string ParseCurrency(string value) => Key(value) switch
    {
        "egp" or "جنيه" or "جنيه مصري" or "ج.م" => "EGP",
        "usd" or "دولار" or "$" => "USD",
        "eur" or "يورو" or "€" => "EUR",
        "sar" or "ريال" => "SAR",
        _ => "UNKNOWN"
    };

    private static bool IsSignedAdjustment(string? text, decimal? quantity, decimal? amount) =>
        amount is < 0m && (quantity is null || quantity < 0m) &&
        !string.IsNullOrWhiteSpace(text) &&
        (text.Contains("خصم", StringComparison.OrdinalIgnoreCase) ||
         text.Contains("هالك", StringComparison.OrdinalIgnoreCase) ||
         text.Contains("تسوية", StringComparison.OrdinalIgnoreCase) ||
         text.Contains("تعديل", StringComparison.OrdinalIgnoreCase) ||
         text.Contains("مرتجع", StringComparison.OrdinalIgnoreCase) ||
         text.Contains("استرداد", StringComparison.OrdinalIgnoreCase));

    private static bool HasLabel(string value, params string[] labels) => labels.Any(label => value.Contains(Key(label), StringComparison.Ordinal));

    private static string? WorkbookProjectCode(XLWorkbook workbook) => WorkbookImportService.TryHistoricalPlotNumber(workbook);
}
