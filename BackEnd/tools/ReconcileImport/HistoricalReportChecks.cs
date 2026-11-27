using Microsoft.EntityFrameworkCore.Storage;
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
        var classifications = new EMG.Api.Modules.AccountingClassification.Services.AccountingClassificationService(db);
        var consolidated = await new EMG.Api.Modules.ConsolidatedStatements.Services.ConsolidatedStatementService(db,classifications)
            .PreviewAsync(null,true,ct);
        var consolidatedRows = consolidated.Projects.ToDictionary(p => p.ProjectId);
        await VerifyReceiptDeltaAsync(db,ct);
        foreach (var project in document.RootElement.EnumerateArray())
        {
            var batch = batches[project.GetProperty("sha256").GetString()!];
            var report = await reports.GetProjectReportAsync(batch.ProjectId,null,true,ct) ?? throw new InvalidDataException();
            var account = report.FinalAccount;
            var ledger = await new EMG.Api.Modules.Advances.Services.AdvancesService(db).GetLedgerAsync(batch.ProjectId,null,true,ct)
                ?? throw new InvalidDataException("Missing advances ledger");
            var egp = ledger.Totals.Single(t => t.Currency == "EGP");
            var historicalReceipts = ledger.Historical.Where(l => l.Currency == "EGP" && l.FundsProject).Sum(l => l.Amount);
            var ordinaryReceipts = ledger.Receipts.Where(r => r.Currency == "EGP" && r.Status is "مرحّل" or "معتمد").Sum(r => r.Amount);
            if (egp.Works != account.TotalWorks || egp.Payments != account.TotalPayments || egp.CashBalance != account.CashBalance ||
                historicalReceipts + ordinaryReceipts + ledger.DirectFunding.Where(f => f.Currency == "EGP").Sum(f => f.Amount) != account.TotalIncoming)
                throw new InvalidDataException($"Advances and project figures differ for {project.GetProperty("id")}");
            foreach (var r in ledger.Historical)
                if (!report.HistoricalFunding.Any(f => f.Sheet == r.Sheet && f.Cell == r.Cell && f.Date == r.Date &&
                    f.Amount == r.Amount && f.Currency == r.Currency && f.Narrative == r.Description && f.Category == r.Category))
                    throw new InvalidDataException("Advances lost an original source attribute");
            var consolidatedRow = consolidatedRows[batch.ProjectId];
            if ((consolidatedRow.TotalWorks,consolidatedRow.TotalPayments,consolidatedRow.CashBalance) !=
                (account.TotalWorks,account.TotalPayments,account.CashBalance))
                throw new InvalidDataException("Consolidated statement differs from project account.");
            var classified = await classifications.PreviewAsync(batch.ProjectId,null,true,ct) ?? throw new InvalidDataException();
            var classifiedWorks = classified.Lines.Where(l => l.IncludedInCurrentTotals &&
                l.EntityType is "historical-work" or "excavation" or "steel" or "site-movement" or "concrete" or "finishing").Sum(l => l.Amount);
            var classifiedFunding = classified.Totals.Single(t => t.Classification == EMG.Api.Modules.AccountingClassification.Dtos.AccountingClassificationCode.OwnerFunding);
            if (classifiedWorks != account.TotalWorks || classifiedFunding.CurrentApprovedPostedAmount != account.TotalIncoming)
                throw new InvalidDataException($"Classification differs for {project.GetProperty("id")}");
            if (await accounts.GetAsync(batch.ProjectId, null, false, ct) != null)
                throw new InvalidDataException("Project financials escaped assignment scope.");
            var control = await db.HistoricalFundingLines.SingleOrDefaultAsync(f => f.BatchId == batch.Id && f.Stream == "source_control",ct);
            if (control != null)
            {
                using var evidence = JsonDocument.Parse(control.SourceJson);
                var qualifications = report.HistoricalFunding.Where(f => f.Stream == "funding_qualification").ToList();
                var conflict = qualifications.Any(f => f.Basis == "conflicting_ownership");
                var officeSupplement = qualifications.Where(f => f.Basis == "verified_office_supplement").Sum(f => f.Amount);
                decimal? expected = conflict ? null : control.Amount + officeSupplement;
                if (account.HistoricalFundingUnknown != conflict || account.HistoricalFundingBasis != (conflict ? "conflicting_ownership" : control.Basis) || (conflict ? (decimal?)null : historicalReceipts) != expected)
                    throw new InvalidDataException("Funding control and acceptance snapshot differ.");
                if (!conflict && evidence.RootElement.TryGetProperty("net_value",out var outstanding) &&
                    decimal.Parse(outstanding.GetString()!,CultureInfo.InvariantCulture) - officeSupplement != account.TotalWorks - historicalReceipts)
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
            Check(account.HistoricalFundingUnknown ? account.CashBalance == null : account.CashBalance == decimal.Round(account.TotalIncoming-account.TotalWorks,2),"cash balance");
            Check(account.Sections.Where(s => s.Currency=="EGP").Sum(s => s.Works)==account.TotalWorks,"section works");
            Console.WriteLine($"{project.GetProperty("id")} project={batch.ProjectId} work={report.HistoricalWorks.Count} funding={report.HistoricalFunding.Count} settlements={report.HistoricalSettlements.Count} XLSX=PASS");
        }
    }

    // Compare the actual ledger with a SELECT-only view immediately before one existing posted receipt.
    // Both projections use the same database snapshot. The original row and its source relationships stay intact.
    private static async Task VerifyReceiptDeltaAsync(EmgDbContext current,CancellationToken ct)
    {
        var receipt = await current.AdvancePayments.AsNoTracking()
            .Where(p => p.Currency == "EGP" && p.Status == "مرحّل" && p.Amount > 0 && !p.Allocations.Any())
            .OrderByDescending(p => p.PaymentDate).ThenByDescending(p => p.Id)
            .Select(p => new { p.Id,p.ProjectId,p.Amount }).FirstAsync(ct);
        var options = new DbContextOptionsBuilder<EmgDbContext>()
            .UseNpgsql(current.Database.GetDbConnection()).AddInterceptors(new WithoutReceipt(receipt.Id)).Options;
        await using var before = new EmgDbContext(options);
        await before.Database.UseTransactionAsync(current.Database.CurrentTransaction!.GetDbTransaction(),ct);
        async Task<(decimal? Payments,decimal? Balance,decimal? Allocated,decimal Works,decimal Office)> Read(EmgDbContext db)
        {
            var accounts = new FinalAccountsService(db);
            var account = (await accounts.GetAsync(receipt.ProjectId,null,true,ct))!;
            var ledger = (await new EMG.Api.Modules.Advances.Services.AdvancesService(db).GetLedgerAsync(receipt.ProjectId,null,true,ct))!;
            var projects = new EMG.Api.Modules.Projects.Services.ProjectsService(new EMG.Api.Modules.Projects.Repositories.EfProjectsRepository(db),accounts);
            var card = (await projects.GetProjectAsync(receipt.ProjectId,null,true,ct))!;
            var total = ledger.Totals.Single(t => t.Currency == "EGP");
            var classified = (await new EMG.Api.Modules.AccountingClassification.Services.AccountingClassificationService(db).PreviewAsync(receipt.ProjectId,null,true,ct))!;
            var funding = classified.Totals.Single(t => t.Classification == EMG.Api.Modules.AccountingClassification.Dtos.AccountingClassificationCode.OwnerFunding);
            if (card.TotalPayments != account.TotalPayments || total.Payments != card.TotalPayments ||
                total.CashBalance != card.CashBalance || total.Payments != funding.CurrentApprovedPostedAmount)
                throw new InvalidDataException("Posted receipt projections disagree.");
            return (total.Payments,total.CashBalance,total.Allocated,total.Works,account.OfficeNetDue);
        }
        var previous = await Read(before);
        var actual = await Read(current);
        if (actual.Payments - previous.Payments != receipt.Amount || actual.Balance - previous.Balance != receipt.Amount ||
            (actual.Allocated,actual.Works,actual.Office) != (previous.Allocated,previous.Works,previous.Office))
            throw new InvalidDataException("Receipt delta affected unrelated accounts.");
        Console.WriteLine("Existing posted receipt delta: project, advances, classification, office and allocations PASS");
    }

    private sealed class WithoutReceipt(int receiptId) : Microsoft.EntityFrameworkCore.Diagnostics.DbCommandInterceptor
    {
        public override ValueTask<Microsoft.EntityFrameworkCore.Diagnostics.InterceptionResult<System.Data.Common.DbDataReader>> ReaderExecutingAsync(
            System.Data.Common.DbCommand command, Microsoft.EntityFrameworkCore.Diagnostics.CommandEventData eventData,
            Microsoft.EntityFrameworkCore.Diagnostics.InterceptionResult<System.Data.Common.DbDataReader> result,CancellationToken ct = default)
        {
            command.CommandText = System.Text.RegularExpressions.Regex.Replace(command.CommandText,
                @"\b(FROM|JOIN) advance_payments\b", match => $"{match.Groups[1]} (SELECT * FROM advance_payments WHERE \"Id\" <> {receiptId})",
                System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            return base.ReaderExecutingAsync(command,eventData,result,ct);
        }
    }
}
