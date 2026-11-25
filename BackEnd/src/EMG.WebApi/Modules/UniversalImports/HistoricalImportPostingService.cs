using System.Text.Json;
using EMG.Domain.Entities;
using EMG.Infrastructure.Db;
using Microsoft.EntityFrameworkCore;

namespace EMG.Api.Modules.UniversalImports;

public interface IHistoricalImportPostingService
{
    Task<HistoricalImportBatchReport> StageAsync(HistoricalImportStageRequest request, int actorUserId, CancellationToken cancellationToken);
    Task<HistoricalImportBatchReport> ApproveAsync(int batchId, int actorUserId, CancellationToken cancellationToken);
    Task<HistoricalImportBatchReport> PostAsync(int batchId, int actorUserId, CancellationToken cancellationToken);
    Task<HistoricalImportBatchReport> RejectAsync(int batchId, int actorUserId, string reason, CancellationToken cancellationToken);
    Task<HistoricalImportBatchReport> GetAsync(int batchId, CancellationToken cancellationToken);
}

public interface IHistoricalImportPostingFaultInjector
{
    void AfterEntryPrepared(int entryIndex);
}

public sealed class HistoricalImportPostingService(EmgDbContext db, IHistoricalImportPostingFaultInjector? faultInjector = null) : IHistoricalImportPostingService
{
    private static readonly HashSet<string> AllowedCurrencies = ["EGP", "USD"];
    private static readonly HashSet<string> PostingFamilies = [
        "F01_SITE_EXPENSE", "F02_EXCAVATION", "F03_CONCRETE", "F04_STEEL_SUPPLY",
        "F05_FINISHING", "F06_ADVANCE_PARALLEL"];

    public async Task<HistoricalImportBatchReport> StageAsync(HistoricalImportStageRequest request, int actorUserId, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.Preview is null) throw new InvalidOperationException("A preview is required before staging.");
        if (string.IsNullOrWhiteSpace(request.SourceApprovalReference)) throw new InvalidOperationException("An owner approval reference is required.");
        if (!await db.Projects.AnyAsync(item => item.Id == request.ProjectId, cancellationToken)) throw new KeyNotFoundException("Project was not found.");

        var existing = await db.HistoricalImportBatches.Include(item => item.Records)
            .SingleOrDefaultAsync(item => item.ProjectId == request.ProjectId && item.SourceSha256 == request.Preview.SourceSha256, cancellationToken);
        if (existing is not null) return ToReport(existing);

        var duplicateIds = request.Preview.DuplicateCandidates.SelectMany(item => item.RecordIds).ToHashSet(StringComparer.Ordinal);
        var batch = new HistoricalImportBatch
        {
            ProjectId = request.ProjectId,
            SourceSha256 = request.Preview.SourceSha256,
            SourceFileName = request.Preview.FileName,
            MappingVersion = request.Preview.EngineVersion,
            SourceApprovalReference = request.SourceApprovalReference.Trim(),
            CreatedByUserId = actorUserId,
            Status = HistoricalImportStatuses.PreviewOnly
        };

        foreach (var item in request.Preview.Records)
        {
            var mappingFamily = request.Preview.Sections.FirstOrDefault(section => section.SectionId == item.SectionId)?.MappingFamily ?? "MAPPING_REQUIRED";
            var independentF06Payment = mappingFamily == "F06_ADVANCE_PARALLEL" && item.StreamKey != "allocation_ledger" && item.RecordType is UniversalRecordType.Payment or UniversalRecordType.Funding;
            var supported = PostingFamilies.Contains(mappingFamily) && (item.EligibleForPosting || independentF06Payment) && !item.IsSummary && item.RecordType != UniversalRecordType.Unresolved;
            var validCurrency = AllowedCurrencies.Contains(item.Currency);
            var duplicate = duplicateIds.Contains(item.RecordId);
            var ready = supported && validCurrency && !duplicate && item.Amount is not null && item.Amount != 0;
            var semantic = ready ? "SEMANTICALLY_COMPLETE" : item.IsSummary ? "DERIVED_OR_SUMMARY" : validCurrency ? "REVIEW_REQUIRED" : "INVALID_CURRENCY";
            var status = ready ? HistoricalImportStatuses.ReadyForApproval : item.IsSummary || item.RecordType is UniversalRecordType.DerivedValue or UniversalRecordType.Summary ? HistoricalImportStatuses.Rejected : HistoricalImportStatuses.ReviewRequired;
            var reason = ready ? null : item.IsSummary ? "Summary or derived source; never posted as a transaction." : duplicate ? "Duplicate candidate requires explicit review." : !validCurrency ? "Currency is not approved for historical posting." : !supported ? "Mapping is preview-only or lacks posting semantics." : "Amount is missing or zero.";
            batch.Records.Add(new HistoricalImportRecord
            {
                DeterministicIdentity = item.RecordId,
                SectionId = item.SectionId,
                SheetName = item.SheetName,
                SourceCoordinate = item.SourceCoordinate,
                SourceCellsJson = JsonSerializer.Serialize(item.SourceCells),
                MappingFamily = mappingFamily,
                MappingVersion = request.Preview.EngineVersion,
                BusinessEventType = item.RecordType.ToString(),
                OriginalAmount = item.Amount,
                Currency = item.Currency,
                SemanticValidationState = semantic,
                LifecycleStatus = status,
                IsSummary = item.IsSummary,
                IsDuplicateCandidate = duplicate,
                EligibleForPosting = ready,
                ValueFingerprint = item.ValueFingerprint,
                ExclusionReason = reason
            });
        }

