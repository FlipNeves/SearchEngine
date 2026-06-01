using SearchEngine.Shared.Dtos;

namespace SearchEngine.Shared.Domain.Interfaces;

public interface ISearchEngine
{
    Task<SearchResponseDto> SearchAsync(string query, int top, bool autoCorrect = true, CancellationToken ct = default);
}
