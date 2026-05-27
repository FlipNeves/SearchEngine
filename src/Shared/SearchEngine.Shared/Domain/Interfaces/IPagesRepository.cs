using SearchEngine.Shared.Domain.Entities;

namespace SearchEngine.Shared.Domain.Interfaces;

public interface IPagesRepository
{
    Task AddAsync(WebPage page, CancellationToken ct = default);
    Task<WebPage?> GetByIdAsync(string id, CancellationToken ct = default);
    Task<IReadOnlyList<WebPage>> ListByIdsAsync(IReadOnlyCollection<string> ids, CancellationToken ct = default);
}
