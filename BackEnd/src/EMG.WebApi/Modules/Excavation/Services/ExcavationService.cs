using System.Linq.Expressions;
using EMG.Api.Modules.Excavation.Dtos;
using EMG.Domain.Entities;
using EMG.Infrastructure.Db;
using Microsoft.EntityFrameworkCore;

namespace EMG.Api.Modules.Excavation.Services;

public sealed class ExcavationService(EmgDbContext db) : IExcavationService
{
    public async Task<IReadOnlyList<ExcavationSummary>> GetAsync(int? projectId, int? userId, bool isAdmin, CancellationToken cancellationToken)
    {
        var query = db.ExcavationRecords.AsNoTracking();
        if (!isAdmin)
        {
            if (userId is null) return [];
            query = query.Where(item => db.UserProjectAssignments.Any(assignment => assignment.ProjectId == item.ProjectId && assignment.UserId == userId && assignment.IsActive));
        }
        if (projectId is not null) query = query.Where(item => item.ProjectId == projectId);
        return await query.OrderByDescending(item => item.ExcavationDate).Select(ToSummary()).ToListAsync(cancellationToken);
    }

    public async Task<ExcavationSummary?> CreateAsync(CreateExcavationRequest request, int? userId, bool isAdmin, CancellationToken cancellationToken)
    {
        if (!isAdmin && (userId is null || !await db.UserProjectAssignments.AnyAsync(item => item.ProjectId == request.ProjectId && item.UserId == userId && item.IsActive, cancellationToken))) return null;
        if (!await db.Projects.AnyAsync(item => item.Id == request.ProjectId, cancellationToken)) return null;
        if (request.SupplierId is not null && !await db.Suppliers.AnyAsync(item => item.Id == request.SupplierId && item.IsActive, cancellationToken)) return null;

        var method = string.IsNullOrWhiteSpace(request.CalculationMethod) ? "كمية مباشرة" : request.CalculationMethod.Trim();
        var quantity = method switch
        {
            "مساحة" => (request.Length ?? 0) * (request.Width ?? 0),
            "حجم" => (request.Length ?? 0) * (request.Width ?? 0) * (request.Depth ?? 0),
            _ => request.DirectQuantity ?? 0
        };
        if (quantity <= 0 || request.UnitPrice < 0 || method is not ("كمية مباشرة" or "مساحة" or "حجم")) return null;

        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        var record = new ExcavationRecord
        {
            ProjectId = request.ProjectId, SupplierId = request.SupplierId, AccountCategory = string.IsNullOrWhiteSpace(request.AccountCategory) ? "العرب" : request.AccountCategory.Trim(), CalculationMethod = method,
            Length = request.Length, Width = request.Width, Depth = request.Depth, CalculatedQuantity = decimal.Round(quantity, 3),
            Unit = string.IsNullOrWhiteSpace(request.Unit) ? "م3" : request.Unit.Trim(), UnitPrice = request.UnitPrice,
            TotalAmount = decimal.Round(quantity * request.UnitPrice, 2), SoilType = request.SoilType?.Trim(),
            Description = request.Description.Trim(), ExcavationDate = request.ExcavationDate is null ? DateTime.UtcNow.Date : DateTime.SpecifyKind(request.ExcavationDate.Value.Date, DateTimeKind.Utc), Status = "مرحّل",
            Notes = request.Notes?.Trim(), CreatedByUserId = userId
        };
        db.ExcavationRecords.Add(record);
        await db.SaveChangesAsync(cancellationToken);
        db.WorkflowActions.Add(new WorkflowAction { EntityType = "excavation", EntityId = record.Id, Action = "ترحيل مباشر", FromStatus = "", ToStatus = "مرحّل", UserId = userId, Notes = "تسجيل مباشر وفق سياسة النشر الموحد" });
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return await db.ExcavationRecords.AsNoTracking().Where(item => item.Id == record.Id).Select(ToSummary()).FirstAsync(cancellationToken);
    }

    private static Expression<Func<ExcavationRecord, ExcavationSummary>> ToSummary() => item => new ExcavationSummary(
        item.Id, item.ProjectId, item.Project.Name, item.SupplierId, item.Supplier == null ? string.Empty : item.Supplier.Name,
        item.AccountCategory, item.CalculationMethod, item.Length, item.Width, item.Depth, item.CalculatedQuantity, item.Unit, item.UnitPrice,
        item.TotalAmount, item.SoilType ?? string.Empty, item.Description, item.ExcavationDate, item.Status, item.Notes ?? string.Empty);
}
