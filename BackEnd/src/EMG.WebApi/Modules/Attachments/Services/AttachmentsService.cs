using EMG.Api.Modules.Attachments.Dtos;
using EMG.Domain.Entities;
using EMG.Infrastructure.Db;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace EMG.Api.Modules.Attachments.Services;

public sealed class AttachmentsService(EmgDbContext db, IWebHostEnvironment environment) : IAttachmentsService
{
    private static readonly HashSet<string> AllowedExtensions = [".pdf", ".jpg", ".jpeg", ".png", ".webp", ".xlsx", ".xls", ".docx", ".doc"];
    private const long MaxFileSize = 20 * 1024 * 1024;
    private const string ViewPermission = "attachments.view";
    private const string UploadPermission = "attachments.upload";

    private enum AttachmentAccess { None, Own, Project }

    public async Task<IReadOnlyList<AttachmentSummary>> GetAsync(string entityType, int entityId, int? userId, bool isAdmin, CancellationToken cancellationToken)
    {
        var normalizedType = Normalize(entityType);
        var access = await GetAccessAsync(normalizedType, entityId, userId, isAdmin, ViewPermission, cancellationToken);
        if (access == AttachmentAccess.None) return [];
        return await db.Set<Attachment>().AsNoTracking()
            .Where(item => item.EntityType == normalizedType && item.EntityId == entityId &&
                (access == AttachmentAccess.Project || item.UploadedByUserId == userId))
            .OrderByDescending(item => item.UploadedAtUtc).Select(ToSummary()).ToListAsync(cancellationToken);
    }

    public async Task<AttachmentSummary?> UploadAsync(string entityType, int entityId, IFormFile file, int? userId, bool isAdmin, CancellationToken cancellationToken)
    {
        var normalizedType = Normalize(entityType);
        if (file.Length <= 0 || file.Length > MaxFileSize || !AllowedExtensions.Contains(Path.GetExtension(file.FileName).ToLowerInvariant()) ||
            normalizedType == "project-import" ||
            (await GetAccessAsync(normalizedType, entityId, userId, isAdmin, UploadPermission, cancellationToken)) == AttachmentAccess.None)
            return null;

        var uploadsRoot = Path.Combine(environment.ContentRootPath, "uploads", normalizedType, entityId.ToString());
        Directory.CreateDirectory(uploadsRoot);
        var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
        var storedName = $"{Guid.NewGuid():N}{extension}";
        var fullPath = Path.Combine(uploadsRoot, storedName);
        await using (var stream = File.Create(fullPath)) await file.CopyToAsync(stream, cancellationToken);

        var attachment = new Attachment
        {
            EntityType = normalizedType, EntityId = entityId, OriginalFileName = Path.GetFileName(file.FileName),
            StoredFileName = storedName, RelativePath = Path.Combine("uploads", normalizedType, entityId.ToString(), storedName),
            ContentType = string.IsNullOrWhiteSpace(file.ContentType) ? "application/octet-stream" : file.ContentType,
            SizeInBytes = file.Length, UploadedByUserId = userId
        };
        db.Set<Attachment>().Add(attachment);
        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch
        {
            if (File.Exists(fullPath)) File.Delete(fullPath);
            throw;
        }
        return ToSummary(attachment);
    }

    public async Task<AttachmentDownload?> GetDownloadAsync(int id, int? userId, bool isAdmin, CancellationToken cancellationToken)
    {
        var attachment = await db.Set<Attachment>().AsNoTracking().FirstOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (attachment is null) return null;
        var access = await GetAccessAsync(attachment.EntityType, attachment.EntityId, userId, isAdmin, ViewPermission, cancellationToken);
        if (access == AttachmentAccess.None || (access == AttachmentAccess.Own && attachment.UploadedByUserId != userId))
            return null;
        var uploadsRoot = Path.GetFullPath(Path.Combine(environment.ContentRootPath, "uploads"));
        var path = Path.GetFullPath(Path.Combine(environment.ContentRootPath, attachment.RelativePath));
        var allowedPath = uploadsRoot.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        return path.StartsWith(allowedPath, StringComparison.OrdinalIgnoreCase) && File.Exists(path)
            ? new AttachmentDownload(path, attachment.OriginalFileName, attachment.ContentType)
            : null;
    }

