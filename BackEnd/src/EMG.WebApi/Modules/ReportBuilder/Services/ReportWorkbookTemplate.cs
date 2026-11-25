using System.IO.Compression;
using System.Xml.Linq;

namespace EMG.Api.Modules.ReportBuilder.Services;

internal sealed class ReportWorkbookTemplate : IDisposable
{
    internal static readonly XNamespace SheetNamespace = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
    private static readonly int[] DetailedTableNumbers = [1, 0, 0, 2, 0, 0, 3, 4, 5, 6, 7];
    private readonly ZipArchive archive;
    private readonly IReadOnlyList<string> strings;

    public ReportWorkbookTemplate(bool master)
    {
        var path = master
            ? Path.Combine(AppContext.BaseDirectory, "Modules", "ReportBuilder", "Templates", "ConsolidatedReportLayout.zip")
            : Path.Combine(AppContext.BaseDirectory, "Modules", "Reports", "Templates", "ProjectReportTemplate.xlsx");
        archive = ZipFile.OpenRead(path);
        try
        {
            IsMaster = master;
            strings = master ? [] : Read("xl/sharedStrings.xml").Elements(SheetNamespace + "si").Select(Text).ToArray();
        }
        catch
        {
            archive.Dispose();
            throw;
        }
    }

    public bool IsMaster { get; }

    public ReportWorksheet Sheet(int number, string name)
    {
        var source = Read($"xl/worksheets/sheet{number}.xml");
        var sheet = new ReportWorksheet(name, source);
        var tableNumber = IsMaster ? 1 : DetailedTableNumbers[number - 1];
        if (tableNumber != 0)
        {
            var table = Read($"xl/tables/table{tableNumber}.xml");
            sheet.AddTable(new ReportExcelTable(
                (string)table.Attribute("ref")!,
                table.Element(SheetNamespace + "tableColumns")!.Elements().Select(cell => (string)cell.Attribute("name")!).ToArray(),
                table.Element(SheetNamespace + "tableStyleInfo")));
        }
        return sheet;
    }

    public void CopyLabel(ReportWorksheet sheet, string address)
    {
        var label = sheet.TemplateCell(address);
        if ((string?)label?.Attribute("t") == "s")
            sheet.Set(address, strings[(int)label!.Element(SheetNamespace + "v")!]);
    }

    public Stream OpenStyles() => Open("xl/styles.xml");
    public Stream OpenTheme() => Open("xl/theme/theme1.xml");

    private XElement Read(string name)
    {
        using var stream = Open(name);
        return XDocument.Load(stream).Root ?? throw new InvalidDataException("Invalid XLSX template part.");
    }

    private Stream Open(string name) =>
        archive.GetEntry(name)?.Open() ?? throw new InvalidDataException($"Missing XLSX template part {name}.");

    private static string Text(XElement cell) => string.Concat(cell.Descendants(SheetNamespace + "t").Select(text => text.Value));

    public void Dispose() => archive.Dispose();
}
