using System.Text.RegularExpressions;
using ClosedXML.Excel;

namespace EMG.Api.Modules.Imports.Services;

internal static class WorkbookCurrencyDeclarations
{
    private static readonly Regex CurrencyLabel = new(
        @"^(?:(?<global>project currency|workbook currency|base currency|عملة المشروع|عملة المصنف|العملة الأساسية)|currency(?: code)?|العمل[ةه]|عمل[ةه](?: السداد| الدفعة)?|نوع العملة)\s*(?:[:：=]\s*(?<value>.*))?$",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    internal static readonly Regex MoneyHeading = new(
        @"^(?:المبلغ|مبلغ|قيم[ةه]|القيم[ةه]|صافي|صافى|[إا]جمال|الدفعات|دفعات|دفع[ةه]|المدفوع|المستحق|السعر|سعر|سداد|تكلف[ةه]|التكلف[ةه]|amount\b|(?:unit )?price\b|total\b|payments?\b|receipts?\b|balance\b|rate\b)",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    public static IEnumerable<(IXLCell Cell, string Code)> OfWorkbook(XLWorkbook workbook) =>
        workbook.Worksheets.SelectMany(sheet => Read(sheet, 20, global: true));

    public static IEnumerable<(IXLCell Cell, string Code)> OfSheet(IXLWorksheet sheet, int firstHeaderRow) =>
        Read(sheet, firstHeaderRow, global: false);

    internal static bool IsCurrencyColumn(string text)
    {
        var label = CurrencyLabel.Match(text.Trim());
        return label.Success && !label.Groups["global"].Success && !label.Groups["value"].Success;
    }

    private static IEnumerable<(IXLCell Cell, string Code)> Read(IXLWorksheet sheet, int lastRow, bool global)
    {
        foreach (var row in sheet.RowsUsed().TakeWhile(r => r.RowNumber() <= lastRow))
        {
            var labels = row.CellsUsed().Where(c => !c.HasFormula && c.DataType == XLDataType.Text).ToList();
            foreach (var cell in labels)
            {
                var label = CurrencyLabel.Match(cell.GetString().Trim());
                if (!label.Success || label.Groups["global"].Success != global) continue;
                if (label.Groups["value"].Value is { Length: > 0 } value)
                {
                    yield return (cell, WorkbookSourceCurrency.ParseExplicit(value));
                }
                else if (global || row.RowNumber() < lastRow && !labels.Any(c => MoneyHeading.IsMatch(c.GetString().Trim())))
                {
                    var adjacent = cell.CellRight();
                    yield return (adjacent, WorkbookSourceCurrency.Resolve(adjacent, null));
                }
            }
        }
    }
}
