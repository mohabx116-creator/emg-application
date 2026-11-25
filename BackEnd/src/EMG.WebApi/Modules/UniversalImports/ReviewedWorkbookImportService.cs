using System.Text.Json;
using EMG.Api.Modules.Imports.Services;
using EMG.Domain.Entities;
using EMG.Infrastructure.Db;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace EMG.Api.Modules.UniversalImports;

public sealed partial class ReviewedWorkbookImportService(
    EmgDbContext db, IUniversalImportPreviewService previewService, ReviewedWorkbookSnapshots snapshots,
    IConfiguration configuration, IWebHostEnvironment environment)
{
    private const string StageEvent = "WORKBOOK_SNAPSHOT_STAGED";
    internal const string OperationalStreamPrefix = "operational:";
    private const string Approved = "معتمد";
    private const int MaxSnapshotRecords = 20000;

    public async Task<ReviewedWorkbookPreview> PreviewAsync(int projectId, IFormFile file, int actor, bool admin,
        CancellationToken cancellationToken)
    {
        await RequireGrant(projectId, actor, admin, cancellationToken);
        var project = await FindProject(projectId, cancellationToken);
        var preview = await previewService.PreviewAsync(file, cancellationToken);
        if (preview.Records.Count > MaxSnapshotRecords || preview.FileName.Length > 260)
            throw new InvalidOperationException("الملف يتجاوز حجم المعاينة المتاح. قسّم المصدر إلى ملفات مستقلة مع الحفاظ على مراجعها.");
        var id = snapshots.Store(actor, projectId, preview);
        return new ReviewedWorkbookPreview(id, project.Id, project.Code, project.Name, preview);
    }

    public async Task<ReviewedWorkbookBatch> StageAsync(ReviewedWorkbookStageRequest request, int actor, bool admin,
        CancellationToken cancellationToken)
    {
        var snapshot = snapshots.Find(request.SnapshotId, actor)
            ?? throw new KeyNotFoundException("انتهت صلاحية المعاينة أو لا تخص حسابك. أعد رفع الملف.");
        var cap = await RequireGrant(snapshot.ProjectId, actor, admin, cancellationToken);
        if (!request.ConfirmedProject || string.IsNullOrWhiteSpace(request.SourceApprovalReference) ||
            request.SourceApprovalReference.Trim().Length > 500 || request.ConfirmedRecordIds is null)
            throw new InvalidOperationException("أكد هوية المشروع وأدخل مرجع مراجعة المصدر.");

        var project = await FindProject(snapshot.ProjectId, cancellationToken);
        var preview = snapshot.Preview;
        if (preview.SourceProjectCode is { } code && WorkbookImportService.NormalizePlotNumber(project.Code) != code)
            throw new InvalidOperationException("رقم القطعة في الملف لا يطابق المشروع المحدد.");
        if (preview.SourceProjectCode is { } plot &&
            (await db.Projects.AsNoTracking().Select(item => item.Code).ToListAsync(cancellationToken))
                .Count(projectCode => WorkbookImportService.NormalizePlotNumber(projectCode) == plot) != 1)
            throw new InvalidOperationException("رقم القطعة غير فريد بين المشروعات.");

        var sections = preview.Sections.ToDictionary(section => section.SectionId);
        var choices = request.ConfirmedRecordIds.ToHashSet(StringComparer.Ordinal);
        var duplicates = preview.DuplicateCandidates.SelectMany(item => item.RecordIds).ToHashSet(StringComparer.Ordinal);
        var mappingEvidence = request.MappingEvidence?.Trim();
        var validChoices = preview.Records.Where(record => record.Currency == "EGP" &&
                (record.EligibleForPosting || record.OperationalEvidence?.RequiresManualMapping == true &&
                    mappingEvidence is { Length: >= 8 and <= 500 }) && !duplicates.Contains(record.RecordId))
            .Select(record => record.RecordId).ToHashSet(StringComparer.Ordinal);
        if (!choices.IsSubsetOf(validChoices))
            throw new InvalidOperationException("أحد السجلات المحددة غير مؤهل للمطابقة أو لا ينتمي لهذه المعاينة.");

        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        await ImportProjectLock.AcquireAsync(db, project.Id, cancellationToken);
        var normalizedHash = preview.SourceSha256.ToUpperInvariant();
        var existing = await Batches().FirstOrDefaultAsync(item => item.SourceSha256.ToUpper() == normalizedHash, cancellationToken);
        if (existing is not null)
        {
            if (existing.ProjectId != project.Id || !IsReviewed(existing))
                throw new InvalidOperationException("هذا المصدر محفوظ في مشروع أو مسار ترحيل آخر.");
            return BuildReport(existing, actor, cap);
        }

        var batch = new HistoricalImportBatch
        {
            ProjectId = project.Id, SourceFileName = preview.FileName, SourceSha256 = normalizedHash,
            MappingVersion = preview.EngineVersion, CreatedByUserId = actor,
            SourceApprovalReference = request.SourceApprovalReference.Trim(), Status = HistoricalImportStatuses.PreviewOnly
        };
        foreach (var record in preview.Records)
        {
            var summary = record.IsSummary || record.RecordType is UniversalRecordType.Summary or UniversalRecordType.DerivedValue;
            var duplicate = duplicates.Contains(record.RecordId);
            var evidence = record.OperationalEvidence;
            var selected = choices.Contains(record.RecordId);
            var supplierId = selected && evidence?.Target == "SupplierSupply" ? await MatchSupplier(evidence.Party!, cancellationToken) : null;
            var f06ParallelReason = sections[record.SectionId].MappingFamily == "F06_ADVANCE_PARALLEL"
                ? "كشف F06 مستقل عن الجداول المالية الموازية؛ لا يثبت المصدر طرفاً أو التزاماً أو علاقة دفع/توزيع صريحة، لذلك يبقى السجل للمراجعة ولا يُرحّل تلقائياً."
                : null;
            var reason = summary ? "إجمالي أو قيد مرجعي للمطابقة."
                : duplicate ? "تكرار محتمل. يلزم دليل يميّز الحركتين."
                : record.Currency != "EGP" ? "عملة المصدر محفوظة للمراجعة. الحساب التشغيلي بالجنيه المصري."
                : evidence is null ? f06ParallelReason ?? "تحتاج الخلية إلى دليل على نوع الحركة وحقولها أو الربط المحاسبي."
                : !selected ? "المطابقة مقترحة وتحتاج تأكيد المراجع."
                : cap.HasValue && record.Amount > cap ? "القيمة تتجاوز حد الصلاحية."
                : evidence.Target == "SupplierSupply" && !supplierId.HasValue ? "اسم المورد في المصدر لا يحدد مورداً نشطاً واحداً."
                : evidence.Target == "AdvancePayment" && !SameParty(project.PayerName, evidence.Party) ? "اسم الممول لا يطابق الدافع المسجل."
                : null;
            var ready = selected && reason is null;
            if (ready) batch.Status = HistoricalImportStatuses.ReviewRequired;
            batch.Records.Add(new HistoricalImportRecord
            {
                DeterministicIdentity = record.RecordId, SectionId = record.SectionId, SheetName = record.SheetName,
                SourceCoordinate = record.SourceCoordinate, MappingFamily = sections[record.SectionId].MappingFamily,
                MappingVersion = sections[record.SectionId].MappingVersion,
                BusinessEventType = ready && evidence?.RequiresManualMapping == true ? nameof(UniversalRecordType.Expense) : record.RecordType.ToString(),
                SourceCellsJson = JsonSerializer.Serialize(new StagedEvidence(record.SourceCells, evidence, record.Amount, supplierId,
                    selected && evidence?.RequiresManualMapping == true ? mappingEvidence : null)),
                OriginalAmount = record.Amount is { } amount && Math.Abs(amount) <= 9999999999999999.99m &&
                    decimal.Round(amount, 2) == amount ? amount : null,
                Currency = record.Currency, SemanticValidationState = summary ? "DERIVED_OR_SUMMARY" :
                    ready ? "SEMANTICALLY_COMPLETE" : "REVIEW_REQUIRED",
                LifecycleStatus = summary ? HistoricalImportStatuses.Rejected : ready ? HistoricalImportStatuses.ReadyForApproval :
                    HistoricalImportStatuses.ReviewRequired,
                IsSummary = summary, IsDuplicateCandidate = duplicate, EligibleForPosting = ready,
                ValueFingerprint = record.ValueFingerprint, ExclusionReason = reason
            });
        }
        batch.AuditEvents.Add(new HistoricalImportAuditEvent
        {
            ActorUserId = actor, Action = StageEvent, DetailsJson = JsonSerializer.Serialize(preview.Reconciliations)
        });
        db.HistoricalImportBatches.Add(batch);
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return BuildReport(batch, actor, cap);
    }

    public async Task<ReviewedWorkbookBatch> GetAsync(int id, int actor, bool admin, CancellationToken cancellationToken)
    {
        var batch = await Load(id, cancellationToken);
        var cap = await RequireGrant(batch.ProjectId, actor, admin, cancellationToken);
        return BuildReport(batch, actor, cap);
    }

    public async Task<IReadOnlyList<int>> ListAsync(int projectId, int actor, bool admin, CancellationToken cancellationToken)
    {
        await RequireGrant(projectId, actor, admin, cancellationToken);
        return await db.HistoricalImportBatches.AsNoTracking().Where(batch => batch.ProjectId == projectId &&
            batch.AuditEvents.Any(action => action.Action == StageEvent)).OrderByDescending(batch => batch.Id)
            .Select(batch => batch.Id).ToListAsync(cancellationToken);
    }

    public async Task<ReviewedWorkbookBatch> ReviseAsync(int id, ReviewedWorkbookDecision decision, int actor, bool admin,
        CancellationToken cancellationToken)
    {
        var batch = await Load(id, cancellationToken);
        var cap = await RequireGrant(batch.ProjectId, actor, admin, cancellationToken);
        if (!CanRevise(batch, actor) || batch.Version != decision.Version)
            throw new InvalidOperationException("تغيّرت الدفعة أو انتهت مرحلة تعديل المطابقة. أعد فتحها.");
        var record = batch.Records.SingleOrDefault(item => item.Id == decision.RecordId)
            ?? throw new KeyNotFoundException("السجل غير موجود في هذه الدفعة.");
        var evidence = ReadEvidence(record);
        if (!CanSelect(record, evidence, cap))
            throw new InvalidOperationException("دليل المصدر لا يسمح بتأكيد هذا السجل.");
        var fields = evidence.Fields!;
        var note = decision.Evidence?.Trim();
        if (decision.Selected && fields.RequiresManualMapping && note is not { Length: >= 8 and <= 500 })
            throw new InvalidOperationException("أدخل دليل المطابقة اليدوية.");
        int? supplierId = null;
        if (decision.Selected && fields.Target == "SupplierSupply")
            supplierId = await MatchSupplier(fields.Party!, cancellationToken)
                ?? throw new InvalidOperationException("يلزم مورد نشط واحد مطابق للاسم في المصدر.");
        if (decision.Selected && fields.Target == "AdvancePayment" &&
            !SameParty((await FindProject(batch.ProjectId, cancellationToken)).PayerName, fields.Party))
            throw new InvalidOperationException("اسم الممول لا يطابق الدافع المسجل.");
        var previous = record.LifecycleStatus;
        record.LifecycleStatus = decision.Selected ? HistoricalImportStatuses.ReadyForApproval : HistoricalImportStatuses.ReviewRequired;
        record.SemanticValidationState = decision.Selected ? "SEMANTICALLY_COMPLETE" : "REVIEW_REQUIRED";
        record.EligibleForPosting = decision.Selected;
        record.ExclusionReason = decision.Selected ? null : "أُعيد السجل للمراجعة.";
        if (fields.RequiresManualMapping)
            record.BusinessEventType = decision.Selected ? nameof(UniversalRecordType.Expense) : nameof(UniversalRecordType.Unresolved);
        record.SourceCellsJson = JsonSerializer.Serialize(evidence with
        {
            SupplierId = supplierId, MappingEvidence = decision.Selected && fields.RequiresManualMapping ? note : null
        });
        batch.Status = batch.Records.Any(item => item.LifecycleStatus == HistoricalImportStatuses.ReadyForApproval)
            ? HistoricalImportStatuses.ReviewRequired : HistoricalImportStatuses.PreviewOnly;
        batch.Version++;
        batch.AuditEvents.Add(new HistoricalImportAuditEvent
        {
            ActorUserId = actor, RecordId = record.Id, Action = "WORKBOOK_MAPPING_REVISED",
            DetailsJson = JsonSerializer.Serialize(new { Previous = previous, Current = record.LifecycleStatus })
        });
        await db.SaveChangesAsync(cancellationToken);
        return BuildReport(batch, actor, cap);
    }

    private static bool CanRevise(HistoricalImportBatch batch, int actor) =>
        batch.CreatedByUserId == actor && batch.Status is HistoricalImportStatuses.PreviewOnly or HistoricalImportStatuses.ReviewRequired;

    private static bool CanSelect(HistoricalImportRecord record, StagedEvidence evidence, decimal? cap) =>
        !record.IsSummary && !record.IsDuplicateCandidate && record.Currency == "EGP" &&
        IsSupportedAmount(record, evidence) &&
        (!cap.HasValue || Math.Abs(record.OriginalAmount!.Value) <= cap) && evidence.Fields is not null && evidence.Amount == record.OriginalAmount;

    public async Task<ReviewedWorkbookBatch> ApproveAsync(int id, int? version, int actor, bool admin, CancellationToken cancellationToken)
    {
        var batch = await Load(id, cancellationToken);
        var cap = await RequireGrant(batch.ProjectId, actor, admin, cancellationToken);
        var ready = batch.Records.Where(record => record.LifecycleStatus == HistoricalImportStatuses.ReadyForApproval).ToArray();
        if (version != batch.Version || !CanApprove(batch, ready, actor, cap))
            throw new InvalidOperationException("الاعتماد يتطلب مراجعاً مختلفاً بصلاحية تغطي السجلات المؤكدة.");
        foreach (var record in ready)
        {
            record.LifecycleStatus = HistoricalImportStatuses.ApprovedForPosting;
            record.SemanticValidationState = "APPROVED";
        }
        batch.Status = HistoricalImportStatuses.ApprovedForPosting;
        batch.ApprovedByUserId = actor;
        batch.ApprovedAtUtc = DateTime.UtcNow;
        batch.Version++;
        batch.AuditEvents.Add(new HistoricalImportAuditEvent
        {
            ActorUserId = actor, Action = "WORKBOOK_SNAPSHOT_APPROVED",
            DetailsJson = JsonSerializer.Serialize(new { Selected = ready.Select(record => record.Id) })
        });
        await db.SaveChangesAsync(cancellationToken);
        return BuildReport(batch, actor, cap);
    }

    private static bool CanApprove(HistoricalImportBatch batch, HistoricalImportRecord[] ready, int actor, decimal? cap) =>
        batch.Status == HistoricalImportStatuses.ReviewRequired && actor != batch.CreatedByUserId &&
        ready.Length > 0 && ready.All(record => record.OriginalAmount is { } amount && amount != 0m &&
            (!cap.HasValue || Math.Abs(amount) <= cap));

    private static bool IsSupportedAmount(HistoricalImportRecord record, StagedEvidence evidence)
    {
        if (record.OriginalAmount is not { } amount || amount == 0m) return false;
        if (amount > 0m) return true;
        return record.BusinessEventType == nameof(UniversalRecordType.Adjustment) &&
            evidence.Fields is { Target: "SiteMovement" or "FinishingRecord" } fields &&
            IsSignedAdjustment(fields.Description, fields.Quantity, amount);
    }

    private static bool IsSignedAdjustment(string? text, decimal? quantity, decimal? amount) =>
        amount is < 0m && (quantity is null || quantity < 0m) &&
        !string.IsNullOrWhiteSpace(text) &&
        (text.Contains("خصم", StringComparison.OrdinalIgnoreCase) ||
         text.Contains("هالك", StringComparison.OrdinalIgnoreCase) ||
         text.Contains("تسوية", StringComparison.OrdinalIgnoreCase) ||
         text.Contains("تعديل", StringComparison.OrdinalIgnoreCase) ||
         text.Contains("مرتجع", StringComparison.OrdinalIgnoreCase) ||
         text.Contains("استرداد", StringComparison.OrdinalIgnoreCase));

    private ReviewedWorkbookBatch BuildReport(HistoricalImportBatch batch, int actor, decimal? cap)
    {
        var ready = batch.Records.Where(record => record.LifecycleStatus == HistoricalImportStatuses.ReadyForApproval).ToArray();
        var reconciliations = JsonSerializer.Deserialize<List<UniversalReconciliationPreview>>(
            batch.AuditEvents.Single(item => item.Action == StageEvent).DetailsJson) ?? [];
        var records = batch.Records.OrderBy(item => item.Id).Select(record =>
        {
            var evidence = ReadEvidence(record);
            var targetId = record.PostedEntry is { } entry
                ? JsonSerializer.Deserialize<OperationalProvenance>(entry.ProvenanceJson)?.TargetId : null;
            return new ReviewedWorkbookRecord(record.Id, record.SourceCoordinate, record.BusinessEventType, record.Currency,
                evidence.Amount, evidence.Fields?.Target, record.LifecycleStatus, record.ExclusionReason, targetId,
                CanSelect(record, evidence, cap), evidence.Fields?.RequiresManualMapping == true);
        }).ToArray();
        var totals = records.Where(record => record.Target is not null && record.Status is
            HistoricalImportStatuses.ReadyForApproval or HistoricalImportStatuses.ApprovedForPosting or HistoricalImportStatuses.Posted)
            .GroupBy(record => (Target: record.Target!, record.Currency)).Select(group =>
            {
                var posted = group.Where(record => record.Status == HistoricalImportStatuses.Posted).ToArray();
                return new ReviewedWorkbookTotal(group.Key.Target, group.Key.Currency, group.Count(),
                    group.Sum(record => record.Amount!.Value), posted.Length, posted.Sum(record => record.Amount!.Value));
            }).OrderBy(total => total.Target).ToArray();
        return new ReviewedWorkbookBatch(batch.Id, batch.ProjectId, batch.SourceFileName, batch.SourceSha256, batch.Status,
            CanApprove(batch, ready, actor, cap), batch.Status == HistoricalImportStatuses.ApprovedForPosting &&
                IsSyntheticPostAllowed(batch), records.Length, records, reconciliations, totals, batch.Version, CanRevise(batch, actor));
    }

    private bool IsSyntheticPostAllowed(HistoricalImportBatch batch)
    {
        if (!environment.IsDevelopment() || !configuration.GetValue<bool>("HistoricalWorkbookQa:Enabled") ||
            batch.SourceSha256 != configuration["HistoricalWorkbookQa:ApprovedSyntheticSha256"] ||
            batch.ProjectId != configuration.GetValue<int>("HistoricalWorkbookQa:ProjectId"))
            return false;
        var configuredConnection = db.Database.GetConnectionString();
        if (string.IsNullOrWhiteSpace(configuredConnection)) return false;
        var connection = new NpgsqlConnectionStringBuilder(configuredConnection);
        var expectedHost = configuration["HistoricalWorkbookQa:DatabaseHost"] ?? "127.0.0.1";
        var expectedPort = configuration.GetValue("HistoricalWorkbookQa:DatabasePort", 55436);
        var expectedDatabase = configuration["HistoricalWorkbookQa:DatabaseName"] ?? "emg_rc03";
        return string.Equals(connection.Host, expectedHost, StringComparison.OrdinalIgnoreCase) &&
            connection.Port == expectedPort && string.Equals(connection.Database, expectedDatabase, StringComparison.Ordinal);
    }

    private Task<decimal?> RequireGrant(int projectId, int actor, bool admin, CancellationToken cancellationToken) =>
        WorkbookImportService.RequireApprovedHistoryGrantAsync(db, projectId, actor, admin, cancellationToken);

    private static bool SameParty(string? first, string? second) => !string.IsNullOrWhiteSpace(first) &&
        string.Equals(first.Trim(), second?.Trim(), StringComparison.OrdinalIgnoreCase);

    private async Task<int?> MatchSupplier(string party, CancellationToken cancellationToken)
    {
        var name = party.Trim().ToUpperInvariant();
        var candidates = await db.Suppliers.Where(item => item.IsActive && item.Name.ToUpper() == name)
            .Select(item => item.Id).Take(2).ToListAsync(cancellationToken);
        return candidates.Count == 1 ? candidates[0] : null;
    }

    private async Task<Project> FindProject(int id, CancellationToken cancellationToken) =>
        await db.Projects.SingleOrDefaultAsync(item => item.Id == id, cancellationToken)
        ?? throw new KeyNotFoundException("المشروع غير موجود.");

    private IQueryable<HistoricalImportBatch> Batches() =>
        db.HistoricalImportBatches.Include(batch => batch.Records).ThenInclude(record => record.PostedEntry)
            .Include(batch => batch.AuditEvents).AsSplitQuery();

    private static bool IsReviewed(HistoricalImportBatch batch) => batch.AuditEvents.Any(item => item.Action == StageEvent);

    private async Task<HistoricalImportBatch> Load(int id, CancellationToken cancellationToken) =>
        await Batches().SingleOrDefaultAsync(item => item.Id == id && item.AuditEvents.Any(action => action.Action == StageEvent), cancellationToken)
        ?? throw new KeyNotFoundException("دفعة المراجعة غير موجودة.");

    private static StagedEvidence ReadEvidence(HistoricalImportRecord record) =>
        JsonSerializer.Deserialize<StagedEvidence>(record.SourceCellsJson) ?? throw new InvalidOperationException("بيانات المصدر غير مكتملة.");

    private sealed record StagedEvidence(IReadOnlyList<string> Cells, UniversalOperationalEvidence? Fields, decimal? Amount, int? SupplierId,
        string? MappingEvidence, HistoricalSettlementLink? SettlementLink = null);
    private sealed record OperationalProvenance(int TargetId, string Target, string SourceCoordinate,
        IReadOnlyList<string> SourceCells, string MappingVersion);
}
