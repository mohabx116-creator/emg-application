using System.Globalization;
using System.Text;
using EMG.Api.Modules.FinalAccounts.Dtos;
using EMG.Api.Modules.Reports.Dtos;
using EMG.Infrastructure.Db;
using EMG.Api.Modules.FinalAccounts.Services;
using Microsoft.EntityFrameworkCore;
using EMG.Api.Modules.UniversalImports;

namespace EMG.Api.Modules.Reports.Services;

public sealed class ReportsService(EmgDbContext db, IFinalAccountsService finalAccountsService) : IReportsService
{
    public async Task<HistoricalProjectReport?> GetHistoricalProjectReportAsync(int projectId, int? userId, bool isAdmin, CancellationToken cancellationToken)
    {
        var accessible = await finalAccountsService.GetAsync(projectId, userId, isAdmin, cancellationToken);
        if (accessible is null) return null;

        var entries = await db.HistoricalPostedEntries.AsNoTracking()
            .Where(item => item.ProjectId == projectId && item.Batch.Status == HistoricalImportStatuses.Posted &&
                !item.LedgerStream.StartsWith(ReviewedWorkbookImportService.OperationalStreamPrefix))
            .OrderBy(item => item.Id)
            .Select(item => new HistoricalReportEntry(
                item.Id, item.BatchId, item.Batch.SourceFileName, item.SourceSha256,
                item.HistoricalRecord.SheetName, item.HistoricalRecord.SourceCoordinate,
                item.HistoricalRecord.MappingFamily, item.BusinessEventType, item.LedgerStream,
                item.Amount, item.Currency, item.PostedAtUtc, item.ProvenanceJson))
            .ToListAsync(cancellationToken);

        var totals = entries.GroupBy(item => new { item.LedgerStream, item.Currency })
            .OrderBy(group => group.Key.Currency).ThenBy(group => group.Key.LedgerStream)
            .Select(group => new HistoricalReportTotal(group.Key.LedgerStream, group.Key.Currency, group.Sum(item => item.Amount), group.Count()))
            .ToList();
        return new HistoricalProjectReport(projectId, totals, entries);
    }

