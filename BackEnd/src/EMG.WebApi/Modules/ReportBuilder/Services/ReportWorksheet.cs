using System.Globalization;
using System.Xml;
using System.Xml.Linq;
using static EMG.Api.Modules.ReportBuilder.Services.ReportWorkbookTemplate;

namespace EMG.Api.Modules.ReportBuilder.Services;

internal sealed record ReportExcelTable(string Reference, IReadOnlyList<string> Headers, XElement? Style)
{
    public XElement Write(int id)
    {
        var name = $"ReportTable{id}";
        return new XElement(SheetNamespace + "table",
            new XAttribute("id", id), new XAttribute("name", name), new XAttribute("displayName", name),
            new XAttribute("ref", Reference), new XAttribute("totalsRowShown", 0),
            new XElement(SheetNamespace + "autoFilter", new XAttribute("ref", Reference)),
            new XElement(SheetNamespace + "tableColumns", new XAttribute("count", Headers.Count),
                Headers.Select((caption, index) => new XElement(SheetNamespace + "tableColumn",
                    new XAttribute("id", index + 1), new XAttribute("name", caption)))),
            Style is null ? null : new XElement(Style));
    }
}

internal sealed class ReportWorksheet
{
    private static readonly string[] BeforeData = ["sheetPr", "dimension", "sheetViews", "sheetFormatPr", "cols"];
    private static readonly string[] AfterData = ["mergeCells", "printOptions", "pageMargins", "pageSetup"];
    private static readonly XNamespace Relationships = "http://schemas.openxmlformats.org/officeDocument/2006/relationships";
    private readonly XElement? template;
    private readonly Dictionary<string, XElement> templateCells;
    private readonly SortedDictionary<int, XElement> rows = [];
    private readonly Dictionary<string, CellValue> values = new(StringComparer.Ordinal);
    private readonly XElement? columns;

    public ReportWorksheet(string name, XElement? template = null, IReadOnlyList<double>? widths = null)
    {
        Name = name;
        this.template = template;
        templateCells = template?.Element(SheetNamespace + "sheetData")?.Descendants(SheetNamespace + "c")
            .ToDictionary(cell => (string)cell.Attribute("r")!, StringComparer.Ordinal) ?? [];
        foreach (var row in template?.Element(SheetNamespace + "sheetData")?.Elements() ?? [])
        {
            var n = (int)row.Attribute("r")!;
            Row(n, (double?)row.Attribute("ht"));
            foreach (var cell in row.Elements(SheetNamespace + "c"))
            {
                var address = (string)cell.Attribute("r")!;
                if ((int?)cell.Attribute("s") is { } style) Set(address, null, style);
            }
        }
        if (widths is not null)
        {
            columns = new XElement(SheetNamespace + "cols", widths.Select((width, index) =>
                new XElement(SheetNamespace + "col", new XAttribute("min", index + 1), new XAttribute("max", index + 1),
                    new XAttribute("width", width), new XAttribute("customWidth", 1))));
        }
    }

    public string Name { get; }
    public ReportExcelTable? Table { get; private set; }
    public XElement? TemplateCell(string address) => templateCells.GetValueOrDefault(address);
    public int StyleAt(string address) => (int?)TemplateCell(address)?.Attribute("s") ?? 0;

    public void AddTable(ReportExcelTable table)
    {
        Table = table;
        var (row, firstColumn) = Position(table.Reference.Split(':')[0]);
        for (var i = 0; i < table.Headers.Count; i++) Set($"{Column(firstColumn + i)}{row}", table.Headers[i], shared: true);
    }

    public void Row(int row, double? height = null)
    {
        if (!rows.TryGetValue(row, out var element))
            rows.Add(row, element = new XElement(SheetNamespace + "row", new XAttribute("r", row)));
        if (height is not null)
        {
            element.SetAttributeValue("ht", height);
            element.SetAttributeValue("customHeight", 1);
        }
    }

    public void Set(string address, object? value, int? style = null, bool shared = false)
    {
        var (row, column) = Position(address);
        Row(row);
        values[address] = new CellValue(address, row, column, style ?? values.GetValueOrDefault(address)?.Style ?? StyleAt(address), value, shared);
    }

