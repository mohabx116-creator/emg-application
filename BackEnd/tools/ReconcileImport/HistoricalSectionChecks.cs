using System.Data;
using System.Security.Claims;
using System.Text.Json;
using System.Globalization;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;
using EMG.Api.Modules.Projects.Services;
using EMG.Api.Modules.Reports.Controllers;
using EMG.Infrastructure.Db;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Npgsql;

internal static class HistoricalSectionChecks
{
    private sealed record Source(int ProjectId, string Workbook, string Sheet, string Cell, string Currency, decimal Amount);
    private sealed record AccountSource(Source Source, string Role, string Name, string Kind);

    internal static async Task RunAsync(string auditPath)
    {
        var connection = new NpgsqlConnectionStringBuilder(Environment.GetEnvironmentVariable("ConnectionStrings__EmgDb"));
        if (connection.Host != "127.0.0.1" || connection.Port != 55464 || connection.Database != "emg_restore")
            throw new InvalidOperationException("Expected the authorized local restore database.");
        await using var db = new EmgDbContext(new DbContextOptionsBuilder<EmgDbContext>().UseNpgsql(connection.ConnectionString).Options);
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.RepeatableRead);
        await db.Database.ExecuteSqlRawAsync("SET TRANSACTION READ ONLY");
        using var projects = SpreadsheetDocument.Open(Path.Combine(auditPath, "03_FULL_PROJECT_RECONCILIATION.xlsx"), false);
        var projectHashes = Data(projects, "Projects").GroupBy(r => int.Parse(r[2], CultureInfo.InvariantCulture))
            .ToDictionary(g => g.Key, g => g.Select(r => r[27]).Distinct().Single());
        var expected = Data(projects, "Original amounts").Where(r => r[4] == "w").Select(r => new
        {
            Row = new Source(int.Parse(r[2], CultureInfo.InvariantCulture), r[3], r[5],
                r[6], r[17], decimal.Parse(r[7], CultureInfo.InvariantCulture)),
            Family = r[11], Target = r[18],
            TargetId = int.TryParse(r[19], out var id) ? (int?)id : null
        }).ToList();
        var sections = new[]
        {
            ("movements", "SiteMovement", new[] { "site" }), ("excavation", "ExcavationRecord", new[] { "excavation" }),
            ("concrete", "ConcreteRecord", new[] { "concrete", "workmanship" }), ("steel", "SteelRecord", new[] { "steel" }),
            ("finishing", "FinishingRecord", new[] { "building", "finishing" })
        };
        var actual = new List<Source>();
        var links = 0;
        foreach (var (project, sha) in projectHashes)
        {
            foreach (var (section, target, families) in sections)
            {
                var result = await HistoricalWorkReader.ReadSectionAsync(db, project, section, null, true, default)
                    ?? throw new InvalidDataException($"Missing project {project}.");
                var sectionRows = result.Rows.Select(r => new Source(r.ProjectId, r.SourceFileName, r.Sheet, r.Cell, r.Currency, r.Amount)).ToArray();
                var detail = expected.Where(e => e.Row.ProjectId == project && families.Contains(e.Family)).Select(e => e.Row);
                Match(detail, sectionRows, $"{project}/{section}");
                if (result.Rows.Any(r => !sha.Equals(r.SourceSha256, StringComparison.OrdinalIgnoreCase)))
                    throw new InvalidDataException($"Workbook identity differs for {project}/{section}.");
                Match(expected.Where(e => e.Row.ProjectId == project && e.Target == target).Select(e => e.TargetId!.Value),
                    result.LinkedOperationalIds, $"{project}/{target}");
                links += result.LinkedOperationalIds.Count;
                actual.AddRange(sectionRows);
            }
        }
        Match(expected.Select(e => e.Row), actual, "all sections");
        if (await HistoricalWorkReader.ReadSectionAsync(db, projectHashes.Keys.First(), "steel", null, false, default) != null)
            throw new InvalidDataException("Unassigned principal gained access.");
        using var counterparties = SpreadsheetDocument.Open(Path.Combine(auditPath, "05_COUNTERPARTY_AND_SETTLEMENT_AUDIT.xlsx"), false);
        var expectedAccount = Data(counterparties, "Named work lines").Select(r => new AccountSource(
            new(int.Parse(r[1], CultureInfo.InvariantCulture), r[2], r[3], r[4], r[5],
                decimal.Parse(r[9], CultureInfo.InvariantCulture)), r[7], r[8], "works"))
            .Concat(Data(counterparties, "All settlements").Where(r => r[11].Length > 0).Select(r => new AccountSource(
                new(int.Parse(r[2], CultureInfo.InvariantCulture), r[3], r[4], r[5], r[6],
                    decimal.Parse(r[7], CultureInfo.InvariantCulture)), r[10], r[11], "settlements"))).ToList();
        var controller = new HistoricalAccountsController(db)
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext {
                User = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.Role, "admin")], "read-check"))
            } }
        };
        var parties = await db.Suppliers.Select(s => new { s.Id, s.Name, Role = "supplier" })
            .Concat(db.Contractors.Select(c => new { c.Id, c.Name, Role = "contractor" })).ToListAsync();
        var actualAccount = new List<AccountSource>();
        foreach (var party in parties)
        {
            var result = party.Role == "supplier" ? await controller.Supplier(party.Id, default) : await controller.Contractor(party.Id, default);
            if (result is NotFoundResult) continue;
            if (result is not OkObjectResult ok) throw new InvalidDataException("Counterparty read failed.");
            var json = JsonSerializer.SerializeToElement(ok.Value, new JsonSerializerOptions(JsonSerializerDefaults.Web));
            foreach (var kind in new[] { "works", "settlements" })
                foreach (var row in json.GetProperty(kind).EnumerateArray())
                {
                    string Field(string name) => row.GetProperty(name).GetString()!;
                    actualAccount.Add(new(new(row.GetProperty("projectId").GetInt32(), Field("sourceFileName"), Field("sheet"),
                        Field("cell"), Field("currency"), row.GetProperty("amount").GetDecimal()), party.Role, party.Name, kind));
                }
        }
        Match(expectedAccount, actualAccount, "counterparty works and settlements");
        Console.WriteLine($"Matched {projectHashes.Count} projects, {actual.Count} work identities and {links} native links. " +
            $"Named account works: {actualAccount.Count(a => a.Kind == "works")}; settlements: {actualAccount.Count(a => a.Kind == "settlements")}.");
        foreach (var group in expected.GroupBy(e => (e.Family, e.Row.Currency)))
            Console.WriteLine($"{group.Key}: {group.Count()} signed rows, {group.Sum(e => e.Row.Amount):F2}");
    }

    private static IEnumerable<string[]> Data(SpreadsheetDocument workbook, string name)
    {
        var part = workbook.WorkbookPart!;
        var sheet = part.Workbook.Sheets!.Elements<Sheet>().Single(s => s.Name == name);
        var worksheet = (WorksheetPart)part.GetPartById(sheet.Id!);
        var strings = part.SharedStringTablePart?.SharedStringTable.Elements<SharedStringItem>().Select(s => s.InnerText).ToArray();
        foreach (var row in worksheet.Worksheet.GetFirstChild<SheetData>()!.Elements<Row>().Where(r => r.RowIndex!.Value > 3))
        {
            var values = new string[33];
            Array.Fill(values, string.Empty);
            foreach (var cell in row.Elements<Cell>())
            {
                var column = 0;
                foreach (char ch in cell.CellReference!.Value!.TakeWhile(char.IsAsciiLetterUpper))
                    column = column * 26 + ch - 'A' + 1;
                if (column >= values.Length) continue;
                var value = cell.CellValue?.InnerText ?? cell.InlineString?.InnerText ?? string.Empty;
                values[column] = cell.DataType?.Value == CellValues.SharedString
                    ? strings![int.Parse(value, CultureInfo.InvariantCulture)] : value;
            }
            yield return values;
        }
    }

    private static void Match<T>(IEnumerable<T> expected, IEnumerable<T> actual, string context) where T : notnull
    {
        var reference = expected.ToList();
        var rows = actual.ToList();
        if (reference.Count != reference.Distinct().Count() || rows.Count != rows.Distinct().Count() ||
            !reference.ToHashSet().SetEquals(rows))
            throw new InvalidDataException($"Signed source identity or duplicate mismatch in {context} ({reference.Count} expected; {rows.Count} actual).");
    }
}