using System.Linq.Expressions;
using EMG.Api.Modules.Finishing.Dtos;
using EMG.Domain.Entities;
using EMG.Infrastructure.Db;
using Microsoft.EntityFrameworkCore;

namespace EMG.Api.Modules.Finishing.Services;

public sealed class FinishingService(EmgDbContext db) : IFinishingService
{
    public async Task<IReadOnlyList<FinishingSummary>> GetAsync(int? projectId, int? userId, bool isAdmin, CancellationToken cancellationToken)
    {
        var query = db.FinishingRecords.AsNoTracking();
        if (!isAdmin)
        {
            if (userId is null) return [];
            query = query.Where(item => db.UserProjectAssignments.Any(assignment => assignment.ProjectId == item.ProjectId && assignment.UserId == userId && assignment.IsActive));
        }
        if (projectId is not null) query = query.Where(item => item.ProjectId == projectId);
        return await query.OrderByDescending(item => item.WorkDate).Select(ToSummary()).ToListAsync(cancellationToken);
    }

    public async Task<FinishingSummary?> CreateAsync(CreateFinishingRequest request, int? userId, bool isAdmin, CancellationToken cancellationToken)
    {
        if (!isAdmin && (userId is null || !await db.UserProjectAssignments.AnyAsync(item => item.ProjectId == request.ProjectId && item.UserId == userId && item.IsActive, cancellationToken))) return null;
        if (!await db.Projects.AnyAsync(item => item.Id == request.ProjectId, cancellationToken)) return null;
        if (request.SupplierId is not null && !await db.Suppliers.AnyAsync(item => item.Id == request.SupplierId && item.IsActive, cancellationToken)) return null;
        if (request.Quantity <= 0 || request.UnitPrice < 0 || string.IsNullOrWhiteSpace(request.WorkType) || string.IsNullOrWhiteSpace(request.Description)) return null;
        var record = new FinishingRecord
        {
            ProjectId = request.ProjectId, SupplierId = request.SupplierId, AccountCategory = string.IsNullOrWhiteSpace(request.AccountCategory) ? "غير محدد" : request.AccountCategory.Trim(), WorkType = request.WorkType.Trim(), AreaName = request.AreaName?.Trim(),
            Quantity = request.Quantity, Unit = string.IsNullOrWhiteSpace(request.Unit) ? "م2" : request.Unit.Trim(), UnitPrice = request.UnitPrice,
            TotalAmount = decimal.Round(request.Quantity * request.UnitPrice, 2), WorkDate = request.WorkDate is null ? DateTime.UtcNow.Date : DateTime.SpecifyKind(request.WorkDate.Value.Date, DateTimeKind.Utc),
            Description = request.Description.Trim(), Notes = request.Notes?.Trim(), CreatedByUserId = userId
        };
        db.FinishingRecords.Add(record);
        await db.SaveChangesAsync(cancellationToken);
        return await db.FinishingRecords.AsNoTracking().Where(item => item.Id == record.Id).Select(ToSummary()).FirstAsync(cancellationToken);
    }

    private static Expression<Func<FinishingRecord, FinishingSummary>> ToSummary() => item => new FinishingSummary(
        item.Id, item.ProjectId, item.Project.Name, item.SupplierId, item.Supplier == null ? string.Empty : item.Supplier.Name,
        item.AccountCategory, item.WorkType, item.AreaName ?? string.Empty, item.Quantity, item.Unit, item.UnitPrice, item.TotalAmount, item.WorkDate,
        item.Description, item.Status, item.Notes ?? string.Empty);
}
