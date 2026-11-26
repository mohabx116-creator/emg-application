using System.Security.Cryptography;
using System.Text.Json;
using System.Text.RegularExpressions;
using ClosedXML.Excel;
using EMG.Api.Modules.Imports.Dtos;
using EMG.Domain.Entities;
using EMG.Domain.Security;
using EMG.Infrastructure.Db;
using Microsoft.EntityFrameworkCore;

namespace EMG.Api.Modules.Imports.Services;

internal sealed class ApprovedHistoryForbiddenException : Exception;

public sealed partial class WorkbookImportService
{
    private const string ApprovedHistoryPermission = "project-imports.import-approved-history";
    private const decimal MaxHistoricalAmount = 9999999999999999.99m;

    public async Task<ImportedProjectResult> CommitApprovedHistoryAsync(IFormFile file, int? projectId, bool confirmedApproved,
        DateOnly? closedThrough, int userId, bool isCanonicalAdmin, CancellationToken cancellationToken)
    {
        var context = db ?? throw new InvalidOperationException("قاعدة البيانات غير متاحة لتنفيذ الاستيراد.");
        var host = environment ?? throw new InvalidOperationException("بيئة التخزين غير متاحة لتنفيذ الاستيراد.");
        await RequireApprovedHistoryGrantAsync(context, projectId, userId, isCanonicalAdmin, cancellationToken);
        if (!confirmedApproved || closedThrough is not DateOnly cutoff || cutoff >= DateOnly.FromDateTime(DateTime.UtcNow))
            throw new InvalidOperationException("أقر بأن الملف روجع ووُقّع وأُغلق خارج النظام، وحدد تاريخ إقفال سابقاً لليوم.");
        var plan = await BuildNewProjectPlanAsync(file, cancellationToken);
        if (plan is null || plan.Sheets.Any(sheet => !sheet.Recognized || IsBlockingZeroCandidateSheet(sheet) ||
            sheet.WillCreateData && sheet.Confidence == "تحتاج مراجعة"))
            throw new InvalidOperationException("يحتوي الملف على صفحات أو حركات لا يمكن مطابقتها بأمان. تحقق من بنية الأرشيف قبل الاستيراد.");

        await using var input = file.OpenReadStream();
        await using var memory = new MemoryStream();
        await input.CopyToAsync(memory, cancellationToken);
        var hash = Convert.ToHexString(SHA256.HashData(memory.GetBuffer().AsSpan(0, checked((int)memory.Length)))).ToLowerInvariant();
        memory.Position = 0;
        using var workbook = new XLWorkbook(memory);
        var plot = RequireHistoricalPlotNumber(workbook);
        var sourceName = Path.GetFileName(file.FileName);
        var storedName = $"approved-{hash}.xlsx";
        string? createdSource = null;
        await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            if (projectId is int target) await ImportProjectLock.AcquireAsync(context, target, cancellationToken);
            var cap = await RequireApprovedHistoryGrantAsync(context, projectId, userId, isCanonicalAdmin, cancellationToken);
            if (await context.Attachments.AnyAsync(item => item.EntityType == "project-import" && item.StoredFileName == storedName, cancellationToken))
                throw new InvalidOperationException("ملف الأرشيف المعتمد محفوظ بالفعل.");

            // Numeric project codes can be written in either Arabic or Latin digits.
            var projects = await context.Projects.Select(item => new { item.Id, item.Code }).ToListAsync(cancellationToken);
            var matching = projects.Where(item => NormalizePlotNumber(item.Code) == plot).ToList();
            Project project;
            if (projectId is int existing)
            {
                if (matching.Count != 1 || matching[0].Id != existing)
                    throw new InvalidOperationException("رقم القطعة في الملف لا يطابق المشروع المحدد بصورة فريدة.");
                await RequireEmptyProjectLedgerAsync(context, existing, cancellationToken);
                project = await context.Projects.SingleAsync(item => item.Id == existing, cancellationToken);
            }
            else
            {
                if (matching.Count != 0)
                    throw new InvalidOperationException("رقم القطعة موجود بالفعل. اختر المشروع المطابق إذا كان خالياً من الحركات المالية.");
                project = new Project
                {
                    Code = plot, Name = plan.SuggestedProjectName, ClientName = plan.SuggestedClientName,
                    OwnerName = plan.SuggestedOwnerName, PayerName = plan.SuggestedPayerName, Location = plan.SuggestedLocation,
                    OfficeSharePercentage = Math.Clamp(ExtractOfficeShare(workbook), 0m, 100m)
                };
                context.Projects.Add(project);
            }

            var rows = ImportWorkbookRows(context, workbook, project, userId, cancellationToken, historical: true);
            ValidateAndApproveHistoricalRows(context, project, cutoff, cap);
            if (project.Status == "تحت المراجعة") project.Status = "نشط";
            await context.SaveChangesAsync(cancellationToken);

            var folder = Path.Combine(host.ContentRootPath, "uploads", "project-imports");
            Directory.CreateDirectory(folder);
            var fullPath = Path.Combine(folder, storedName);
            try
            {
                await using var output = new FileStream(fullPath, FileMode.CreateNew, FileAccess.Write, FileShare.None, 81920, useAsync: true);
                createdSource = fullPath;
                memory.Position = 0;
                await memory.CopyToAsync(output, cancellationToken);
            }
            catch (IOException) when (createdSource is null && File.Exists(fullPath))
            {
                throw new InvalidOperationException("نسخة مطابقة من ملف الأرشيف محفوظة بالفعل.");
            }
            var attachment = new Attachment
            {
                EntityType = "project-import", EntityId = project.Id, OriginalFileName = sourceName, StoredFileName = storedName,
                RelativePath = Path.Combine("uploads", "project-imports", storedName),
                ContentType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                SizeInBytes = memory.Length, UploadedByUserId = userId
            };
            context.Attachments.Add(attachment);
            await context.SaveChangesAsync(cancellationToken);
            context.SecurityAuditLogs.Add(new SecurityAuditLog
            {
                Action = "اعتماد استيراد تاريخي", EntityType = "project-import", EntityId = project.Id, UserId = userId,
                CreatedAtUtc = DateTime.UtcNow,
                Details = JsonSerializer.Serialize(new
                {
                    confirmedApproved = true, closedThrough = cutoff, sourceFile = sourceName, sha256 = hash,
                    attachmentId = attachment.Id, projectCode = project.Code, rows
                })
            });
            await context.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return rows.Result(project, attachment, ["تم إثبات إقرار الاستيراد وحفظ ملف المصدر ضمن الأرشيف المعتمد."]);
        }
        catch
        {
            await transaction.RollbackAsync(CancellationToken.None);
            if (createdSource is not null) File.Delete(createdSource);
            throw;
        }
    }

    internal static async Task<decimal?> RequireApprovedHistoryGrantAsync(EmgDbContext context, int? projectId, int userId,
        bool isCanonicalAdmin, CancellationToken cancellationToken)
    {
        if (isCanonicalAdmin) return null;
        var grant = await context.UserRoles.AsNoTracking()
            .Where(link => link.UserId == userId && link.User.IsActive && link.Role.Name == "accountant")
            .SelectMany(link => link.Role.RolePermissions)
            .Where(item => item.Permission.Code == ApprovedHistoryPermission)
            .Select(item => new { item.Scope, item.AmountLimit }).SingleOrDefaultAsync(cancellationToken);
        if (grant is not null)
        {
            if (grant.Scope == "all") return grant.AmountLimit;
            if ((grant.Scope is "assigned" or "project") && projectId is int target && await context.UserProjectAssignments.AnyAsync(
                item => item.UserId == userId && item.ProjectId == target && item.IsActive, cancellationToken))
                return grant.AmountLimit;
        }
        throw new ApprovedHistoryForbiddenException();
    }

    private static string RequireHistoricalPlotNumber(XLWorkbook workbook) => TryHistoricalPlotNumber(workbook) ??
        throw new InvalidOperationException("يلزم رقم قطعة صريح وموحد داخل ملف الأرشيف المعتمد.");

    internal static string? TryHistoricalPlotNumber(XLWorkbook workbook)
    {
        var plots = new HashSet<string>(StringComparer.Ordinal);
        foreach (var sheet in workbook.Worksheets)
        foreach (var row in sheet.RowsUsed().Take(25))
        foreach (var cell in row.CellsUsed())
        {
            var parts = SafeCellText(cell).Replace('：', ':').Split(':', 2, StringSplitOptions.TrimEntries);
            if (NormalizeHeader(parts[0]) is not ("رقمالقطعه" or "رقمقطعه" or "رقمالقطعة" or "رقمقطعة")) continue;
            var value = parts.Length > 1 ? NormalizePlotNumber(parts[1]) : null;
            value ??= NormalizePlotNumber(SafeCellText(sheet.Cell(cell.Address.RowNumber, cell.Address.ColumnNumber + 1)));
            value ??= NormalizePlotNumber(SafeCellText(sheet.Cell(cell.Address.RowNumber + 1, cell.Address.ColumnNumber)));
            if (value is not null) plots.Add(value);
        }
        return plots.Count == 1 ? plots.Single() : null;
    }

    internal static string? NormalizePlotNumber(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        var digits = string.Concat(value.Where(character => !char.IsWhiteSpace(character)).Select(character => character switch
        {
            >= '٠' and <= '٩' => (char)('0' + character - '٠'),
            >= '۰' and <= '۹' => (char)('0' + character - '۰'),
            _ => character
        }));
        digits = digits.TrimStart('ق');
        return Regex.IsMatch(digits, @"\A[0-9]{1,30}(?:[-/][0-9]{1,30})?\z") ? digits : null;
    }

    private static void ValidateHistoricalSourceSheet(IXLWorksheet sheet, string classification)
    {
        // The concrete blocks and advance allocations validate their own columns in the shared parser.
        if (classification is not ("أعمال الموقع" or "الحفر" or "الحديد" or "المباني" or "المحارة" or "العزل" or "الكهرباء" or "السباكة" or "أعمال أخرى")) return;
        var layout = DetectLayout(sheet, classification);
        var isSteel = classification == "الحديد";
        var isExcavation = classification == "الحفر";
        var isSite = classification == "أعمال الموقع";
        var totalColumn = layout.Column("total", isSteel ? 6 : 10);
        var quantityColumn = layout.Column("quantity", isSteel ? 3 : isExcavation || isSite ? 8 : 9);
        var priceColumn = layout.Column("unitPrice", isSteel ? 5 : isExcavation ? 9 : 6);
        var descriptionColumn = layout.Column("description", isExcavation ? 6 : 3);
        var hasPaid = !isSteel && !isExcavation;
        var paidColumn = hasPaid ? layout.Column("paid", 11) : 0;
        var columns = new[] { totalColumn, quantityColumn, priceColumn, paidColumn }
            .Concat(isExcavation ? [layout.Column("length", 0), layout.Column("width", 0), layout.Column("depth", 0), layout.Column("loads", 0)] : Array.Empty<int>())
            .Distinct().ToArray();
        foreach (var row in RowsAfter(sheet, layout.HeaderRow))
        {
            if (isSteel && IsSteelSummaryRow(row)) continue;
            ValidateHistoricalMoneyCells(row, columns);
            if (columns.All(column => Decimal(row, column) == 0m)) continue;
            var description = NormalizeHeader(Text(row, descriptionColumn));
            if ((!isSteel && description.StartsWith("اجمالي", StringComparison.Ordinal)) || Decimal(row, totalColumn) == 0 ||
                !isSteel && !isExcavation && string.IsNullOrEmpty(description) || Decimal(row, paidColumn) < 0 ||
                isSteel && Decimal(row, quantityColumn) == 0)
                throw HistoricalSourceError(row, "صف مالي ناقص أو إجمالي غير منفصل عن التفاصيل.");
        }
    }

    private static void ValidateHistoricalMoneyCells(IXLRow row, IEnumerable<int> columns)
    {
        foreach (var column in columns)
            if (column > 0 && !row.Cell(column).IsEmpty())
            {
                var number = ReadDecimal(row.Cell(column));
                if (number is null || Math.Abs(number.Value) > MaxHistoricalAmount)
                    throw HistoricalSourceError(row, "خلية مالية غير رقمية أو خارج المدى أو معادلة بلا قيمة مؤكدة.");
            }
    }

    private static InvalidOperationException HistoricalSourceError(IXLRow row, string reason) =>
        new($"تعذر اعتماد الأرشيف في «{row.Worksheet.Name}» صف {row.RowNumber()}. {reason}");

    private static void ValidateAndApproveHistoricalRows(EmgDbContext context, Project project, DateOnly cutoff, decimal? cap)
    {
        var seen = new HashSet<object>();
        var transactions = 0;
        IEnumerable<T> Rows<T>() where T : class =>
            context.ChangeTracker.Entries<T>().Where(entry => entry.State == EntityState.Added).Select(entry => entry.Entity);

        static string Name(string? text) => Normalize(text ?? string.Empty).ToUpperInvariant();
        void Validate(object key, decimal amount, DateTime date, string? source, bool signed = false)
        {
            if (amount == 0 || amount < 0 && !signed || Math.Abs(amount) > MaxHistoricalAmount ||
                DateOnly.FromDateTime(date) > cutoff || date == default)
                throw new InvalidOperationException($"قيمة أو تاريخ غير صالح للأرشيف المعتمد في {source}.");
            if (cap is decimal limit && Math.Abs(amount) > limit) throw new ApprovedHistoryForbiddenException();
            if (!seen.Add(key)) throw new InvalidOperationException($"حركة مكررة أو غير قابلة للتمييز داخل الأرشيف في {source}.");
            transactions++;
        }

        static void ValidateRate(decimal quantity, decimal price, decimal total, string? source, bool lumpSum = false)
        {
            if (lumpSum && quantity == 0 && price == 0) return;
            if (quantity == 0 || Math.Abs(quantity) > 999999999999999.999m || price <= 0 ||
                price > MaxHistoricalAmount || price > MaxHistoricalAmount / Math.Abs(quantity) ||
                decimal.Round(quantity * price, 2) != total)
                throw new InvalidOperationException($"الإجمالي لا يطابق الكمية والسعر في {source}.");
        }

        foreach (var row in Rows<SiteMovement>())
        {
            ValidateRate(row.Quantity ?? 0m, row.UnitPrice ?? 0m, row.TotalAmount, row.Reference, lumpSum: true);
            Validate(("site", row.MovementDate, Name(row.Description), Name(row.Supplier?.Name), row.TotalAmount), row.TotalAmount,
                row.MovementDate, row.Reference, HasHistoricalAdjustmentText(row.Description));
            row.Status = "معتمد";
        }
        foreach (var row in Rows<ExcavationRecord>())
        {
            ValidateRate(row.CalculatedQuantity, row.UnitPrice, row.TotalAmount, row.Notes);
            Validate(("excavation", row.ExcavationDate, Name(row.Description), row.CalculatedQuantity, row.UnitPrice), row.TotalAmount,
                row.ExcavationDate, row.Notes, row.CalculatedQuantity < 0 && HasHistoricalAdjustmentText(row.Description));
            row.Status = "معتمد";
        }
        foreach (var row in Rows<ConcreteRecord>())
        {
            ValidateRate(row.Quantity, row.UnitPrice, row.TotalAmount, row.Notes);
            Validate(("concrete", row.PourDate, Name(row.Description), row.Quantity, row.UnitPrice), row.TotalAmount,
                row.PourDate, row.Notes, row.Quantity < 0 && HasHistoricalAdjustmentText(row.Description));
            row.Status = "معتمد";
        }
        foreach (var row in Rows<SteelRecord>())
        {
            ValidateRate(row.Quantity, row.UnitPrice, row.TotalAmount, row.Notes);
            Validate(("steel", row.OperationDate, Name(row.Supplier?.Name), Name(row.BarDiameter), row.Quantity, row.UnitPrice),
                row.TotalAmount, row.OperationDate, row.Notes, row.Quantity < 0 && row.OperationType == "تسوية حديد");
            row.Status = "معتمد";
        }
        foreach (var row in Rows<FinishingRecord>())
        {
            ValidateRate(row.Quantity, row.UnitPrice, row.TotalAmount, row.Notes);
            Validate(("finishing", row.WorkDate, Name(row.Description), Name(row.Supplier?.Name), row.Quantity, row.UnitPrice),
                row.TotalAmount, row.WorkDate, row.Notes, row.Quantity < 0 && HasHistoricalAdjustmentText(row.Description));
            row.Status = "معتمد";
        }
        var supplies = Rows<SupplierSupply>().ToHashSet();
        foreach (var row in supplies)
        {
            if (string.IsNullOrWhiteSpace(row.Supplier.Name) || row.Supplier.Name == "غير محدد" || !ReferenceEquals(row.Project, project))
                throw new InvalidOperationException($"تعذر تحديد مورد أو مشروع التوريدة في {row.Notes}.");
            ValidateRate(row.Quantity, row.UnitPrice, row.TotalAmount, row.Notes);
            Validate(("supply", row.SupplyDate, Name(row.Supplier.Name), Name(row.ItemName), Name(row.Unit), row.Quantity, row.UnitPrice),
                row.TotalAmount, row.SupplyDate, row.Notes);
            row.Status = "معتمد";
        }
        foreach (var row in Rows<SupplierPayment>())
        {
            if (row.Supply is not { } supply || !supplies.Contains(supply) || !ReferenceEquals(row.Project, supply.Project) ||
                !ReferenceEquals(row.Supplier, supply.Supplier))
                throw new InvalidOperationException($"دفعة غير مرتبطة بتوريدة من نفس المصدر والمورد في {row.Notes}.");
            Validate(("payment", row.PaymentDate, Name(row.Supplier.Name), row.Amount), row.Amount, row.PaymentDate, row.Notes);
            row.Status = "معتمد";
        }
        foreach (var row in Rows<AdvancePayment>())
        {
            Validate(("advance", row.PaymentDate, Name(row.PayerName), row.Amount), row.Amount, row.PaymentDate ?? throw new InvalidOperationException("يلزم تاريخ الدفعة لهذا المسار."), row.Notes);
            foreach (var allocation in row.Allocations)
                Validate(("allocation", row, allocation.AllocationDate, Name(allocation.AccountCategory), allocation.Amount),
                    allocation.Amount, allocation.AllocationDate, allocation.Notes);
            row.Status = "معتمد";
        }
        if (transactions == 0) throw new InvalidOperationException("ملف الأرشيف لا يحتوي على حركات مالية قابلة للاعتماد.");
    }

    private static bool HasHistoricalAdjustmentText(string value) =>
        new[] { "تسوية", "خصم", "هالك", "تعديل", "مرتجع", "استرداد" }.Any(value.Contains);
}
