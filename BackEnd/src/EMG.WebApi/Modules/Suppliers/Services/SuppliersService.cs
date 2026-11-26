using EMG.Api.Modules.Suppliers.Dtos;
using EMG.Domain.Entities;
using EMG.Infrastructure.Db;
using Microsoft.EntityFrameworkCore;

namespace EMG.Api.Modules.Suppliers.Services;

public sealed class SuppliersService(EmgDbContext db) : ISuppliersService
{
    public async Task<IReadOnlyList<SupplierSummary>> GetSuppliersAsync(int? userId, bool globalAccess, CancellationToken cancellationToken)
    {
        var suppliers = await db.Suppliers.AsNoTracking().OrderBy(item => item.Name).ToListAsync(cancellationToken);
        var projectIds = await GetVisibleProjectIdsAsync(userId, globalAccess, cancellationToken);
        var supplies = await db.SupplierSupplies.AsNoTracking()
            .Where(item => (item.Status == "معتمد" || item.Status == "مرحّل") && (globalAccess || projectIds.Contains(item.ProjectId)))
            .ToListAsync(cancellationToken);
        var payments = await db.SupplierPayments.AsNoTracking()
            .Where(item => (item.Status == "معتمد" || item.Status == "مرحّل") && (globalAccess || item.ProjectId.HasValue && projectIds.Contains(item.ProjectId.Value)))
            .ToListAsync(cancellationToken);

        return suppliers.Select(item =>
        {
            var supplierSupplies = supplies.Where(supply => supply.SupplierId == item.Id).ToList();
            var supplierPayments = payments.Where(payment => payment.SupplierId == item.Id).ToList();
            var totalSupplies = supplierSupplies.Sum(supply => supply.TotalAmount);
            var totalPaid = supplierPayments.Sum(payment => payment.Amount);
            return new SupplierSummary(item.Id, item.Name, item.SupplierType, item.Phone ?? string.Empty,
                item.TaxNumber ?? string.Empty, item.Notes ?? string.Empty, item.IsActive,
                supplierSupplies.Count, totalSupplies, totalPaid, totalSupplies - totalPaid);
        }).ToList();
    }

    public async Task<SupplierSummary?> GetSupplierAsync(int id, int? userId, bool globalAccess, CancellationToken cancellationToken) =>
        (await GetSuppliersAsync(userId, globalAccess, cancellationToken)).FirstOrDefault(item => item.Id == id);

    private async Task<List<int>> GetVisibleProjectIdsAsync(int? userId, bool globalAccess, CancellationToken cancellationToken)
    {
        if (globalAccess) return [];
        if (!userId.HasValue) return [];
        return await db.UserProjectAssignments.AsNoTracking()
            .Where(item => item.UserId == userId.Value && item.IsActive)
            .Select(item => item.ProjectId)
            .Distinct()
            .ToListAsync(cancellationToken);
    }

    public async Task<SupplierSummary> CreateAsync(CreateSupplierRequest request, CancellationToken cancellationToken)
    {
        var normalizedName = Normalize(request.Name);
        var normalizedPhone = NormalizeDigits(request.Phone);
        var normalizedTaxNumber = Normalize(request.TaxNumber);
        var existingSuppliers = await db.Suppliers.AsNoTracking()
            .Where(item => item.IsActive)
            .Select(item => new { item.Name, item.Phone, item.TaxNumber })
            .ToListAsync(cancellationToken);
        var matches = existingSuppliers
            .Where(item =>
                Normalize(item.Name) == normalizedName ||
                !string.IsNullOrWhiteSpace(normalizedPhone) && NormalizeDigits(item.Phone) == normalizedPhone ||
                !string.IsNullOrWhiteSpace(normalizedTaxNumber) && Normalize(item.TaxNumber) == normalizedTaxNumber)
            .Select(item => item.Name)
            .Distinct(StringComparer.Ordinal)
            .ToList();
        if (matches.Count > 0) throw new DuplicateSupplierException(matches);

        var supplier = new Supplier
        {
            Name = request.Name.Trim(), SupplierType = string.IsNullOrWhiteSpace(request.SupplierType) ? "مورد" : request.SupplierType.Trim(),
            Phone = request.Phone?.Trim(), TaxNumber = request.TaxNumber?.Trim(), Notes = request.Notes?.Trim()
        };
        db.Suppliers.Add(supplier);
        await db.SaveChangesAsync(cancellationToken);
        return new SupplierSummary(supplier.Id, supplier.Name, supplier.SupplierType, supplier.Phone ?? string.Empty,
            supplier.TaxNumber ?? string.Empty, supplier.Notes ?? string.Empty, supplier.IsActive, 0, 0, 0, 0);
    }

