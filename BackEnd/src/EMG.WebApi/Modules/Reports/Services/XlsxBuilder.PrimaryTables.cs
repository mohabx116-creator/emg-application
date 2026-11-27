using System.Globalization;
using System.IO.Compression;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using EMG.Api.Modules.Reports.Dtos;

namespace EMG.Api.Modules.Reports.Services;

internal static partial class XlsxBuilder
{
    private static readonly string[] FinishingCategories = ["المباني", "المحارة", "العزل", "الكهرباء", "السباكة"];

    private static string FinishingCategory(ProjectWorkRow row)
    {
        if (row.Family.Length > 0 || FinishingCategories.Contains(row.Category))
            return row.Category;
        if (IsBuilding(row)) return FinishingCategories[0];
        if (IsPlaster(row)) return FinishingCategories[1];
        if (IsInsulation(row)) return FinishingCategories[2];
        if (IsElectrical(row)) return FinishingCategories[3];
        if (IsPlumbing(row)) return FinishingCategories[4];
        return string.IsNullOrWhiteSpace(row.Category) ? "غير محدد" : row.Category;
    }

    private static int DetailStart(int number) => number switch { 1 => 9, 2 => 7, 5 => 4, _ => 10 };
    private static int DetailCount(ProjectReportSummary report, int number) => number switch
    {
        1 => report.Excavations.Count,
        2 => report.Steels.Count,
        3 => Math.Max(report.Concretes.Count(IsStation), report.Concretes.Count(item => !IsStation(item))),
        4 => report.Movements.Count(m => m.Type != "وارد" && m.Currency == "EGP"),
        5 => Math.Max(report.Allocations.Count(x => x.Currency == "EGP"),
            Math.Max(report.RecognizedFunding.Count(x => x.Currency == "EGP"),
                report.HistoricalFunding.Count == 0 ? report.Advances.Count(x => x.Currency == "EGP") : 0)),
        >= 7 and <= 11 => report.Finishings.Count(item => FinishingCategory(item) == FinishingCategories[number - 7]),
        _ => 0
    };

    private static bool IsStation(ProjectWorkRow item) =>
        item.Family == "concrete" || item.Family == "" && item.Category == "المحطة";

    private static string ScheduleDescription(ProjectScheduleRow row) =>
        string.Join(" — ", new[] { row.Category, row.Narrative, row.Reference }.Where(v => !string.IsNullOrWhiteSpace(v)));

    // Insert inside the existing worksheet. The last formatted detail row supplies styles,
    // relative formulas and horizontal merges; move the original footer intact.
    private static int ExpandDetails(XElement sheet, int number, int count, XNamespace ns)
    {
        var last = number switch { 1 or 3 => 29, 2 or 4 => 51, 5 => 17, >= 7 and <= 11 => 45, _ => 0 };
        var extra = Math.Max(0, DetailStart(number) + count - 1 - last);
        if (last == 0 || extra == 0) return 0;
        sheet.RemoveAnnotations<Dictionary<string, XElement>>();
        var data = sheet.Element(ns + "sheetData")!;
        var prototype = data.Elements(ns + "row").Single(row => (int)row.Attribute("r")! == last);
        var merges = sheet.Element(ns + "mergeCells");
        var detailMerges = merges?.Elements(ns + "mergeCell").Select(m => (string)m.Attribute("ref")!)
            .Where(range => range.Split(':').All(a => RegexRow(a) == last)).ToArray() ?? [];
        foreach (var row in data.Elements(ns + "row").Where(r => (int)r.Attribute("r")! > last))
            MoveRow(row, extra, ns);
        foreach (var range in sheet.Descendants().Where(e => e.Name == ns + "dimension" || e.Name == ns + "mergeCell")
                     .Attributes("ref"))
            range.Value = ShiftReferences(range.Value, extra, last + (range.Parent?.Name == ns + "dimension" ? 0 : 1));
        var anchor = prototype;
        for (var offset = 1; offset <= extra; offset++)
        {
            var row = new XElement(prototype);
            MoveRow(row, offset, ns);
            anchor.AddAfterSelf(row);
            anchor = row;
            foreach (var range in detailMerges)
                merges!.Add(new XElement(ns + "mergeCell", new XAttribute("ref", ShiftReferences(range, offset, last))));
        }
        merges?.SetAttributeValue("count", merges.Elements(ns + "mergeCell").Count());
        return extra;
    }