        batch.Status = batch.Records.Any(item => item.LifecycleStatus == HistoricalImportStatuses.ReadyForApproval)
            ? HistoricalImportStatuses.ReviewRequired : HistoricalImportStatuses.PreviewOnly;
        batch.AuditEvents.Add(new HistoricalImportAuditEvent { ActorUserId = actorUserId, Action = "BATCH_STAGED", DetailsJson = JsonSerializer.Serialize(new { batch.SourceSha256, batch.Records.Count }) });
        db.HistoricalImportBatches.Add(batch);
        await db.SaveChangesAsync(cancellationToken);
        return ToReport(batch);
    }

    public async Task<HistoricalImportBatchReport> ApproveAsync(int batchId, int actorUserId, CancellationToken cancellationToken)
    {
        var batch = await Load(batchId, cancellationToken);
        if (string.IsNullOrWhiteSpace(batch.SourceApprovalReference)) throw new InvalidOperationException("Owner approval reference is required.");
        if (batch.Status is HistoricalImportStatuses.Posted or HistoricalImportStatuses.Rejected) throw new InvalidOperationException($"Batch is already {batch.Status}.");
        if (batch.CreatedByUserId == actorUserId) throw new InvalidOperationException("The batch creator cannot approve the same historical batch.");
        var ready = batch.Records.Where(item => item.LifecycleStatus == HistoricalImportStatuses.ReadyForApproval).ToList();
        if (ready.Count == 0) throw new InvalidOperationException("No semantically complete records are available for approval.");
        foreach (var record in ready) { record.LifecycleStatus = HistoricalImportStatuses.ApprovedForPosting; record.SemanticValidationState = "APPROVED"; }
        batch.Status = HistoricalImportStatuses.ApprovedForPosting;
        batch.ApprovedByUserId = actorUserId;
        batch.ApprovedAtUtc = DateTime.UtcNow;
        batch.Version++;
        batch.AuditEvents.Add(new HistoricalImportAuditEvent { ActorUserId = actorUserId, Action = "BATCH_APPROVED", DetailsJson = JsonSerializer.Serialize(new { approved = ready.Count, quarantined = batch.Records.Count(item => item.LifecycleStatus == HistoricalImportStatuses.ReviewRequired) }) });
        await db.SaveChangesAsync(cancellationToken);
        return ToReport(batch);
    }

    public async Task<HistoricalImportBatchReport> PostAsync(int batchId, int actorUserId, CancellationToken cancellationToken)
    {
        var batch = await Load(batchId, cancellationToken);
        if (batch.Status == HistoricalImportStatuses.Posted) return ToReport(batch);
        if (batch.Status != HistoricalImportStatuses.ApprovedForPosting) throw new InvalidOperationException("Batch must be approved before posting.");
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            var approved = batch.Records.Where(item => item.LifecycleStatus == HistoricalImportStatuses.ApprovedForPosting).ToList();
            for (var entryIndex = 0; entryIndex < approved.Count; entryIndex++)
            {
                var record = approved[entryIndex];
                if (!AllowedCurrencies.Contains(record.Currency) || record.OriginalAmount is null || record.OriginalAmount == 0)
                    throw new InvalidOperationException($"Record {record.Id} failed currency or amount validation.");
                var existing = await db.HistoricalPostedEntries.SingleOrDefaultAsync(item => item.DeterministicIdentity == record.DeterministicIdentity, cancellationToken);
                if (existing is not null)
                {
                    if (existing.BatchId != batch.Id) throw new InvalidOperationException("A record with the same deterministic identity is already posted in another batch.");
                    record.LifecycleStatus = HistoricalImportStatuses.Posted;
                    continue;
                }
                var entry = new HistoricalPostedEntry
                {
                    ProjectId = batch.ProjectId,
                    BatchId = batch.Id,
                    HistoricalRecordId = record.Id,
                    DeterministicIdentity = record.DeterministicIdentity,
                    LedgerStream = StreamFor(record),
                    BusinessEventType = record.BusinessEventType,
                    Amount = record.OriginalAmount.Value,
                    Currency = record.Currency,
                    SourceSha256 = batch.SourceSha256,
                    ProvenanceJson = JsonSerializer.Serialize(new { record.SheetName, record.SourceCoordinate, record.SectionId, record.MappingVersion }),
                    PostedByUserId = actorUserId
                };
                db.HistoricalPostedEntries.Add(entry);
                record.LifecycleStatus = HistoricalImportStatuses.Posted;
                record.PostedEntry = entry;
                batch.AuditEvents.Add(new HistoricalImportAuditEvent { Record = record, ActorUserId = actorUserId, Action = "RECORD_POSTED", DetailsJson = JsonSerializer.Serialize(new { record.DeterministicIdentity, record.Currency, record.OriginalAmount }) });
                faultInjector?.AfterEntryPrepared(entryIndex);
            }
            batch.Status = HistoricalImportStatuses.Posted;
            batch.PostedAtUtc = DateTime.UtcNow;
            batch.Version++;
            batch.AuditEvents.Add(new HistoricalImportAuditEvent { ActorUserId = actorUserId, Action = "BATCH_POSTED", DetailsJson = JsonSerializer.Serialize(new { posted = approved.Count }) });
            await db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return ToReport(batch);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }

    public async Task<HistoricalImportBatchReport> RejectAsync(int batchId, int actorUserId, string reason, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(reason)) throw new InvalidOperationException("A rejection reason is required.");
        var batch = await Load(batchId, cancellationToken);
        if (batch.Status == HistoricalImportStatuses.Posted) throw new InvalidOperationException("A posted batch cannot be rejected.");
        batch.Status = HistoricalImportStatuses.Rejected;
        batch.RejectionReason = reason.Trim();
        foreach (var record in batch.Records.Where(item => item.LifecycleStatus != HistoricalImportStatuses.Posted))
        { record.LifecycleStatus = HistoricalImportStatuses.Rejected; record.ExclusionReason = reason.Trim(); }
        batch.Version++;
        batch.AuditEvents.Add(new HistoricalImportAuditEvent { ActorUserId = actorUserId, Action = "BATCH_REJECTED", DetailsJson = JsonSerializer.Serialize(new { reason = reason.Trim() }) });
        await db.SaveChangesAsync(cancellationToken);
        return ToReport(batch);
    }

    public async Task<HistoricalImportBatchReport> GetAsync(int batchId, CancellationToken cancellationToken) => ToReport(await Load(batchId, cancellationToken));

    private async Task<HistoricalImportBatch> Load(int id, CancellationToken cancellationToken) =>
        await db.HistoricalImportBatches.Include(item => item.Records).Include(item => item.AuditEvents).SingleOrDefaultAsync(item => item.Id == id, cancellationToken)
        ?? throw new KeyNotFoundException("Historical import batch was not found.");

    private static string StreamFor(HistoricalImportRecord record) => record.MappingFamily == "F06_ADVANCE_PARALLEL" ? record.BusinessEventType == nameof(UniversalRecordType.Allocation) ? "allocation" : "payment_or_funding" : record.MappingFamily;

    private static HistoricalImportBatchReport ToReport(HistoricalImportBatch batch)
    {
        var records = batch.Records.Select(item => new HistoricalImportRecordReport(item.Id, item.DeterministicIdentity, item.SheetName, item.SourceCoordinate, item.MappingFamily, item.BusinessEventType, item.OriginalAmount, item.Currency, item.SemanticValidationState, item.LifecycleStatus, item.IsDuplicateCandidate, item.EligibleForPosting, item.ExclusionReason)).ToList();
        var streams = batch.Records.Where(item => item.OriginalAmount.HasValue && !item.IsSummary).GroupBy(item => (item.MappingFamily, item.Currency)).Select(group => new HistoricalImportStreamReport(group.Key.MappingFamily, group.Key.Currency, group.Sum(item => item.OriginalAmount!.Value), group.Count(), group.Count(item => item.LifecycleStatus == HistoricalImportStatuses.Posted))).ToList();
        return new HistoricalImportBatchReport(batch.Id, batch.ProjectId, batch.SourceSha256, batch.Status, records.Count,
            records.Count(item => item.LifecycleStatus == HistoricalImportStatuses.ReadyForApproval),
            records.Count(item => item.LifecycleStatus == HistoricalImportStatuses.ApprovedForPosting),
            records.Count(item => item.LifecycleStatus == HistoricalImportStatuses.Posted),
            records.Count(item => item.LifecycleStatus == HistoricalImportStatuses.ReviewRequired),
            records.Count(item => item.LifecycleStatus == HistoricalImportStatuses.Rejected), streams, records);
    }
}
