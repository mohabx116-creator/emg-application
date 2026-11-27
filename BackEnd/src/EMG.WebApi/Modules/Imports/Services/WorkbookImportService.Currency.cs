using System.Text.RegularExpressions;
using ClosedXML.Excel;
using EMG.Api.Modules.Imports.Dtos;

namespace EMG.Api.Modules.Imports.Services;

public sealed partial class WorkbookImportService
{
    private static string? LegacyCurrencyIssue(XLWorkbook workbook, IReadOnlyList<WorkbookSheetPreview> sheets)
    {
        static string Issue(IXLCell cell, string code) =>
            $"الاستيراد بهذا القالب يدعم الجنيه المصري فقط. راجع عملة {cell.Worksheet.Name}!{cell.Address} ({code}).";

        string projectCurrency = string.Empty;
        foreach (var (cell, code) in WorkbookCurrencyDeclarations.OfWorkbook(workbook))
        {
            if (code != "EGP") return Issue(cell, code);
            projectCurrency = code;
        }

        foreach (var source in sheets.Where(s => s.IsSourceData))
        {
            var sheet = workbook.Worksheet(source.OriginalName);
            var layout = DetectLayout(sheet, source.Classification);
            var header = layout.HeaderRow;
            var currencyColumns = new Dictionary<int, int>();
            var headings = new Dictionary<int, string>();
            var sheetCurrency = projectCurrency;
            foreach (var (cell, code) in WorkbookCurrencyDeclarations.OfSheet(sheet, header))
            {
                if (code != "EGP") return Issue(cell, code);
                sheetCurrency = code;
            }
            var title = WorkbookCurrencyDeclarations.MoneyHeading.IsMatch(sheet.Name) ? sheet.Name : string.Empty;
            foreach (var row in sheet.RowsUsed())
            {
                var cells = row.CellsUsed().ToList();
                var rowNumber = row.RowNumber();
                var headingArea = rowNumber <= header || rowNumber == header + 1 &&
                    cells.All(c => !c.HasFormula && c.DataType == XLDataType.Text);
                if (headingArea)
                {
                    foreach (var cell in cells.Where(c => !c.HasFormula && c.DataType == XLDataType.Text))
                    {
                        var text = cell.GetString().Trim();
                        if (WorkbookCurrencyDeclarations.IsCurrencyColumn(text) && rowNumber >= header)
                            currencyColumns[cell.Address.ColumnNumber] = rowNumber;
                        else if (WorkbookCurrencyDeclarations.MoneyHeading.IsMatch(text))
                        {
                            var code = WorkbookSourceCurrency.Resolve(null, null, text);
                            if (code != "EGP") return Issue(cell, code);
                            headings[cell.Address.ColumnNumber] = text;
                        }
                    }
                }

                if (rowNumber <= header) continue;
                var amounts = cells.Where(c => c.HasFormula || c.DataType == XLDataType.Number ||
                    c.DataType == XLDataType.Text && headings.ContainsKey(c.Address.ColumnNumber) &&
                    Regex.IsMatch(c.GetString().Trim(), @"^(?:[\p{Sc}A-Za-z. ]+)?[()\d+−.,\s-]+(?:[\p{Sc}\p{L}. ]+)?$")).ToList();
                if (amounts.Count == 0) continue;
                IXLCell? rowCurrency = null;
                foreach (var (column, labelRow) in currencyColumns)
                {
                    if (rowNumber <= labelRow) continue;
                    var currency = row.Cell(column);
                    if (currency.IsEmpty() && !MeaningfulDataRow(row, source.Classification, layout)) continue;
                    var code = WorkbookSourceCurrency.Resolve(currency, null);
                    if (code != "EGP") return Issue(currency, code);
                    rowCurrency = currency;
                }
                foreach (var amount in amounts)
                {
                    var heading = headings.GetValueOrDefault(amount.Address.ColumnNumber, string.Empty);
                    var amountText = !amount.HasFormula && amount.DataType == XLDataType.Text ? "Amount " + amount.GetString() : string.Empty;
                    var code = WorkbookSourceCurrency.Resolve(rowCurrency, amount, heading, sheetCurrency, title, amountText);
                    if (code != "EGP") return Issue(amount, code);
                }
            }
        }
        return null;
    }
}
