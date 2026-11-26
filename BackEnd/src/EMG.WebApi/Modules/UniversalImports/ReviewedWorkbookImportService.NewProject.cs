using System.Data;
using System.Text.Json;
using EMG.Api.Modules.Imports.Services;
using EMG.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace EMG.Api.Modules.UniversalImports;

public sealed record UniversalNewProjectRequest(string SnapshotId, string Name, string? Code, string? ClientName,
    string? OwnerName, string? PayerName, string? Location, string? EngineerName,
    Dictionary<string, string>? MappingEvidence, bool ConfirmExcluded, string? HistoricalBaselineJson = null);

public sealed record UniversalImportDisposition(string RecordId, string SourceCoordinate, string? Target,
    decimal? Amount, string Currency, bool CanImport, string? Reason);

public sealed record UniversalNewProjectValidation(string Code, IReadOnlyList<UniversalImportDisposition> Records,
    bool PostingEnabled, bool RequiresExclusionConfirmation);

public sealed record UniversalNewProjectResult(int ProjectId, string Name, string Code, int BatchId,
    IReadOnlyDictionary<string, int> Counts, IReadOnlyDictionary<string, decimal> Totals, int RetainedRecords);

public sealed partial class ReviewedWorkbookImportService
{
    public async Task<UniversalNewProjectValidation> RevalidateNewProjectAsync(UniversalNewProjectRequest request,
        int actor, CancellationToken ct)
    {
        var preview = GetNewProjectPreview(request, actor);
        var plan = await PlanNewProjectAsync(request, preview, ct);
        return new(plan.Project.Code, plan.Rows.Select(row => row.Disposition).ToArray(), IsNewProjectPostingAllowed(preview),
            plan.Rows.Any(row => !row.Disposition.CanImport && !row.Record.IsSummary));
    }

