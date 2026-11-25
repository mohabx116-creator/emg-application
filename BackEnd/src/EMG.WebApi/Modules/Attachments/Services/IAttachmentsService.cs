using EMG.Api.Modules.Attachments.Dtos;
using Microsoft.AspNetCore.Http;

namespace EMG.Api.Modules.Attachments.Services;

public sealed record AttachmentDownload(string FilePath, string FileName, string ContentType);

public interface IAttachmentsService
{
    Task<IReadOnlyList<AttachmentSummary>> GetAsync(string entityType, int entityId, int? userId, bool isAdmin, CancellationToken cancellationToken);
    Task<AttachmentSummary?> UploadAsync(string entityType, int entityId, IFormFile file, int? userId, bool isAdmin, CancellationToken cancellationToken);
    Task<AttachmentDownload?> GetDownloadAsync(int id, int? userId, bool isAdmin, CancellationToken cancellationToken);
}
