using MongoDB.Bson;
using MongoDB.Driver;
using SearchEngine.FromScratch.Infrastructure.DataModels;
using SearchEngine.Shared.Persistence.DataModels;
using SearchEngine.Shared.Persistence.Internal;

namespace SearchEngine.FromScratch.Infrastructure.Daos;

public sealed class IndexStatsDao : IIndexStatsDao
{
    private readonly IMongoCollection<IndexStatsDataModel> _stats;
    private readonly IMongoCollection<WebPageDataModel> _pages;

    public IndexStatsDao(IMongoDatabase database)
    {
        _stats = CollectionResolver.Resolve<IndexStatsDataModel>(database);
        _pages = CollectionResolver.Resolve<WebPageDataModel>(database);
    }

    public async Task<IndexStatsDataModel> GetAsync(CancellationToken ct = default)
    {
        var doc = await _stats.Find(s => s.Id == IndexStatsDataModel.GlobalId).FirstOrDefaultAsync(ct);
        return doc ?? new IndexStatsDataModel();
    }

    public async Task RecomputeAsync(CancellationToken ct = default)
    {
        var pipeline = PipelineDefinition<WebPageDataModel, BsonDocument>.Create(
            new BsonDocument("$group", new BsonDocument
            {
                { "_id", BsonNull.Value },
                { "total", new BsonDocument("$sum", 1) },
                { "avgT", new BsonDocument("$avg", "$lengthTitle") },
                { "avgC", new BsonDocument("$avg", "$lengthContent") }
            }));

        var result = await _pages.Aggregate(pipeline).FirstOrDefaultAsync(ct);

        var stats = new IndexStatsDataModel
        {
            Id = IndexStatsDataModel.GlobalId,
            TotalDocs = result?["total"].ToInt64() ?? 0,
            AvgLengthTitle = ReadDouble(result, "avgT"),
            AvgLengthContent = ReadDouble(result, "avgC")
        };

        await _stats.ReplaceOneAsync(
            s => s.Id == IndexStatsDataModel.GlobalId,
            stats,
            new ReplaceOptions { IsUpsert = true },
            ct);
    }

    private static double ReadDouble(BsonDocument? doc, string field)
        => doc is not null && doc.TryGetValue(field, out var val) && val != BsonNull.Value
            ? val.ToDouble()
            : 0.0;
}