    public async Task<UniversalNewProjectResult> CommitNewProjectAsync(UniversalNewProjectRequest request, int actor, CancellationToken ct)
    {
        var preview = GetNewProjectPreview(request, actor);
        if (!IsNewProjectPostingAllowed(preview)) throw new HistoricalWorkbookPostingDisabledException();
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        var (project, rows) = await PlanNewProjectAsync(request, preview, ct);
        var accepted = rows.Where(row => row.Disposition.CanImport).ToArray();
        if (accepted.Length == 0) throw new InvalidOperationException("لا توجد حركات مؤهلة للاستيراد.");
        if (!request.ConfirmExcluded && rows.Any(row => !row.Disposition.CanImport && !row.Record.IsSummary))
            throw new InvalidOperationException("أكد حفظ السجلات غير المؤهلة للمراجعة قبل الاستيراد.");

        var batch = new HistoricalImportBatch
        {
            Project = project, SourceFileName = preview.FileName, SourceSha256 = preview.SourceSha256,
            MappingVersion = preview.EngineVersion, CreatedByUserId = actor, Status = HistoricalImportStatuses.Posted,
            PostedAtUtc = DateTime.UtcNow
        };
        db.HistoricalImportBatches.Add(batch);
        await db.SaveChangesAsync(ct);
        var sections = preview.Sections.ToDictionary(section => section.SectionId);
        var entities = new List<(HistoricalImportRecord Record, StagedEvidence Evidence, object Entity)>();
        foreach (var row in rows)
        {
            var source = row.Record;
            var section = sections[source.SectionId];
            var evidence = new StagedEvidence(source.SourceCells, source.OperationalEvidence, source.Amount, row.SupplierId, row.MappingNote);
            var record = new HistoricalImportRecord
            {
                DeterministicIdentity = source.RecordId, SheetName = source.SheetName, SectionId = source.SectionId,
                SourceCoordinate = source.SourceCoordinate, MappingFamily = section.MappingFamily, MappingVersion = section.MappingVersion,
                SourceCellsJson = JsonSerializer.Serialize(evidence), BusinessEventType = source.RecordType.ToString(),
                OriginalAmount = FitsAmount(source.Amount) ? source.Amount : null, Currency = source.Currency,
                IsSummary = source.IsSummary, IsDuplicateCandidate = row.Duplicate, EligibleForPosting = row.Disposition.CanImport,
                LifecycleStatus = row.Disposition.CanImport ? HistoricalImportStatuses.Posted :
                    source.IsSummary ? HistoricalImportStatuses.Rejected : HistoricalImportStatuses.ReviewRequired,
                SemanticValidationState = row.Disposition.CanImport ? "SEMANTICALLY_COMPLETE" : "REVIEW_REQUIRED",
                ExclusionReason = row.Disposition.Reason, ValueFingerprint = source.ValueFingerprint
            };
            batch.Records.Add(record);
            if (!row.Disposition.CanImport) continue;
            var entity = BuildOperationalRow(project, record, evidence, batch.Id);
            db.Add(entity);
            entities.Add((record, evidence, entity));
        }
        await db.SaveChangesAsync(ct);
        foreach (var (record, evidence, entity) in entities)
        {
            var target = evidence.Fields!.Target;
            var targetId = (int)db.Entry(entity).Property("Id").CurrentValue!;
            record.PostedEntry = new HistoricalPostedEntry
            {
                ProjectId = project.Id, BatchId = batch.Id, HistoricalRecordId = record.Id,
                DeterministicIdentity = record.DeterministicIdentity, LedgerStream = OperationalStreamPrefix + target,
                BusinessEventType = record.BusinessEventType, Amount = record.OriginalAmount!.Value, Currency = record.Currency,
                SourceSha256 = preview.SourceSha256, PostedByUserId = actor,
                ProvenanceJson = JsonSerializer.Serialize(new OperationalProvenance(targetId, target, record.SourceCoordinate,
                    evidence.Cells, record.MappingVersion))
            };
        }
        batch.AuditEvents.Add(new HistoricalImportAuditEvent
        {
            ActorUserId = actor, Action = StageEvent,
            DetailsJson = JsonSerializer.Serialize(preview.Reconciliations)
        });
        batch.AuditEvents.Add(new HistoricalImportAuditEvent
        {
            ActorUserId = actor, Action = HistoricalImportStatuses.UniversalProjectCommitted,
            DetailsJson = JsonSerializer.Serialize(new { preview.Issues, preview.Exceptions, request.ConfirmExcluded })
        });
        if (!string.IsNullOrWhiteSpace(request.HistoricalBaselineJson))
        {
            batch.AuditEvents.Add(new HistoricalImportAuditEvent
            {
                ActorUserId = actor, Action = "HISTORICAL_BASELINE_CAPTURED",
                DetailsJson = request.HistoricalBaselineJson
            });
        }
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        var groups = accepted.GroupBy(row => row.Disposition.Target!).ToArray();
        return new(project.Id, project.Name, project.Code, batch.Id, groups.ToDictionary(g => g.Key, g => g.Count()),
            groups.ToDictionary(g => g.Key, g => g.Sum(row => row.Record.Amount!.Value)), rows.Count - accepted.Length);
    }

    private UniversalPreviewResponse GetNewProjectPreview(UniversalNewProjectRequest request, int actor) =>
        snapshots.Find(request.SnapshotId, actor) is { ProjectId: 0 } snapshot ? snapshot.Preview :
            throw new KeyNotFoundException("انتهت صلاحية المعاينة أو لا تخص حسابك. أعد رفع الملف.");

