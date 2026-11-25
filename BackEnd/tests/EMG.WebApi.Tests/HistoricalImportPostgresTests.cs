using EMG.Api.Modules.UniversalImports;
using EMG.Infrastructure.Db;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace EMG.WebApi.Tests;

public sealed class HistoricalImportPostgresTests
{
    [Fact]
    public async Task QaPostgresPostingRollsBackAfterFirstPreparedEntry()
    {
        var connectionString = Environment.GetEnvironmentVariable("EMG_HISTORICAL_QA_CONNECTION");
        if (string.IsNullOrWhiteSpace(connectionString)) throw new InvalidOperationException("Set EMG_HISTORICAL_QA_CONNECTION for the isolated QA PostgreSQL rollback test.");
        var settings = new NpgsqlConnectionStringBuilder(connectionString);
        Assert.Equal("127.0.0.1", settings.Host);
        var expectedPort = int.TryParse(Environment.GetEnvironmentVariable("EMG_HISTORICAL_QA_PORT"), out var configuredPort)
            ? configuredPort
            : 55432;
        Assert.Equal(expectedPort, settings.Port);
        Assert.Equal("emg_qa_stg01c", settings.Database);

        await using var db = new EmgDbContext(new DbContextOptionsBuilder<EmgDbContext>().UseNpgsql(settings.ConnectionString).Options);
        var sha = Guid.NewGuid().ToString("N").PadRight(64, 'a')[..64];
        var section = new UniversalSectionPreview("pg-atomic-sec", "Synthetic QA", "A1:B3", "payment_ledger", "F06_ADVANCE_PARALLEL", UniversalImportMappingRegistry.Version, "HIGH", false, true, 2, 2, []);
        var records = new[]
        {
            new UniversalRecordPreview("pg-atomic-1-" + Guid.NewGuid().ToString("N"), "pg-atomic-sec", "Synthetic QA", "A1", ["A1"], UniversalRecordType.Payment, "payment_ledger", "EGP", 101m, false, false, true, "HIGH", "pg-atomic-fp-1"),
            new UniversalRecordPreview("pg-atomic-2-" + Guid.NewGuid().ToString("N"), "pg-atomic-sec", "Synthetic QA", "A2", ["A2"], UniversalRecordType.Payment, "payment_ledger", "EGP", 202m, false, false, true, "HIGH", "pg-atomic-fp-2")
        };
        var preview = new UniversalPreviewResponse("pg-atomic.xlsx", 100, sha, UniversalImportMappingRegistry.Version, false, [section], records, [], [], [], []);
        var request = new HistoricalImportStageRequest(3, "QA PostgreSQL atomic rollback", preview);
        var normal = new HistoricalImportPostingService(db);
        var staged = await normal.StageAsync(request, 2, CancellationToken.None);
        await normal.ApproveAsync(staged.BatchId, 3, CancellationToken.None);

        var failing = new HistoricalImportPostingService(db, new FailAfterFirstEntry());
        await Assert.ThrowsAsync<InvalidOperationException>(() => failing.PostAsync(staged.BatchId, 4, CancellationToken.None));

        Assert.Equal(0, await db.HistoricalPostedEntries.CountAsync(item => item.BatchId == staged.BatchId));
        Assert.Equal(HistoricalImportStatuses.ApprovedForPosting, await db.HistoricalImportBatches.Where(item => item.Id == staged.BatchId).Select(item => item.Status).SingleAsync());
        // Leave the synthetic batch as an auditable QA artifact; it has no posted rows.
    }

    private sealed class FailAfterFirstEntry : IHistoricalImportPostingFaultInjector
    {
        public void AfterEntryPrepared(int entryIndex)
        {
            if (entryIndex == 0) throw new InvalidOperationException("QA_TEST_FAULT_AFTER_FIRST_ENTRY");
        }
    }
}
