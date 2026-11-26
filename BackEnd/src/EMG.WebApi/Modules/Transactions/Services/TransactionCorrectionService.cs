using System.Reflection;
using System.Text.Json;
using EMG.Api.Modules.Transactions.Dtos;
using EMG.Domain.Entities;
using EMG.Infrastructure.Db;
using Microsoft.EntityFrameworkCore;

namespace EMG.Api.Modules.Transactions.Services;

public sealed class TransactionCorrectionService(EmgDbContext db) : ITransactionCorrectionService
{
    private static readonly IReadOnlyDictionary<string, string[]> EditableProperties = new Dictionary<string, string[]>(StringComparer.Ordinal)
    {
        ["site-movement"] = ["MovementType", "Description", "Unit", "Quantity", "UnitPrice", "TotalAmount", "ExpenseCategory", "PaidTo", "PaymentMethod", "IsPaid", "MovementDate", "Reference", "Details"],
        ["excavation"] = ["AccountCategory", "CalculationMethod", "Length", "Width", "Depth", "CalculatedQuantity", "Unit", "UnitPrice", "TotalAmount", "SoilType", "Description", "ExcavationDate", "Notes"],
        ["concrete"] = ["AccountCategory", "ConcreteType", "ElementType", "MixGrade", "Quantity", "Unit", "UnitPrice", "TotalAmount", "PourDate", "DeliveryNoteNumber", "Description", "Notes"],
        ["steel"] = ["AccountCategory", "OperationType", "SteelType", "BarDiameter", "Grade", "Quantity", "Unit", "UnitPrice", "TotalAmount", "OperationDate", "DeliveryNoteNumber", "Description", "Notes"],
        ["finishing"] = ["AccountCategory", "WorkType", "AreaName", "Quantity", "Unit", "UnitPrice", "TotalAmount", "WorkDate", "Description", "Notes"],
        ["supplier-supply"] = ["SupplyType", "ItemName", "Unit", "Quantity", "UnitPrice", "TotalAmount", "SupplyDate", "InvoiceNumber", "Notes"],
        ["supplier-payment"] = ["Amount", "PaymentDate", "PaymentMethod", "Reference", "Notes"],
        ["advance-payment"] = ["ClientName", "PayerName", "Amount", "PaymentDate", "PaymentMethod", "Reference", "Notes"],
        ["attendance"] = ["CheckInAtUtc", "CheckOutAtUtc", "Notes"]
    };

    public async Task<TransactionOperationResult?> CorrectAsync(string entityType, int entityId, TransactionCorrectionRequest request, int? userId, bool globalAccess, bool hasCorrectionPermission, CancellationToken cancellationToken)
    {
        if (userId is null || request is null) return null;
        var type = Normalize(entityType);
        if (!EditableProperties.ContainsKey(type)) return null;
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        var record = await FindAsync(type, entityId, cancellationToken);
        if (record is null || !await CanAccessAsync(type, record, userId.Value, globalAccess, hasCorrectionPermission, cancellationToken) || IsCancelled(record)) return null;
        if (!ValidateValues(type, request.Values, out var validation)) throw new TransactionValidationException(validation!);

        var before = Capture(record, EditableProperties[type]);
        Apply(record, request.Values, EditableProperties[type]);
        Recalculate(record, type);
        var dependencyError = await ValidateFinancialDependenciesAsync(type, record, entityId, cancellationToken);
        if (dependencyError is not null) throw new TransactionValidationException([dependencyError]);
        var after = Capture(record, EditableProperties[type]);
        if (JsonSerializer.Serialize(before) == JsonSerializer.Serialize(after)) return null;

        db.SecurityAuditLogs.Add(new EMG.Domain.Security.SecurityAuditLog
        {
            UserId = userId, Action = "transaction.corrected", EntityType = type, EntityId = entityId,
            Details = "تم تصحيح المعاملة مباشرة وفق صلاحية المنشئ أو الصلاحية الممنوحة."
        });
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return new TransactionOperationResult(type, entityId, StatusOf(record), "تصحيح", DateTime.UtcNow);
    }