    private async Task<(Project Project, List<NewProjectRow> Rows)> PlanNewProjectAsync(
        UniversalNewProjectRequest request, UniversalPreviewResponse preview, CancellationToken ct)
    {
        var code = preview.SourceProjectCode ?? request.Code?.Trim();
        if (string.IsNullOrWhiteSpace(code) || code.Length > 80 || string.IsNullOrWhiteSpace(request.Name) || request.Name.Length > 250)
            throw new InvalidOperationException("اسم المشروع ورقم القطعة مطلوبان.");
        if (preview.SourceProjectCode is { } plot && !string.IsNullOrWhiteSpace(request.Code) &&
            WorkbookImportService.NormalizePlotNumber(request.Code) != plot)
            throw new InvalidOperationException("رقم القطعة المدخل يختلف عن المصدر.");
        if (await db.HistoricalImportBatches.AnyAsync(batch => batch.SourceSha256.ToUpper() == preview.SourceSha256, ct) ||
            (await db.Projects.Select(item => item.Code).ToListAsync(ct)).Any(existing =>
                string.Equals(existing, code, StringComparison.OrdinalIgnoreCase) ||
                preview.SourceProjectCode is not null && WorkbookImportService.NormalizePlotNumber(existing) == code))
            throw new InvalidOperationException("سبق تسجيل هذا المصدر أو رقم المشروع.");
        var metadata = preview.ProjectMetadata;
        var project = new Project
        {
            Name = request.Name.Trim(), Code = code, ClientName = Clean(request.ClientName, metadata?.ClientName),
            OwnerName = Clean(request.OwnerName, metadata?.OwnerName), PayerName = Clean(request.PayerName, metadata?.PayerName),
            Location = Clean(request.Location, metadata?.Location), EngineerName = Clean(request.EngineerName, null), Status = "نشط"
        };
        if (new[] { project.ClientName, project.OwnerName, project.PayerName, project.Location, project.EngineerName }
            .Any(value => value?.Length > 250))
            throw new InvalidOperationException("حقول بيانات المشروع تدعم حتى 250 حرفاً.");
        var corrections = request.MappingEvidence ?? [];
        var manualIds = preview.Records.Where(record => record.OperationalEvidence?.RequiresManualMapping == true)
            .Select(record => record.RecordId).ToHashSet(StringComparer.Ordinal);
        if (corrections.Any(pair => !manualIds.Contains(pair.Key) || pair.Value?.Trim() is not { Length: >= 8 and <= 500 }))
            throw new InvalidOperationException("دليل التصحيح يجب أن يخص صفاً قابلاً للمطابقة ويحتوي على 8 إلى 500 حرف.");
        var duplicates = preview.DuplicateCandidates.SelectMany(item => item.RecordIds).ToHashSet(StringComparer.Ordinal);
        var rows = new List<NewProjectRow>(preview.Records.Count);
        var suppliers = new Dictionary<string, int?>(StringComparer.OrdinalIgnoreCase);
        foreach (var record in preview.Records)
        {
            var fields = record.OperationalEvidence;
            var note = corrections.GetValueOrDefault(record.RecordId)?.Trim();
            var duplicate = duplicates.Contains(record.RecordId);
            int? supplierId = null;
            var reason = record.IsSummary || record.RecordType is UniversalRecordType.DerivedValue or UniversalRecordType.Summary
                ? "قيمة مرجعية للمطابقة."
                : duplicate ? "تكرار محتمل يحتاج دليلاً مستقلاً."
                : record.Currency != "EGP" ? "عملة المصدر محفوظة للمراجعة."
                : fields is null ? MissingSourceEvidence(record.StreamKey)
                : !FitsAmount(record.Amount) || record.Amount == 0 ? "القيمة لا تصلح للتسجيل كحركة مالية."
                : !record.EligibleForPosting && !(fields.RequiresManualMapping && note is not null) ? "تحتاج المطابقة إلى تأكيد مدعوم بالمصدر."
                : record.Amount < 0 && (record.RecordType != UniversalRecordType.Adjustment ||
                    fields.Target is not ("SiteMovement" or "FinishingRecord") ||
                    !IsSignedAdjustment(fields.Description, fields.Quantity, record.Amount)) ? "نوع التسوية السالبة غير مدعوم."
                : null;
            if (reason is null && fields?.Target == "SupplierSupply")
            {
                if (!suppliers.TryGetValue(fields.Party!, out supplierId))
                    suppliers[fields.Party!] = supplierId = await MatchSupplier(fields.Party!, ct);
                if (supplierId is null) reason = "اسم المورد لا يطابق مورداً نشطاً واحداً.";
            }
            if (reason is null && fields?.Target == "AdvancePayment" &&
                (!SameParty(metadata?.PayerName, fields.Party) || !SameParty(project.PayerName, fields.Party)))
                reason = "اسم الممول يحتاج إثباتاً من المصدر.";
            rows.Add(new(record, new(record.RecordId, record.SourceCoordinate, fields?.Target, record.Amount,
                record.Currency, reason is null, reason), duplicate, supplierId, note));
        }
        return (project, rows);
    }

