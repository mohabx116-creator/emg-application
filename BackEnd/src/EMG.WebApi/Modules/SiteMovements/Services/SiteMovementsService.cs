using System.Linq.Expressions;
using EMG.Api.Modules.SiteMovements.Dtos;
using EMG.Domain.Entities;
using EMG.Infrastructure.Db;
using Microsoft.EntityFrameworkCore;

namespace EMG.Api.Modules.SiteMovements.Services;

public sealed class SiteMovementsService(EmgDbContext db) : ISiteMovementsService
{
    public async Task<IReadOnlyList<SiteMovementSummary>> GetAsync(int? projectId, string? movementType, int? userId, bool isAdmin, CancellationToken cancellationToken)
    {
        var query = db.SiteMovements.AsNoTracking();
        if (!isAdmin)
        {
            if (userId is null) return [];
            query = query.Where(item => db.UserProjectAssignments.Any(assignment =>
                assignment.ProjectId == item.ProjectId && assignment.UserId == userId && assignment.IsActive));
        }
        if (projectId is not null) query = query.Where(item => item.ProjectId == projectId);
        if (!string.IsNullOrWhiteSpace(movementType)) query = query.Where(item => item.MovementType == movementType.Trim());
        return await query.OrderByDescending(item => item.MovementDate).ThenByDescending(item => item.Id)
            .Select(ToSummary()).ToListAsync(cancellationToken);
    }

