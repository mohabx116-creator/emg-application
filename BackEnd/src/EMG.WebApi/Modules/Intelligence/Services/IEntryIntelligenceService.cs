using EMG.Api.Modules.Intelligence.Dtos;

namespace EMG.Api.Modules.Intelligence.Services;

public interface IEntryIntelligenceService
{
    Task<EntrySuggestionResponse> SuggestAsync(EntrySuggestionRequest request, CancellationToken cancellationToken);
}
