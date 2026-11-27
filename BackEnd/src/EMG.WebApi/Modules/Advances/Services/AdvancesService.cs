using System.Linq.Expressions;
using System.Data;
using EMG.Api.Modules.Advances.Dtos;
using EMG.Domain.Entities;
using EMG.Infrastructure.Db;
using Microsoft.EntityFrameworkCore;

namespace EMG.Api.Modules.Advances.Services;

public sealed partial class AdvancesService(EmgDbContext db) : IAdvancesService
{
    private static readonly HashSet<string> AccountCategories = new(StringComparer.Ordinal)
    {
        "الموقع", "العرب", "المقاول", "المحطة", "الحديد", "المباني", "المحارة", "العزل", "الكهرباء", "السباكة", "المكتب", "غير محدد"
    };
    public async Task<IReadOnlyList<AdvanceSummary>> GetAsync(int? projectId, int? userId, bool isAdmin, CancellationToken cancellationToken)
    {
        var query = db.AdvancePayments.AsNoTracking();
        if (!isAdmin)
        {
            if (userId is null) return [];
            query = query.Where(item => db.UserProjectAssignments.Any(assignment => assignment.ProjectId == item.ProjectId && assignment.UserId == userId && assignment.IsActive));
        }
        if (projectId is not null) query = query.Where(item => item.ProjectId == projectId);
        return await query.OrderByDescending(item => item.PaymentDate).Select(ToSummary()).ToListAsync(cancellationToken);
    }

