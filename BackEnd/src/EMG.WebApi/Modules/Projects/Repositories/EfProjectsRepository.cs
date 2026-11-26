using System.Linq.Expressions;
using EMG.Api.Modules.Imports.Services;
using EMG.Api.Modules.Projects.Dtos;
using EMG.Domain.Entities;
using EMG.Infrastructure.Db;
using Microsoft.EntityFrameworkCore;

namespace EMG.Api.Modules.Projects.Repositories;

public sealed class EfProjectsRepository(EmgDbContext db) : IProjectsRepository
{
    public async Task<IReadOnlyList<ProjectSummary>> GetProjectsAsync(int? userId, bool isAdmin, CancellationToken cancellationToken)
    {
        var query = db.Projects.AsNoTracking();
        if (!isAdmin)
        {
            if (userId is null) return [];
            query = query.Where(project => db.UserProjectAssignments.Any(assignment =>
                assignment.ProjectId == project.Id && assignment.UserId == userId && assignment.IsActive));
        }

        return await query.OrderBy(project => project.Name).Select(project => new ProjectSummary(
            project.Id, project.Name, project.Code, project.ClientName ?? string.Empty, project.Location ?? string.Empty,
            project.Status, project.EngineerName ?? string.Empty,
            db.SiteMovements.Count(movement => movement.ProjectId == project.Id && movement.Status == "للمراجعة"),
            (db.SiteMovements.Where(movement => movement.ProjectId == project.Id && movement.MovementType == "مصروفات" && (movement.Status == "معتمد" || movement.Status == "مرحّل")).Sum(movement => (decimal?)movement.TotalAmount) ?? 0m)
                + (db.SupplierPayments.Where(payment => payment.ProjectId == project.Id && (payment.Status == "معتمد" || payment.Status == "مرحّل")).Sum(payment => (decimal?)payment.Amount) ?? 0m),
            db.AdvancePayments.Where(advance => advance.ProjectId == project.Id && advance.Currency == "EGP" && (advance.Status == "معتمد" || advance.Status == "مرحّل")).Sum(advance => (decimal?)advance.Amount) ?? 0m)).ToListAsync(cancellationToken);
    }

    public async Task<ProjectSummary?> GetProjectAsync(int id, int? userId, bool isAdmin, CancellationToken cancellationToken)
    {
        var query = db.Projects.AsNoTracking().Where(item => item.Id == id);
        if (!isAdmin)
        {
            if (userId is null) return null;
            query = query.Where(item => db.UserProjectAssignments.Any(assignment =>
                assignment.ProjectId == item.Id && assignment.UserId == userId && assignment.IsActive));
        }

        var project = await query.Select(item => new ProjectSummary(
            item.Id, item.Name, item.Code, item.ClientName ?? string.Empty, item.Location ?? string.Empty,
            item.Status, item.EngineerName ?? string.Empty,
            db.SiteMovements.Count(movement => movement.ProjectId == item.Id && movement.Status == "للمراجعة"),
            (db.SiteMovements.Where(movement => movement.ProjectId == item.Id && movement.MovementType == "مصروفات" && (movement.Status == "معتمد" || movement.Status == "مرحّل")).Sum(movement => (decimal?)movement.TotalAmount) ?? 0m)
                + (db.SupplierPayments.Where(payment => payment.ProjectId == item.Id && (payment.Status == "معتمد" || payment.Status == "مرحّل")).Sum(payment => (decimal?)payment.Amount) ?? 0m),
            db.AdvancePayments.Where(advance => advance.ProjectId == item.Id && advance.Currency == "EGP" && (advance.Status == "معتمد" || advance.Status == "مرحّل")).Sum(advance => (decimal?)advance.Amount) ?? 0m)).FirstOrDefaultAsync(cancellationToken);
        return project;
    }

    public async Task<ProjectSummary> CreateAsync(CreateProjectRequest request, CancellationToken cancellationToken)
    {
        var entity = new Project
        {
            Name = request.Name.Trim(), Code = request.Code.Trim(), ClientName = request.ClientName?.Trim(),
            OwnerName = request.OwnerName?.Trim(), PayerName = request.PayerName?.Trim(), Location = request.Location?.Trim(),
            Status = string.IsNullOrWhiteSpace(request.Status) ? "نشط" : request.Status.Trim(),
            EngineerName = request.EngineerName?.Trim(), OfficeSharePercentage = request.OfficeSharePercentage
        };
        db.Projects.Add(entity);
        await db.SaveChangesAsync(cancellationToken);
        return ToSummary(entity);
    }

