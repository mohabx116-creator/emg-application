using System.Text.Json;
using EMG.Api.Modules.Imports.Services;
using EMG.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace EMG.Api.Modules.UniversalImports;

public sealed partial class ReviewedWorkbookImportService
{
    public async Task<ReviewedWorkbookBatch> PostAsync(int id, int actor, bool admin, CancellationToken cancellationToken)
    {
        var projectId = await db.HistoricalImportBatches.Where(batch => batch.Id == id &&
                batch.AuditEvents.Any(item => item.Action == StageEvent))
            .Select(batch => (int?)batch.ProjectId).SingleOrDefaultAsync(cancellationToken)
            ?? throw new KeyNotFoundException("دفعة المراجعة غير موجودة.");
        var cap = await RequireGrant(projectId, actor, admin, cancellationToken);
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        await ImportProjectLock.AcquireAsync(db, projectId, cancellationToken);
        var batch = await Load(id, cancellationToken);
        if (!IsSyntheticPostAllowed(batch)) throw new HistoricalWorkbookPostingDisabledException();
        if (batch.Status == HistoricalImportStatuses.Posted) return BuildReport(batch, actor, cap);
        if (batch.Status != HistoricalImportStatuses.ApprovedForPosting || batch.ApprovedByUserId is null)
            throw new InvalidOperationException("يلزم اعتماد الدفعة قبل الترحيل.");

        // The existing approved-archive rule gives each workbook a clean
        // opening ledger. Subsequent day-to-day operations use normal ERP
        // workflows. Revised/overlapping archives need explicit reconciliation.
        await WorkbookImportService.RequireEmptyProjectLedgerAsync(db, projectId, cancellationToken);
        if (await db.HistoricalPostedEntries.AnyAsync(item => item.ProjectId == projectId, cancellationToken))
            throw new InvalidOperationException("توجد حركات تاريخية مرحّلة بالفعل لهذا المشروع.");
        var project = await FindProject(projectId, cancellationToken);
        var rows = batch.Records.Where(item => item.LifecycleStatus == HistoricalImportStatuses.ApprovedForPosting).ToList();
        var pending = new List<(HistoricalImportRecord Record, StagedEvidence Evidence, object Entity)>();
        if (rows.Count == 0) throw new InvalidOperationException("لا توجد سجلات معتمدة لهذه الدفعة.");
        foreach (var record in rows)
        {
            var evidence = ReadEvidence(record);
            if (record.Currency != "EGP" || record.IsSummary || record.IsDuplicateCandidate || !record.EligibleForPosting ||
                !IsSupportedAmount(record, evidence) || cap.HasValue && Math.Abs(record.OriginalAmount!.Value) > cap)
                throw new InvalidOperationException($"تعذر التحقق من السجل في {record.SourceCoordinate}.");
            if (evidence.Fields is null || evidence.Amount != record.OriginalAmount)
                throw new InvalidOperationException($"تغيّر دليل القيمة في {record.SourceCoordinate}.");
            if (evidence.Fields.Target == "SupplierSupply" &&
                await MatchSupplier(evidence.Fields.Party!, cancellationToken) != evidence.SupplierId)
                throw new InvalidOperationException($"تغيّرت مطابقة المورد في {record.SourceCoordinate}.");
            if (evidence.Fields.Target == "AdvancePayment" && !SameParty(project.PayerName, evidence.Fields.Party))
                throw new InvalidOperationException($"تغيّر الدافع المسجل في {record.SourceCoordinate}.");
            var entity = BuildOperationalRow(project, record, evidence, batch.Id);
            db.Add(entity);
            pending.Add((record, evidence, entity));
        }
        await db.SaveChangesAsync(cancellationToken);
        foreach (var (record, evidence, entity) in pending)
        {
            var targetId = (int)db.Entry(entity).Property("Id").CurrentValue!;
            var target = evidence.Fields!.Target;
            record.LifecycleStatus = HistoricalImportStatuses.Posted;
            // This is the provenance link. Operational reporting reads the
            // actual transaction once. The legacy standalone historical
            // report excludes these linked entries.
            record.PostedEntry = new HistoricalPostedEntry
            {
                ProjectId = projectId, BatchId = batch.Id, HistoricalRecordId = record.Id,
                DeterministicIdentity = record.DeterministicIdentity, LedgerStream = OperationalStreamPrefix + target,
                BusinessEventType = record.BusinessEventType, Amount = record.OriginalAmount!.Value, Currency = record.Currency,
                SourceSha256 = batch.SourceSha256, PostedByUserId = actor,
                ProvenanceJson = JsonSerializer.Serialize(new OperationalProvenance(targetId, target, record.SourceCoordinate,
                    evidence.Cells, record.MappingVersion))
            };
        }
        batch.Status = HistoricalImportStatuses.Posted;
        batch.Version++;
        batch.PostedAtUtc = DateTime.UtcNow;
        batch.AuditEvents.Add(new HistoricalImportAuditEvent
        {
            ActorUserId = actor, Action = "WORKBOOK_SNAPSHOT_POSTED",
            DetailsJson = JsonSerializer.Serialize(new { Posted = pending.Select(item => item.Record.Id) })
        });
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return BuildReport(batch, actor, cap);
    }

