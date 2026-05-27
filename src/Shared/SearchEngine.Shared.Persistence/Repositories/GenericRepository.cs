using MongoDB.Bson;
using MongoDB.Driver;
using SearchEngine.Shared.Persistence.Internal;

namespace SearchEngine.Shared.Persistence.Repositories;

public sealed class GenericRepository<TDocument> : IGenericRepository<TDocument>
    where TDocument : class
{
    private readonly IMongoCollection<TDocument> _collection;

    public GenericRepository(IMongoDatabase database)
        => _collection = CollectionResolver.Resolve<TDocument>(database);

    public Task AddAsync(TDocument document, CancellationToken ct = default)
        => _collection.InsertOneAsync(document, cancellationToken: ct);

    public async Task<TDocument?> GetByIdAsync(string id, CancellationToken ct = default)
    {
        if (!ObjectId.TryParse(id, out var oid)) return null;
        var filter = Builders<TDocument>.Filter.Eq("_id", oid);
        return await _collection.Find(filter).FirstOrDefaultAsync(ct);
    }

    public async Task<IReadOnlyList<TDocument>> ListByIdsAsync(IReadOnlyCollection<string> ids, CancellationToken ct = default)
    {
        var oids = ids.Where(i => ObjectId.TryParse(i, out _)).Select(ObjectId.Parse).ToArray();
        if (oids.Length == 0) return Array.Empty<TDocument>();
        var filter = Builders<TDocument>.Filter.In("_id", oids);
        return await _collection.Find(filter).ToListAsync(ct);
    }
}