    public async Task<ProjectSummary?> UpdateAsync(int id, UpdateProjectRequest request, CancellationToken cancellationToken)
    {
        var entity = await db.Projects.FirstOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (entity is null) return null;
        entity.Name = request.Name.Trim(); entity.Code = request.Code.Trim(); entity.ClientName = request.ClientName?.Trim();
        entity.OwnerName = request.OwnerName?.Trim(); entity.PayerName = request.PayerName?.Trim(); entity.Location = request.Location?.Trim();
        entity.Status = string.IsNullOrWhiteSpace(request.Status) ? "نشط" : request.Status.Trim();
        entity.EngineerName = request.EngineerName?.Trim(); entity.OfficeSharePercentage = request.OfficeSharePercentage;
        await db.SaveChangesAsync(cancellationToken);
        return ToSummary(entity);
    }

    public async Task<ProjectSummary?> DecideImportAsync(int id, bool approve, string? notes, int? userId, CancellationToken cancellationToken)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        await ImportProjectLock.AcquireAsync(db, id, cancellationToken);
        var entity = await db.Projects.FirstOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (entity is null || entity.Status != "تحت المراجعة") return null;

        var isImported = await db.Attachments.AnyAsync(item => item.EntityType == "project-import" && item.EntityId == id, cancellationToken);
        if (!isImported) return null;

        var action = approve ? "اعتماد ترحيل المشروع" : "رفض ترحيل المشروع";
        if (approve)
        {
            if (await db.SupplierSupplies.AnyAsync(item => item.ProjectId == id && item.Notes != null && item.Notes.StartsWith("استيراد:") &&
                item.Status != "معتمد" && item.Status != "مرحّل", cancellationToken))
                throw new InvalidOperationException("اعتمد التوريدات المستوردة أولاً من خلال القبول الفني والاعتماد المالي بواسطة مستخدمين مختلفين، ثم أعد محاولة اعتماد ترحيل المشروع.");

            // This decision can activate the project. Workbook notes and references do not authorize recognition.
            var hasIncompleteImportedRows =
                await db.SiteMovements.AnyAsync(item => item.ProjectId == id && item.Reference != null && item.Reference.StartsWith("استيراد:") && item.Status == "تحت المراجعة", cancellationToken)
                || await db.ExcavationRecords.AnyAsync(item => item.ProjectId == id && item.Notes != null && item.Notes.StartsWith("استيراد:") && item.Status == "تحت المراجعة", cancellationToken)
                || await db.ConcreteRecords.AnyAsync(item => item.ProjectId == id && item.Notes != null && item.Notes.StartsWith("استيراد:") && item.Status == "تحت المراجعة", cancellationToken)
                || await db.SteelRecords.AnyAsync(item => item.ProjectId == id && item.Notes != null && item.Notes.StartsWith("استيراد:") && item.Status == "تحت المراجعة", cancellationToken)
                || await db.FinishingRecords.AnyAsync(item => item.ProjectId == id && item.Notes != null && item.Notes.StartsWith("استيراد:") && item.Status == "تحت المراجعة", cancellationToken)
                || await db.AdvancePayments.AnyAsync(item => item.ProjectId == id && item.Notes != null && item.Notes.StartsWith("استيراد:") && item.Status == "تحت المراجعة", cancellationToken)
                || await db.SupplierSupplies.AnyAsync(item => item.ProjectId == id && item.Notes != null && item.Notes.StartsWith("استيراد:") && item.Status == "تحت المراجعة", cancellationToken)
                || await db.SupplierPayments.AnyAsync(item => item.ProjectId == id && item.Notes != null && item.Notes.StartsWith("استيراد") && item.Status == "تحت المراجعة", cancellationToken);

            if (hasIncompleteImportedRows)
                throw new InvalidOperationException("لا يمكن اعتماد المشروع قبل مراجعة الصفوف المستوردة التي تحتوي على قيمة أو سعر ناقص.");

        }
        entity.Status = approve ? "نشط" : "مرفوض";
        db.SecurityAuditLogs.Add(new EMG.Domain.Security.SecurityAuditLog
        {
            UserId = userId,
            Action = action,
            EntityType = "project-import",
            EntityId = id,
            Details = string.IsNullOrWhiteSpace(notes) ? action : $"{action}: {notes.Trim()}",
            CreatedAtUtc = DateTime.UtcNow
        });
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return ToSummary(entity);
    }

    private static Expression<Func<Project, ProjectSummary>> ToSummary() => project => new ProjectSummary(
        project.Id, project.Name, project.Code, project.ClientName ?? string.Empty, project.Location ?? string.Empty,
        project.Status, project.EngineerName ?? string.Empty, 0, 0, 0);

    private static ProjectSummary ToSummary(Project project) => new(
        project.Id, project.Name, project.Code, project.ClientName ?? string.Empty, project.Location ?? string.Empty,
        project.Status, project.EngineerName ?? string.Empty, 0, 0, 0);
}
