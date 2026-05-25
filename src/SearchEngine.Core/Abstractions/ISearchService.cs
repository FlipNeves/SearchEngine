using SearchEngine.Core.Models;

namespace SearchEngine.Core.Abstractions;

public interface ISearchService
{
    Task IndexPageAsync(WebPageDocument page, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<WebPageDocument>> ExecuteSearchAsync(string query, CancellationToken cancellationToken = default);
}