    public async Task<ProjectReportSummary?> GetProjectReportAsync(int projectId, int? userId, bool isAdmin, CancellationToken cancellationToken)
    {
        var finalAccount = await finalAccountsService.GetAsync(projectId, userId, isAdmin, cancellationToken);
        if (finalAccount is null) return null;
        // Imported rows carry their source row in Reference and are inserted
        // in source order.  Ordering by date changes the workbook's business
        // sequence (and can move equal-date rows around), so use the stable
        // database identity for report/export ordering.
        var movements = await db.SiteMovements.AsNoTracking().Where(item => item.ProjectId == projectId && (item.Status == "معتمد" || item.Status == "مرحّل")).OrderBy(item => item.Id)
            .Select(item => new ProjectMovementRow(item.Id, item.MovementDate, item.MovementType, item.Description,
                item.Supplier == null ? string.Empty : item.Supplier.Name, item.Unit ?? string.Empty, item.Quantity, item.UnitPrice,
                item.TotalAmount, item.ExpenseCategory ?? string.Empty, item.Status, item.Reference ?? string.Empty, item.Currency,
                item.Contractor == null ? string.Empty : item.Contractor.Name)).ToListAsync(cancellationToken);
        var advanceRows = await db.AdvancePayments.AsNoTracking().Where(item => item.ProjectId == projectId && (item.Status == "معتمد" || item.Status == "مرحّل")).OrderBy(item => item.Id)
            .Select(item => new { item.Id, item.PaymentDate, item.ClientName, item.PayerName, item.Amount, item.Status, Reference = item.Reference ?? string.Empty }).ToListAsync(cancellationToken);
        var advanceIds = advanceRows.Select(item => item.Id).ToArray();
        var advanceAllocations = await db.AdvanceAllocations.AsNoTracking().Where(item => advanceIds.Contains(item.AdvancePaymentId))
            .Select(item => new { item.AdvancePaymentId, item.AccountCategory, item.Amount }).ToListAsync(cancellationToken);
        var allocationsByAdvance = advanceAllocations.GroupBy(item => item.AdvancePaymentId).ToDictionary(group => group.Key, group => (IReadOnlyDictionary<string, decimal>)group.GroupBy(item => item.AccountCategory).ToDictionary(items => items.Key, items => items.Sum(item => item.Amount)));
        var advances = advanceRows.Select(item => { var byCategory = allocationsByAdvance.GetValueOrDefault(item.Id) ?? new Dictionary<string, decimal>(); var allocated = byCategory.Values.Sum(); return new ProjectAdvanceRow(item.Id, item.PaymentDate, item.ClientName, item.PayerName, item.Amount, allocated, item.Amount - allocated, item.Status, item.Reference, byCategory); }).ToList();
        var supplies = await db.SupplierSupplies.AsNoTracking().Where(item => item.ProjectId == projectId && (item.Status == "معتمد" || item.Status == "مرحّل")).OrderBy(item => item.Id)
            .Select(item => new ProjectSupplierSupplyRow(item.Id, item.SupplyDate, item.Supplier.Name, item.SupplyType, item.ItemName, item.Unit, item.Quantity, item.UnitPrice, item.TotalAmount, item.InvoiceNumber ?? string.Empty)).ToListAsync(cancellationToken);
        var payments = await db.SupplierPayments.AsNoTracking().Where(item => item.ProjectId == projectId && (item.Status == "معتمد" || item.Status == "مرحّل")).OrderBy(item => item.Id)
            .Select(item => new ProjectSupplierPaymentRow(item.Id, item.PaymentDate, item.Supplier.Name, item.Amount, item.PaymentMethod, item.Reference ?? string.Empty, item.PaymentSource)).ToListAsync(cancellationToken);
        var contractorPayments = await db.ContractorPayments.AsNoTracking().Where(item => item.ProjectId == projectId).OrderBy(item => item.Id)
            .Select(item => new ProjectContractorPaymentRow(item.Id, item.PaymentDate, item.Contractor.Name, item.Amount, item.Currency, item.PaymentMethod, item.PaymentSource, item.Reference ?? string.Empty)).ToListAsync(cancellationToken);
        var excavations = await db.ExcavationRecords.AsNoTracking().Where(item => item.ProjectId == projectId && (item.Status == "معتمد" || item.Status == "مرحّل")).OrderBy(item => item.Id)
            .Select(item => new ProjectWorkRow(item.Id, item.ExcavationDate, item.Supplier == null ? string.Empty : item.Supplier.Name, item.AccountCategory, item.Description, item.Description, item.Unit, item.CalculatedQuantity, item.UnitPrice, item.TotalAmount, item.CalculationMethod, item.SoilType ?? string.Empty, $"{item.Length} × {item.Width} × {item.Depth}", item.Status, item.Notes ?? string.Empty)).ToListAsync(cancellationToken);
        var concretes = await db.ConcreteRecords.AsNoTracking().Where(item => item.ProjectId == projectId && (item.Status == "معتمد" || item.Status == "مرحّل")).OrderBy(item => item.Id)
            .Select(item => new ProjectWorkRow(item.Id, item.PourDate, item.Supplier == null ? string.Empty : item.Supplier.Name, item.AccountCategory, item.ElementType, item.Description, item.Unit, item.Quantity, item.UnitPrice, item.TotalAmount, item.ConcreteType, item.MixGrade ?? string.Empty, item.DeliveryNoteNumber ?? string.Empty, item.Status, item.Notes ?? string.Empty)).ToListAsync(cancellationToken);
        var steels = await db.SteelRecords.AsNoTracking().Where(item => item.ProjectId == projectId && (item.Status == "معتمد" || item.Status == "مرحّل")).OrderBy(item => item.Id)
            .Select(item => new ProjectWorkRow(item.Id, item.OperationDate, item.Supplier == null ? string.Empty : item.Supplier.Name, item.AccountCategory, item.SteelType ?? item.OperationType, item.Description, item.Unit, item.Quantity, item.UnitPrice, item.TotalAmount, item.BarDiameter ?? string.Empty, item.Grade ?? string.Empty, item.DeliveryNoteNumber ?? string.Empty, item.Status, item.Notes ?? string.Empty)).ToListAsync(cancellationToken);
        var finishings = await db.FinishingRecords.AsNoTracking().Where(item => item.ProjectId == projectId && (item.Status == "معتمد" || item.Status == "مرحّل")).OrderBy(item => item.Id)
            .Select(item => new ProjectWorkRow(item.Id, item.WorkDate, item.Supplier == null ? string.Empty : item.Supplier.Name, item.AccountCategory, item.WorkType, item.Description, item.Unit, item.Quantity, item.UnitPrice, item.TotalAmount, item.AreaName ?? string.Empty, string.Empty, string.Empty, item.Status, item.Notes ?? string.Empty)).ToListAsync(cancellationToken);
        return new ProjectReportSummary(finalAccount, movements, advances, supplies, payments, excavations, concretes, steels, finishings) { ContractorPayments = contractorPayments };
    }

    public async Task<string?> ExportProjectMovementsCsvAsync(int projectId, int? userId, bool isAdmin, CancellationToken cancellationToken)
    {
        var report = await GetProjectReportAsync(projectId, userId, isAdmin, cancellationToken);
        if (report is null) return null;
        var builder = new StringBuilder();
        builder.Append('\uFEFF');
        builder.AppendLine("التاريخ,نوع الحركة,الوصف,الطرف,المبلغ,العملة,الحالة,المرجع");
        foreach (var row in report.Movements)
            builder.AppendLine(string.Join(',', Csv(row.Date.ToString("yyyy-MM-dd")), Csv(row.Type), Csv(row.Description), Csv(string.IsNullOrWhiteSpace(row.SupplierName) ? row.CounterpartyName : row.SupplierName), row.Amount.ToString("0.00", CultureInfo.InvariantCulture), Csv(row.Currency), Csv(row.Status), Csv(row.Reference)));
        return builder.ToString();
    }