    private static string Normalize(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return string.Empty;
        return value.Trim().ToLowerInvariant()
            .Replace('أ', 'ا').Replace('إ', 'ا').Replace('آ', 'ا')
            .Replace('ى', 'ي').Replace('ة', 'ه')
            .Replace("ـ", string.Empty)
            .Replace(" ", string.Empty).Replace("-", string.Empty)
            .Replace("_", string.Empty).Replace("/", string.Empty);
    }

    private static string NormalizeDigits(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return string.Empty;
        var arabic = "٠١٢٣٤٥٦٧٨٩";
        var eastern = "۰۱۲۳۴۵۶۷۸۹";
        return new string(value.Where(char.IsDigit).Select(character =>
        {
            var index = arabic.IndexOf(character);
            if (index >= 0) return (char)('0' + index);
            index = eastern.IndexOf(character);
            return index >= 0 ? (char)('0' + index) : character;
        }).ToArray());
    }

    public async Task<IReadOnlyList<SupplySummary>> GetSuppliesAsync(int supplierId, int? userId, bool globalAccess, CancellationToken cancellationToken) =>
        await db.SupplierSupplies.AsNoTracking().Where(item => item.SupplierId == supplierId && (globalAccess || userId.HasValue && db.UserProjectAssignments.Any(a => a.ProjectId == item.ProjectId && a.UserId == userId && a.IsActive)))
            .OrderByDescending(item => item.SupplyDate)
            .Select(ToSummary()).ToListAsync(cancellationToken);

