using System.Linq.Expressions;
using EMG.Api.Modules.Steel.Dtos;
using EMG.Domain.Entities;
using EMG.Infrastructure.Db;
using Microsoft.EntityFrameworkCore;

namespace EMG.Api.Modules.Steel.Services;

public sealed class SteelService(EmgDbContext db) : ISteelService
{
    public async Task<IReadOnlyList<SteelSummary>> GetAsync(int? projectId, int? userId, bool isAdmin, CancellationToken cancellationToken)
    {
        var query = db.SteelRecords.AsNoTracking();
        if (!isAdmin)
        {
            if (userId is null) return [];
            query = query.Where(item => db.UserProjectAssignments.Any(assignment => assignment.ProjectId == item.ProjectId && assignment.UserId == userId && assignment.IsActive));
        }
        if (projectId is not null) query = query.Where(item => item.ProjectId == projectId);
        return await query.OrderByDescending(item => item.OperationDate).Select(ToSummary()).ToListAsync(cancellationToken);
    }

    public async Task<SteelSummary?> CreateAsync(CreateSteelRequest request, int? userId, bool isAdmin, CancellationToken cancellationToken)
    {
        if (!isAdmin && (userId is null || !await db.UserProjectAssignments.AnyAsync(item => item.ProjectId == request.ProjectId && item.UserId == userId && item.IsActive, cancellationToken))) return null;
        if (!await db.Projects.AnyAsync(item => item.Id == request.ProjectId, cancellationToken)) return null;
        if (request.SupplierId is not null && !await db.Suppliers.AnyAsync(item => item.Id == request.SupplierId && item.IsActive, cancellationToken)) return null;
        if (request.Quantity <= 0 || request.UnitPrice < 0 || string.IsNullOrWhiteSpace(request.Description)) return null;
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        var record = new SteelRecord
        {
            ProjectId = request.ProjectId, SupplierId = request.SupplierId, AccountCategory = string.IsNullOrWhiteSpace(request.AccountCategory) ? "الحديد" : request.AccountCategory.Trim(),
            OperationType = string.IsNullOrWhiteSpace(request.OperationType) ? "توريد حديد" : request.OperationType.Trim(),
            SteelType = request.SteelType?.Trim(), BarDiameter = request.BarDiameter?.Trim(), Grade = request.Grade?.Trim(),
            Quantity = request.Quantity, Unit = string.IsNullOrWhiteSpace(request.Unit) ? "طن" : request.Unit.Trim(), UnitPrice = request.UnitPrice,
            TotalAmount = decimal.Round(request.Quantity * request.UnitPrice, 2),
            OperationDate = request.OperationDate is null ? DateTime.UtcNow.Date : DateTime.SpecifyKind(request.OperationDate.Value.Date, DateTimeKind.Utc), Status = "مرحّل",
            DeliveryNoteNumber = request.DeliveryNoteNumber?.Trim(), Description = request.Description.Trim(), Notes = request.Notes?.Trim(), CreatedByUserId = userId
        };
        db.SteelRecords.Add(record);
        await db.SaveChangesAsync(cancellationToken);
        db.WorkflowActions.Add(new WorkflowAction { EntityType = "steel", EntityId = record.Id, Action = "ترحيل مباشر", FromStatus = "", ToStatus = "مرحّل", UserId = userId, Notes = "تسجيل مباشر وفق سياسة النشر الموحد" });
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return await db.SteelRecords.AsNoTracking().Where(item => item.Id == record.Id).Select(ToSummary()).FirstAsync(cancellationToken);
    }

    private static Expression<Func<SteelRecord, SteelSummary>> ToSummary() => item => new SteelSummary(
        item.Id, item.ProjectId, item.Project.Name, item.SupplierId, item.Supplier == null ? string.Empty : item.Supplier.Name,
        item.AccountCategory, item.OperationType, item.SteelType ?? string.Empty, item.BarDiameter ?? string.Empty, item.Grade ?? string.Empty,
        item.Quantity, item.Unit, item.UnitPrice, item.TotalAmount, item.OperationDate, item.DeliveryNoteNumber ?? string.Empty,
        item.Description, item.Status, item.Notes ?? string.Empty);
}