    private static string MissingSourceEvidence(string streamKey) => streamKey switch
    {
        "allocation_ledger" => "توزيع مصدر مستقل. مطلوب مرجع إيصال التمويل أو الالتزام الذي يخصه.",
        "main_payment_ledger" => "دفعة رئيسية في المصدر. مطلوب إثبات الدافع والمستلم وعلاقتها بالإيصالات الأخرى.",
        "unallocated_multicurrency_ledger" => "دفعة غير موزعة. مطلوب إثبات الطرف وعلاقتها بالدفعات الرئيسية.",
        _ => "دليل نوع الحركة أو الطرف أو الربط المحاسبي غير مكتمل."
    };

    private bool IsNewProjectPostingAllowed(UniversalPreviewResponse preview)
    {
        if (configuration.GetValue<bool>("HistoricalMigration:Enabled") && environment.IsDevelopment() &&
            db.Database.IsNpgsql() && IsIsolatedQaDatabase()) return true;
        // Set only after the owner authorizes production imports. The controller
        // independently requires active administrator authorization and both permissions.
        if (environment.IsProduction() && db.Database.IsNpgsql())
            return configuration.GetValue<bool>("UniversalImport:EnableProductionCommit");

        if (!environment.IsDevelopment() || !configuration.GetValue<bool>("UniversalImportQa:Enabled") ||
            !string.Equals(preview.SourceSha256, configuration["UniversalImportQa:ApprovedSha256"], StringComparison.OrdinalIgnoreCase) ||
            !db.Database.IsNpgsql() || string.IsNullOrWhiteSpace(db.Database.GetConnectionString())) return false;
        var connection = new NpgsqlConnectionStringBuilder(db.Database.GetConnectionString());
        return connection.Host is "127.0.0.1" or "localhost" && connection.Port != 5432 &&
            connection.Port == configuration.GetValue<int>("UniversalImportQa:Port") &&
            connection.Database?.StartsWith("emg_universal_qa_", StringComparison.Ordinal) == true &&
            connection.Database == configuration["UniversalImportQa:Database"];
    }

    private bool IsIsolatedQaDatabase()
    {
        if (string.IsNullOrWhiteSpace(db.Database.GetConnectionString())) return false;
        var connection = new NpgsqlConnectionStringBuilder(db.Database.GetConnectionString());
        return connection.Port != 5432 && connection.Database?.StartsWith("emg_historical_qa_", StringComparison.Ordinal) == true;
    }

    private static bool FitsAmount(decimal? amount) => amount is >= -9999999999999999.99m and <= 9999999999999999.99m &&
        decimal.Round(amount.Value, 2) == amount.Value;

    private static string? Clean(string? value, string? fromSource) => string.IsNullOrWhiteSpace(value) ? fromSource : value.Trim();

    private sealed record NewProjectRow(UniversalRecordPreview Record, UniversalImportDisposition Disposition,
        bool Duplicate, int? SupplierId, string? MappingNote);
}