    public async Task<AdvanceSummary?> CreateAsync(CreateAdvanceRequest request, int? userId, bool isAdmin, CancellationToken cancellationToken)
    {
        if (!isAdmin && (userId is null || !await db.UserProjectAssignments.AnyAsync(item => item.ProjectId == request.ProjectId && item.UserId == userId && item.IsActive, cancellationToken))) return null;
        var project = await db.Projects.FirstOrDefaultAsync(item => item.Id == request.ProjectId, cancellationToken);
        var currency = (request.Currency ?? "EGP").Trim().ToUpperInvariant();
        if (project is null || request.Amount <= 0 || currency is not ("EGP" or "USD")) return null;
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        var advance = new AdvancePayment
        {
            ProjectId = request.ProjectId, Currency = currency,
            ClientName = string.IsNullOrWhiteSpace(request.ClientName) ? project.ClientName ?? string.Empty : request.ClientName.Trim(),
            PayerName = string.IsNullOrWhiteSpace(request.PayerName) ? project.PayerName ?? string.Empty : request.PayerName.Trim(),
            Amount = request.Amount,
            PaymentDate = request.PaymentDate is null ? DateTime.UtcNow.Date : DateTime.SpecifyKind(request.PaymentDate.Value.Date, DateTimeKind.Utc),
            PaymentMethod = string.IsNullOrWhiteSpace(request.PaymentMethod) ? "تحويل" : request.PaymentMethod.Trim(),
            Reference = request.Reference?.Trim(), Notes = request.Notes?.Trim(), CreatedByUserId = userId, Status = "مرحّل"
        };
        db.AdvancePayments.Add(advance);
        await db.SaveChangesAsync(cancellationToken);
        db.WorkflowActions.Add(new WorkflowAction { EntityType = "advance-payment", EntityId = advance.Id, Action = "ترحيل مباشر", FromStatus = "", ToStatus = "مرحّل", UserId = userId, Notes = "تسجيل مباشر وفق سياسة النشر الموحد" });
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return await db.AdvancePayments.AsNoTracking().Where(item => item.Id == advance.Id).Select(ToSummary()).FirstAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<AllocationSummary>> GetAllocationsAsync(int advanceId, int? userId, bool isAdmin, CancellationToken cancellationToken)
    {
        var advance = await GetAccessibleAdvanceAsync(advanceId, userId, isAdmin, cancellationToken);
        if (advance is null) return [];
        return await db.AdvanceAllocations.AsNoTracking().Where(item => item.AdvancePaymentId == advanceId)
            .OrderByDescending(item => item.AllocationDate).Select(item => new AllocationSummary(
                item.Id, item.AdvancePaymentId, item.SiteMovementId, item.SupplierPaymentId, item.AccountCategory, item.Amount, item.AllocationDate, item.Notes ?? string.Empty)).ToListAsync(cancellationToken);
    }

    public async Task<AllocationSummary?> AllocateAsync(int advanceId, AllocateAdvanceRequest request, int? userId, bool isAdmin, CancellationToken cancellationToken)
    {
        var category = string.IsNullOrWhiteSpace(request.AccountCategory) ? "غير محدد" : request.AccountCategory.Trim();
        if (request.Amount <= 0 || (request.SiteMovementId is not null && request.SupplierPaymentId is not null) || (request.SiteMovementId is null && request.SupplierPaymentId is null && category == "غير محدد") || !AccountCategories.Contains(category)) return null;

        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted, cancellationToken);
        try
        {
            // PostgreSQL row-locks the parent advance for the whole validation and
            // insert. This serializes all allocations for the same advance and
            // prevents two requests from both spending the same remaining balance.
            var advance = await LockAdvanceAsync(advanceId, userId, isAdmin, cancellationToken);
            if (advance is null || advance.Status is not ("معتمد" or "مرحّل")) return null;

            var alreadyAllocatedToAdvance = await db.AdvanceAllocations.Where(item => item.AdvancePaymentId == advanceId).SumAsync(item => item.Amount, cancellationToken);
            if (request.Amount > advance.Amount - alreadyAllocatedToAdvance) return null;

            if ((request.SiteMovementId ?? request.SupplierPaymentId) is int targetId)
            {
                // Different receipts can fund the same invoice through its movement or a supplier payment.
                // Serialize target validation across receipts in this project. The receipt lock above still applies.
                if (db.Database.IsNpgsql())
                    await db.Database.ExecuteSqlInterpolatedAsync(
                        $"SELECT pg_advisory_xact_lock(hashtext('emg.advance-allocation'), {advance.ProjectId})", cancellationToken);
                var kind = request.SiteMovementId.HasValue ? "movement" : "supplier";
                var targets = await ReadTargetsAsync(advance, cancellationToken);
                var target = targets.SingleOrDefault(item => item.Kind == kind && item.Id == targetId);
                if (target == null || request.Amount > target.Remaining) return null;
            }
            var allocation = new AdvanceAllocation
            {
                AdvancePaymentId = advanceId, SiteMovementId = request.SiteMovementId, SupplierPaymentId = request.SupplierPaymentId,
                AccountCategory = category,
                Amount = request.Amount, AllocationDate = request.AllocationDate is null ? DateTime.UtcNow.Date : DateTime.SpecifyKind(request.AllocationDate.Value.Date, DateTimeKind.Utc), Notes = request.Notes?.Trim()
            };
            db.AdvanceAllocations.Add(allocation);
            await db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return new AllocationSummary(allocation.Id, advanceId, allocation.SiteMovementId, allocation.SupplierPaymentId, allocation.AccountCategory, allocation.Amount, allocation.AllocationDate, allocation.Notes ?? string.Empty);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }

    private async Task<AdvancePayment?> LockAdvanceAsync(int advanceId, int? userId, bool isAdmin, CancellationToken cancellationToken)
    {
        AdvancePayment? advance;
        if (db.Database.IsNpgsql())
        {
            advance = await db.AdvancePayments.FromSqlInterpolated($"SELECT * FROM advance_payments WHERE \"Id\" = {advanceId} FOR UPDATE").AsNoTracking().SingleOrDefaultAsync(cancellationToken);
        }
        else
        {
            // The production provider is PostgreSQL. This fallback keeps the
            // service usable by provider-agnostic unit tests; SQLite has no
            // equivalent row-lock syntax.
            advance = await db.AdvancePayments.AsNoTracking().SingleOrDefaultAsync(item => item.Id == advanceId, cancellationToken);
        }
        if (advance is null || isAdmin) return advance;
        return await db.AdvancePayments.AsNoTracking().Where(item => item.Id == advanceId)
            .Where(item => db.UserProjectAssignments.Any(assignment => assignment.ProjectId == item.ProjectId && assignment.UserId == userId && assignment.IsActive))
            .SingleOrDefaultAsync(cancellationToken);
    }

    private async Task<AdvancePayment?> GetAccessibleAdvanceAsync(int id, int? userId, bool isAdmin, CancellationToken cancellationToken)
    {
        var query = db.AdvancePayments.AsNoTracking().Where(item => item.Id == id);
        if (!isAdmin)
        {
            if (userId is null) return null;
            query = query.Where(item => db.UserProjectAssignments.Any(assignment => assignment.ProjectId == item.ProjectId && assignment.UserId == userId && assignment.IsActive));
        }
        return await query.FirstOrDefaultAsync(cancellationToken);
    }

    private static Expression<Func<AdvancePayment, AdvanceSummary>> ToSummary() => item => new AdvanceSummary(
        item.Id, item.ProjectId, item.Project.Name, item.ClientName, item.PayerName, item.Amount, item.Currency,
        item.Allocations.Sum(allocation => allocation.Amount), item.Amount - item.Allocations.Sum(allocation => allocation.Amount),
        item.Amount - item.Allocations.Sum(allocation => allocation.Amount) > 0 ? item.Amount - item.Allocations.Sum(allocation => allocation.Amount) : 0,
        item.Allocations.Sum(allocation => allocation.Amount) - item.Amount > 0 ? item.Allocations.Sum(allocation => allocation.Amount) - item.Amount : 0,
        item.Amount - item.Allocations.Sum(allocation => allocation.Amount) < 0 ? "تجاوز التخصيص" : "مسجلة",
        item.PaymentDate, item.PaymentMethod, item.Reference ?? string.Empty, item.Status, item.Notes ?? string.Empty);
}
