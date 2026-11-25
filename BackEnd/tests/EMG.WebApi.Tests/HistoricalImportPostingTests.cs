using EMG.Api.Modules.UniversalImports;
using EMG.Domain.Entities;
using EMG.Infrastructure.Db;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace EMG.WebApi.Tests;

public sealed class HistoricalImportPostingTests
{
    [Fact]
    public async Task StagesQuarantinesApprovesPostsAndRepostsIdempotently()
    {
        await using var fixture = await Fixture.Create();
        var service = new HistoricalImportPostingService(fixture.Db);
        var request = Request(includeUnresolved: true, includeDuplicate: true);

        var staged = await service.StageAsync(request, 10, CancellationToken.None);
        Assert.Equal(HistoricalImportStatuses.ReviewRequired, staged.Status);
        Assert.Equal(1, staged.ReadyRecords);
        Assert.Equal(2, staged.ReviewRequiredRecords);
        Assert.Equal(0, staged.ExcludedRecords);

        var duplicateStage = await service.StageAsync(request, 10, CancellationToken.None);
        Assert.Equal(staged.BatchId, duplicateStage.BatchId);

        var approved = await service.ApproveAsync(staged.BatchId, 11, CancellationToken.None);
        Assert.Equal(HistoricalImportStatuses.ApprovedForPosting, approved.Status);
        Assert.Equal(1, approved.ApprovedRecords);

        var posted = await service.PostAsync(staged.BatchId, 12, CancellationToken.None);
        Assert.Equal(HistoricalImportStatuses.Posted, posted.Status);
        Assert.Equal(1, posted.PostedRecords);
        Assert.Single(fixture.Db.HistoricalPostedEntries);
        Assert.Equal("EGP", fixture.Db.HistoricalPostedEntries.Single().Currency);

        var reposted = await service.PostAsync(staged.BatchId, 12, CancellationToken.None);
        Assert.Equal(1, reposted.PostedRecords);
        Assert.Single(fixture.Db.HistoricalPostedEntries);
    }

