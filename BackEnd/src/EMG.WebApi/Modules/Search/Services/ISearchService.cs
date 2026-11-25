using EMG.Api.Modules.Search.Dtos;

namespace EMG.Api.Modules.Search.Services;

public interface ISearchService
{
    Task<IReadOnlyList<SearchResult>> SearchAsync(string query, int? userId, bool isAdmin, CancellationToken cancellationToken);
}