    private static object BuildOperationalRow(Project project, HistoricalImportRecord record, StagedEvidence evidence, int batchId)
    {
        var fields = evidence.Fields!;
        var amount = record.OriginalAmount!.Value;
        var date = fields.Date.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
        var source = $"استيراد: {record.SourceCoordinate} | #{batchId}";
        var shortSource = source.Length > 150 ? source[..150] : source;
        return fields.Target switch
        {
            "SiteMovement" => new SiteMovement
            {
                ProjectId = project.Id, MovementType = "أعمال الموقع", ExpenseCategory = "أعمال الموقع",
                Description = fields.Description, Unit = fields.Unit, Quantity = fields.Quantity, UnitPrice = fields.UnitPrice,
                TotalAmount = amount, MovementDate = date, Status = Approved, Reference = shortSource, Details = source
            },
            "ExcavationRecord" => new ExcavationRecord
            {
                ProjectId = project.Id, Description = fields.Description, CalculatedQuantity = fields.Quantity!.Value,
                Unit = fields.Unit!, UnitPrice = fields.UnitPrice!.Value, TotalAmount = amount, ExcavationDate = date,
                Status = Approved, Notes = source
            },
            "ConcreteRecord" => new ConcreteRecord
            {
                ProjectId = project.Id, Description = fields.Description, AccountCategory = "باقي المصنعيات",
                ConcreteType = "خرسانة", ElementType = fields.Description.Length <= 150 ? fields.Description : fields.Description[..150],
                Quantity = fields.Quantity!.Value, Unit = fields.Unit!, UnitPrice = fields.UnitPrice!.Value,
                TotalAmount = amount, PourDate = date, Status = Approved, Notes = source
            },
            "SteelRecord" => new SteelRecord
            {
                ProjectId = project.Id, Description = fields.Description, Quantity = fields.Quantity!.Value,
                Unit = fields.Unit!, UnitPrice = fields.UnitPrice!.Value, TotalAmount = amount, OperationDate = date,
                Status = Approved, Notes = source
            },
            "FinishingRecord" => new FinishingRecord
            {
                ProjectId = project.Id, Description = fields.Description, WorkType = record.SheetName,
                Quantity = fields.Quantity!.Value, Unit = fields.Unit!, UnitPrice = fields.UnitPrice!.Value,
                TotalAmount = amount, WorkDate = date, Status = Approved, Notes = source
            },
            "SupplierSupply" => new SupplierSupply
            {
                ProjectId = project.Id, SupplierId = evidence.SupplierId!.Value, ItemName = fields.Description,
                InvoiceNumber = fields.Reference, Quantity = fields.Quantity!.Value, Unit = fields.Unit!,
                UnitPrice = fields.UnitPrice!.Value, TotalAmount = amount, SupplyDate = date, Status = Approved, Notes = source
            },
            "AdvancePayment" => new AdvancePayment
            {
                ProjectId = project.Id, ClientName = project.ClientName ?? "", PayerName = fields.Party!,
                PaymentDate = date, Amount = amount, PaymentMethod = "غير محدد", Reference = fields.Reference, Status = Approved, Notes = source
            },
            _ => throw new InvalidOperationException($"لا توجد وجهة تشغيلية معتمدة للسجل {record.SourceCoordinate}.")
        };
    }
}
