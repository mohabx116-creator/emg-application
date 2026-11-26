using System.Globalization;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.RegularExpressions;
using ClosedXML.Excel;
using EMG.Domain.Entities;
using EMG.Infrastructure.Db;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace EMG.Api.Modules.UniversalImports;

/// <summary>
/// Internal, opt-in migration runner. It reuses the normal workbook preview and
/// atomic project import path, while retaining every preview row in the
/// HistoricalImportBatch. It is deliberately unavailable in Production and on
/// the permanent PostgreSQL port.
/// </summary>
public sealed class HistoricalBulkMigrationService(
    EmgDbContext db,
    IUniversalImportPreviewService previewService,
    ReviewedWorkbookSnapshots snapshots,
    ReviewedWorkbookImportService imports,
    IConfiguration configuration,
    IWebHostEnvironment environment)
{
    public async Task<HistoricalBulkMigrationResult> RunAsync(int actor, CancellationToken cancellationToken)
    {
        EnsureEnabled();
        var root = ResolveSourceRoot();
        var manifest = await LoadManifestAsync(root, cancellationToken);
        var candidates = manifest.Where(item => item.Kind == "project_candidate").ToArray();
        var results = new List<HistoricalBulkMigrationProjectResult>();
        var skipped = manifest.Where(item => item.Kind != "project_candidate")
            .Select(item => $"{item.FileName}: {item.Kind} محفوظ كمرجع ولم يُنشئ مشروعاً.").ToList();

        foreach (var source in candidates)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var path = Path.GetFullPath(Path.Combine(root, source.ArchivePath.Replace('/', Path.DirectorySeparatorChar)));
            EnsureInside(root, path);
            if (!File.Exists(path))
            {
                results.Add(Failed(source, "ملف المصدر غير موجود."));
                continue;
            }

            var actualHash = await ComputeSha256Async(path, cancellationToken);
            if (!string.Equals(actualHash, source.Sha256, StringComparison.OrdinalIgnoreCase))
            {
                results.Add(Failed(source, "بصمة المصدر لا تطابق manifest."));
                continue;
            }

            await using var stream = File.OpenRead(path);
            var formFile = new FileFormFile(stream, source.FileName, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet");
            var preview = await previewService.PreviewAsync(formFile, cancellationToken);
            var snapshotId = snapshots.Store(actor, 0, preview);
            try
            {
                var projectName = Path.GetFileNameWithoutExtension(source.FileName);
                if (projectName.Length > 250) projectName = projectName[..250];
                var projectCode = preview.SourceProjectCode ?? $"H-{source.Sha256[..12]}";
                var request = new UniversalNewProjectRequest(snapshotId, projectName, projectCode,
                    preview.ProjectMetadata?.ClientName, preview.ProjectMetadata?.OwnerName, preview.ProjectMetadata?.PayerName,
                    preview.ProjectMetadata?.Location, null, null, true);
                var baseline = new HistoricalMigrationBaseline(preview.Totals
                    .Select(total => new HistoricalOpeningBalanceSeed(total.StreamKey, total.Currency, total.Amount,
                        $"{source.FileName}:{total.StreamKey}"))
                    .ToArray(), ExtractContractorNames(path), source.FileName);
                var committed = await imports.CommitNewProjectAsync(request, actor, cancellationToken, baseline);
                results.Add(new(source.FileName, source.Sha256, preview.SourceProjectCode, committed.ProjectId, committed.BatchId,
                    "MIGRATED", preview.Records.Count, committed.Counts.Values.Sum(), committed.RetainedRecords,
                    preview.Totals.GroupBy(item => item.Currency).ToDictionary(group => group.Key, group => group.Sum(item => item.Amount)),
                    preview.Exceptions));
            }
            catch (Exception error) when (error is InvalidOperationException or DbUpdateException or PostgresException)
            {
                results.Add(new(source.FileName, source.Sha256, preview.SourceProjectCode, null, null, "FAILED",
                    preview.Records.Count, 0, preview.Records.Count, new Dictionary<string, decimal>(), [error.Message]));
            }
        }

        return new(root, DateTime.UtcNow, candidates.Length, results.Count(item => item.Status == "MIGRATED"),
            results.Sum(item => item.PreviewRecordCount), results, skipped);
    }

    private void EnsureEnabled()
    {
        if (environment.IsProduction() || !configuration.GetValue<bool>("HistoricalMigration:Enabled"))
            throw new InvalidOperationException("الترحيل التاريخي الداخلي غير مفعّل لهذه البيئة.");
        if (!db.Database.IsNpgsql()) throw new InvalidOperationException("يلزم PostgreSQL معزول للترحيل التاريخي.");
        var connection = new NpgsqlConnectionStringBuilder(db.Database.GetConnectionString());
        if (connection.Port == 5432 || connection.Database is not { } database || !IsAuthorizedLocalTarget(connection, database))
            throw new InvalidOperationException("الترحيل التاريخي مسموح فقط على قاعدة QA معزولة.");
    }

    private bool IsAuthorizedLocalTarget(NpgsqlConnectionStringBuilder connection, string database) =>
        database.StartsWith("emg_historical_qa_", StringComparison.Ordinal) ||
        (configuration.GetValue<bool>("HistoricalMigration:AllowAuthorizedLocalTarget") &&
         database == "emg_restore" && connection.Port == 55464 && (connection.Host is "127.0.0.1" or "localhost"));

    private string ResolveSourceRoot()
    {
        var configured = configuration["HistoricalMigration:SourceRoot"];
        if (string.IsNullOrWhiteSpace(configured)) throw new InvalidOperationException("مسار حزمة البيانات التاريخية غير مضبوط.");
        var root = Path.GetFullPath(configured);
        var originals = Path.Combine(root, "Originals");
        if (!Directory.Exists(originals) || !File.Exists(Path.Combine(root, "Index", "workbooks.csv")))
            throw new InvalidOperationException("حزمة البيانات التاريخية غير مكتملة.");
        return root;
    }

    private static async Task<string> ComputeSha256Async(string path, CancellationToken cancellationToken)
    {
        await using var stream = File.OpenRead(path);
        return Convert.ToHexString(await SHA256.HashDataAsync(stream, cancellationToken)).ToLowerInvariant();
    }

    private static IReadOnlyList<string> ExtractContractorNames(string path)
    {
        using var workbook = new XLWorkbook(path);
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var cell in workbook.Worksheets.SelectMany(sheet => sheet.CellsUsed()))
        {
            if (cell.DataType != XLDataType.Text) continue;
            var text = cell.GetString().Trim();
            if (!text.Contains("المقاول", StringComparison.Ordinal)) continue;
            var match = Regex.Match(text, @"المقاول(?:ين)?\s*[:：]\s*(?<name>.+)$", RegexOptions.CultureInvariant);
            if (!match.Success) continue;
            var name = Regex.Replace(match.Groups["name"].Value.Trim(), @"\s+", " ");
            if (name.Length is > 1 and <= 250 && !name.Contains("حساب", StringComparison.Ordinal)) names.Add(name);
        }
        return names.OrderBy(item => item, StringComparer.Ordinal).ToArray();
    }

    private static async Task<IReadOnlyList<ManifestRow>> LoadManifestAsync(string root, CancellationToken cancellationToken)
    {
        var lines = await File.ReadAllLinesAsync(Path.Combine(root, "Index", "workbooks.csv"), cancellationToken);
        if (lines.Length < 2) throw new InvalidOperationException("ملف manifest فارغ.");
        var header = ParseCsv(lines[0]);
        int Index(string name) => Array.FindIndex(header, item => item == name);
        var fileIndex = Index("file"); var archiveIndex = Index("archive_path"); var hashIndex = Index("sha256"); var kindIndex = Index("kind");
        if (new[] { fileIndex, archiveIndex, hashIndex, kindIndex }.Any(item => item < 0)) throw new InvalidOperationException("manifest يفتقد أعمدة المصدر.");
        return lines.Skip(1).Where(line => !string.IsNullOrWhiteSpace(line)).Select(line =>
        {
            var values = ParseCsv(line);
            return new ManifestRow(values[fileIndex], values[archiveIndex], values[hashIndex], values[kindIndex]);
        }).ToArray();
    }

    private static string[] ParseCsv(string line)
    {
        var values = new List<string>(); var current = new System.Text.StringBuilder(); var quoted = false;
        for (var i = 0; i < line.Length; i++)
        {
            var ch = line[i];
            if (ch == '"' && quoted && i + 1 < line.Length && line[i + 1] == '"') { current.Append('"'); i++; continue; }
            if (ch == '"') { quoted = !quoted; continue; }
            if (ch == ',' && !quoted) { values.Add(current.ToString()); current.Clear(); continue; }
            current.Append(ch);
        }
        values.Add(current.ToString()); return values.ToArray();
    }

    private static void EnsureInside(string root, string path)
    {
        var rootWithSlash = Path.TrimEndingDirectorySeparator(root) + Path.DirectorySeparatorChar;
        if (!path.StartsWith(rootWithSlash, StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("مسار المصدر خارج الحزمة المعتمدة.");
    }

    private static HistoricalBulkMigrationProjectResult Failed(ManifestRow item, string error) =>
        new(item.FileName, item.Sha256, null, null, null, "FAILED", 0, 0, 0, new Dictionary<string, decimal>(), [error]);

    private sealed record ManifestRow(string FileName, string ArchivePath, string Sha256, string Kind);

    private sealed class FileFormFile(Stream stream, string fileName, string contentType) : IFormFile
    {
        public Stream OpenReadStream() => stream;
        public long Length => stream.Length;
        public string FileName { get; } = fileName;
        public string Name => "file";
        public string ContentType { get; } = contentType;
        public string ContentDisposition => $"form-data; name=\"file\"; filename=\"{FileName}\"";
        public IHeaderDictionary Headers { get; } = new HeaderDictionary();
        public void CopyTo(Stream target) => stream.CopyTo(target);
        public Task CopyToAsync(Stream target, CancellationToken cancellationToken = default) => stream.CopyToAsync(target, cancellationToken);
    }
}