    private async Task<AttachmentAccess> GetAccessAsync(string entityType, int entityId, int? userId, bool isAdmin,
        string permissionCode, CancellationToken cancellationToken)
    {
        if (userId is not int accountId) return AttachmentAccess.None;
        var projectId = entityType switch
        {
            "project-import" => (int?)entityId,
            "supplier-supply" => await db.SupplierSupplies.Where(item => item.Id == entityId).Select(item => (int?)item.ProjectId).FirstOrDefaultAsync(cancellationToken),
            "supplier-payment" => await db.SupplierPayments.Where(item => item.Id == entityId)
                .Select(item => item.ProjectId ?? (item.Supply != null ? (int?)item.Supply.ProjectId : null))
                .FirstOrDefaultAsync(cancellationToken),
            "site-movement" => await db.SiteMovements.Where(item => item.Id == entityId).Select(item => (int?)item.ProjectId).FirstOrDefaultAsync(cancellationToken),
            "excavation" => await db.ExcavationRecords.Where(item => item.Id == entityId).Select(item => (int?)item.ProjectId).FirstOrDefaultAsync(cancellationToken),
            "concrete" => await db.ConcreteRecords.Where(item => item.Id == entityId).Select(item => (int?)item.ProjectId).FirstOrDefaultAsync(cancellationToken),
            "steel" => await db.SteelRecords.Where(item => item.Id == entityId).Select(item => (int?)item.ProjectId).FirstOrDefaultAsync(cancellationToken),
            "finishing" => await db.FinishingRecords.Where(item => item.Id == entityId).Select(item => (int?)item.ProjectId).FirstOrDefaultAsync(cancellationToken),
            "advance-payment" => await db.AdvancePayments.Where(item => item.Id == entityId).Select(item => (int?)item.ProjectId).FirstOrDefaultAsync(cancellationToken),
            _ => null
        };
        if (projectId is not int project || !await db.Projects.AnyAsync(item => item.Id == project, cancellationToken))
            return AttachmentAccess.None;
        if (isAdmin) return AttachmentAccess.Project;

        var grants = await db.UserRoles
            .Where(link => link.UserId == accountId && link.User.IsActive)
            .SelectMany(link => link.Role.RolePermissions.Where(grant => grant.Permission.Code == permissionCode)
                .Select(grant => new { grant.Scope, Role = link.Role.Name }))
            .ToListAsync(cancellationToken);
        if (grants.Any(grant => grant.Scope == "all" && grant.Role is "director" or "accountant"))
            return AttachmentAccess.Project;

        if (!await db.UserProjectAssignments.AnyAsync(item => item.UserId == accountId && item.ProjectId == project && item.IsActive, cancellationToken))
            return AttachmentAccess.None;
        if (grants.Any(grant => grant.Scope is "all" or "assigned" or "project"))
            return AttachmentAccess.Project;
        return grants.Any(grant => grant.Scope == "own") ? AttachmentAccess.Own : AttachmentAccess.None;
    }

    private static string Normalize(string value) => value.Trim().ToLowerInvariant();
    private static System.Linq.Expressions.Expression<Func<Attachment, AttachmentSummary>> ToSummary() => item => new AttachmentSummary(
        item.Id, item.EntityType, item.EntityId, item.OriginalFileName, item.ContentType, item.SizeInBytes, item.UploadedByUserId, item.UploadedAtUtc);
    private static AttachmentSummary ToSummary(Attachment item) => new(item.Id, item.EntityType, item.EntityId, item.OriginalFileName, item.ContentType, item.SizeInBytes, item.UploadedByUserId, item.UploadedAtUtc);
}
