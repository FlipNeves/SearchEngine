using MongoDB.Bson;
using MongoDB.Driver;
using SearchEngine.Shared.Domain.Entities;
using SearchEngine.Shared.Domain.Interfaces;
using SearchEngine.Shared.Persistence.DataModels;
using SearchEngine.Shared.Persistence.Internal;

namespace SearchEngine.Shared.Persistence.Repositories;

public sealed class PagesRepository : IPagesRepository
{
    private readonly IGenericRepository<WebPageDataModel> _generic;
    private readonly IMongoCollection<WebPageDataModel> _collection;

    public PagesRepository(IGenericRepository<WebPageDataModel> generic, IMongoDatabase database)
    {
        _generic = generic;
        _collection = CollectionResolver.Resolve<WebPageDataModel>(database);
    }

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

    public async Task UpdateLengthsAsync(string id, int lengthTitle, int lengthContent, CancellationToken ct = default)
    {
        if (!ObjectId.TryParse(id, out var oid)) return;

        var filter = Builders<WebPageDataModel>.Filter.Eq("_id", oid);
        var update = Builders<WebPageDataModel>.Update
            .Set(d => d.LengthTitle, lengthTitle)
            .Set(d => d.LengthContent, lengthContent);

        await _collection.UpdateOneAsync(filter, update, cancellationToken: ct);
    }

    public async Task<IReadOnlyDictionary<string, (int Title, int Content)>> GetLengthsByIdsAsync(IReadOnlyCollection<string> ids, CancellationToken ct = default)
    {
        if (ids.Count == 0)
            return new Dictionary<string, (int, int)>();

        var dms = await _generic.ListByIdsAsync(ids, ct);
        return dms.ToDictionary(d => d.Id!, d => (d.LengthTitle, d.LengthContent));
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
