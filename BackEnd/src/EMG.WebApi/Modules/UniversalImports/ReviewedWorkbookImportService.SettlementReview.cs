using System.Text.Json;
using EMG.Api.Modules.Imports.Services;
using EMG.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace EMG.Api.Modules.UniversalImports;

public sealed partial class ReviewedWorkbookImportService
{
    private static readonly HashSet<string> SettlementEventTypes = [
        nameof(UniversalRecordType.Payment), nameof(UniversalRecordType.Funding),
        nameof(UniversalRecordType.Allocation), nameof(UniversalRecordType.OpeningBalance),
        nameof(UniversalRecordType.Adjustment), nameof(UniversalRecordType.Expense),
        nameof(UniversalRecordType.Liability), nameof(UniversalRecordType.Transaction),
        nameof(UniversalRecordType.Unresolved), nameof(UniversalRecordType.Summary),
        nameof(UniversalRecordType.DerivedValue)];

    public async Task<IReadOnlyList<HistoricalSettlementReview>> ListSettlementReviewsAsync(
        int projectId, int actor, bool admin, CancellationToken cancellationToken)
    {
        await RequireGrant(projectId, actor, admin, cancellationToken);
        var batches = await Batches()
            .Where(batch => batch.ProjectId == projectId &&
                batch.AuditEvents.Any(item => item.Action == StageEvent))
            .OrderByDescending(batch => batch.Id)
            .ToListAsync(cancellationToken);
        return batches.Select(ToSettlementReview).ToArray();
    }

    public async Task<HistoricalSettlementReview> ReviewSettlementAsync(
        int batchId, HistoricalSettlementDecision decision, int actor, bool admin,
        CancellationToken cancellationToken)
    {
        if (decision is null) throw new InvalidOperationException("بيانات قرار المراجعة مطلوبة.");
        var batch = await Load(batchId, cancellationToken);
        await RequireGrant(batch.ProjectId, actor, admin, cancellationToken);
        if (!CanRevise(batch, actor) || batch.Version != decision.Version)
            throw new InvalidOperationException("تغيّرت الدفعة أو انتهت مرحلة مراجعة التسوية. أعد فتحها.");

        var record = batch.Records.SingleOrDefault(item => item.Id == decision.RecordId)
            ?? throw new KeyNotFoundException("سجل التسوية غير موجود في هذه الدفعة.");
        if (!SettlementEventTypes.Contains(record.BusinessEventType) ||
            record.IsSummary || record.BusinessEventType is nameof(UniversalRecordType.Summary) or nameof(UniversalRecordType.DerivedValue) or nameof(UniversalRecordType.Unresolved))
            throw new InvalidOperationException("نوع السجل لا يمثل حركة تسوية تاريخية.");

        var normalized = decision.Decision.Trim().ToLowerInvariant();
        if (normalized is not ("confirm" or "reject"))
            throw new InvalidOperationException("قرار التسوية يجب أن يكون confirm أو reject.");
        var evidence = decision.Evidence?.Trim();
        if (normalized == "confirm" && evidence is not { Length: >= 8 and <= 500 })
            throw new InvalidOperationException("تأكيد العلاقة يحتاج إلى مرجع دليل واضح.");

        HistoricalSettlementLink? settlementLink = null;
        if (normalized == "confirm")
            settlementLink = await ValidateSupplierObligationLink(batch, record, decision, evidence!, cancellationToken);

        var previous = record.SemanticValidationState;
        record.EligibleForPosting = false;
        record.SemanticValidationState = normalized == "confirm" ? "SETTLEMENT_CONFIRMED" : "REVIEW_REQUIRED";
        record.LifecycleStatus = HistoricalImportStatuses.ReviewRequired;
        record.ExclusionReason = normalized == "confirm"
            ? "تم تأكيد العلاقة للمراجعة فقط؛ الترحيل المالي التاريخي ما زال مغلقاً."
            : evidence is { Length: > 0 } ? evidence : "رفض المراجع علاقة التسوية وتحتاج الحركة إلى دليل إضافي.";
        if (settlementLink is not null)
        {
            var stored = ReadStoredEvidence(record);
            record.SourceCellsJson = JsonSerializer.Serialize(stored with { SettlementLink = settlementLink });
        }
        batch.Version++;
        batch.AuditEvents.Add(new HistoricalImportAuditEvent
        {
            ActorUserId = actor,
            RecordId = record.Id,
            Action = "HISTORICAL_SETTLEMENT_REVIEWED",
            DetailsJson = JsonSerializer.Serialize(new { Decision = normalized, Previous = previous, Evidence = evidence })
        });
        await db.SaveChangesAsync(cancellationToken);
        return ToSettlementReview(batch);
    }