    public async Task<byte[]?> ExportProjectWorkbookAsync(int projectId, int? userId, bool isAdmin, CancellationToken cancellationToken)
    {
        var report = await GetProjectReportAsync(projectId, userId, isAdmin, cancellationToken);
        if (report is null) return null;
        var audit = await GetOperationalAuditAsync(projectId, userId, isAdmin, cancellationToken);
        return XlsxBuilder.Build(report, audit);
    }

    public async Task<OperationalAuditSummary?> GetOperationalAuditAsync(int projectId, int? userId, bool isAdmin, CancellationToken cancellationToken)
    {
        var accessible = await finalAccountsService.GetAsync(projectId, userId, isAdmin, cancellationToken);
        if (accessible is null) return null;
        var excavationRows = await db.ExcavationRecords.AsNoTracking().Where(item => item.ProjectId == projectId && (item.Status == "معتمد" || item.Status == "مرحّل"))
            .Select(item => new { item.TotalAmount, item.Description, item.Notes }).ToListAsync(cancellationToken);
        var concreteRows = await db.ConcreteRecords.AsNoTracking().Where(item => item.ProjectId == projectId && (item.Status == "معتمد" || item.Status == "مرحّل"))
            .Select(item => new { item.TotalAmount, item.Description, item.Notes }).ToListAsync(cancellationToken);
        var steelRows = await db.SteelRecords.AsNoTracking().Where(item => item.ProjectId == projectId && (item.Status == "معتمد" || item.Status == "مرحّل"))
            .Select(item => new { item.TotalAmount, Description = item.Description + " " + item.OperationType, item.Notes }).ToListAsync(cancellationToken);
        var finishingRows = await db.FinishingRecords.AsNoTracking().Where(item => item.ProjectId == projectId && (item.Status == "معتمد" || item.Status == "مرحّل"))
            .Select(item => new { item.TotalAmount, item.Description, item.Notes }).ToListAsync(cancellationToken);
        var excavation = excavationRows.Sum(item => item.TotalAmount);
        var concrete = concreteRows.Sum(item => item.TotalAmount);
        var steel = steelRows.Sum(item => item.TotalAmount);
        var finishing = finishingRows.Sum(item => item.TotalAmount);
        var supplies = await db.SupplierSupplies.Where(item => item.ProjectId == projectId && (item.Status == "معتمد" || item.Status == "مرحّل")).SumAsync(item => item.TotalAmount, cancellationToken);
        var payments = await db.SupplierPayments.Where(item => item.ProjectId == projectId && (item.Status == "معتمد" || item.Status == "مرحّل")).SumAsync(item => item.Amount, cancellationToken);
        var checks = new List<AuditCheck>
        {
            new("رصيد حساب الموردين", true, supplies - payments,
                supplies - payments < 0 ? "رصيد دائن للمورد (المدفوعات أكبر من التوريدات)" : "الرصيد المتبقي للمورد"),
            new("الحفر والتحويلات", HasOnlyValidAdjustments(excavationRows), excavation, "القيم الأصلية والتسويات المصرح بها"),
            new("الخرسانات والتحويلات", HasOnlyValidAdjustments(concreteRows), concrete, "القيم الأصلية والتسويات المصرح بها"),
            new("الحديد والتحويلات", HasOnlyValidAdjustments(steelRows), steel, "القيم الأصلية والتسويات المصرح بها"),
            new("التشطيبات والتحويلات", HasOnlyValidAdjustments(finishingRows), finishing, "القيم الأصلية والتسويات المصرح بها")
        };
        return new OperationalAuditSummary(projectId, excavation, concrete, steel, finishing, supplies, payments, checks);
    }

    private static string Csv(string? value) => $"\"{(value ?? string.Empty).Replace("\"", "\"\"")}\"";

    private static bool HasOnlyValidAdjustments<T>(IEnumerable<T> rows) where T : class
    {
        foreach (var row in rows)
        {
            var amount = (decimal)row.GetType().GetProperty("TotalAmount")!.GetValue(row)!;
            if (amount >= 0) continue;
            var description = row.GetType().GetProperty("Description")?.GetValue(row) as string;
            var notes = row.GetType().GetProperty("Notes")?.GetValue(row) as string;
            var text = $"{description} {notes}";
            if (!(text.Contains("خصم", StringComparison.OrdinalIgnoreCase)
                || text.Contains("هالك", StringComparison.OrdinalIgnoreCase)
                || text.Contains("تسوية", StringComparison.OrdinalIgnoreCase)
                || text.Contains("تعديل", StringComparison.OrdinalIgnoreCase)
                || text.Contains("مرتجع", StringComparison.OrdinalIgnoreCase)
                || text.Contains("استرداد", StringComparison.OrdinalIgnoreCase))) return false;
        }
        return true;
    }
}
