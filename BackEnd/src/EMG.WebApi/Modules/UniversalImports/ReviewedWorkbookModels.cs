using Microsoft.Extensions.Caching.Memory;

namespace EMG.Api.Modules.UniversalImports;

internal sealed class HistoricalWorkbookPostingDisabledException : Exception;

public sealed record ReviewedWorkbookPreview(string SnapshotId, int ProjectId, string ProjectCode, string ProjectName,
    UniversalPreviewResponse Workbook);

public sealed record ReviewedWorkbookStageRequest(string SnapshotId, string SourceApprovalReference,
    bool ConfirmedProject, IReadOnlyList<string> ConfirmedRecordIds, string? MappingEvidence = null);

public sealed record ReviewedWorkbookBatch(int Id, int ProjectId, string SourceFileName, string SourceSha256, string Status,
    bool CanApprove, bool CanPost, int OriginalRecordCount, IReadOnlyList<ReviewedWorkbookRecord> Records,
    IReadOnlyList<UniversalReconciliationPreview> Reconciliations, IReadOnlyList<ReviewedWorkbookTotal> Totals, int Version, bool CanRevise);

public sealed record ReviewedWorkbookRecord(int Id, string SourceCoordinate, string RecordType, string Currency, decimal? Amount,
    string? Target, string Status, string? Reason, int? TargetId, bool CanSelect, bool NeedsMappingEvidence);

public sealed record ReviewedWorkbookDecision(int Version, int RecordId, bool Selected, string? Evidence);

public sealed record ReviewedWorkbookTotal(string Target, string Currency, int SelectedCount, decimal SelectedAmount,
    int PostedCount, decimal PostedAmount);

// Only the server can create snapshots. A snapshot stays bound to its uploader
// and project, and staged evidence remains available after this cache expires.
public sealed class ReviewedWorkbookSnapshots : IDisposable
{
    private readonly MemoryCache cache = new(new MemoryCacheOptions { SizeLimit = 200_000 });

    public string Store(int actor, int project, UniversalPreviewResponse preview)
    {
        var id = Guid.NewGuid().ToString("N");
        cache.Set(id, new Snapshot(actor, project, preview),
            new MemoryCacheEntryOptions { Size = Math.Max(preview.Records.Count, 1), AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(45) });
        return id;
    }

    public Snapshot? Find(string? id, int actor) =>
        id is { Length: 32 } && cache.TryGetValue<Snapshot>(id, out var snapshot) && snapshot?.Actor == actor ? snapshot : null;

    public void Dispose() => cache.Dispose();

    public sealed record Snapshot(int Actor, int ProjectId, UniversalPreviewResponse Preview)
    {
        private DailyValidation? dailyValidation;
        internal DailyValidation? Validation => Volatile.Read(ref dailyValidation);

        internal void BindDailyPlan(string planSha256) =>
            Volatile.Write(ref dailyValidation, new DailyValidation(Preview.SourceSha256, planSha256));
    }

    internal sealed record DailyValidation(string SourceSha256, string PlanSha256);
}
