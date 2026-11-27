using System.Data;
using System.Globalization;
using System.IO.Compression;
using System.Xml.Linq;
using EMG.Api.Modules.FinalAccounts.Services;
using EMG.Api.Modules.Reports.Services;
using EMG.Infrastructure.Db;
using Microsoft.EntityFrameworkCore;
using Npgsql;

internal static class PrimaryReportChecks
{
    public static async Task RunAsync()
    {
        var config = new NpgsqlConnectionStringBuilder(Environment.GetEnvironmentVariable("ConnectionStrings__EmgDb"));
        if (config.Host != "127.0.0.1" || config.Port != 55464 || config.Database != "emg_restore")
            throw new InvalidOperationException("Only the owner-authorized local database can be checked.");
        await using var db = new EmgDbContext(new DbContextOptionsBuilder<EmgDbContext>().UseNpgsql(config.ConnectionString).Options);
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.RepeatableRead);
        await db.Database.ExecuteSqlRawAsync("SET TRANSACTION READ ONLY");
        var accounts = new FinalAccountsService(db);
        var reports = new ReportsService(db, accounts);
        var projects = await db.HistoricalImportBatches.Where(b => b.Status == "POSTED" && b.MappingVersion == "universal-v1.1")
            .Select(b => b.ProjectId).Distinct().Order().ToListAsync();
        if (projects.Count == 0) throw new InvalidDataException("No historical projects found.");
        XNamespace ns = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
        XNamespace rel = "http://schemas.openxmlformats.org/officeDocument/2006/relationships";
        foreach (var project in projects)
        {
            var account = (await accounts.GetAsync(project, null, true, default))!;
            var original = await db.HistoricalWorkLines.Where(w => w.Batch.ProjectId == project && w.Currency == "EGP")
                .Select(w => new { w.Family, w.Category, w.Sheet, w.Cell, w.Amount }).ToListAsync();
            using var zip = new ZipArchive(new MemoryStream((await reports.ExportProjectWorkbookAsync(project, null, true, default))!));
            XDocument Read(string path) { using var input = zip.GetEntry(path)!.Open(); return XDocument.Load(input); }
            var paths = Read("xl/_rels/workbook.xml.rels").Root!.Elements().ToDictionary(r => (string)r.Attribute("Id")!, r => "xl/" + (string)r.Attribute("Target")!);
            var sheets = Read("xl/workbook.xml").Descendants(ns + "sheet").ToDictionary(s => (string)s.Attribute("name")!,
                s => paths[(string)s.Attribute(rel + "id")!]);
            static string Value(XElement cell, XNamespace ns) => cell?.Element(ns + "v")?.Value ?? cell?.Element(ns + "is")?.Value ?? "";
            static decimal Number(string value) => string.IsNullOrEmpty(value) ? 0 : decimal.Parse(value, CultureInfo.InvariantCulture);
            var actual = new Dictionary<(string Family, string Source), decimal>();
            var primary = new Dictionary<string, decimal>();
            decimal printedWorks = 0;
            void CheckSheet(string name, string family, string amountCol, string noteCol, int start, int end)
            {
                var rows = Read(sheets[name]).Descendants(ns + "row");
                decimal sum = 0;
                foreach (var row in rows)
                {
                    var index = (int)row.Attribute("r")!;
                    if (index < start || index > end) continue;
                    var cells = row.Elements(ns + "c").ToDictionary(c => (string)c.Attribute("r")!);
                    var amount = Number(Value(cells.GetValueOrDefault(amountCol + index), ns));
                    sum += amount;
                    var note = Value(cells.GetValueOrDefault(noteCol + index), ns);
                    var source = note.Split(" — ").Last();
                    if (source.Contains('!')) actual.Add((family, source), amount);
                }
                printedWorks += sum;
                primary[family + "/" + name] = sum;
            }
            int Footer(string tab, string rangeStart)
            {
                var sheet = Read(sheets[tab]);
                var ranges = sheet.Descendants(ns + "mergeCell").Select(m => (string)m.Attribute("ref")!);
                return ranges.Where(s => s.StartsWith(rangeStart, StringComparison.Ordinal))
                    .Select(s => int.Parse(string.Concat(s.Split(':')[0].Where(char.IsDigit))))
                    .Where(i => i >= (rangeStart == "M" ? 30 : 52)).Min() - 1;
            }
            CheckSheet("اعمال الحفر", "excavation", "J", "F", 9, int.MaxValue);
            CheckSheet("اعمال توريد الحديد", "steel", "F", "G", 7, Footer("اعمال توريد الحديد", "A"));
            var concreteEnd = Footer("اعمال الخرسانات", "M");
            CheckSheet("اعمال الخرسانات", "concrete", "H", "I", 10, concreteEnd);
            CheckSheet("اعمال الخرسانات", "workmanship", "U", "W", 10, concreteEnd);
            CheckSheet("خاص باعمال الموقع", "site", "J", "K", 10, int.MaxValue);
            foreach (var sheet in sheets.Keys.Where(s => s.StartsWith("حـ ") || s is "اعمال المباني" or "اعمال المحاره" or "اعمال العزل" or "اعمال الكهرباء" or "اعمال السباكه"))
                CheckSheet(sheet, "finishing", "J", "K", 10, int.MaxValue);
            void Equal(decimal value, decimal expected, string label)
            { if (value != expected) throw new InvalidDataException($"Project {project} {label}: printed {value}, expected {expected}"); }
            Equal(printedWorks, account.TotalWorks, "primary work tables");
            foreach (var w in original)
            {
                var key = (w.Family == "building" ? "finishing" : w.Family, $"{w.Sheet}!{w.Cell}");
                if (!actual.TryGetValue(key, out var value)) throw new InvalidDataException($"Project {project} missing {key}");
                Equal(value, w.Amount, key.Item2);
            }
            var schedule = Read(sheets["دفعات تحت الحساب"]);
            var grid = schedule.Descendants(ns + "c").ToDictionary(c => (string)c.Attribute("r")!);
            string Cell(string address) => Value(grid.GetValueOrDefault(address), ns);
            var total = int.Parse(string.Concat(grid.Single(kv => Value(kv.Value, ns) == "اجمالي دفعات").Key.Where(char.IsDigit)));
            var rows = Enumerable.Range(4, total - 5);
            var columns = grid.Keys.Where(k => k.EndsWith("3") && k.SkipWhile(char.IsLetter).SequenceEqual("3"))
                .Select(k => string.Concat(k.TakeWhile(char.IsLetter))).Where(c => c.Length > 1 || c[0] is >= 'B' and <= 'L' or >= 'Q')
                .Where(c => Cell(c + "3") != "").ToArray();
            var allocations = columns.Sum(c => rows.Sum(r => Number(Cell(c + r))));
            var funding = rows.Sum(r => Number(Cell("N" + r)));
            Equal(allocations, account.IncomingByCategory.Values.Sum(), "primary allocations");
            Equal(funding, account.TotalIncoming, "primary funding");
            if (account.HistoricalFundingUnknown && Cell("N" + (total + 3)) != "")
                throw new InvalidDataException("Unknown funding printed as a complete amount.");
            foreach (var (tab, category) in new[] { ("اعمال المباني","المباني"), ("اعمال المحاره","المحارة"),
                ("اعمال العزل","العزل"), ("اعمال الكهرباء","الكهرباء"), ("اعمال السباكه","السباكة") })
                Equal(primary["finishing/" + tab], account.OutgoingByCategory.GetValueOrDefault(category), tab);
            foreach (var col in columns)
                Equal(rows.Sum(r => Number(Cell(col + r))), Number(Cell(col + total)), "allocation column " + col);

            // Cross-check chronology and traceability against the operational ledger, independently
            // of sorting and placement of the two independent printed schedules.
            var ledger = (await new EMG.Api.Modules.Advances.Services.AdvancesService(db).GetLedgerAsync(project, null, true, default))!;
            var printedAllocations = rows.Select(r => new { Row = r, Text = Cell("M" + r) })
                .Where(x => x.Text.Contains('!')).ToLookup(x => x.Text.Split(" — ").Last());
            var printedFunding = rows.Select(r => new { Row = r, Text = Cell("P" + r) })
                .Where(x => x.Text.Contains('!')).ToLookup(x => x.Text.Split(" — ").Last());
            foreach (var h in ledger.Historical.Where(h => h.Currency == "EGP" && (h.FundsProject || h.IsAllocation)))
            {
                var key = $"{h.Sheet}!{h.Cell}";
                void Metadata(string text, string date)
                {
                    var expected = h.Date?.Date.ToOADate().ToString(CultureInfo.InvariantCulture) ?? "";
                    if (date != expected || h.Description.Length > 0 && !text.Contains(h.Description, StringComparison.Ordinal))
                        throw new InvalidDataException($"Project {project} printed source metadata {key}");
                }
                if (h.IsAllocation)
                {
                    var found = printedAllocations[key].Single();
                    Metadata(found.Text, Cell("A" + found.Row));
                    Equal(columns.Sum(c => Number(Cell(c + found.Row))), h.Amount, key);
                }
                if (h.FundsProject)
                {
                    var found = printedFunding[key].Single();
                    Metadata(found.Text, Cell("O" + found.Row));
                    Equal(Number(Cell("N" + found.Row)), h.Amount, key);
                }
            }
            var serial = rows.Where(r => Number(Cell("N" + r)) != 0).Select(r => Cell("O" + r)).FirstOrDefault();
            Console.WriteLine($"project={project} original-work-keys={original.Count} works={printedWorks} site={primary["site/خاص باعمال الموقع"]} funding={funding} allocations={allocations} first-funding-date={serial} PASS");
        }
        Console.WriteLine($"Primary workbook tables verified for {projects.Count} projects.");
    }
}
