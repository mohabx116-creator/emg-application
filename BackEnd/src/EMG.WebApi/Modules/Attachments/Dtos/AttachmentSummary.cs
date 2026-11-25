namespace EMG.Api.Modules.Attachments.Dtos;

public sealed record AttachmentSummary(
    int Id,
    string EntityType,
    int EntityId,
    string OriginalFileName,
    string ContentType,
    long SizeInBytes,
    int? UploadedByUserId,
    DateTime UploadedAtUtc);
