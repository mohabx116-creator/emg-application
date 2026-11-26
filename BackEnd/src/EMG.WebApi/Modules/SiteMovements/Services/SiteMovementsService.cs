using System.Linq.Expressions;
using EMG.Api.Modules.SiteMovements.Dtos;
using EMG.Domain.Entities;
using EMG.Infrastructure.Db;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;
using System.Data;

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
        if (!await ValidateCounterpartyAsync(request, cancellationToken)) return null;

        var amount = request.Amount ?? (request.Quantity ?? 0) * (request.UnitPrice ?? 0);
        if (amount < 0) return null;
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        var movement = new SiteMovement
        {
            ProjectId = request.ProjectId, SupplierId = request.SupplierId, ContractorId = request.ContractorId,
            CounterpartyType = NormalizeCounterparty(request), Currency = NormalizeCurrency(request.Currency),
            FundingSource = request.FundingSource?.Trim(), Recipient = request.Recipient?.Trim(), WorkCategory = request.WorkCategory?.Trim(),
            MovementType = string.IsNullOrWhiteSpace(request.MovementType) ? "مصروفات" : request.MovementType.Trim(),
            Description = request.Description.Trim(), Unit = request.Unit?.Trim(), Quantity = request.Quantity,
            UnitPrice = request.UnitPrice, TotalAmount = decimal.Round(amount, 2),
            MovementDate = request.MovementDate is null ? DateTime.UtcNow.Date : DateTime.SpecifyKind(request.MovementDate.Value.Date, DateTimeKind.Utc),
            Status = "مرحّل", Reference = request.Reference?.Trim(), Details = request.Details?.Trim(), CreatedByUserId = userId,
            ExpenseCategory = request.ExpenseCategory?.Trim(), PaidTo = request.PaidTo?.Trim(),
            PaymentMethod = request.PaymentMethod?.Trim(), IsPaid = request.IsPaid ?? false
        };
        db.SiteMovements.Add(movement);
        await db.SaveChangesAsync(cancellationToken);
        db.WorkflowActions.Add(new WorkflowAction { EntityType = "site-movement", EntityId = movement.Id, Action = "ترحيل مباشر", FromStatus = "", ToStatus = "مرحّل", UserId = userId, Notes = "تسجيل مباشر وفق سياسة النشر الموحد" });
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return await db.SiteMovements.AsNoTracking().Where(item => item.Id == movement.Id).Select(ToSummary()).FirstAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<SiteMovementSummary>?> CreateBatchAsync(CreateSiteMovementBatchRequest request, int? userId, bool isAdmin, CancellationToken cancellationToken)
    {
        if (request.Movements is null || request.Movements.Count == 0) return null;
        var projectIds = request.Movements.Select(item => item.ProjectId).Distinct().ToArray();
        if (projectIds.Length != 1 || !isAdmin && (userId is null || !await db.UserProjectAssignments.AnyAsync(item =>
                item.ProjectId == projectIds[0] && item.UserId == userId && item.IsActive, cancellationToken))) return null;
        if (!await db.Projects.AnyAsync(item => item.Id == projectIds[0], cancellationToken)) return null;
        foreach (var item in request.Movements)
            if (!await ValidateCounterpartyAsync(item, cancellationToken)) return null;

        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        var entities = request.Movements.Select(item => new SiteMovement
        {
            ProjectId = item.ProjectId, SupplierId = item.SupplierId, ContractorId = item.ContractorId,
            CounterpartyType = NormalizeCounterparty(item), Currency = NormalizeCurrency(item.Currency),
            FundingSource = item.FundingSource?.Trim(), Recipient = item.Recipient?.Trim(), WorkCategory = item.WorkCategory?.Trim(), MovementType = item.MovementType.Trim(),
            Description = item.Description.Trim(), Unit = item.Unit?.Trim(), Quantity = item.Quantity, UnitPrice = item.UnitPrice,
            TotalAmount = decimal.Round(item.Amount ?? (item.Quantity ?? 0) * (item.UnitPrice ?? 0), 2),
            MovementDate = item.MovementDate is null ? DateTime.UtcNow.Date : DateTime.SpecifyKind(item.MovementDate.Value.Date, DateTimeKind.Utc),
            Status = "مرحّل", Reference = item.Reference?.Trim(), Details = item.Details?.Trim(), CreatedByUserId = userId,
            ExpenseCategory = item.ExpenseCategory?.Trim(), PaidTo = item.PaidTo?.Trim(), PaymentMethod = item.PaymentMethod?.Trim(), IsPaid = item.IsPaid ?? false
        }).ToList();
        if (entities.Any(item => item.TotalAmount < 0)) return null;
        db.SiteMovements.AddRange(entities);
        await db.SaveChangesAsync(cancellationToken);
        db.WorkflowActions.AddRange(entities.Select(item => new WorkflowAction { EntityType = "site-movement", EntityId = item.Id, Action = "ترحيل مباشر", FromStatus = "", ToStatus = "مرحّل", UserId = userId, Notes = "تسجيل مجموعة حركات في معاملة واحدة" }));
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return await db.SiteMovements.AsNoTracking().Where(item => entities.Select(entity => entity.Id).Contains(item.Id))
            .OrderBy(item => item.Id).Select(ToSummary()).ToListAsync(cancellationToken);
    }

    public async Task<SiteMovementSummary?> UpdateAsync(int id, CreateSiteMovementRequest request, int? userId, bool isAdmin, CancellationToken cancellationToken, bool hasOverridePermission)
    {
        if (await db.SupplierInvoices.AnyAsync(item => item.SiteMovementId == id, cancellationToken)) return null;
        var movement = await db.SiteMovements.FirstOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (movement is null || (!isAdmin && (userId is null || !await CanManageAsync(movement, userId.Value, hasOverridePermission, cancellationToken)))) return null;
        if (movement.Status == "محذوف" || request.ProjectId != movement.ProjectId) return null;
        if (!await ValidateCounterpartyAsync(request, cancellationToken)) return null;

        var amount = request.Amount ?? (request.Quantity ?? 0) * (request.UnitPrice ?? 0);
        if (amount < 0) return null;
        var previousStatus = movement.Status;
        var previousValues = JsonSerializer.Serialize(new { movement.Description, movement.Quantity, movement.UnitPrice, movement.TotalAmount, movement.MovementDate, movement.SupplierId });
        movement.SupplierId = request.SupplierId;
        movement.ContractorId = request.ContractorId;
        movement.CounterpartyType = NormalizeCounterparty(request);
        movement.Currency = NormalizeCurrency(request.Currency);
        movement.FundingSource = request.FundingSource?.Trim();
        movement.Recipient = request.Recipient?.Trim();
        movement.WorkCategory = request.WorkCategory?.Trim();
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
        var updatedValues = JsonSerializer.Serialize(new { movement.Description, movement.Quantity, movement.UnitPrice, movement.TotalAmount, movement.MovementDate, movement.SupplierId });
        db.WorkflowActions.Add(new WorkflowAction { EntityType = "site-movement", EntityId = id, Action = "تصحيح", FromStatus = previousStatus, ToStatus = previousStatus, UserId = userId, Notes = $"قبل={previousValues};بعد={updatedValues}" });
        await db.SaveChangesAsync(cancellationToken);
        return await db.SiteMovements.AsNoTracking().Where(item => item.Id == id).Select(ToSummary()).FirstAsync(cancellationToken);
    }

    public async Task<bool> DeleteAsync(int id, int? userId, bool isAdmin, CancellationToken cancellationToken, bool hasOverridePermission)
    {
        if (await db.SupplierInvoices.AnyAsync(item => item.SiteMovementId == id, cancellationToken)) return false;
        var movement = await db.SiteMovements.FirstOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (movement is null || (!isAdmin && (userId is null || !await CanManageAsync(movement, userId.Value, hasOverridePermission, cancellationToken)))) return false;
        if (await db.AdvanceAllocations.AnyAsync(item => item.SiteMovementId == id, cancellationToken) || await db.ContractorStatementLines.AnyAsync(item => item.SourceEntityType == "site-movement" && item.SourceEntityId == id, cancellationToken)) return false;
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        await db.WorkflowActions.Where(item => item.EntityType == "site-movement" && item.EntityId == id).ExecuteDeleteAsync(cancellationToken);
        await db.Attachments.Where(item => item.EntityType == "site-movement" && item.EntityId == id).ExecuteDeleteAsync(cancellationToken);
        db.SecurityAuditLogs.Add(new EMG.Domain.Security.SecurityAuditLog { UserId = userId, Action = "transaction.deleted", EntityType = "site-movement", EntityId = id, Details = "حذف دائم لحركة الموقع." });
        db.SiteMovements.Remove(movement);
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return true;
    }

    private async Task<bool> HasProjectAccessAsync(int projectId, int userId, CancellationToken cancellationToken) =>
        await db.UserProjectAssignments.AnyAsync(item => item.ProjectId == projectId && item.UserId == userId && item.IsActive, cancellationToken);

    private async Task<bool> CanManageAsync(SiteMovement movement, int userId, bool hasOverridePermission, CancellationToken cancellationToken) =>
        await HasProjectAccessAsync(movement.ProjectId, userId, cancellationToken) &&
        (hasOverridePermission || movement.CreatedByUserId == userId);

    private async Task<bool> ValidateCounterpartyAsync(CreateSiteMovementRequest request, CancellationToken cancellationToken)
    {
        if (request.SupplierId is not null && request.ContractorId is not null) return false;
        var type = NormalizeCounterparty(request);
        if (type == "مورد" && request.SupplierId is null || type == "مقاول" && request.ContractorId is null) return false;
        if (type == "مورد" && !await db.Suppliers.AnyAsync(item => item.Id == request.SupplierId && item.IsActive, cancellationToken)) return false;
        if (type == "مقاول" && !await db.Contractors.AnyAsync(item => item.Id == request.ContractorId && item.IsActive, cancellationToken)) return false;
        return type is "بدون طرف" or "مورد" or "مقاول";
    }

    private static string NormalizeCounterparty(CreateSiteMovementRequest request) =>
        string.IsNullOrWhiteSpace(request.CounterpartyType)
            ? request.SupplierId.HasValue ? "مورد" : request.ContractorId.HasValue ? "مقاول" : "بدون طرف"
            : request.CounterpartyType.Trim();

    private static string NormalizeCurrency(string? currency) => string.IsNullOrWhiteSpace(currency) ? "EGP" : currency.Trim().ToUpperInvariant();

    private Expression<Func<SiteMovement, SiteMovementSummary>> ToSummary() => item => new SiteMovementSummary(
        item.Id, item.ProjectId, item.Project.Name, item.SupplierId, item.Supplier == null ? string.Empty : item.Supplier.Name,
        item.ContractorId, item.Contractor == null ? string.Empty : item.Contractor.Name, item.CounterpartyType, item.Currency,
        item.FundingSource ?? string.Empty, item.Recipient ?? string.Empty, item.WorkCategory ?? string.Empty,
        item.MovementType, item.Description, item.Unit ?? string.Empty, item.Quantity, item.UnitPrice, item.TotalAmount,
        item.MovementDate, item.Status, item.Reference ?? string.Empty, item.Details ?? string.Empty, item.CreatedByUserId,
        item.ExpenseCategory ?? string.Empty, item.PaidTo ?? string.Empty, item.PaymentMethod ?? string.Empty, item.IsPaid,
        db.SupplierInvoices.Where(invoice => invoice.SiteMovementId == item.Id).Select(invoice => (int?)invoice.Id).FirstOrDefault());
}