    public async Task<HistoricalSettlementReview> RecognizeSettlementAsync(
        int batchId, int recordId, int actor, bool admin, CancellationToken cancellationToken)
    {
        var batch = await Load(batchId, cancellationToken);
        await RequireGrant(batch.ProjectId, actor, admin, cancellationToken);
        if (!IsSyntheticPostAllowed(batch)) throw new HistoricalWorkbookPostingDisabledException();

        var record = batch.Records.SingleOrDefault(item => item.Id == recordId)
            ?? throw new KeyNotFoundException("سجل التسوية غير موجود في هذه الدفعة.");
        var stored = ReadStoredEvidence(record);
        var link = stored.SettlementLink;
        if (link is null || link.ObligationType != "SupplierSupply" || link.ObligationId <= 0)
            throw new InvalidOperationException("لا يمكن إنشاء دفعة تشغيلية قبل تأكيد التزام مورد بدليل المصدر.");
        if (record.BusinessEventType != nameof(UniversalRecordType.Payment) || record.Currency != "EGP" || record.OriginalAmount is not > 0)
            throw new InvalidOperationException("الاعتراف التشغيلي المدعوم حالياً يخص دفعة EGP مثبتة فقط.");

        var supply = await db.SupplierSupplies.Include(item => item.Supplier)
            .SingleOrDefaultAsync(item => item.Id == link.ObligationId && item.ProjectId == batch.ProjectId, cancellationToken)
            ?? throw new InvalidOperationException("الالتزام غير موجود في نفس المشروع.");
        if (!SameParty(link.Party, supply.Supplier.Name) || decimal.Round(link.ObligationAmount, 2) != decimal.Round(supply.TotalAmount, 2))
            throw new InvalidOperationException("تغيّر دليل الالتزام أو الطرف منذ المراجعة.");

        var marker = HistoricalPaymentMarker(record.Id);
        var existing = await db.SupplierPayments.AsNoTracking()
            .SingleOrDefaultAsync(item => item.Notes != null && item.Notes.Contains(marker), cancellationToken);
        if (existing is not null)
        {
            if (existing.ProjectId != batch.ProjectId || existing.SupplyId != supply.Id || existing.Amount != record.OriginalAmount)
                throw new InvalidOperationException("يوجد ربط تشغيلي غير متوافق مع سجل المصدر.");
            var existingLink = link with { OperationalPaymentId = existing.Id, OperationalPaymentStatus = existing.Status };
            record.SourceCellsJson = JsonSerializer.Serialize(stored with { SettlementLink = existingLink });
            await db.SaveChangesAsync(cancellationToken);
            return ToSettlementReview(batch);
        }

        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        await ImportProjectLock.AcquireAsync(db, batch.ProjectId, cancellationToken);
        // Recheck inside the project lock so two recognition requests cannot
        // create two operational drafts for the same reviewed source record.
        if (await db.SupplierPayments.AnyAsync(item => item.Notes != null && item.Notes.Contains(marker), cancellationToken))
            throw new InvalidOperationException("تم إنشاء مسودة الدفع التشغيلي لهذا المصدر بالفعل.");
        await ValidateSupplierObligationLink(batch, record, new HistoricalSettlementDecision(
            batch.Version, record.Id, "confirm", link.Evidence, link.ObligationType, link.ObligationId,
            link.ObligationAmount, link.Party), link.Evidence, cancellationToken);

        var fields = stored.Fields;
        var payment = new SupplierPayment
        {
            SupplierId = supply.SupplierId,
            ProjectId = batch.ProjectId,
            SupplyId = supply.Id,
            Amount = record.OriginalAmount.Value,
            PaymentDate = fields?.Date.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc) ?? batch.CreatedAtUtc.Date,
            PaymentMethod = "غير محدد",
            Reference = HistoricalPaymentReference(batch, record),
            Notes = $"{marker} | استيراد تاريخي: {record.SheetName}!{record.SourceCoordinate} | {batch.SourceFileName}",
            Status = "مسودة",
            CreatedByUserId = actor
        };
        db.SupplierPayments.Add(payment);
        await db.SaveChangesAsync(cancellationToken);

