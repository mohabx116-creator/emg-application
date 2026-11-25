namespace EMG.Api.Modules.Imports.Dtos;

public sealed record WorkbookPreview(
    string FileName,
    long SizeBytes,
    bool IsValid,
    bool RequiresReview,
    IReadOnlyList<WorkbookSheetPreview> Sheets,
    IReadOnlyList<string> Warnings);

public sealed record WorkbookMigrationPlan(
    string Mode,
    string SuggestedProjectName,
    string SuggestedProjectCode,
    IReadOnlyList<WorkbookSheetPlan> Sheets,
    IReadOnlyList<string> Warnings,
    string? SuggestedClientName = null,
    string? SuggestedOwnerName = null,
    string? SuggestedPayerName = null,
    string? SuggestedLocation = null);

public sealed record WorkbookSheetPlan(
    int Order,
    string OriginalName,
    string Classification,
    bool Recognized,
    bool WillCreateData,
    int SourceRows = 0,
    int CandidateRows = 0,
    string DetectionProfile = "غير محدد",
    string Confidence = "غير محددة",
    IReadOnlyList<string>? Diagnostics = null);

public sealed record ImportedProjectResult(
    int ProjectId,
    string Name,
    string Code,
    string SourceFileName,
    int AttachmentId,
    string Status,
    int SiteMovementsImported,
    int ExcavationRecordsImported,
    int ConcreteRecordsImported,
    int SteelRecordsImported,
    int FinishingRecordsImported,
    int SupplierSuppliesImported,
    int SupplierPaymentsImported,
    int AdvancePaymentsImported,
    int AdvanceAllocationsImported,
    IReadOnlyList<string> Warnings);

public sealed record WorkbookSheetPreview(
    int Order,
    string OriginalName,
    string Classification,
    bool Recognized,
    bool IsSourceData);
