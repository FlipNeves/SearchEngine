using SearchEngine.Shared.Domain.Entities;

namespace SearchEngine.Shared.Domain.Interfaces;

public interface IPageIndexer
{
    Task IndexAsync(WebPage page, CancellationToken ct = default);
}
