using EMG.Api.Modules.FinalAccounts.Services;
using EMG.Api.Modules.Reports.Services;
using EMG.Api.Modules.UniversalImports;
using EMG.Infrastructure.Db;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;
using Npgsql;
using Xunit.Abstractions;

namespace EMG.WebApi.Tests;

public sealed class UniversalNewProjectTests(ITestOutputHelper output)
{
    private const string QaConnection = "EMG_UNIVERSAL_NEW_PROJECT_QA";

    [Fact]
    public async Task RevalidationAcceptsOnlySupportedMappingChangesAndKeepsProductionPostingDisabled()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var db = new EmgDbContext(new DbContextOptionsBuilder<EmgDbContext>().UseSqlite(connection).Options);
        await db.Database.EnsureCreatedAsync();
        var parser = new UniversalImportPreviewService(new UniversalImportMappingRegistry());
        var host = WebApplication.CreateSlimBuilder(new WebApplicationOptions { EnvironmentName = "Production" });
        using var snapshots = new ReviewedWorkbookSnapshots();
        var fields = new UniversalOperationalEvidence("SiteMovement", new DateOnly(2020, 1, 1),
            "مصروف موقع", null, null, null, null, null, "C5", true);
        var row = new UniversalRecordPreview("manual", "section", "Site", "Site!C5", ["C5"], UniversalRecordType.Unresolved,
            "site", "EGP", 250m, false, false, false, "LOW", "fingerprint", fields);
        var preview = new UniversalPreviewResponse("synthetic.xlsx", 100, new string('A', 64), "test", true, [],
            [row, row with { RecordId = "reference", IsSummary = true, OperationalEvidence = null }],
            [], [], [], [], "123");
        var service = new ReviewedWorkbookImportService(db, parser, snapshots, new ConfigurationBuilder().Build(), host.Environment);
        var request = new UniversalNewProjectRequest(snapshots.Store(7, 0, preview), "QA", "123", null, null, null, null, null, null, true);

