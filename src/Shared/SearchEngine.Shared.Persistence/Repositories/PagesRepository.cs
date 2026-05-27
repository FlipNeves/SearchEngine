using SearchEngine.Shared.Domain.Entities;
using SearchEngine.Shared.Domain.Interfaces;
using SearchEngine.Shared.Persistence.DataModels;

namespace SearchEngine.Shared.Persistence.Repositories;

public sealed class PagesRepository : IPagesRepository
{
    private readonly IGenericRepository<WebPageDataModel> _generic;

    public PagesRepository(IGenericRepository<WebPageDataModel> generic) => _generic = generic;

    public async Task AddAsync(WebPage page, CancellationToken ct = default)
    {
        var dataModel = ToDataModel(page);
        await _generic.AddAsync(dataModel, ct);
        page.Id = dataModel.Id!;
    }

    public async Task<WebPage?> GetByIdAsync(string id, CancellationToken ct = default)
    {
        var dm = await _generic.GetByIdAsync(id, ct);
        return dm is null ? null : ToDomain(dm);
    }

    public async Task<IReadOnlyList<WebPage>> ListByIdsAsync(IReadOnlyCollection<string> ids, CancellationToken ct = default)
    {
        var dms = await _generic.ListByIdsAsync(ids, ct);
        return dms.Select(ToDomain).ToArray();
    }

    private static WebPageDataModel ToDataModel(WebPage p) => new()
    {
        Id = string.IsNullOrWhiteSpace(p.Id) ? null : p.Id,
        Url = p.Url,
        Title = p.Title,
        Content = p.Content,
        CrawledAtUtc = p.CrawledAtUtc
    };

    private static WebPage ToDomain(WebPageDataModel d) => new()
    {
        Id = d.Id ?? string.Empty,
        Url = d.Url,
        Title = d.Title,
        Content = d.Content,
        CrawledAtUtc = d.CrawledAtUtc
    };
}
