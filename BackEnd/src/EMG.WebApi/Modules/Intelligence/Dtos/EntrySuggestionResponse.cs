namespace EMG.Api.Modules.Intelligence.Dtos;

public sealed record EntrySuggestionResponse(
    string SuggestedMovementType,
    string SuggestedEntryKind,
    string SuggestedExpenseCategory,
    string SuggestedUnit,
    string? MatchedSupplierName,
    decimal Confidence,
    bool RequiresConfirmation,
    IReadOnlyList<string> Alternatives,
    IReadOnlyList<string> Checks,
    string Explanation);