    public async Task<TransactionOperationResult?> DeleteAsync(string entityType, int entityId, int? userId, bool globalAccess, bool hasDeletePermission, CancellationToken cancellationToken)
    {
        if (userId is null) return null;
        var type = Normalize(entityType);
        if (!EditableProperties.ContainsKey(type)) return null;
        var record = await FindAsync(type, entityId, cancellationToken);
        if (record is null || !await CanAccessAsync(type, record, userId.Value, globalAccess, hasDeletePermission, cancellationToken) || await HasDependentFinancialEffectsAsync(type, entityId, cancellationToken)) return null;
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        await db.WorkflowActions.Where(x => x.EntityType == type && x.EntityId == entityId).ExecuteDeleteAsync(cancellationToken);
        await db.Attachments.Where(x => x.EntityType == type && x.EntityId == entityId).ExecuteDeleteAsync(cancellationToken);
        db.SecurityAuditLogs.Add(new EMG.Domain.Security.SecurityAuditLog
        {
            UserId = userId, Action = "transaction.deleted", EntityType = type, EntityId = entityId,
            Details = "حذف دائم للمعاملة بعد التحقق من الملكية ونطاق المشروع والاعتماديات."
        });
        db.Remove(record);
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return new TransactionOperationResult(type, entityId, "محذوف", "حذف", DateTime.UtcNow);
    }

    private async Task<object?> FindAsync(string type, int id, CancellationToken cancellationToken) => type switch
    {
        "site-movement" => await db.SiteMovements.SingleOrDefaultAsync(x => x.Id == id, cancellationToken),
        "excavation" => await db.ExcavationRecords.SingleOrDefaultAsync(x => x.Id == id, cancellationToken),
        "concrete" => await db.ConcreteRecords.SingleOrDefaultAsync(x => x.Id == id, cancellationToken),
        "steel" => await db.SteelRecords.SingleOrDefaultAsync(x => x.Id == id, cancellationToken),
        "finishing" => await db.FinishingRecords.SingleOrDefaultAsync(x => x.Id == id, cancellationToken),
        "supplier-supply" => await db.SupplierSupplies.SingleOrDefaultAsync(x => x.Id == id, cancellationToken),
        "supplier-payment" => await db.SupplierPayments.SingleOrDefaultAsync(x => x.Id == id, cancellationToken),
        "advance-payment" => await db.AdvancePayments.SingleOrDefaultAsync(x => x.Id == id, cancellationToken),
        "attendance" => await db.AttendanceRecords.SingleOrDefaultAsync(x => x.Id == id, cancellationToken),
        _ => null
    };

    private async Task<bool> CanAccessAsync(string type, object record, int userId, bool globalAccess, bool hasOverridePermission, CancellationToken cancellationToken)
    {
        if (globalAccess) return true;
        var projectValue = record.GetType().GetProperty("ProjectId")?.GetValue(record);
        int? projectId = projectValue is int value ? value : null;
        if (projectId is null && type == "supplier-payment")
        {
            var supplyId = (int?)record.GetType().GetProperty("SupplyId")?.GetValue(record);
            projectId = supplyId is null ? null : await db.SupplierSupplies.Where(x => x.Id == supplyId).Select(x => (int?)x.ProjectId).SingleOrDefaultAsync(cancellationToken);
        }
        var inProject = projectId is not null && await db.UserProjectAssignments.AnyAsync(x => x.ProjectId == projectId && x.UserId == userId && x.IsActive, cancellationToken);
        if (!inProject && projectId is not null) return false;
        var owner = record.GetType().GetProperty("CreatedByUserId")?.GetValue(record) as int?;
        owner ??= record.GetType().GetProperty("UserId")?.GetValue(record) as int?;
        return inProject && (hasOverridePermission || owner == userId) || projectId is null && (hasOverridePermission || owner == userId);
    }

    private async Task<bool> HasDependentFinancialEffectsAsync(string type, int id, CancellationToken cancellationToken)
    {
        if (type == "supplier-supply" && await db.SupplierPayments.AnyAsync(x => x.SupplyId == id, cancellationToken)) return true;
        if (type == "advance-payment" && await db.AdvanceAllocations.AnyAsync(x => x.AdvancePaymentId == id, cancellationToken)) return true;
        if (type == "supplier-payment" && await db.AdvanceAllocations.AnyAsync(x => x.SupplierPaymentId == id, cancellationToken)) return true;
        if (type == "site-movement" && await db.AdvanceAllocations.AnyAsync(x => x.SiteMovementId == id, cancellationToken)) return true;
        return await db.ContractorStatementLines.AnyAsync(x => x.SourceEntityType == type && x.SourceEntityId == id, cancellationToken);
    }