    [Fact]
    public async Task RequiresOwnerApprovalAndRejectsInvalidCurrencyWithoutPosting()
    {
        await using var fixture = await Fixture.Create();
        var service = new HistoricalImportPostingService(fixture.Db);
        var request = Request(includeUnresolved: false, includeDuplicate: false, currency: "EUR");

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.StageAsync(request with { SourceApprovalReference = "" }, 10, CancellationToken.None));
        var staged = await service.StageAsync(request, 10, CancellationToken.None);
        Assert.Equal(HistoricalImportStatuses.PreviewOnly, staged.Status);
        Assert.Equal(1, staged.ReviewRequiredRecords);
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.ApproveAsync(staged.BatchId, 11, CancellationToken.None));
        var rejected = await service.RejectAsync(staged.BatchId, 11, "Currency requires owner mapping.", CancellationToken.None);
        Assert.Equal(HistoricalImportStatuses.Rejected, rejected.Status);
        Assert.Empty(fixture.Db.HistoricalPostedEntries);
    }

    [Fact]
    public async Task PostingRollsBackAtomicallyWhenAnApprovedRecordBecomesInvalid()
    {
        await using var fixture = await Fixture.Create();
        var service = new HistoricalImportPostingService(fixture.Db);
        var staged = await service.StageAsync(Request(includeUnresolved: false, includeDuplicate: false), 10, CancellationToken.None);
        await service.ApproveAsync(staged.BatchId, 11, CancellationToken.None);
        var record = await fixture.Db.HistoricalImportRecords.SingleAsync();
        record.OriginalAmount = null;
        await fixture.Db.SaveChangesAsync();

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.PostAsync(staged.BatchId, 12, CancellationToken.None));
        Assert.Empty(fixture.Db.HistoricalPostedEntries);
        var current = await fixture.Db.HistoricalImportBatches.AsNoTracking().SingleAsync();
        Assert.Equal(HistoricalImportStatuses.ApprovedForPosting, current.Status);
    }

    [Fact]
    public async Task PostingRollsBackAfterFirstEntryIsPreparedWhenFaultIsInjected()
    {
        await using var fixture = await Fixture.Create();
        var request = Request(includeUnresolved: false, includeDuplicate: false) with
        {
            Preview = Request(false, false).Preview with
            {
                Records =
                [
                    new("atomic-1", "section-1", "Synthetic", "Synthetic!A1", ["A1"], UniversalRecordType.Expense, "workbook_stream", "EGP", 100m, false, false, true, "HIGH", "atomic-1"),
                    new("atomic-2", "section-1", "Synthetic", "Synthetic!A2", ["A2"], UniversalRecordType.Expense, "workbook_stream", "EGP", 200m, false, false, true, "HIGH", "atomic-2")
                ]
            }
        };
        var normal = new HistoricalImportPostingService(fixture.Db);
        var staged = await normal.StageAsync(request, 10, CancellationToken.None);
        await normal.ApproveAsync(staged.BatchId, 11, CancellationToken.None);

        var failing = new HistoricalImportPostingService(fixture.Db, new FailAfterFirstEntryInjector());
        await Assert.ThrowsAsync<InvalidOperationException>(() => failing.PostAsync(staged.BatchId, 12, CancellationToken.None));

        Assert.Empty(await fixture.Db.HistoricalPostedEntries.AsNoTracking().ToListAsync());
        var current = await fixture.Db.HistoricalImportBatches.AsNoTracking().SingleAsync();
        Assert.Equal(HistoricalImportStatuses.ApprovedForPosting, current.Status);
        Assert.Equal(2, await fixture.Db.HistoricalImportAuditEvents.CountAsync());
    }

    [Fact]
    public async Task F06PostsIndependentPaymentsButKeepsAllocationsQuarantined()
    {
        await using var fixture = await Fixture.Create();
        var service = new HistoricalImportPostingService(fixture.Db);
        var baseRequest = Request(includeUnresolved: false, includeDuplicate: false);
        var paymentSection = new UniversalSectionPreview("f06-payment", "دفعات تحت الحساب", "K1:L2", "main_payment_ledger", "F06_ADVANCE_PARALLEL", UniversalImportMappingRegistry.Version, "MEDIUM", false, false, 2, 1, []);
        var payment = baseRequest.Preview.Records[0] with
        {
            RecordId = "f06-payment-record", SectionId = "f06-payment", SheetName = "دفعات تحت الحساب",
            SourceCoordinate = "دفعات تحت الحساب!K1:L1", RecordType = UniversalRecordType.Payment,
            StreamKey = "main_payment_ledger", EligibleForPosting = false
        };
        var request = baseRequest with
        {
            Preview = baseRequest.Preview with
            {
                SourceSha256 = "FFEEDDCCBBAA00112233445566778899FFEEDDCCBBAA00112233445566778899",
                Sections = [paymentSection], Records = [payment]
            }
        };
        var staged = await service.StageAsync(request, 10, CancellationToken.None);
        Assert.Equal(1, staged.ReadyRecords);
        await service.ApproveAsync(staged.BatchId, 11, CancellationToken.None);
        var posted = await service.PostAsync(staged.BatchId, 12, CancellationToken.None);
        Assert.Equal(1, posted.PostedRecords);
        Assert.Equal("payment_or_funding", fixture.Db.HistoricalPostedEntries.Single().LedgerStream);

        var allocationRequest = request with
        {
            Preview = request.Preview with
            {
                SourceSha256 = "11223344556677889900AABBCCDDEEFF11223344556677889900AABBCCDDEEFF",
                Sections = [paymentSection with { SectionId = "f06-allocation", StreamKey = "allocation_ledger" }],
                Records = [payment with { RecordId = "f06-allocation-record", SectionId = "f06-allocation", StreamKey = "allocation_ledger", RecordType = UniversalRecordType.Allocation }]
            }
        };
        var allocation = await service.StageAsync(allocationRequest, 10, CancellationToken.None);
        Assert.Equal(0, allocation.ReadyRecords);
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.ApproveAsync(allocation.BatchId, 11, CancellationToken.None));
    }

    private static HistoricalImportStageRequest Request(bool includeUnresolved, bool includeDuplicate, string currency = "EGP")
    {
        var sections = new List<UniversalSectionPreview>
        {
            new("section-1", "Synthetic", "A1:B2", "workbook_stream", "F01_SITE_EXPENSE", UniversalImportMappingRegistry.Version, "MEDIUM", false, true, 2, 1, []),
            new("section-2", "Synthetic", "C3:D3", "workbook_stream", "F07_FINAL_ACCOUNT", UniversalImportMappingRegistry.Version, "HIGH", false, false, 1, 1, [])
        };
        var records = new List<UniversalRecordPreview>
        {
            new("record-1", "section-1", "Synthetic", "Synthetic!A1:B1", ["A1", "B1"], UniversalRecordType.Expense, "workbook_stream", currency, 1250m, false, false, currency == "EGP", "MEDIUM", "expense-fingerprint")
        };
        if (includeUnresolved)
            records.Add(new("record-unresolved", "section-1", "Synthetic", "Synthetic!A2:B2", ["A2", "B2"], UniversalRecordType.Unresolved, "workbook_stream", "EGP", 50m, false, false, false, "LOW", "unresolved-fingerprint"));
        if (includeDuplicate)
            records.Add(new("record-duplicate", "section-1", "Synthetic", "Synthetic!A3:B3", ["A3", "B3"], UniversalRecordType.Expense, "workbook_stream", "EGP", 1250m, false, false, true, "MEDIUM", "duplicate-fingerprint"));
        return new HistoricalImportStageRequest(1, "owner-approval-ref", new UniversalPreviewResponse("synthetic.xlsx", 100, "AABBCCDDEEFF00112233445566778899AABBCCDDEEFF00112233445566778899", UniversalImportMappingRegistry.Version, false, sections, records, [], [], includeDuplicate ? [new UniversalDuplicateCandidate("duplicate-fingerprint", ["record-duplicate"], 1)] : [], []));
    }

    private sealed class Fixture(SqliteConnection connection, EmgDbContext db) : IAsyncDisposable
    {
        public SqliteConnection Connection { get; } = connection;
        public EmgDbContext Db { get; } = db;
        public static async Task<Fixture> Create()
        {
            var connection = new SqliteConnection("Data Source=:memory:");
            await connection.OpenAsync();
            var options = new DbContextOptionsBuilder<EmgDbContext>().UseSqlite(connection).Options;
            var db = new EmgDbContext(options);
            await db.Database.EnsureCreatedAsync();
            db.Projects.Add(new Project { Id = 1, Name = "Synthetic", Code = "SYN-POST" });
            await db.SaveChangesAsync();
            return new Fixture(connection, db);
        }
        public async ValueTask DisposeAsync() { await Db.DisposeAsync(); await Connection.DisposeAsync(); }
    }

    private sealed class FailAfterFirstEntryInjector : IHistoricalImportPostingFaultInjector
    {
        public void AfterEntryPrepared(int entryIndex)
        {
            if (entryIndex == 0) throw new InvalidOperationException("TEST_FAULT_AFTER_FIRST_ENTRY");
        }
    }
}
