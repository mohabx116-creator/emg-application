using System.IO.Compression;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace EMG.WebApi.Tests;

internal static class TemplateRetentionProbe
{
    private static readonly XNamespace Ns = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
    private record Cell(string Address, string Type, string? Value, string? Formula, string? Cache, int? SharedIndex);

    public static void Compare(string directory, byte[] emptyBytes, byte[] populatedBytes)
    {
        using var template = ZipFile.OpenRead(Path.Combine(AppContext.BaseDirectory, "Modules", "Reports", "Templates", "ProjectReportTemplate.xlsx"));
        using var empty = new ZipArchive(new MemoryStream(emptyBytes));
        using var populated = new ZipArchive(new MemoryStream(populatedBytes));
        var report = new StringBuilder();
        var rows = new List<string> { "sheet\tcell\tclass\tempty\tpopulated\tkind" };
        var inputs = new[] { template, empty, populated };
        var shared = inputs.Select(a => Xml(a, "xl/sharedStrings.xml").Root!.Elements(Ns + "si").Select(SharedText).ToArray()).ToArray();
        var titles = inputs.Select(a => Xml(a, "xl/workbook.xml").Descendants(Ns + "sheet").Select(s => (string?)s.Attribute("name")).ToArray()).ToArray();
        var headers = template.Entries.Where(e => Regex.IsMatch(e.FullName, @"^xl/tables/table\d+\.xml$"))
            .SelectMany(e => Xml(template, e.FullName).Descendants(Ns + "tableColumn"))
            .Select(c => (string)c.Attribute("name")!).ToHashSet(StringComparer.Ordinal);
        var used = new[] { new HashSet<int>(), new HashSet<int>(), new HashSet<int>() };
        var cellsBySheet = new List<Cell[][]>();
        for (int index = 1; index <= 13; index++)
        {
            var path = $"xl/worksheets/sheet{index}.xml";
            var sheets = inputs.Select((zip, j) => zip.GetEntry(path) is null ? [] : Cells(zip, path, shared[j])).ToArray();
            cellsBySheet.Add(sheets);
            for (var j = 0; j < inputs.Length; j++)
                foreach (var cell in sheets[j])
                    if (cell.SharedIndex.HasValue) used[j].Add(cell.SharedIndex.Value);
        }
        for (int index = 1; index <= 11; index++)
        {
            var sheets = cellsBySheet[index - 1];
            var outputs = sheets.Skip(1).Select(a => a.ToDictionary(c => c.Address)).ToArray();
            foreach (var c in sheets[0])
            {
                var a = outputs[0].GetValueOrDefault(c.Address);
                var b = outputs[1].GetValueOrDefault(c.Address);
                if (c.Formula is not null)
                {
                    rows.Add($"{index}\t{c.Address}\tformula\t{FormulaState(c,a)}\t{FormulaState(c,b)}\tformula");
                    if (!string.IsNullOrEmpty(c.Cache))
                        rows.Add($"{index}\t{c.Address}\t{(c.Cache == "0" ? "zero_cache" : "nonzero_cache")}\t{CacheState(c,a)}\t{CacheState(c,b)}\t{c.Type}");
                    continue;
                }
                if (string.IsNullOrEmpty(c.Value)) continue;
                var kind = c.Type is "s" or "inlineStr" or "str" ? "text" : c.Type;
                var classification = kind == "text" && headers.Contains(c.Value) ? "table_label"
                    : c.Value == "0" && kind == "n" ? "zero_literal" : "unresolved_literal";
                rows.Add($"{index}\t{c.Address}\t{classification}\t{LiteralState(c,a)}\t{LiteralState(c,b)}\t{kind}");
            }
        }
        report.AppendLine($"Sheets template={titles[0].Length}; empty={titles[1].Length}; populated={titles[2].Length}");
        report.AppendLine($"Original titles retained empty={titles[0].SequenceEqual(titles[1].Take(11))}; populated={titles[0].SequenceEqual(titles[2].Take(11))}");
        report.AppendLine($"Original shared strings retained empty={shared[0].SequenceEqual(shared[1])}; populated={shared[0].SequenceEqual(shared[2])}; count={shared[0].Length}");
        report.AppendLine($"Shared-string counts template={shared[0].Length}; empty={shared[1].Length}; populated={shared[2].Length}");
        report.AppendLine($"Output orphan shared strings empty={shared[1].Length - used[1].Count}; populated={shared[2].Length - used[2].Count}");
        report.AppendLine($"Unused original shared-string entries empty={used[0].Except(used[1]).Count()}; populated={used[0].Except(used[2]).Count()}");
        report.AppendLine("Original shared-string indexes no longer addressed by any worksheet cell:");
        report.AppendLine($"empty: {string.Join(",", used[0].Except(used[1]).Order())}");
        report.AppendLine($"populated: {string.Join(",", used[0].Except(used[2]).Order())}");
        foreach (var e in template.Entries)
        {
            var source = Read(e);
            var same = inputs.Skip(1).Select(z => z.GetEntry(e.FullName) is { } item && source.AsSpan().SequenceEqual(Read(item))).ToArray();
            report.AppendLine($"Part {e.FullName}: empty={(same[0] ? "identical" : "changed-or-absent")} populated={(same[1] ? "identical" : "changed-or-absent")}");
        }
        File.WriteAllLines(Path.Combine(directory, "coordinate-comparison.tsv"), rows);
        File.WriteAllText(Path.Combine(directory, "package-comparison.txt"), report.ToString());
    }