        var operationalLink = link with { OperationalPaymentId = payment.Id, OperationalPaymentStatus = payment.Status };
        record.SourceCellsJson = JsonSerializer.Serialize(stored with { SettlementLink = operationalLink });
        record.SemanticValidationState = "OPERATIONAL_PAYMENT_DRAFT";
        record.LifecycleStatus = HistoricalImportStatuses.ReviewRequired;
        record.EligibleForPosting = false;
        record.ExclusionReason = "تم إنشاء مسودة دفع تشغيلية. لا تُحتسب مالياً قبل اعتمادها وترحيلها عبر دورة الدفع العادية.";
        batch.Version++;
        batch.AuditEvents.Add(new HistoricalImportAuditEvent
        {
            ActorUserId = actor,
            RecordId = record.Id,
            Action = "HISTORICAL_SETTLEMENT_OPERATIONAL_DRAFT_CREATED",
            DetailsJson = JsonSerializer.Serialize(new { PaymentId = payment.Id, SupplyId = supply.Id, Amount = payment.Amount, Currency = record.Currency })
        });
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return ToSettlementReview(batch);
    }

    private async Task<HistoricalSettlementLink> ValidateSupplierObligationLink(
        HistoricalImportBatch batch, HistoricalImportRecord record, HistoricalSettlementDecision decision,
        string evidence, CancellationToken cancellationToken)
    {
        if (record.BusinessEventType != nameof(UniversalRecordType.Payment) || record.Currency != "EGP" || record.OriginalAmount is not > 0)
            throw new InvalidOperationException("الربط المدعوم حالياً يخص دفعة EGP مثبتة فقط.");
        if (!string.Equals(decision.ObligationType?.Trim(), "SupplierSupply", StringComparison.Ordinal) || decision.ObligationId is not > 0)
            throw new InvalidOperationException("حدد توريدة المورد كالتزام فعلي قبل تأكيد دفعة تاريخية.");
        if (decision.ObligationAmount is not > 0)
            throw new InvalidOperationException("قيمة الالتزام مطلوبة للتحقق من الربط.");
        var supply = await db.SupplierSupplies.AsNoTracking().Include(item => item.Supplier)
            .SingleOrDefaultAsync(item => item.Id == decision.ObligationId && item.ProjectId == batch.ProjectId, cancellationToken)
            ?? throw new InvalidOperationException("الالتزام غير موجود في نفس المشروع.");
        if (decimal.Round(decision.ObligationAmount.Value, 2) != decimal.Round(supply.TotalAmount, 2))
            throw new InvalidOperationException("قيمة الالتزام لا تطابق التوريدة المسجلة.");
        var party = decision.Party?.Trim();
        if (string.IsNullOrWhiteSpace(party) || !SameParty(party, supply.Supplier.Name))
            throw new InvalidOperationException("اسم الطرف يجب أن يطابق المورد المرتبط بالالتزام.");
        var fields = ReadStoredEvidence(record).Fields;
        if (fields?.Party is { Length: > 0 } sourceParty && !SameParty(sourceParty, party))
            throw new InvalidOperationException("طرف المصدر لا يطابق طرف الالتزام المحدد.");
        var operationalPaid = await db.SupplierPayments.AsNoTracking()
            .Where(item => item.SupplyId == supply.Id && (item.Status == Approved || item.Status == "مرحّل"))
            .SumAsync(item => (decimal?)item.Amount, cancellationToken) ?? 0m;
        if (record.OriginalAmount > supply.TotalAmount - operationalPaid)
            throw new InvalidOperationException("الدفعة التاريخية تتجاوز المتبقي على الالتزام.");

        var otherRecords = await db.HistoricalImportRecords.AsNoTracking()
            .Where(item => item.Batch.ProjectId == batch.ProjectId && item.Id != record.Id)
            .Select(item => new { item.Id, item.SheetName, item.SourceCoordinate, item.OriginalAmount, item.SourceCellsJson, item.Batch.SourceSha256 })
            .ToListAsync(cancellationToken);
        var otherLinks = otherRecords
            .Select(item => new { item, Link = ReadStoredEvidence(item.SourceCellsJson)?.SettlementLink })
            .Where(item => item.Link is { ObligationType: "SupplierSupply", ObligationId: var id } && id == supply.Id)
            .ToArray();
        if (otherLinks.Any(item => item.Link!.OperationalPaymentId.HasValue &&
            item.item.SheetName == record.SheetName && item.item.SourceCoordinate == record.SourceCoordinate &&
            item.item.OriginalAmount == record.OriginalAmount))
            throw new InvalidOperationException("يوجد اعتراف تشغيلي سابق لنفس مصدر الحركة.");
        var otherConfirmedAmount = otherLinks.Sum(item => item.item.OriginalAmount ?? 0m);
        if (record.OriginalAmount > supply.TotalAmount - operationalPaid - otherConfirmedAmount)
            throw new InvalidOperationException("الدفعات التاريخية المؤكدة تتجاوز المتبقي على الالتزام.");
        return new HistoricalSettlementLink("SupplierSupply", supply.Id, supply.TotalAmount, supply.Supplier.Name, evidence);
    }

    private HistoricalSettlementReview ToSettlementReview(HistoricalImportBatch batch) =>
        new(batch.Id, batch.ProjectId, batch.SourceFileName, batch.SourceSha256, batch.Status, batch.Version,
            batch.Records.OrderBy(item => item.Id).Select(ToSettlementRecord).ToArray());

    private static HistoricalSettlementRecord ToSettlementRecord(HistoricalImportRecord record)
    {
        using var json = JsonDocument.Parse(string.IsNullOrWhiteSpace(record.SourceCellsJson) ? "{}" : record.SourceCellsJson);
        var root = json.RootElement;
        var cells = root.TryGetProperty("Cells", out var cellProperty) && cellProperty.ValueKind == JsonValueKind.Array
            ? cellProperty.EnumerateArray().Where(item => item.ValueKind == JsonValueKind.String).Select(item => item.GetString() ?? string.Empty).ToArray()
            : Array.Empty<string>();
        var fields = root.TryGetProperty("Fields", out var fieldProperty) && fieldProperty.ValueKind == JsonValueKind.Object
            ? fieldProperty : default;
        string? Read(string name) => fields.ValueKind == JsonValueKind.Object && fields.TryGetProperty(name, out var value) && value.ValueKind != JsonValueKind.Null
            ? value.ToString() : null;
        var stream = record.MappingFamily == "F06_ADVANCE_PARALLEL"
            ? record.BusinessEventType == nameof(UniversalRecordType.Allocation) ? "allocation" : "payment_or_funding"
            : record.MappingFamily;
        var referenceOnly = record.IsSummary || record.BusinessEventType is nameof(UniversalRecordType.Summary) or nameof(UniversalRecordType.DerivedValue);
        var link = root.TryGetProperty("SettlementLink", out var linkProperty) && linkProperty.ValueKind == JsonValueKind.Object
            ? linkProperty : default;
        string? LinkValue(string name) => link.ValueKind == JsonValueKind.Object && link.TryGetProperty(name, out var value) && value.ValueKind != JsonValueKind.Null ? value.ToString() : null;
        int? LinkId() => int.TryParse(LinkValue("ObligationId"), out var value) ? value : null;
        decimal? LinkAmount() => decimal.TryParse(LinkValue("ObligationAmount"), out var value) ? value : null;
        int? OperationalPaymentId() => int.TryParse(LinkValue("OperationalPaymentId"), out var value) ? value : null;
        return new HistoricalSettlementRecord(record.Id, record.SheetName, record.SourceCoordinate, cells,
            record.MappingFamily, stream, record.BusinessEventType, record.OriginalAmount, record.Currency,
            Read("Target"), Read("Party"), Read("Reference"), record.SemanticValidationState,
            record.LifecycleStatus, referenceOnly, record.IsDuplicateCandidate, record.ExclusionReason,
            LinkValue("ObligationType"), LinkId(), LinkAmount(), OperationalPaymentId(), LinkValue("OperationalPaymentStatus"));
    }

    private static string HistoricalPaymentMarker(int recordId) => $"EMG-HISTORICAL-RECORD:{recordId}";

    private static string HistoricalPaymentReference(HistoricalImportBatch batch, HistoricalImportRecord record) =>
        $"HIST-{batch.SourceSha256[..Math.Min(12, batch.SourceSha256.Length)]}-{record.Id}";

    private static StagedEvidence ReadStoredEvidence(HistoricalImportRecord record) =>
        JsonSerializer.Deserialize<StagedEvidence>(record.SourceCellsJson) ?? throw new InvalidOperationException("بيانات المصدر غير مكتملة.");

    private static StagedEvidence? ReadStoredEvidence(string source) =>
        JsonSerializer.Deserialize<StagedEvidence>(source);
}
