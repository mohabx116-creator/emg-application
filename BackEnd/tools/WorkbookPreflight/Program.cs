using ClosedXML.Excel;
using System.Globalization;

var path = args.FirstOrDefault() ?? throw new ArgumentException("Supply a workbook path.");
using var workbook = new XLWorkbook(path);
static decimal D(IXLRow row, int col)
{
    if (row.Cell(col).TryGetValue<decimal>(out var value)) return value;
    var text = row.Cell(col).GetString().Trim().Replace(",", string.Empty).Replace("٬", string.Empty).Replace("٫", ".");
    return decimal.TryParse(text, NumberStyles.Any, CultureInfo.InvariantCulture, out value) ? value : 0;
}
static string S(IXLRow row, int col) => row.Cell(col).GetString().Trim();
void Print(string name, int header, Func<IXLRow, bool> predicate)
{
    if (!workbook.Worksheets.Contains(name)) return;
    var rows = workbook.Worksheet(name).RowsUsed().Where(x => x.RowNumber() > header).ToList();
    var candidates = rows.Where(predicate).ToList();
    Console.WriteLine($"{name}|used_after_header={rows.Count}|import_candidates={candidates.Count}|candidate_rows={string.Join(',', candidates.Select(x => x.RowNumber()))}");
}
Print("خاص باعمال الموقع", 9, r => S(r, 3).Length > 0 && D(r, 10) != 0);
Print("اعمال الحفر", 8, r => D(r, 10) != 0);
Print("اعمال الخرسانات", 8, r => D(r, 21) != 0 && D(r, 17) != 0);
foreach (var name in new[] { "اعمال توريد الحديد الغانم", "اعمال توريد الحديد اولاد شرف" }) Print(name, 6, r => D(r, 3) != 0 && D(r, 6) != 0);
foreach (var name in new[] { "اعمال المباني", "اعمال المحاره", "اعمال العزل", "اعمال الكهرباء", "اعمال السباكه" }) Print(name, 9, r => S(r, 3).Length > 0 && D(r, 9) != 0 && D(r, 10) != 0);
Print("دفعات تحت الحساب", 3, r => D(r, 14) > 0 || Enumerable.Range(0, 11).Sum(i => D(r, i + 2)) != 0);
foreach (var name in new[] { "اعمال الخرسانات", "حساب الخرسانات", "اعمال توريد الحديد", "اعمال توريد الحديد الغانم", "اعمال توريد الحديد اولاد شرف", "اعمال المباني", "حساب المباني", "اعمال المحاره", "حساب المحاره", "اعمال العزل", "اعمال الكهرباء", "اعمال السباكه" })
{
    if (!workbook.Worksheets.Contains(name)) continue;
    Console.WriteLine($"RAW|{name}");
    foreach (var row in workbook.Worksheet(name).RowsUsed().Where(x => x.RowNumber() > 8).Take(6))
    {
        var cells = Enumerable.Range(1, Math.Min(row.Worksheet.LastColumnUsed()?.ColumnNumber() ?? 1, 22)).Select(i => $"{i}:{S(row, i)}").Where(x => !x.EndsWith(":"));
        Console.WriteLine($"row={row.RowNumber()}|{string.Join(" || ", cells)}");
    }
}
Console.WriteLine("HEADER|اعمال الخرسانات");
foreach (var row in workbook.Worksheet("اعمال الخرسانات").RowsUsed().Where(x => x.RowNumber() <= 8))
{
    var cells = Enumerable.Range(1, 22).Select(i => $"{i}:{S(row, i)}").Where(x => !x.EndsWith(":"));
    Console.WriteLine($"row={row.RowNumber()}|{string.Join(" || ", cells)}");
}