    public static void AssertReviewedLegacyPayloadsAbsent(string originalTemplatePath, params byte[][] exports)
    {
        using var original = ZipFile.OpenRead(originalTemplatePath);
        var originalShared = Xml(original, "xl/sharedStrings.xml").Root!.Elements(Ns + "si").Select(SharedText).ToArray();
        var reviewedIndexes = new[] { 76, 77, 126, 170, 177, 194 };
        var payloads = reviewedIndexes.Select(index => originalShared[index]).ToHashSet(StringComparer.Ordinal);
        var oldTitle = Xml(original, "xl/workbook.xml").Descendants(Ns + "sheet").ElementAt(1).Attribute("name")!.Value;
        var retained = 0;
        foreach (var export in exports)
        {
            using var zip = new ZipArchive(new MemoryStream(export));
            foreach (var entry in zip.Entries.Where(entry => entry.FullName.EndsWith(".xml", StringComparison.OrdinalIgnoreCase)
                                                              || entry.FullName.EndsWith(".rels", StringComparison.OrdinalIgnoreCase)))
            {
                var text = Encoding.UTF8.GetString(Read(entry));
                retained += payloads.Count(text.Contains);
                if (text.Contains(oldTitle, StringComparison.Ordinal)) retained++;
            }
        }
        if (retained != 0)
            throw new InvalidDataException($"Synthetic exports retained {retained} reviewed legacy payload references.");
    }

    public static void AssertNoOrphanSharedStrings(params byte[][] exports)
    {
        foreach (var export in exports)
        {
            using var zip = new ZipArchive(new MemoryStream(export));
            var items = Xml(zip, "xl/sharedStrings.xml").Root!.Elements(Ns + "si").ToArray();
            var used = new HashSet<int>();
            foreach (var entry in zip.Entries.Where(entry => Regex.IsMatch(entry.FullName, @"^xl/worksheets/sheet\d+\.xml$")))
            {
                foreach (var cell in Xml(zip, entry.FullName).Descendants(Ns + "c").Where(cell => (string?)cell.Attribute("t") == "s"))
                {
                    var index = (int?)cell.Element(Ns + "v") ?? throw new InvalidDataException("Missing shared-string index.");
                    if (index < 0 || index >= items.Length) throw new InvalidDataException("Shared-string index is out of range.");
                    used.Add(index);
                }
            }
            if (used.Count != items.Length || !used.Order().SequenceEqual(Enumerable.Range(0, items.Length)))
                throw new InvalidDataException("Synthetic export contains unreachable shared strings.");
        }
    }

    public static void AssertReviewedFormulaCachesCleared(params byte[][] exports)
    {
        foreach (var export in exports)
        {
            using var zip = new ZipArchive(new MemoryStream(export));
            foreach (var (sheet, address) in new[] { (4, "D46"), (9, "D11") })
            {
                var cell = Xml(zip, $"xl/worksheets/sheet{sheet}.xml").Descendants(Ns + "c")
                    .Single(item => (string?)item.Attribute("r") == address);
                if (cell.Element(Ns + "f") is null || cell.Element(Ns + "v") is not null || (string?)cell.Attribute("t") != "str")
                    throw new InvalidDataException("Reviewed formula-cache contract was not preserved.");
            }
            var calculation = Xml(zip, "xl/workbook.xml").Descendants(Ns + "calcPr").Single();
            if ((string?)calculation.Attribute("calcMode") != "auto" ||
                (string?)calculation.Attribute("fullCalcOnLoad") != "1" ||
                (string?)calculation.Attribute("forceFullCalc") != "1")
                throw new InvalidDataException("Workbook recalculation settings are incomplete.");
        }
    }

    private static Cell[] Cells(ZipArchive zip, string path, string[] shared)
    {
        return Xml(zip, path).Descendants(Ns + "c").Select(c =>
        {
            var type = (string?)c.Attribute("t") ?? "n";
            var raw = (string?)c.Element(Ns + "v");
            var formula = c.Element(Ns + "f");
            int? index = type == "s" && int.TryParse(raw, out var i) ? i : null;
            var value = index.HasValue ? shared[index.Value]
                : type == "inlineStr" ? SharedText(c.Element(Ns + "is")!) : raw;
            return new Cell((string)c.Attribute("r")!, type, formula is null ? value : null,
                formula?.ToString(SaveOptions.DisableFormatting), formula is not null ? raw : null, index);
        }).ToArray();
    }

    private static string FormulaState(Cell original, Cell? output) =>
        output?.Formula is null ? "removed" : original.Formula == output.Formula ? "retained" : "changed";
    private static string CacheState(Cell original, Cell? output) =>
        output?.Cache is null or "" ? "removed" : original.Cache == output.Cache ? "retained" : "changed";
    private static string LiteralState(Cell original, Cell? output) =>
        output?.Formula is not null ? "replaced-by-formula" : string.IsNullOrEmpty(output?.Value) ? "cleared"
        : original.Value != output.Value ? "overwritten"
        : original.Type == output.Type && original.SharedIndex == output.SharedIndex ? "same-token" : "same-value-reencoded";
    private static string SharedText(XElement e) => string.Concat(e.Descendants(Ns + "t").Select(t => t.Value));
    private static XDocument Xml(ZipArchive z, string path)
    {
        using var stream = z.GetEntry(path)!.Open();
        return XDocument.Load(stream);
    }
    private static byte[] Read(ZipArchiveEntry e)
    {
        using var source = e.Open();
        using var buffer = new MemoryStream();
        source.CopyTo(buffer);
        return buffer.ToArray();
    }
}
