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

    public async Task<TransactionOperationResult?> CorrectAsync(string entityType, int entityId, TransactionCorrectionRequest request, int? userId, bool globalAccess, CancellationToken cancellationToken)
    {
        if (userId is null || string.IsNullOrWhiteSpace(request.Reason) || request.Reason.Trim().Length > 2000) return null;
        var type = Normalize(entityType);
        if (!EditableProperties.ContainsKey(type)) return null;
        var record = await FindAsync(type, entityId, cancellationToken);
        if (record is null || !await CanAccessAsync(type, record, userId.Value, globalAccess, cancellationToken) || IsCancelled(record)) return null;
        if (!ValidateValues(type, request.Values, out var validation)) throw new TransactionValidationException(validation!);

        var before = Capture(record, EditableProperties[type]);
        Apply(record, request.Values, EditableProperties[type]);
        Recalculate(record, type);
        var after = Capture(record, EditableProperties[type]);
        if (JsonSerializer.Serialize(before) == JsonSerializer.Serialize(after)) return null;

        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        db.WorkflowActions.Add(new WorkflowAction
        {
            EntityType = type, EntityId = entityId, Action = "تصحيح", FromStatus = StatusOf(record), ToStatus = StatusOf(record), UserId = userId,
            Notes = $"السبب={request.Reason.Trim()};قبل={JsonSerializer.Serialize(before)};بعد={JsonSerializer.Serialize(after)}"
        });
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return new TransactionOperationResult(type, entityId, StatusOf(record), "تصحيح", DateTime.UtcNow);
    }

    public async Task<TransactionOperationResult?> CancelAsync(string entityType, int entityId, TransactionCancellationRequest request, int? userId, bool globalAccess, CancellationToken cancellationToken)
    {
        if (userId is null || string.IsNullOrWhiteSpace(request.Reason) || request.Reason.Trim().Length > 2000) return null;
        var type = Normalize(entityType);
        if (!EditableProperties.ContainsKey(type)) return null;
        var record = await FindAsync(type, entityId, cancellationToken);
        if (record is null || !await CanAccessAsync(type, record, userId.Value, globalAccess, cancellationToken) || IsCancelled(record) || await HasDependentFinancialEffectsAsync(type, entityId, cancellationToken)) return null;
        var previousStatus = StatusOf(record);
        SetStatus(record, "ملغى");
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        db.WorkflowActions.Add(new WorkflowAction { EntityType = type, EntityId = entityId, Action = "إلغاء", FromStatus = previousStatus, ToStatus = "ملغى", UserId = userId, Notes = $"السبب={request.Reason.Trim()}" });
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return new TransactionOperationResult(type, entityId, "ملغى", "إلغاء", DateTime.UtcNow);
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

    private async Task<bool> CanAccessAsync(string type, object record, int userId, bool globalAccess, CancellationToken cancellationToken)
    {
        if (globalAccess) return true;
        var projectValue = record.GetType().GetProperty("ProjectId")?.GetValue(record);
        int? projectId = projectValue is int value ? value : null;
        if (projectId is null && type == "supplier-payment")
        {
            var supplyId = (int?)record.GetType().GetProperty("SupplyId")?.GetValue(record);
            projectId = supplyId is null ? null : await db.SupplierSupplies.Where(x => x.Id == supplyId).Select(x => (int?)x.ProjectId).SingleOrDefaultAsync(cancellationToken);
        }
        return projectId is not null && await db.UserProjectAssignments.AnyAsync(x => x.ProjectId == projectId && x.UserId == userId && x.IsActive, cancellationToken);
    }

    private async Task<bool> HasDependentFinancialEffectsAsync(string type, int id, CancellationToken cancellationToken)
    {
        if (type == "supplier-supply" && await db.SupplierPayments.AnyAsync(x => x.SupplyId == id && (x.Status == "معتمد" || x.Status == "مرحّل"), cancellationToken)) return true;
        if (type == "advance-payment" && await db.AdvanceAllocations.AnyAsync(x => x.AdvancePaymentId == id, cancellationToken)) return true;
        return await db.ContractorStatementLines.AnyAsync(x => x.SourceEntityType == type && x.SourceEntityId == id, cancellationToken);
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
