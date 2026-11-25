using System.Globalization;
using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using EMG.Api.Modules.UniversalImports;
using Microsoft.AspNetCore.Http;

namespace EMG.WebApi.Tests;

public sealed class UniversalImportCoverageCrosswalkTests
{
    [Fact]
    public async Task AuditBlocksAndRowsReceiveExactlyOneAuditableOutcome()
    {
        var source = Environment.GetEnvironmentVariable("EMG_UNIVERSAL_IMPORT_SOURCE_DIR");
        var audit = Environment.GetEnvironmentVariable("EMG_UNIVERSAL_IMPORT_AUDIT_DIR");
        var output = Environment.GetEnvironmentVariable("EMG_UNIVERSAL_IMPORT_RESULTS_DIR");
        if (string.IsNullOrWhiteSpace(source) || string.IsNullOrWhiteSpace(audit))
            throw new InvalidOperationException("Set EMG_UNIVERSAL_IMPORT_SOURCE_DIR and EMG_UNIVERSAL_IMPORT_AUDIT_DIR for the approved golden corpus.");

        var blocks = ReadCsv(Path.Combine(audit, "SEMANTIC_BLOCK_CLASSIFICATION.csv"));
        var rows = ReadCsv(Path.Combine(audit, "SOURCE_RECORD_CLASSIFICATION.csv"));
        Assert.Equal(680, blocks.Count);
        Assert.Equal(11931, rows.Count);

        var service = new UniversalImportPreviewService(new UniversalImportMappingRegistry());
        var previews = new Dictionary<string, UniversalPreviewResponse>(StringComparer.OrdinalIgnoreCase);
        foreach (var path in Directory.GetFiles(source, "*.xlsx", SearchOption.TopDirectoryOnly))
        {
            await using var stream = File.OpenRead(path);
            var formFile = new FormFile(stream, 0, stream.Length, "file", Path.GetFileName(path));
            previews[Path.GetFileName(path)] = await service.PreviewAsync(formFile, CancellationToken.None);
        }
        Assert.Equal(40, previews.Count);

        var blockOut = new List<string> { "file,sheet,audit_range,outcome,engine_sections,reason" };
        foreach (var row in blocks)
        {
            var file = row["file_name"];
            var sheet = row["sheet"];
            var auditRange = row["range"];
            var auditRows = ParseRowRange(auditRange);
            var engine = previews[file].Sections.Where(item => item.SheetName == sheet && Overlaps(ParseRowRange(item.SourceRange), auditRows)).ToList();
            var classification = row["semantic_classification"];
            var outcome = classification.Contains("SUMMARY", StringComparison.OrdinalIgnoreCase) || classification.Contains("REFERENCE", StringComparison.OrdinalIgnoreCase)
                ? "DERIVED_OR_SUMMARY"
                : engine.Count == 0 ? "DETECTOR_ERROR"
                : engine.Count > 1 ? "SPLIT_INTO" : "MAPPED";
            var reason = outcome switch
            {
                "DERIVED_OR_SUMMARY" => "Audit block is derived, reference, or summary material and is excluded from posting.",
                "SPLIT_INTO" => "Independent source column streams or structural sub-blocks are represented by multiple preview sections.",
                "DETECTOR_ERROR" => "No preview section overlaps the audited source range.",
                _ => "Preview section retains the audited sheet and row range provenance."
            };
            blockOut.Add(string.Join(',', Csv(file), Csv(sheet), Csv(auditRange), outcome, engine.Count, Csv(reason)));
        }

        var rowOut = new List<string> { "file,sheet,source_row,audit_record_type,outcome,engine_record_count,eligible_engine_count,reason" };
        var ambiguousOut = new List<string> { "file,sheet,source_row,audit_record_type,outcome,reason" };
        foreach (var row in rows)
        {
            var preview = previews[row["file_name"]];
            var sourceRow = int.Parse(row["source_row"], CultureInfo.InvariantCulture);
            var matches = preview.Records.Where(item => item.SheetName == row["sheet"] && ParseSourceRow(item.SourceCoordinate) == sourceRow).ToList();
            var auditType = row["record_type"];
            var outcome = auditType is "DERIVED_SUMMARY" or "REFERENCE_SUMMARY" or "CUMULATIVE_OR_PRIOR" ? "DERIVED_OR_SUMMARY"
                : auditType is "TEXT_OR_METADATA" ? "NON_TRANSACTION"
                : auditType == "UNRESOLVED_PARALLEL" && matches.Count == 0 ? "UNRESOLVED"
                : matches.Count == 0 ? "DETECTOR_ERROR"
                : matches.Any(item => item.RecordType == UniversalRecordType.Unresolved) ? "UNRESOLVED"
                : "MAPPED";
            var reason = outcome switch
            {
                "DERIVED_OR_SUMMARY" => "Audit row is a derived or summary value; it is not a source transaction.",
                "NON_TRANSACTION" => "Audit row is text, heading, or metadata.",
                "DETECTOR_ERROR" => "No extracted record retained the source row provenance.",
                "UNRESOLVED" when auditType == "UNRESOLVED_PARALLEL" => "Parallel source row has no standalone row identity; it remains unresolved rather than being invented.",
                "UNRESOLVED" => "Source evidence remains insufficient for a safe business classification.",
                _ => "Source row has a deterministic preview record and provenance."
            };
            rowOut.Add(string.Join(',', Csv(row["file_name"]), Csv(row["sheet"]), sourceRow, auditType, outcome, matches.Count, matches.Count(item => item.EligibleForPosting), Csv(reason)));
            if (auditType is "NUMERIC_UNRESOLVED" or "UNRESOLVED_PARALLEL")
                ambiguousOut.Add(string.Join(',', Csv(row["file_name"]), Csv(row["sheet"]), sourceRow, auditType, outcome, Csv(reason)));
        }

        if (!string.IsNullOrWhiteSpace(output))
        {
            Directory.CreateDirectory(output);
            await File.WriteAllLinesAsync(Path.Combine(output, "universal-import-block-crosswalk.csv"), blockOut);
            await File.WriteAllLinesAsync(Path.Combine(output, "universal-import-row-crosswalk.csv"), rowOut);
            await File.WriteAllLinesAsync(Path.Combine(output, "universal-import-ambiguous-row-crosswalk.csv"), ambiguousOut);
            var duplicateOut = new List<string> { "file,fingerprint_sha256,count,outcome,reason" };
            foreach (var pair in previews)
                duplicateOut.AddRange(pair.Value.DuplicateCandidates.Select(candidate => string.Join(',', Csv(pair.Key), Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(candidate.ValueFingerprint))), candidate.Count, "DUPLICATE_CANDIDATE", Csv("Retained for review; no source record was deleted."))));
            await File.WriteAllLinesAsync(Path.Combine(output, "universal-import-duplicate-candidates.csv"), duplicateOut);
            await File.WriteAllLinesAsync(Path.Combine(output, "universal-import-crosswalk-summary.csv"),
                ["scope,outcome,count", .. Summaries(blockOut.Skip(1), 3, 4, "blocks"), .. Summaries(rowOut.Skip(1), 4, 5, "rows"), .. Summaries(ambiguousOut.Skip(1), 4, 5, "ambiguous_rows")]);
        }
        Assert.Equal(blocks.Count + 1, blockOut.Count);
        Assert.Equal(rows.Count + 1, rowOut.Count);
    }

    private static IEnumerable<string> Summaries(IEnumerable<string> lines, int outcomeIndex, int unused, string scope)
    {
        return lines.Select(ParseCsvLine).GroupBy(item => item[outcomeIndex], StringComparer.Ordinal)
            .Select(group => $"{scope},{group.Key},{group.Count()}");
    }

    private static List<Dictionary<string, string>> ReadCsv(string path)
    {
        var lines = File.ReadAllLines(path);
        var headers = ParseCsvLine(lines[0]);
        return lines.Skip(1).Where(line => line.Length > 0).Select(line =>
        {
            var values = ParseCsvLine(line);
            return headers.Select((header, index) => new { header, value = index < values.Count ? values[index] : string.Empty })
                .ToDictionary(item => item.header, item => item.value, StringComparer.OrdinalIgnoreCase);
        }).ToList();
    }

    private static List<string> ParseCsvLine(string line)
    {
        var values = new List<string>(); var value = new StringBuilder(); var quoted = false;
        for (var i = 0; i < line.Length; i++)
        {
            var ch = line[i];
            if (ch == '"' && quoted && i + 1 < line.Length && line[i + 1] == '"') { value.Append('"'); i++; continue; }
            if (ch == '"') { quoted = !quoted; continue; }
            if (ch == ',' && !quoted) { values.Add(value.ToString()); value.Clear(); continue; }
            value.Append(ch);
        }
        values.Add(value.ToString());
        return values;
    }

    private static (int First, int Last) ParseRowRange(string range)
    {
        var matches = Regex.Matches(range ?? string.Empty, "\\d+").Select(item => int.Parse(item.Value, CultureInfo.InvariantCulture)).ToList();
        return matches.Count switch { 0 => (0, 0), 1 => (matches[0], matches[0]), _ => (matches[0], matches[1]) };
    }

    private static bool Overlaps((int First, int Last) left, (int First, int Last) right) => left.First <= right.Last && right.First <= left.Last;
    private static int ParseSourceRow(string coordinate) => ParseRowRange(coordinate[(coordinate.LastIndexOf('!') + 1)..]).First;
    private static string Csv(string value) => $"\"{value.Replace("\"", "\"\"", StringComparison.Ordinal)}\"";
}
