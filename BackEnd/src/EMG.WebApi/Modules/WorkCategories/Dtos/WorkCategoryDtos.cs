namespace EMG.Api.Modules.WorkCategories.Dtos;

public sealed record WorkCategorySummary(int Id, int? ProjectId, string Name, string Classification, bool IsActive);
public sealed record CreateWorkCategoryRequest(int? ProjectId, string Name, string? Classification);
