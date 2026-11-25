using ClosedXML.Excel;
using System.Globalization;

var root = args.FirstOrDefault() ?? throw new ArgumentException("Supply a workbook directory.");
var files = Directory.EnumerateFiles(root, "*.xlsx").OrderBy(x => x, StringComparer.OrdinalIgnoreCase).ToList();
Console.WriteLine($"FILES|{files.Count}");

foreach (var path in files)
{
    try
    {
        using var workbook = new XLWorkbook(path);
        var parts = new List<string>();
        foreach (var sheet in workbook.Worksheets)
        {
            var kind = Classify(sheet.Name);
            if (kind is null || sheet.RangeUsed() is null) continue;
            var rows = sheet.RangeUsed()!.Rows().ToList();
            var candidate = kind switch
            {
                "site" => rows.Count(row => Text(row, 3).Length > 0 && Money(row, 10) != 0),
                "excavation" => rows.Count(row => Money(row, 10) != 0 || Money(row, 11) != 0),
                "concrete" => rows.Count(row => (Money(row, 21) != 0 && (Money(row, 17) != 0 || Money(row, 18) != 0)) || (Money(row, 8) != 0 && Money(row, 4) != 0)),
                "steel" => rows.Count(row => Money(row, 3) != 0 && Money(row, 6) != 0),
                "finishing" => rows.Count(row => Text(row, 3).Length > 0 && (Money(row, 9) != 0 || Money(row, 10) != 0)),
                "advances" => AdvanceCandidateCount(rows),
                "final" => 0,
                _ => 0
            };
            var signature = HeaderSignature(rows);
            var final = kind == "final" ? FinalSignature(rows) : string.Empty;
            parts.Add($"{kind}:{sheet.Name}:{rows.Count}/{candidate}:{signature}{final}");
        }
        Console.WriteLine($"FILE|{Path.GetFileName(path)}|{string.Join(" || ", parts)}");
    }
    catch (Exception ex) { Console.WriteLine($"ERR|{Path.GetFileName(path)}|{ex.GetType().Name}|{ex.Message}"); }
}

static string? Classify(string name)
{
    if (name.Contains("دفعات", StringComparison.OrdinalIgnoreCase)) return "advances";
    if (name.Contains("حساب نهائي", StringComparison.OrdinalIgnoreCase)) return "final";
    if (name.Contains("موقع", StringComparison.OrdinalIgnoreCase)) return "site";
    if (name.Contains("حفر", StringComparison.OrdinalIgnoreCase)) return "excavation";
    if (name.Contains("خرسان", StringComparison.OrdinalIgnoreCase)) return "concrete";
    if (name.Contains("حديد", StringComparison.OrdinalIgnoreCase)) return "steel";
    if (new[] { "مباني", "محار", "عزل", "كهرب", "سباك", "تشطيب" }.Any(x => name.Contains(x, StringComparison.OrdinalIgnoreCase))) return "finishing";
    return null;
}

static int AdvanceCandidateCount(List<IXLRangeRow> rows)
{
    var count = 0;
    foreach (var row in rows.Skip(3))
    {
        var first = Text(row, 1);
        if (first.StartsWith("اجمالي", StringComparison.OrdinalIgnoreCase) || first.StartsWith("المتبقي", StringComparison.OrdinalIgnoreCase) || first.Contains("المستحق للاعمال", StringComparison.OrdinalIgnoreCase)) break;
        if (Money(row, 14) > 0 || Enumerable.Range(0, 11).Sum(i => Money(row, i + 2)) != 0) count++;
    }
    return count;
}

static string HeaderSignature(List<IXLRangeRow> rows)
{
    var row = rows.FirstOrDefault(x => x.CellsUsed().Count() >= 3 && x.CellsUsed().Any(c => c.GetString().Contains("التاريخ")));
    if (row is null) return "";
    var text = string.Join("/", row.CellsUsed().Select(c => c.GetString().Trim()).Where(x => x.Length > 0).Take(8));
    return text.Length > 90 ? text[..90] : text;
}

static string FinalSignature(List<IXLRangeRow> rows)
{
    var values = rows.Where(row => new[] { "صادر", "وارد" }.Contains(Text(row, 4), StringComparer.OrdinalIgnoreCase))
        .Select(row => $"{Text(row, 4)}={string.Join(",", Enumerable.Range(5, 7).Select(i => Money(row, i).ToString("0.##", CultureInfo.InvariantCulture)))}");
    return string.Join(";", values);
}

static string Text(IXLRangeRow row, int column) => row.Cell(column).GetString().Trim();

static decimal Money(IXLRangeRow row, int column)
{
    var cell = row.Cell(column);
    if (cell.TryGetValue<decimal>(out var value)) return value;
    var text = cell.GetString().Trim().Replace(",", string.Empty).Replace("٬", string.Empty).Replace("٫", ".");
    return decimal.TryParse(text, NumberStyles.Any, CultureInfo.InvariantCulture, out value) ? value : 0m;
}