    private async Task<string?> ValidateFinancialDependenciesAsync(string type, object record, int id, CancellationToken cancellationToken)
    {
        var amount = record.GetType().GetProperty("Amount")?.GetValue(record) as decimal?;
        var total = record.GetType().GetProperty("TotalAmount")?.GetValue(record) as decimal?;
        if (amount is < 0 || total is < 0)
        {
            return "لا يمكن تسجيل قيمة مالية سالبة.";
        }
        if (type == "advance-payment")
        {
            var allocated = await db.AdvanceAllocations.Where(x => x.AdvancePaymentId == id).SumAsync(x => (decimal?)x.Amount, cancellationToken) ?? 0m;
            if ((amount ?? 0m) < allocated)
            {
                return "قيمة الدفعة لا يمكن أن تقل عن إجمالي التخصيصات الحالية.";
            }
        }
        if (type == "supplier-supply")
        {
            var paid = await db.SupplierPayments.Where(x => x.SupplyId == id && (x.Status == "معتمد" || x.Status == "مرحّل"))
                .SumAsync(x => (decimal?)x.Amount, cancellationToken) ?? 0m;
            if ((total ?? 0m) < paid)
            {
                return "إجمالي التوريدة لا يمكن أن يقل عن الدفعات المسجلة عليها.";
            }
        }
        if (type == "supplier-payment")
        {
            var supplyId = (int?)record.GetType().GetProperty("SupplyId")?.GetValue(record);
            if (supplyId is not null)
            {
                var supplyTotal = await db.SupplierSupplies.Where(x => x.Id == supplyId).Select(x => (decimal?)x.TotalAmount).SingleOrDefaultAsync(cancellationToken) ?? 0m;
                var otherPayments = await db.SupplierPayments.Where(x => x.SupplyId == supplyId && x.Id != id && (x.Status == "معتمد" || x.Status == "مرحّل"))
                    .SumAsync(x => (decimal?)x.Amount, cancellationToken) ?? 0m;
                if ((amount ?? 0m) > supplyTotal - otherPayments)
                {
                    return "قيمة الدفع تتجاوز المتبقي على التوريدة.";
                }
            }
        }
        return null;
    }

    private static bool ValidateValues(string type, IReadOnlyDictionary<string, JsonElement> values, out IReadOnlyList<string>? errors)
    {
        var list = values.Keys.Where(key => !EditableProperties[type].Contains(key, StringComparer.Ordinal)).Select(key => $"الحقل {key} غير قابل للتصحيح.").ToList();
        foreach (var value in values)
            if (value.Value.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined) continue;
            else if (value.Value.ValueKind is JsonValueKind.String && string.IsNullOrWhiteSpace(value.Value.GetString()) && value.Key is "Description" or "ItemName" or "Unit") list.Add($"الحقل {value.Key} مطلوب.");
        errors = list;
        return list.Count == 0;
    }

    private static Dictionary<string, object?> Capture(object record, IEnumerable<string> names) => names.ToDictionary(name => name, name => record.GetType().GetProperty(name)?.GetValue(record));

    private static void Apply(object record, IReadOnlyDictionary<string, JsonElement> values, IEnumerable<string> allowed)
    {
        foreach (var (name, json) in values)
        {
            if (!allowed.Contains(name, StringComparer.Ordinal)) continue;
            var property = record.GetType().GetProperty(name, BindingFlags.Public | BindingFlags.Instance);
            if (property is null || !property.CanWrite) continue;
            property.SetValue(record, JsonSerializer.Deserialize(json.GetRawText(), property.PropertyType, new JsonSerializerOptions { PropertyNameCaseInsensitive = true }));
        }
    }

    private static void Recalculate(object record, string type)
    {
        var quantity = record.GetType().GetProperty(type == "site-movement" ? "Quantity" : "Quantity")?.GetValue(record) as decimal?;
        var unitPrice = record.GetType().GetProperty("UnitPrice")?.GetValue(record) as decimal?;
        var total = record.GetType().GetProperty("TotalAmount");
        if (quantity is not null && unitPrice is not null && total is not null && type is not "site-movement" && type is not "supplier-payment" && type is not "advance-payment") total.SetValue(record, decimal.Round(quantity.Value * unitPrice.Value, 2));
    }

    private static string Normalize(string type) => type.Trim().ToLowerInvariant() switch { "site" or "site-movement" => "site-movement", "supplier" or "supplier-supply" => "supplier-supply", "payment" or "supplier-payment" => "supplier-payment", "advance" or "advance-payment" => "advance-payment", _ => type.Trim().ToLowerInvariant() };
    private static string StatusOf(object record) => (string?)record.GetType().GetProperty("Status")?.GetValue(record) ?? "";
    private static void SetStatus(object record, string status) => record.GetType().GetProperty("Status")?.SetValue(record, status);
    private static bool IsCancelled(object record) => StatusOf(record) == "ملغى";
}

public sealed class TransactionValidationException(IReadOnlyList<string> errors) : Exception(string.Join(" ", errors))
{
    public IReadOnlyList<string> Errors { get; } = errors;
}
