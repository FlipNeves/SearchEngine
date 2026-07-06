using SearchEngine.Shared.Domain.Entities;

namespace SearchEngine.Shared.Domain.Interfaces;

public interface IPagesRepository
{
    Task AddAsync(WebPage page, CancellationToken ct = default);
    Task<WebPage?> GetByIdAsync(string id, CancellationToken ct = default);
    Task<IReadOnlyList<WebPage>> ListByIdsAsync(IReadOnlyCollection<string> ids, CancellationToken ct = default);
    Task<IReadOnlyList<string>> ListTitlesAsync(CancellationToken ct = default);

    Task UpdateDerivedFieldsAsync(string id, int lengthTitle, int lengthContent, string language, CancellationToken ct = default);
    Task<IReadOnlyDictionary<string, (int Title, int Content, string Language)>> GetLengthsByIdsAsync(IReadOnlyCollection<string> ids, CancellationToken ct = default);
}