    public async Task<SiteMovementSummary?> GetByIdAsync(int id, int? userId, bool isAdmin, CancellationToken cancellationToken)
    {
        var query = db.SiteMovements.AsNoTracking().Where(item => item.Id == id);
        if (!isAdmin)
        {
            if (userId is null) return null;
            query = query.Where(item => db.UserProjectAssignments.Any(assignment =>
                assignment.ProjectId == item.ProjectId && assignment.UserId == userId && assignment.IsActive));
        }
        return await query.Select(ToSummary()).FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<SiteMovementSummary?> CreateAsync(CreateSiteMovementRequest request, int? userId, bool isAdmin, CancellationToken cancellationToken)
    {
        if (!isAdmin && (userId is null || !await db.UserProjectAssignments.AnyAsync(item =>
                item.ProjectId == request.ProjectId && item.UserId == userId && item.IsActive, cancellationToken))) return null;
        if (!await db.Projects.AnyAsync(item => item.Id == request.ProjectId, cancellationToken)) return null;
        if (request.SupplierId is not null && !await db.Suppliers.AnyAsync(item => item.Id == request.SupplierId && item.IsActive, cancellationToken)) return null;

        var amount = request.Amount ?? (request.Quantity ?? 0) * (request.UnitPrice ?? 0);
        if (amount < 0) return null;
        var movement = new SiteMovement
        {
            ProjectId = request.ProjectId, SupplierId = request.SupplierId,
            MovementType = string.IsNullOrWhiteSpace(request.MovementType) ? "مصروفات" : request.MovementType.Trim(),
            Description = request.Description.Trim(), Unit = request.Unit?.Trim(), Quantity = request.Quantity,
            UnitPrice = request.UnitPrice, TotalAmount = decimal.Round(amount, 2),
            MovementDate = request.MovementDate is null ? DateTime.UtcNow.Date : DateTime.SpecifyKind(request.MovementDate.Value.Date, DateTimeKind.Utc),
            Status = "مسودة", Reference = request.Reference?.Trim(), Details = request.Details?.Trim(), CreatedByUserId = userId,
            ExpenseCategory = request.ExpenseCategory?.Trim(), PaidTo = request.PaidTo?.Trim(),
            PaymentMethod = request.PaymentMethod?.Trim(), IsPaid = request.IsPaid ?? false
        };
        db.SiteMovements.Add(movement);
        await db.SaveChangesAsync(cancellationToken);
        return await db.SiteMovements.AsNoTracking().Where(item => item.Id == movement.Id).Select(ToSummary()).FirstAsync(cancellationToken);
    }

    public async Task<SiteMovementSummary?> UpdateAsync(int id, CreateSiteMovementRequest request, int? userId, bool isAdmin, CancellationToken cancellationToken)
    {
        var movement = await db.SiteMovements.FirstOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (movement is null || (!isAdmin && (userId is null || !await HasProjectAccessAsync(movement.ProjectId, userId.Value, cancellationToken)))) return null;
        if (movement.Status is not ("مسودة" or "مرفوض") || request.ProjectId != movement.ProjectId) return null;
        if (request.SupplierId is not null && !await db.Suppliers.AnyAsync(item => item.Id == request.SupplierId && item.IsActive, cancellationToken)) return null;

        var amount = request.Amount ?? (request.Quantity ?? 0) * (request.UnitPrice ?? 0);
        if (amount < 0) return null;
        var previousStatus = movement.Status;
        movement.SupplierId = request.SupplierId;
        movement.MovementType = request.MovementType.Trim();
        movement.Description = request.Description.Trim();
        movement.Unit = request.Unit?.Trim();
        movement.Quantity = request.Quantity;
        movement.UnitPrice = request.UnitPrice;
        movement.TotalAmount = decimal.Round(amount, 2);
        movement.MovementDate = request.MovementDate is null ? movement.MovementDate : DateTime.SpecifyKind(request.MovementDate.Value.Date, DateTimeKind.Utc);
        movement.Reference = request.Reference?.Trim();
        movement.Details = request.Details?.Trim();
        movement.ExpenseCategory = request.ExpenseCategory?.Trim();
        movement.PaidTo = request.PaidTo?.Trim();
        movement.PaymentMethod = request.PaymentMethod?.Trim();
        movement.IsPaid = request.IsPaid ?? false;
        db.WorkflowActions.Add(new WorkflowAction { EntityType = "site-movement", EntityId = id, Action = "تعديل", FromStatus = previousStatus, ToStatus = previousStatus, UserId = userId, Notes = "تعديل بيانات الحركة" });
        await db.SaveChangesAsync(cancellationToken);
        return await db.SiteMovements.AsNoTracking().Where(item => item.Id == id).Select(ToSummary()).FirstAsync(cancellationToken);
    }

    public async Task<bool> DeleteAsync(int id, int? userId, bool isAdmin, CancellationToken cancellationToken)
    {
        var movement = await db.SiteMovements.FirstOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (movement is null || movement.Status == "مرحّل" || (!isAdmin && (userId is null || !await HasProjectAccessAsync(movement.ProjectId, userId.Value, cancellationToken)))) return false;
        db.WorkflowActions.Add(new WorkflowAction { EntityType = "site-movement", EntityId = id, Action = "حذف", FromStatus = movement.Status, ToStatus = "محذوف", UserId = userId, Notes = "حذف حركة قبل الترحيل" });
        db.SiteMovements.Remove(movement);
        await db.SaveChangesAsync(cancellationToken);
        return true;
    }

    private async Task<bool> HasProjectAccessAsync(int projectId, int userId, CancellationToken cancellationToken) =>
        await db.UserProjectAssignments.AnyAsync(item => item.ProjectId == projectId && item.UserId == userId && item.IsActive, cancellationToken);

    private static Expression<Func<SiteMovement, SiteMovementSummary>> ToSummary() => item => new SiteMovementSummary(
        item.Id, item.ProjectId, item.Project.Name, item.SupplierId, item.Supplier == null ? string.Empty : item.Supplier.Name,
        item.MovementType, item.Description, item.Unit ?? string.Empty, item.Quantity, item.UnitPrice, item.TotalAmount,
        item.MovementDate, item.Status, item.Reference ?? string.Empty, item.Details ?? string.Empty, item.CreatedByUserId,
        item.ExpenseCategory ?? string.Empty, item.PaidTo ?? string.Empty, item.PaymentMethod ?? string.Empty, item.IsPaid);
}
