using System.Data;
using EMG.Api.Modules.Suppliers.Dtos;
using EMG.Domain.Entities;
using EMG.Infrastructure.Db;
using Microsoft.EntityFrameworkCore;

namespace EMG.Api.Modules.Suppliers.Services;

public sealed class SupplierInvoiceService(EmgDbContext db) : ISupplierInvoiceService
{
    public async Task<SupplierInvoiceSummary?> CreateAsync(CreateSupplierInvoiceRequest request, int? userId, bool globalAccess, CancellationToken cancellationToken)
    {
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

        var total = decimal.Round(request.Lines.Sum(line => line.Quantity * line.UnitPrice), 2);
        if (request.InitialPayment > total) throw new DuplicateSupplierTransactionException("الدفعة الأولية تتجاوز إجمالي الفاتورة.");
        var invoiceNumber = GenerateInvoiceNumber(request.ProjectId, request.SupplierId);
        var invoice = new SupplierInvoice
        {
            ProjectId = request.ProjectId, SupplierId = request.SupplierId, SiteMovementId = request.SiteMovementId,
            InvoiceNumber = invoiceNumber, Currency = "EGP", Status = "مرحّل", CreatedByUserId = userId,
            InvoiceDate = request.InvoiceDate is null ? DateTime.UtcNow.Date : DateTime.SpecifyKind(request.InvoiceDate.Value.Date, DateTimeKind.Utc)
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
                Reference = request.PaymentReference?.Trim(), Notes = request.Notes?.Trim(), Status = "مرحّل", CreatedByUserId = userId
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
        var invoice = await db.SupplierInvoices.Include(item => item.Lines).ThenInclude(line => line.SupplierSupply)
            .Include(item => item.Payments).SingleOrDefaultAsync(item => item.Id == invoiceId, cancellationToken);
        if (invoice is null || !await CanManage(invoice.ProjectId, invoice.CreatedByUserId, userId, globalAccess, cancellationToken)) return null;
        var paid = invoice.Payments.Where(payment => payment.Status is "معتمد" or "مرحّل").Sum(payment => payment.Amount);
        var total = decimal.Round(request.Lines.Sum(line => line.Quantity * line.UnitPrice), 2);
        if (paid > total) throw new InvalidOperationException("لا يمكن خفض إجمالي الفاتورة عن الدفعات المسجلة.");
        if (request.Lines.Count != invoice.Lines.Count)
            throw new InvalidOperationException("تعديل عدد بنود الفاتورة غير مدعوم بعد تسجيلها؛ أنشئ فاتورة جديدة عند الحاجة.");
        for (var index = 0; index < invoice.Lines.Count; index++)
        {
            var line = invoice.Lines.OrderBy(item => item.LineNumber).ElementAt(index);
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
            invoice.InvoiceDate = DateTime.SpecifyKind(request.InvoiceDate.Value.Date, DateTimeKind.Utc);
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return await GetAsync(invoiceId, userId, globalAccess, cancellationToken);
    }

    public async Task<bool> DeleteAsync(int invoiceId, int? userId, bool globalAccess, CancellationToken cancellationToken)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        var invoice = await db.SupplierInvoices.Include(item => item.Lines).ThenInclude(line => line.SupplierSupply)
            .Include(item => item.Payments).SingleOrDefaultAsync(item => item.Id == invoiceId, cancellationToken);
        if (invoice is null || !await CanManage(invoice.ProjectId, invoice.CreatedByUserId, userId, globalAccess, cancellationToken)) return false;
        if (invoice.Payments.Any(payment => payment.Status is "معتمد" or "مرحّل"))
            throw new InvalidOperationException("لا يمكن حذف فاتورة عليها دفعات؛ عالج الدفعات المرتبطة أولاً.");
        var supplyIds = invoice.Lines.Select(item => item.SupplierSupplyId).ToList();
        db.SupplierInvoiceLines.RemoveRange(invoice.Lines);
        var supplies = await db.SupplierSupplies.Where(item => supplyIds.Contains(item.Id)).ToListAsync(cancellationToken);
        db.SupplierSupplies.RemoveRange(supplies);
        db.SupplierInvoices.Remove(invoice);
        db.Attachments.RemoveRange(db.Attachments.Where(item => item.EntityType == "supplier-invoice" && item.EntityId == invoiceId));
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return true;
    }

    private async Task<bool> CanManage(int projectId, int? ownerId, int? userId, bool globalAccess, CancellationToken cancellationToken) =>
        globalAccess || ownerId == userId && userId.HasValue || userId.HasValue && await db.UserProjectAssignments.AnyAsync(item =>
            item.UserId == userId && item.ProjectId == projectId && item.IsActive, cancellationToken);

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