    private static void MoveRow(XElement row, int offset, XNamespace ns)
    {
        row.SetAttributeValue("r", (int)row.Attribute("r")! + offset);
        foreach (var cell in row.Elements(ns + "c"))
        {
            cell.SetAttributeValue("r", ShiftReferences((string)cell.Attribute("r")!, offset, 1));
            var formula = cell.Element(ns + "f");
            if (formula != null && formula.Value.Length > 0)
                formula.Value = ShiftReferences(formula.Value, offset, 1);
        }
    }

    private static string ShiftReferences(string text, int offset, int minimum) =>
        Regex.Replace(text, @"(?<![A-Za-z0-9_])(\$?[A-Z]{1,3})([1-9][0-9]*)(?![A-Za-z0-9_])", m =>
        {
            var row = int.Parse(m.Groups[2].Value, CultureInfo.InvariantCulture);
            return row < minimum ? m.Value : m.Groups[1].Value + (row + offset).ToString(CultureInfo.InvariantCulture);
        });

    private static void AddAdditionalPrimarySheets(ZipArchive template, ProjectReportSummary report,
        List<(string Name, XDocument Content)> sheets)
    {
        XNamespace ns = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
        foreach (var group in report.Finishings.GroupBy(FinishingCategory).Where(g => !FinishingCategories.Contains(g.Key)))
        {
            using var stream = template.GetEntry("xl/worksheets/sheet7.xml")!.Open();
            var copy = XDocument.Load(stream);
            var root = copy.Root!;
            root.Element(ns + "tableParts")?.Remove();
            root.Element(ns + "pageSetup")?.Attributes().Where(a => a.Name.LocalName == "id").Remove();
            var rows = group.ToList();
            // The other category is a separate primary schedule with the same approved finishing layout.
            PopulateTemplateSheet(copy, 7, report with { Finishings = [] });
            ExpandDetails(root, 7, rows.Count, ns);
            SetText(root, "C2", $"كشف حساب {group.Key}", ns);
            PopulateFinishingTemplate(root, rows, report, group.Key, ns);
            NormalizeCellOrder(root, ns);
            var name = Regex.Replace("حـ " + group.Key, @"[\\/?*:\[\]]", "-").TrimEnd('\'');
            name = name[..Math.Min(31, name.Length)];
            var stem = name;
            for (var i = 2; sheets.Any(s => string.Equals(s.Name, name, StringComparison.OrdinalIgnoreCase)); i++)
            {
                var suffix = " " + i;
                name = stem[..Math.Min(stem.Length, 31 - suffix.Length)] + suffix;
            }
            sheets.Add((name, copy));
        }
        foreach (var currency in report.RecognizedFunding.Concat(report.Allocations).Select(r => r.Currency)
                     .Where(c => c != "EGP").Distinct())
        {
            var rows = new List<string[]> { new[] { "نوع السجل", "التاريخ", "الحساب", "العملة", "المبلغ", "البيان", "المرجع" } };
            foreach (var (label, schedule) in new[] { ("تمويل", report.RecognizedFunding), ("توزيع", report.Allocations) })
                rows.AddRange(schedule.Where(r => r.Currency == currency).Select(r => new[] { label,
                    r.Date?.ToString("yyyy-MM-dd") ?? "", r.Category, currency, N(r.Amount), r.Narrative, r.Reference }));
            sheets.Add(($"دفعات {currency}", Sheet(rows, numericColumns: new HashSet<int> { 4 })));
        }
    }
}