        Assert.Contains((await service.RevalidateNewProjectAsync(request, 7, default)).Records, entry => entry.RecordId == "manual" && !entry.CanImport);
        var corrected = request with { MappingEvidence = new() { ["manual"] = "Confirmed source field C5" } };
        var result = await service.RevalidateNewProjectAsync(corrected, 7, default);
        Assert.Contains(result.Records, entry => entry.RecordId == "manual" && entry.CanImport && entry.Amount == 250m);
        Assert.Contains(result.Records, entry => entry.RecordId == "reference" && !entry.CanImport);
        Assert.False(result.PostingEnabled);
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.RevalidateNewProjectAsync(
            request with { MappingEvidence = new() { ["reference"] = "Unsupported source claim" } }, 7, default));
        await Assert.ThrowsAsync<HistoricalWorkbookPostingDisabledException>(() => service.CommitNewProjectAsync(corrected, 7, default));
        Assert.False(await db.Projects.AnyAsync());
    }

    [Theory]
    [InlineData("127.0.0.1", 55464, "emg_restore", true)]
    [InlineData("localhost", 55464, "emg_restore", true)]
    [InlineData("not-loopback.invalid", 55464, "emg_restore", false)]
    [InlineData("127.0.0.1", 5432, "emg_restore", false)]
    [InlineData("127.0.0.1", 55465, "emg_restore", false)]
    [InlineData("127.0.0.1", 55464, "unapproved_target", false)]
    public void DailyWorkbookApprovalIsBoundToFileAndTargetAndCannotEnableRecovery(
        string hostName, int port, string databaseName, bool primaryLocal)
    {
        var connection = new NpgsqlConnectionStringBuilder { Host = hostName, Port = port, Database = databaseName };
        using var db = new EmgDbContext(new DbContextOptionsBuilder<EmgDbContext>()
            .UseNpgsql(connection.ConnectionString).Options);
        var host = WebApplication.CreateSlimBuilder(new WebApplicationOptions { EnvironmentName = "Development" });
        var config = new ConfigurationBuilder().AddInMemoryCollection().Build();
        using var snapshots = new ReviewedWorkbookSnapshots();
        var service = new ReviewedWorkbookImportService(db, new UniversalImportPreviewService(new UniversalImportMappingRegistry()),
            snapshots, config, host.Environment);
        var approved = new string('A', 64);
        var different = new string('B', 64);

        Assert.False(service.IsNewProjectPostingAllowed(approved, historical: false));
        config["UniversalImport:ApprovedLocalWorkbookSha256"] = approved.ToLowerInvariant();
        Assert.Equal(primaryLocal, service.IsNewProjectPostingAllowed(approved, historical: false));
        Assert.False(service.IsNewProjectPostingAllowed(different, historical: false));
        Assert.False(service.IsNewProjectPostingAllowed(approved, historical: true));
        config["HistoricalMigration:Enabled"] = "true";
        Assert.False(service.IsNewProjectPostingAllowed(approved, historical: true));
        config["HistoricalMigration:AllowAuthorizedLocalTarget"] = "true";
        Assert.Equal(primaryLocal, service.IsNewProjectPostingAllowed(approved, historical: true));
        Assert.False(service.IsNewProjectPostingAllowed(different, historical: false));
        Assert.Equal(System.Data.ConnectionState.Closed, db.Database.GetDbConnection().State);
    }

    [Q315Fact]
    public async Task Q315CreatesProjectAndReportOnlyAfterAtomicCommit()
    {
        var token = CancellationToken.None;
        var fixture = Environment.GetEnvironmentVariable("EMG_UNIVERSAL_IMPORT_SOURCE_DIR")!;
        var path = Assert.Single(Directory.EnumerateFiles(fixture, "*.xlsx"), path => Path.GetFileName(path).Contains("315"));
        var connection = new NpgsqlConnectionStringBuilder(Environment.GetEnvironmentVariable(QaConnection));
        Assert.Equal("127.0.0.1", connection.Host);
        Assert.NotEqual(5432, connection.Port);
        Assert.Equal("emg_universal_qa_q315", connection.Database);
        var options = new DbContextOptionsBuilder<EmgDbContext>().UseNpgsql(connection.ConnectionString);
        await using var db = new EmgDbContext(options.Options);
        await db.Database.MigrateAsync(token);
        Assert.False(await db.Projects.AnyAsync(token));
        var parser = new UniversalImportPreviewService(new UniversalImportMappingRegistry());
        using var snapshots = new ReviewedWorkbookSnapshots();
        await using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        var preview = await parser.PreviewAsync(new FormFile(stream, 0, stream.Length, "file", Path.GetFileName(path)), token);
        Assert.Equal("7FEF67459E1DF1910EAF9657EFDE376C59CCD2BF6BF13DBBE0A41B6611A353D4", preview.SourceSha256);
        Assert.Contains(preview.Reconciliations, row => row.EvidenceCoordinate == "خاص باعمال الموقع!K7" &&
            row.SourceSummaryAmount == 2949413m && row.Difference == 0m);
        Assert.Contains(preview.Reconciliations, row => row.EvidenceCoordinate == "حساب المباني!K7" &&
            row.SourceSummaryAmount == 155385m && row.Difference == 0m);
        var host = WebApplication.CreateSlimBuilder(new WebApplicationOptions { EnvironmentName = "Development" });
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["UniversalImportQa:Enabled"] = "true",
            ["UniversalImportQa:ApprovedSha256"] = preview.SourceSha256,
            ["UniversalImportQa:Port"] = connection.Port.ToString(),
            ["UniversalImportQa:Database"] = connection.Database
        }).Build();
        var id = snapshots.Store(7, 0, preview);
        var service = new ReviewedWorkbookImportService(db, parser, snapshots, config, host.Environment);
        var request = new UniversalNewProjectRequest(id, "Q315 isolated QA", preview.SourceProjectCode ?? "315", null, null, null,
            null, null, null, false);
        var initial = await service.RevalidateNewProjectAsync(request, 7, token);
        Assert.True(initial.PostingEnabled);
        Assert.True(initial.RequiresExclusionConfirmation);
        await Assert.ThrowsAsync<KeyNotFoundException>(() => service.RevalidateNewProjectAsync(request, 8, token));
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.RevalidateNewProjectAsync(
            request with { MappingEvidence = new() { ["unknown"] = "unsupported mapping" } }, 7, token));
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.CommitNewProjectAsync(request, 7, token));
        var corrected = request with { Name = "Q315 corrected QA project", ConfirmExcluded = true };
        var manual = preview.Records.FirstOrDefault(row => row.OperationalEvidence?.RequiresManualMapping == true &&
            !preview.DuplicateCandidates.Any(group => group.RecordIds.Contains(row.RecordId)));
        if (manual is not null)
            corrected = corrected with { MappingEvidence = new() { [manual.RecordId] = $"مراجعة القيمة في خلية المصدر {manual.SourceCoordinate}" } };
        var validated = await service.RevalidateNewProjectAsync(corrected, 7, token);
        if (manual is not null) Assert.Contains(validated.Records, row => row.RecordId == manual.RecordId && row.CanImport);

        await using (var failing = new EmgDbContext(options.AddInterceptors(new FailBeforeProvenance()).Options))
        {
            var failedService = new ReviewedWorkbookImportService(failing, parser, snapshots, config, host.Environment);
            var failure = await Assert.ThrowsAsync<InvalidOperationException>(() => failedService.CommitNewProjectAsync(corrected, 7, token));
            Assert.Equal("QA injected fault after writing operational rows", failure.Message);
        }
        Assert.False(await db.Projects.AnyAsync(token));
        Assert.False(await db.HistoricalImportBatches.AnyAsync(token));
        Assert.False(await db.HistoricalImportRecords.AnyAsync(token));
        Assert.False(await db.SiteMovements.AnyAsync(token));
        Assert.False(await db.FinishingRecords.AnyAsync(token));
        Assert.False(await db.HistoricalPostedEntries.AnyAsync(token));

        var result = await service.CommitNewProjectAsync(corrected, 7, token);
        Assert.Equal(corrected.Name, result.Name);
        Assert.Equal(preview.Records.Count, await db.HistoricalImportRecords.CountAsync(token));
        var provenance = await db.HistoricalPostedEntries.AsNoTracking().ToListAsync(token);
        Assert.Equal(validated.Records.Count(row => row.CanImport), provenance.Count);
        var report = await new ReportsService(db, new FinalAccountsService(db)).GetProjectReportAsync(result.ProjectId, 7, true, token);
        Assert.NotNull(report);
        Assert.Equal(result.Totals["SiteMovement"], report.Movements.Sum(row => row.Amount));
        Assert.Equal(result.Totals["FinishingRecord"], report.Finishings.Sum(row => row.TotalAmount));
        Assert.Equal(2923313m, report.Movements.Sum(row => row.Amount));
        Assert.Equal(155385m, report.Finishings.Sum(row => row.TotalAmount));
        var duplicateSiteRows = validated.Records.Where(row => row.Target == "SiteMovement" && !row.CanImport).ToArray();
        Assert.Equal(6, duplicateSiteRows.Length);
        Assert.Equal(26100m, duplicateSiteRows.Sum(row => row.Amount));
        output.WriteLine("Retained potential duplicates: " + string.Join(", ", duplicateSiteRows.Select(row => row.SourceCoordinate)));
        Assert.NotEmpty(XlsxBuilder.Build(report, null));
        output.WriteLine($"Workbook records: {preview.Records.Count}. Posted: {provenance.Count}. Retained: {result.RetainedRecords}.");
        foreach (var group in validated.Records.GroupBy(row => (row.Target, row.Currency, row.CanImport, row.Reason)))
            output.WriteLine($"{group.Key}: {group.Count()} rows / {group.Sum(row => row.Amount ?? 0)}");
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.CommitNewProjectAsync(corrected, 7, token));
        Assert.Equal(1, await db.Projects.CountAsync(token));
    }

    private sealed class FailBeforeProvenance : SaveChangesInterceptor
    {
        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData,
            InterceptionResult<int> result, CancellationToken cancellationToken = default)
        {
            if (eventData.Context!.ChangeTracker.Entries<EMG.Domain.Entities.HistoricalPostedEntry>()
                .Any(entry => entry.State == EntityState.Added))
                throw new InvalidOperationException("QA injected fault after writing operational rows");
            return base.SavingChangesAsync(eventData, result, cancellationToken);
        }
    }

    public sealed class Q315FactAttribute : FactAttribute
    {
        public Q315FactAttribute()
        {
            if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(QaConnection)))
                Skip = "Requires a fresh isolated PostgreSQL database and the read-only company workbook.";
        }
    }
}
