using System.Globalization;
using System.IO.Compression;
using System.Text.Json;
using System.Xml.Linq;
using EMG.Api.Modules.FinalAccounts.Services;
using EMG.Api.Modules.Reports.Services;
using EMG.Infrastructure.Db;
using Microsoft.EntityFrameworkCore;
using Npgsql;

internal static class HistoricalReportChecks
{
    private static readonly XNamespace Main = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
    private static readonly XNamespace Relationships = "http://schemas.openxmlformats.org/officeDocument/2006/relationships";

    internal static async Task RunAsync(string mapping)
    {
        var connection = new NpgsqlConnectionStringBuilder(Environment.GetEnvironmentVariable("ConnectionStrings__EmgDb"));
        if (connection.Host != "127.0.0.1" || connection.Port != 55464 || connection.Database != "emg_restore")
            throw new InvalidOperationException("Expected the authorized loopback restore database.");
        using var document = JsonDocument.Parse(await File.ReadAllTextAsync(mapping));
        await using var db = new EmgDbContext(new DbContextOptionsBuilder<EmgDbContext>().UseNpgsql(connection.ConnectionString).Options);
        await using var tx = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.RepeatableRead);
        await db.Database.ExecuteSqlRawAsync("SET TRANSACTION READ ONLY");
        var accounts = new FinalAccountsService(db);
        var reports = new ReportsService(db, accounts);
        var ct = CancellationToken.None;
        var batches = await db.HistoricalImportBatches.Where(b => b.Status == "POSTED" && b.MappingVersion == "universal-v1.1")
            .ToDictionaryAsync(b => b.SourceSha256.ToLowerInvariant());
        var keys = document.RootElement.EnumerateArray().Select(p => p.GetProperty("sha256").GetString()).ToList();
        if (keys.Count != batches.Count || !batches.Keys.ToHashSet().SetEquals(keys))
            throw new InvalidDataException("Mapping and database batches differ.");
        foreach (var project in document.RootElement.EnumerateArray())
        {
            var batch = batches[project.GetProperty("sha256").GetString()!];
            var report = await reports.GetProjectReportAsync(batch.ProjectId,null,true,ct) ?? throw new InvalidDataException();
            var account = report.FinalAccount;
            if (await accounts.GetAsync(batch.ProjectId, null, false, ct) != null)
                throw new InvalidDataException("Project financials escaped assignment scope.");
            var control = await db.HistoricalFundingLines.SingleOrDefaultAsync(f => f.BatchId == batch.Id && f.Stream == "source_control",ct);
            if (control != null)
            {
                using var evidence = JsonDocument.Parse(control.SourceJson);
                var qualifications = report.HistoricalFunding.Where(f => f.Stream == "funding_qualification").ToList();
                var conflict = qualifications.Any(f => f.Basis == "conflicting_ownership");
                var officeSupplement = qualifications.Where(f => f.Basis == "verified_office_supplement").Sum(f => f.Amount);
                var expected = conflict ? 0 : control.Amount + officeSupplement;
                if (account.HistoricalFundingUnknown != conflict || account.HistoricalFundingBasis != (conflict ? "conflicting_ownership" : control.Basis) || account.TotalPayments != expected)
                    throw new InvalidDataException("Funding control and acceptance snapshot differ.");
                if (!conflict && evidence.RootElement.TryGetProperty("net_value",out var outstanding) &&
                    decimal.Parse(outstanding.GetString()!,CultureInfo.InvariantCulture) - officeSupplement != -account.CashBalance)
                    throw new InvalidDataException("Outstanding works differ from workbook control.");
            }
            var bytes = await reports.ExportProjectWorkbookAsync(batch.ProjectId,null,true,ct) ?? throw new InvalidDataException();
            using var zip = new ZipArchive(new MemoryStream(bytes),ZipArchiveMode.Read);
            XDocument Read(string name)
            {
                using var stream = zip.GetEntry(name)!.Open();
                return XDocument.Load(stream);
            }
            var rels = Read("xl/_rels/workbook.xml.rels").Root!.Elements().ToDictionary(e => (string)e.Attribute("Id")!,e => (string)e.Attribute("Target")!);
            var paths = Read("xl/workbook.xml").Descendants(Main+"sheet").ToDictionary(e => (string)e.Attribute("name")!,
                e => "xl/"+rels[(string)e.Attribute(Relationships+"id")!]);
            void Check(bool condition,string label)
            {
                if (!condition) throw new InvalidDataException($"{project.GetProperty("id")}: {label}");
            }
            void CheckRows(string sheet,IEnumerable<(string Sheet,string Cell,decimal Amount,string Currency,string Narrative)> expected,
                int amountCol,int currencyCol,int textCol)
            {
                var rows = expected.ToList();
                if (rows.Count == 0 && !paths.ContainsKey(sheet)) return;
                var data = Read(paths[sheet]).Descendants(Main+"row").Skip(1)
                    .Select(row => row.Elements(Main+"c").Select(c => c.Value).ToArray()).ToList();
                Check(rows.Count==data.Count,sheet+" row count");
                for (var i=0;i<rows.Count;i++)
                {
                    var r=rows[i]; var cells=data[i];
                    Check((cells[0],cells[1],decimal.Parse(cells[amountCol],CultureInfo.InvariantCulture),cells[currencyCol],cells[textCol])
                        == (r.Sheet,r.Cell,r.Amount,r.Currency,r.Narrative),sheet+" detail");
                }
            }
            CheckRows("تفاصيل الأعمال الأصلية",report.HistoricalWorks.Select(w => (w.Sheet,w.Cell,w.Amount,w.Currency,w.Description)),5,4,7);
            CheckRows("جداول التمويل الأصلية",report.HistoricalFunding.Select(f => (f.Sheet,f.Cell,f.Amount,f.Currency,f.Narrative)),6,5,8);
            CheckRows("التسويات الأصلية",report.HistoricalSettlements.Select(s => (s.Sheet,s.Cell,s.Amount,s.Currency,s.Narrative)),4,3,6);
            var finalSheet = Read(paths["كشف حساب نهائي"]).Descendants(Main+"c").ToDictionary(c => (string)c.Attribute("r")!);
            decimal Number(string addr) => decimal.Parse(finalSheet[addr].Element(Main+"v")!.Value,CultureInfo.InvariantCulture);
            Check(Number("K9")==account.TotalWorks && (account.HistoricalFundingUnknown ? finalSheet["K10"].Element(Main+"v") == null && finalSheet.GetValueOrDefault("K15")?.Element(Main+"v") == null : Number("K10")==account.TotalPayments && Number("K15")==account.CashBalance),"exported works/payments");
            Check(Number("F12")==account.OfficePaid && Number("I13")==account.OfficeNetDue,"exported office account");
            Check(account.CashBalance==decimal.Round(account.TotalPayments-account.TotalWorks,2),"cash balance");
            Check(account.Sections.Where(s => s.Currency=="EGP").Sum(s => s.Works)==account.TotalWorks,"section works");
            Console.WriteLine($"{project.GetProperty("id")} project={batch.ProjectId} work={report.HistoricalWorks.Count} funding={report.HistoricalFunding.Count} settlements={report.HistoricalSettlements.Count} XLSX=PASS");
        }
    }
}
