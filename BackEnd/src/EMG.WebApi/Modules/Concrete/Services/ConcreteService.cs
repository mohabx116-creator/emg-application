using System.Linq.Expressions;
using EMG.Api.Modules.Concrete.Dtos;
using EMG.Domain.Entities;
using EMG.Infrastructure.Db;
using Microsoft.EntityFrameworkCore;

namespace EMG.Api.Modules.Concrete.Services;

public sealed class ConcreteService(EmgDbContext db) : IConcreteService
{
    public async Task<IReadOnlyList<ConcreteSummary>> GetAsync(int? projectId, int? userId, bool isAdmin, CancellationToken cancellationToken)
    {
        var query = db.ConcreteRecords.AsNoTracking();
        if (!isAdmin)
        {
            if (userId is null) return [];
            query = query.Where(item => db.UserProjectAssignments.Any(assignment => assignment.ProjectId == item.ProjectId && assignment.UserId == userId && assignment.IsActive));
        }
        if (projectId is not null) query = query.Where(item => item.ProjectId == projectId);
        return await query.OrderByDescending(item => item.PourDate).Select(ToSummary()).ToListAsync(cancellationToken);
    }

    public async Task<ConcreteSummary?> CreateAsync(CreateConcreteRequest request, int? userId, bool isAdmin, CancellationToken cancellationToken)
    {
        if (!isAdmin && (userId is null || !await db.UserProjectAssignments.AnyAsync(item => item.ProjectId == request.ProjectId && item.UserId == userId && item.IsActive, cancellationToken))) return null;
        if (!await db.Projects.AnyAsync(item => item.Id == request.ProjectId, cancellationToken)) return null;
        if (request.SupplierId is not null && !await db.Suppliers.AnyAsync(item => item.Id == request.SupplierId && item.IsActive, cancellationToken)) return null;
        if (request.Quantity <= 0 || request.UnitPrice < 0 || string.IsNullOrWhiteSpace(request.ElementType)) return null;
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        var record = new ConcreteRecord
        {
            ProjectId = request.ProjectId, SupplierId = request.SupplierId, AccountCategory = string.IsNullOrWhiteSpace(request.AccountCategory) ? "المقاول" : request.AccountCategory.Trim(),
            ConcreteType = string.IsNullOrWhiteSpace(request.ConcreteType) ? "خرسانة مسلحة" : request.ConcreteType.Trim(),
            ElementType = request.ElementType.Trim(), MixGrade = request.MixGrade?.Trim(), Quantity = request.Quantity,
            Unit = string.IsNullOrWhiteSpace(request.Unit) ? "م3" : request.Unit.Trim(), UnitPrice = request.UnitPrice,
            TotalAmount = decimal.Round(request.Quantity * request.UnitPrice, 2),
            PourDate = request.PourDate is null ? DateTime.UtcNow.Date : DateTime.SpecifyKind(request.PourDate.Value.Date, DateTimeKind.Utc), Status = "مرحّل",
            DeliveryNoteNumber = request.DeliveryNoteNumber?.Trim(), Description = request.Description.Trim(), Notes = request.Notes?.Trim(), CreatedByUserId = userId
        };
        db.ConcreteRecords.Add(record);
        await db.SaveChangesAsync(cancellationToken);
        db.WorkflowActions.Add(new WorkflowAction { EntityType = "concrete", EntityId = record.Id, Action = "ترحيل مباشر", FromStatus = "", ToStatus = "مرحّل", UserId = userId, Notes = "تسجيل مباشر وفق سياسة النشر الموحد" });
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return await db.ConcreteRecords.AsNoTracking().Where(item => item.Id == record.Id).Select(ToSummary()).FirstAsync(cancellationToken);
    }

    private static Expression<Func<ConcreteRecord, ConcreteSummary>> ToSummary() => item => new ConcreteSummary(
        item.Id, item.ProjectId, item.Project.Name, item.SupplierId, item.Supplier == null ? string.Empty : item.Supplier.Name,
        item.AccountCategory, item.ConcreteType, item.ElementType, item.MixGrade ?? string.Empty, item.Quantity, item.Unit, item.UnitPrice, item.TotalAmount,
        item.PourDate, item.DeliveryNoteNumber ?? string.Empty, item.Description, item.Status, item.Notes ?? string.Empty);
}