    public XElement Write(Func<string, int> stringIndex, string? tableId)
    {
        foreach (var group in values.Values.GroupBy(cell => cell.Row))
            rows[group.Key].Add(group.OrderBy(cell => cell.Column).Select(cell => cell.Write(stringIndex)));

        var root = new XElement(SheetNamespace + "worksheet");
        if (template is null)
        {
            root.Add(new XElement(SheetNamespace + "sheetViews",
                new XElement(SheetNamespace + "sheetView", new XAttribute("workbookViewId", 0), new XAttribute("rightToLeft", 1))));
            root.Add(columns);
        }
        else
        {
            foreach (var name in BeforeData)
            {
                if (template.Element(SheetNamespace + name) is not { } source) continue;
                var copy = new XElement(source);
                if (name == "sheetPr") copy.Attribute("codeName")?.Remove();
                if (name == "sheetViews")
                    foreach (var view in copy.Elements()) view.Attribute("tabSelected")?.Remove();
                root.Add(copy);
            }
        }
        root.Add(new XElement(SheetNamespace + "sheetData", rows.Values));
        if (template is not null)
        {
            foreach (var name in AfterData)
            {
                if (template.Element(SheetNamespace + name) is not { } source) continue;
                var copy = new XElement(source);
                copy.Attribute(Relationships + "id")?.Remove();
                root.Add(copy);
            }
        }
        if (tableId is not null)
            root.Add(new XElement(SheetNamespace + "tableParts", new XAttribute("count", 1),
                new XElement(SheetNamespace + "tablePart", new XAttribute(Relationships + "id", tableId))));
        return root;
    }

    public static string Column(int number)
    {
        var result = string.Empty;
        for (; number > 0; number = (number - 1) / 26)
            result = (char)('A' + (number - 1) % 26) + result;
        return result;
    }

    private static (int Row, int Column) Position(string address)
    {
        var column = 0;
        var i = 0;
        while (i < address.Length && address[i] is >= 'A' and <= 'Z') column = column * 26 + address[i++] - 'A' + 1;
        return (int.Parse(address.AsSpan(i), CultureInfo.InvariantCulture), column);
    }

    private sealed record CellValue(string Address, int Row, int Column, int Style, object? Value, bool Shared)
    {
        public XElement Write(Func<string, int> stringIndex)
        {
            var cell = new XElement(SheetNamespace + "c", new XAttribute("r", Address));
            if (Style != 0) cell.SetAttributeValue("s", Style);
            switch (Value)
            {
                case null: break;
                case string text:
                    if (text.Length > 32767) throw ReportWorkbookException.Unsupported("قيمة نصية أطول من سعة خلية Excel.");
                    XmlConvert.VerifyXmlChars(text);
                    cell.SetAttributeValue("t", Shared ? "s" : "inlineStr");
                    cell.Add(Shared
                        ? new XElement(SheetNamespace + "v", stringIndex(text))
                        : new XElement(SheetNamespace + "is", PreservedText(text)));
                    break;
                case DateTime date:
                    if (date.Year < 1900) throw ReportWorkbookException.Unsupported("تاريخ إحدى الحركات يسبق نطاق التواريخ المعروض في Excel.");
                    var serial = date.Date.ToOADate();
                    if (date < new DateTime(1900, 3, 1)) serial--;
                    cell.Add(new XElement(SheetNamespace + "v", serial.ToString(CultureInfo.InvariantCulture)));
                    break;
                case decimal amount:
                    var precision = ((double)amount).ToString("G15", CultureInfo.InvariantCulture);
                    if (!decimal.TryParse(precision, NumberStyles.Float, CultureInfo.InvariantCulture, out var stored) || stored != amount)
                        throw ReportWorkbookException.Unsupported("لا يمكن تمثيل إحدى القيم المالية بدقة ضمن سعة الأرقام في Excel.");
                    cell.Add(new XElement(SheetNamespace + "v", precision));
                    break;
                case int count:
                    cell.Add(new XElement(SheetNamespace + "v", count));
                    break;
                default: throw new InvalidOperationException("Not an XLSX cell value.");
            }
            return cell;
        }
    }

    internal static XElement PreservedText(string text) =>
        new(SheetNamespace + "t", new XAttribute(XNamespace.Xml + "space", "preserve"), text);
}
