using System.Data;
using EMG.Api.Modules.Suppliers.Dtos;
using EMG.Domain.Entities;
using EMG.Infrastructure.Db;
using Microsoft.EntityFrameworkCore;

namespace EMG.Api.Modules.Suppliers.Services;

public sealed class SupplierInvoiceService(EmgDbContext db, IWebHostEnvironment environment) : ISupplierInvoiceService
{
    public async Task<SupplierInvoiceSummary?> CreateAsync(CreateSupplierInvoiceRequest request, int? userId, bool globalAccess, CancellationToken cancellationToken)
    {
        if (request.Currency is not null && !string.Equals(request.Currency.Trim(), "EGP", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("فواتير الموردين بالجنيه المصري فقط.");
        if (request.Lines is null || request.Lines.Count == 0 || request.Lines.Any(line => string.IsNullOrWhiteSpace(line.Description) || string.IsNullOrWhiteSpace(line.Unit) || line.Quantity <= 0 || line.UnitPrice < 0) || request.InitialPayment < 0)
            throw new InvalidOperationException("يجب إدخال بند واحد على الأقل بقيم كمية وسعر صحيحة.");
        if (!globalAccess && (!userId.HasValue || !await db.UserProjectAssignments.AnyAsync(item => item.UserId == userId && item.ProjectId == request.ProjectId && item.IsActive, cancellationToken))) return null;

        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        var project = await db.Projects.SingleOrDefaultAsync(item => item.Id == request.ProjectId, cancellationToken);
        var supplier = await db.Suppliers.SingleOrDefaultAsync(item => item.Id == request.SupplierId && item.IsActive, cancellationToken);
        if (project is null || supplier is null) return null;
        if (request.SiteMovementId is int movementId &&
            !await db.SiteMovements.AnyAsync(item => item.Id == movementId && item.ProjectId == request.ProjectId, cancellationToken)) return null;
        if (request.SiteMovementId is int sourceMovement && await db.SupplierInvoices.AnyAsync(item => item.SiteMovementId == sourceMovement, cancellationToken))
            throw new DuplicateSupplierTransactionException("تم ربط فاتورة مورد بالفعل بحركة الموقع المحددة.");

        var total = request.Lines.Sum(line => decimal.Round(line.Quantity * line.UnitPrice, 2));
        if (request.SiteMovementId is int linkedId)
        {
            var target = await db.SiteMovements.SingleAsync(m => m.Id == linkedId, cancellationToken);
            if (target.Currency != "EGP" || target.MovementType == "وارد" || target.SupplierId.HasValue && target.SupplierId != supplier.Id ||
                target.TotalAmount != total || target.Status is not ("مرحّل" or "معتمد") ||
                await EMG.Api.Modules.Projects.Services.HistoricalWorkReader.IsLinkedAsync(db, "site-movement", linkedId, cancellationToken))
                throw new InvalidOperationException("حركة الموقع المرتبطة يجب أن تطابق الفاتورة والمورد وقيمة الأعمال.");
        }
        if (request.InitialPayment > total) throw new DuplicateSupplierTransactionException("الدفعة الأولية تتجاوز إجمالي الفاتورة.");
        var paymentSource = request.PaymentSource?.Trim() == "مالك مباشر" ? "مالك مباشر" : "مكتب";
        var invoiceNumber = GenerateInvoiceNumber(request.ProjectId, request.SupplierId);
        var invoiceDate = request.InvoiceDate is null ? DateTime.UtcNow.Date : DateTime.SpecifyKind(request.InvoiceDate.Value.Date, DateTimeKind.Utc);
        if (request.SiteMovementId is null)
        {
            var movement = new SiteMovement
            {
                ProjectId = request.ProjectId, SupplierId = request.SupplierId, MovementType = "مصروفات",
                Description = string.IsNullOrWhiteSpace(request.MovementDescription)
                    ? paymentSource == "مالك مباشر" ? "دفع فاتورة مباشرة من المالك" : request.Lines[0].Description.Trim()
                    : request.MovementDescription.Trim(),
                CounterpartyType = "مورد", Currency = "EGP", FundingSource = paymentSource == "مالك مباشر" ? "المالك" : "المكتب",
                Recipient = supplier.Name, WorkCategory = request.ExpenseCategory?.Trim(),
                TotalAmount = total, ExpenseCategory = request.ExpenseCategory?.Trim(), PaidTo = request.PaidTo?.Trim(),
                PaymentMethod = request.InitialPayment == 0 ? "آجل" : request.PaymentMethod?.Trim() ?? "نقدي",
                IsPaid = request.InitialPayment >= total && total > 0, MovementDate = invoiceDate, Status = "مرحّل",
                Reference = invoiceNumber, Details = request.Details?.Trim(), CreatedByUserId = userId
            };
            db.SiteMovements.Add(movement);
            await db.SaveChangesAsync(cancellationToken);
            request = request with { SiteMovementId = movement.Id };
        }
        var invoice = new SupplierInvoice
        {
            ProjectId = request.ProjectId, SupplierId = request.SupplierId, SiteMovementId = request.SiteMovementId,
            InvoiceNumber = invoiceNumber, Currency = "EGP", Status = "مرحّل", CreatedByUserId = userId,
            InvoiceDate = invoiceDate
        };
        db.SupplierInvoices.Add(invoice);
        await db.SaveChangesAsync(cancellationToken);
        var lineNumber = 0;
        foreach (var line in request.Lines)
        {
            var supply = new SupplierSupply
            {
                SupplierId = request.SupplierId, ProjectId = request.ProjectId, InvoiceId = invoice.Id,
                SupplyType = string.IsNullOrWhiteSpace(line.SupplyType) ? "خامات" : line.SupplyType.Trim(),
                ItemName = line.Description.Trim(), Unit = line.Unit.Trim(), Quantity = line.Quantity, UnitPrice = line.UnitPrice,
                TotalAmount = decimal.Round(line.Quantity * line.UnitPrice, 2), SupplyDate = invoice.InvoiceDate,
                InvoiceNumber = invoiceNumber, Notes = request.Notes?.Trim(), Status = "مرحّل", CreatedByUserId = userId
            };
            db.SupplierSupplies.Add(supply);
            await db.SaveChangesAsync(cancellationToken);
            db.SupplierInvoiceLines.Add(new SupplierInvoiceLine { InvoiceId = invoice.Id, SupplierSupplyId = supply.Id, LineNumber = ++lineNumber });
        }
        if (request.InitialPayment > 0)
        {
            db.SupplierPayments.Add(new SupplierPayment
            {
                SupplierId = request.SupplierId, ProjectId = request.ProjectId, InvoiceId = invoice.Id,
                Amount = request.InitialPayment, PaymentDate = invoice.InvoiceDate,
                PaymentMethod = string.IsNullOrWhiteSpace(request.PaymentMethod) ? "نقدي" : request.PaymentMethod.Trim(),
                Reference = request.PaymentReference?.Trim(), Notes = string.IsNullOrWhiteSpace(request.Notes) && paymentSource == "مالك مباشر" ? "تخليص حساب" : request.Notes?.Trim(),
                PaymentSource = paymentSource, Status = "مرحّل", CreatedByUserId = userId
            });
        }
        db.WorkflowActions.Add(new WorkflowAction { EntityType = "supplier-invoice", EntityId = invoice.Id, Action = "ترحيل مباشر", FromStatus = "", ToStatus = "مرحّل", UserId = userId, Notes = "فاتورة مورد مرتبطة بحركة الموقع" });
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return await GetAsync(invoice.Id, userId, globalAccess, cancellationToken);
    }

    public async Task<SupplierInvoiceSummary?> GetAsync(int invoiceId, int? userId, bool globalAccess, CancellationToken cancellationToken)
    {
        var invoice = await db.SupplierInvoices.AsNoTracking().Include(item => item.Lines).ThenInclude(line => line.SupplierSupply)
            .Include(item => item.Payments).SingleOrDefaultAsync(item => item.Id == invoiceId, cancellationToken);
        if (invoice is null || !globalAccess && (!userId.HasValue || !await db.UserProjectAssignments.AnyAsync(item => item.UserId == userId && item.ProjectId == invoice.ProjectId && item.IsActive, cancellationToken))) return null;
        var lines = invoice.Lines.OrderBy(item => item.LineNumber).Select(line => new SupplierInvoiceLineSummary(line.Id, line.SupplierSupplyId,
            line.SupplierSupply.ItemName, line.SupplierSupply.Unit, line.SupplierSupply.Quantity, line.SupplierSupply.UnitPrice, line.SupplierSupply.TotalAmount)).ToList();
        var total = decimal.Round(lines.Sum(line => line.TotalAmount), 2);
        var paid = decimal.Round(invoice.Payments.Where(payment => payment.Status is "معتمد" or "مرحّل").Sum(payment => payment.Amount), 2);
        return new SupplierInvoiceSummary(invoice.Id, invoice.SupplierId, invoice.ProjectId, invoice.SiteMovementId, invoice.InvoiceNumber,
            invoice.Currency, total, paid, total - paid, lines);
    }

    public async Task<SupplierInvoiceSummary?> UpdateAsync(int invoiceId, UpdateSupplierInvoiceRequest request, int? userId, bool globalAccess, CancellationToken cancellationToken)
    {
        ValidateLines(request.Lines);
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        var invoice = await db.SupplierInvoices.Include(item => item.SiteMovement).Include(item => item.Lines).ThenInclude(line => line.SupplierSupply)
            .Include(item => item.Payments).SingleOrDefaultAsync(item => item.Id == invoiceId, cancellationToken);
        if (invoice is null || !await CanManage(invoice.ProjectId, invoice.CreatedByUserId, userId, globalAccess,
                invoice.Lines.Sum(item => item.SupplierSupply.TotalAmount), "transactions.correct", cancellationToken)) return null;
        var paid = invoice.Payments.Where(payment => payment.Status is "معتمد" or "مرحّل").Sum(payment => payment.Amount);
        var total = request.Lines.Sum(line => decimal.Round(line.Quantity * line.UnitPrice, 2));
        if (paid > total) throw new InvalidOperationException("لا يمكن خفض إجمالي الفاتورة عن الدفعات المسجلة.");
        if (request.Lines.Count != invoice.Lines.Count)
            throw new InvalidOperationException("تعديل عدد بنود الفاتورة غير مدعوم بعد تسجيلها؛ أنشئ فاتورة جديدة عند الحاجة.");
        if (invoice.SiteMovement is { } movement)
        {
            var allocated = await db.AdvanceAllocations.Where(a => a.SiteMovementId == movement.Id)
                .SumAsync(a => a.Amount, cancellationToken);
            if (total < allocated || total != movement.TotalAmount && await db.ContractorStatementLines.AnyAsync(
                    l => l.SourceEntityType == "site-movement" && l.SourceEntityId == movement.Id, cancellationToken))
                throw new InvalidOperationException("القيمة الجديدة تتعارض مع توزيع أو مستخلص مرتبط بحركة الموقع.");
            movement.TotalAmount = total;
            movement.IsPaid = total > 0 && paid == total;
        }
        var ordered = invoice.Lines.OrderBy(item => item.LineNumber).ToList();
        for (var index = 0; index < ordered.Count; index++)
        {
            var line = ordered[index];
            var input = request.Lines[index];
            line.SupplierSupply.ItemName = input.Description.Trim();
            line.SupplierSupply.Unit = input.Unit.Trim();
            line.SupplierSupply.Quantity = input.Quantity;
            line.SupplierSupply.UnitPrice = input.UnitPrice;
            line.SupplierSupply.TotalAmount = decimal.Round(input.Quantity * input.UnitPrice, 2);
            line.SupplierSupply.SupplyType = string.IsNullOrWhiteSpace(input.SupplyType) ? "خامات" : input.SupplyType.Trim();
            line.SupplierSupply.Notes = request.Notes?.Trim();
        }
        if (request.InvoiceDate is not null)
        {
            invoice.InvoiceDate = DateTime.SpecifyKind(request.InvoiceDate.Value.Date, DateTimeKind.Utc);
            foreach (var line in ordered) line.SupplierSupply.SupplyDate = invoice.InvoiceDate;
            if (invoice.SiteMovement is { } dated && dated.Reference == invoice.InvoiceNumber)
                dated.MovementDate = invoice.InvoiceDate;
        }
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return await GetAsync(invoiceId, userId, globalAccess, cancellationToken);
    }

    public async Task<bool> DeleteAsync(int invoiceId, int? userId, bool globalAccess, CancellationToken cancellationToken)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        var invoice = await db.SupplierInvoices.Include(item => item.SiteMovement).Include(item => item.Lines).ThenInclude(line => line.SupplierSupply)
            .Include(item => item.Payments).SingleOrDefaultAsync(item => item.Id == invoiceId, cancellationToken);
        if (invoice is null || !await CanManage(invoice.ProjectId, invoice.CreatedByUserId, userId, globalAccess,
                invoice.Lines.Sum(item => item.SupplierSupply.TotalAmount), "transactions.cancel", cancellationToken)) return false;
        if (invoice.Payments.Any(payment => payment.Status is "معتمد" or "مرحّل"))
            throw new InvalidOperationException("لا يمكن حذف فاتورة عليها دفعات؛ عالج الدفعات المرتبطة أولاً.");
        // Invoice-created movements carry the generated invoice number. An independently
        // recorded movement supplied by ID survives detaching the invoice.
        if (invoice.SiteMovement is { } movement && movement.Reference == invoice.InvoiceNumber)
        {
            if (await db.AdvanceAllocations.AnyAsync(a => a.SiteMovementId == movement.Id, cancellationToken) ||
                await db.ContractorStatementLines.AnyAsync(l => l.SourceEntityType == "site-movement" && l.SourceEntityId == movement.Id, cancellationToken) ||
                await db.Attachments.AnyAsync(a => a.EntityType == "site-movement" && a.EntityId == movement.Id, cancellationToken))
                throw new InvalidOperationException("حركة الموقع مرتبطة بتوزيع أو مستخلص أو مستند يمنع حذفها.");
            db.SiteMovements.Remove(movement);
        }
        var supplyIds = invoice.Lines.Select(item => item.SupplierSupplyId).ToList();
        var attachmentPaths = await db.Attachments.Where(item => item.EntityType == "supplier-invoice" && item.EntityId == invoiceId)
            .Select(item => item.RelativePath).ToListAsync(cancellationToken);
        db.SupplierInvoiceLines.RemoveRange(invoice.Lines);
        var supplies = await db.SupplierSupplies.Where(item => supplyIds.Contains(item.Id)).ToListAsync(cancellationToken);
        db.SupplierSupplies.RemoveRange(supplies);
        db.SupplierInvoices.Remove(invoice);
        db.Attachments.RemoveRange(db.Attachments.Where(item => item.EntityType == "supplier-invoice" && item.EntityId == invoiceId));
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        DeleteFilesSafely(attachmentPaths);
        return true;
    }

    private async Task<bool> CanManage(int projectId, int? ownerId, int? userId, bool globalAccess, decimal amount,
        string permissionCode, CancellationToken cancellationToken)
    {
        if (userId is not int accountId) return false;
        var assigned = await db.UserProjectAssignments.AnyAsync(item =>
            item.UserId == accountId && item.ProjectId == projectId && item.IsActive, cancellationToken);
        if (!assigned && !globalAccess) return false;
        if (ownerId == accountId) return true;

        var grants = await db.UserRoles.Where(item => item.UserId == accountId && item.User.IsActive)
            .SelectMany(item => item.Role.RolePermissions)
            .Where(item => item.Permission.Code == permissionCode)
            .Select(item => new { item.Scope, item.AmountLimit })
            .ToListAsync(cancellationToken);
        return grants.Any(grant =>
            (grant.AmountLimit is null || grant.AmountLimit >= Math.Abs(amount)) &&
            (grant.Scope == "all" && globalAccess || assigned && grant.Scope is "assigned" or "project"));
    }

    private void DeleteFilesSafely(IEnumerable<string> relativePaths)
    {
        var uploadsRoot = Path.GetFullPath(Path.Combine(environment.ContentRootPath, "uploads"));
        var allowedPrefix = uploadsRoot.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        foreach (var relativePath in relativePaths)
        {
            var fullPath = Path.GetFullPath(Path.Combine(environment.ContentRootPath, relativePath));
            if (fullPath.StartsWith(allowedPrefix, StringComparison.OrdinalIgnoreCase) && File.Exists(fullPath))
                File.Delete(fullPath);
        }
    }

    private static void ValidateLines(IReadOnlyList<SupplierInvoiceLineRequest> lines)
    {
        if (lines is null || lines.Count == 0 || lines.Any(line => string.IsNullOrWhiteSpace(line.Description) || string.IsNullOrWhiteSpace(line.Unit) || line.Quantity <= 0 || line.UnitPrice < 0))
            throw new InvalidOperationException("يجب إدخال بند واحد على الأقل بقيم كمية وسعر صحيحة.");
    }

    private static string GenerateInvoiceNumber(int projectId, int supplierId)
    {
        var value = $"INV-{projectId}-{supplierId}-{Guid.NewGuid():N}";
        return value[..Math.Min(120, value.Length)];
    }
}