    public async Task<SupplySummary?> AddSupplyAsync(int supplierId, CreateSupplyRequest request, int? userId, bool globalAccess, CancellationToken cancellationToken)
    {
        if (!await db.Suppliers.AnyAsync(item => item.Id == supplierId && item.IsActive, cancellationToken) ||
            !await db.Projects.AnyAsync(item => item.Id == request.ProjectId, cancellationToken) ||
            !globalAccess && (!userId.HasValue || !await db.UserProjectAssignments.AnyAsync(a => a.ProjectId == request.ProjectId && a.UserId == userId && a.IsActive, cancellationToken))) return null;
        if (!string.IsNullOrWhiteSpace(request.InvoiceNumber))
        {
            var invoice = request.InvoiceNumber.Trim().ToLowerInvariant();
            var duplicateInvoice = await db.SupplierSupplies.AnyAsync(item => item.SupplierId == supplierId && item.ProjectId == request.ProjectId &&
                item.InvoiceNumber != null && item.InvoiceNumber.ToLower() == invoice, cancellationToken);
            if (duplicateInvoice) throw new DuplicateSupplierTransactionException("رقم الفاتورة مستخدم بالفعل لهذا المورد داخل نفس المشروع.");
        }
        await using var supplyTransaction = await db.Database.BeginTransactionAsync(cancellationToken);
        var supply = new SupplierSupply
        {
            SupplierId = supplierId, ProjectId = request.ProjectId,
            SupplyType = string.IsNullOrWhiteSpace(request.SupplyType) ? "خامات" : request.SupplyType.Trim(),
            ItemName = request.ItemName.Trim(), Unit = request.Unit.Trim(), Quantity = request.Quantity,
            UnitPrice = request.UnitPrice, TotalAmount = decimal.Round(request.Quantity * request.UnitPrice, 2),
            SupplyDate = request.SupplyDate is null
                ? DateTime.UtcNow.Date
                : DateTime.SpecifyKind(request.SupplyDate.Value.Date, DateTimeKind.Utc),
            InvoiceNumber = request.InvoiceNumber?.Trim(), Notes = request.Notes?.Trim(), Status = "مرحّل", CreatedByUserId = userId
        };
        db.SupplierSupplies.Add(supply);
        await db.SaveChangesAsync(cancellationToken);
        db.WorkflowActions.Add(new WorkflowAction { EntityType = "supplier-supply", EntityId = supply.Id, Action = "ترحيل مباشر", FromStatus = "", ToStatus = "مرحّل", UserId = userId, Notes = "تسجيل مباشر وفق سياسة النشر الموحد" });
        await db.SaveChangesAsync(cancellationToken);
        await supplyTransaction.CommitAsync(cancellationToken);
        return await db.SupplierSupplies.AsNoTracking().Where(item => item.Id == supply.Id).Select(ToSummary()).FirstAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<PaymentSummary>> GetPaymentsAsync(int supplierId, int? userId, bool globalAccess, CancellationToken cancellationToken) =>
        await db.SupplierPayments.AsNoTracking().Where(item => item.SupplierId == supplierId && (globalAccess || item.ProjectId.HasValue && userId.HasValue && db.UserProjectAssignments.Any(a => a.ProjectId == item.ProjectId && a.UserId == userId && a.IsActive)))
            .OrderByDescending(item => item.PaymentDate).Select(ToPaymentSummary()).ToListAsync(cancellationToken);

    public async Task<PaymentSummary?> AddPaymentAsync(int supplierId, CreateSupplierPaymentRequest request, int? userId, bool globalAccess, CancellationToken cancellationToken)
    {
        if (request.Amount <= 0 || !await db.Suppliers.AnyAsync(item => item.Id == supplierId && item.IsActive, cancellationToken)) return null;
        if (!globalAccess && request.ProjectId is null) return null;
        if (request.ProjectId is not null && (!await db.Projects.AnyAsync(item => item.Id == request.ProjectId, cancellationToken) ||
            !globalAccess && (!userId.HasValue || !await db.UserProjectAssignments.AnyAsync(a => a.ProjectId == request.ProjectId && a.UserId == userId && a.IsActive, cancellationToken)))) return null;
        if (request.SupplyId is not null)
        {
            var supply = await db.SupplierSupplies.AsNoTracking().FirstOrDefaultAsync(item => item.Id == request.SupplyId && item.SupplierId == supplierId, cancellationToken);
            if (supply is null || request.ProjectId is not null && supply.ProjectId != request.ProjectId) return null;
            var settled = await db.SupplierPayments.AsNoTracking()
                .Where(item => item.SupplyId == supply.Id && (item.Status == "معتمد" || item.Status == "مرحّل"))
                .SumAsync(item => (decimal?)item.Amount, cancellationToken) ?? 0m;
            if (request.Amount > supply.TotalAmount - settled)
                throw new DuplicateSupplierTransactionException("قيمة الدفع تتجاوز المتبقي على التوريدة بعد الدفعات المعتمدة.");
        }
        if (!string.IsNullOrWhiteSpace(request.Reference))
        {
            var reference = request.Reference.Trim().ToLowerInvariant();
            var duplicateReference = await db.SupplierPayments.AnyAsync(item => item.SupplierId == supplierId && item.Amount == request.Amount &&
                item.ProjectId == request.ProjectId && item.Reference != null && item.Reference.ToLower() == reference, cancellationToken);
            if (duplicateReference) throw new DuplicateSupplierTransactionException("مرجع الدفع مستخدم بالفعل لنفس المورد والقيمة والمشروع.");
        }

        await using var paymentTransaction = await db.Database.BeginTransactionAsync(cancellationToken);
        var payment = new SupplierPayment
        {
            SupplierId = supplierId, ProjectId = request.ProjectId, SupplyId = request.SupplyId, Amount = request.Amount,
            PaymentDate = request.PaymentDate is null ? DateTime.UtcNow.Date : DateTime.SpecifyKind(request.PaymentDate.Value.Date, DateTimeKind.Utc),
            PaymentMethod = string.IsNullOrWhiteSpace(request.PaymentMethod) ? "نقدي" : request.PaymentMethod.Trim(),
            Reference = request.Reference?.Trim(), Notes = request.Notes?.Trim(), Status = "مرحّل", CreatedByUserId = userId
        };
        db.SupplierPayments.Add(payment);
        await db.SaveChangesAsync(cancellationToken);
        db.WorkflowActions.Add(new WorkflowAction { EntityType = "supplier-payment", EntityId = payment.Id, Action = "ترحيل مباشر", FromStatus = "", ToStatus = "مرحّل", UserId = userId, Notes = "تسجيل مباشر وفق سياسة النشر الموحد" });
        await db.SaveChangesAsync(cancellationToken);
        await paymentTransaction.CommitAsync(cancellationToken);
        return await db.SupplierPayments.AsNoTracking().Where(item => item.Id == payment.Id).Select(ToPaymentSummary()).FirstAsync(cancellationToken);
    }

    private static System.Linq.Expressions.Expression<Func<SupplierSupply, SupplySummary>> ToSummary() => item => new SupplySummary(
        item.Id, item.SupplierId, item.Supplier.Name, item.ProjectId, item.Project.Name, item.SupplyType, item.ItemName,
        item.Unit, item.Quantity, item.UnitPrice, item.TotalAmount, item.Payments.Where(payment => payment.Status == "معتمد" || payment.Status == "مرحّل").Sum(payment => (decimal?)payment.Amount) ?? 0m, item.TotalAmount - (item.Payments.Where(payment => payment.Status == "معتمد" || payment.Status == "مرحّل").Sum(payment => (decimal?)payment.Amount) ?? 0m), item.SupplyDate, item.InvoiceNumber ?? string.Empty, item.Notes ?? string.Empty, item.Status);

    private static System.Linq.Expressions.Expression<Func<SupplierPayment, PaymentSummary>> ToPaymentSummary() => item => new PaymentSummary(
        item.Id, item.SupplierId, item.ProjectId, item.Project == null ? string.Empty : item.Project.Name, item.SupplyId,
        item.Amount, item.PaymentDate, item.PaymentMethod, item.Reference ?? string.Empty, item.Notes ?? string.Empty, item.Status);
}
