using System.Runtime.CompilerServices;
using Microsoft.Extensions.Logging;
using MongoDB.Bson;
using MongoDB.Driver;
using SearchEngine.FromScratch.Infrastructure.DataModels;
using SearchEngine.Shared.Persistence.Internal;

namespace SearchEngine.FromScratch.Infrastructure.Daos;

public sealed class InvertedIndexDao : IInvertedIndexDao
{
    private readonly IMongoCollection<InvertedIndexDataModel> _collection;
    private readonly ILogger<InvertedIndexDao> _logger;

    public InvertedIndexDao(IMongoDatabase database, ILogger<InvertedIndexDao> logger)
    {
        _collection = CollectionResolver.Resolve<InvertedIndexDataModel>(database);
        _logger = logger;
    }

    public async Task UpsertPostingsAsync(IReadOnlyCollection<string> words, ObjectId pageId, CancellationToken ct = default)
    {
        if (words.Count == 0) return;

        var ops = new List<WriteModel<InvertedIndexDataModel>>(words.Count);
        foreach (var word in words)
        {
            var filter = Builders<InvertedIndexDataModel>.Filter.Eq(d => d.Word, word);
            var update = Builders<InvertedIndexDataModel>.Update
                .SetOnInsert(d => d.Word, word)
                .AddToSet(d => d.PageIds, pageId);

            ops.Add(new UpdateOneModel<InvertedIndexDataModel>(filter, update) { IsUpsert = true });
        }

        try
        {
            await _collection.BulkWriteAsync(ops, new BulkWriteOptions { IsOrdered = false }, ct);
        }
        catch (MongoBulkWriteException<InvertedIndexDataModel> ex)
        {
            _logger.LogWarning(ex, "Partial bulk write for page {PageId}: {Errors} errors", pageId, ex.WriteErrors.Count);
        }
    }

    public async Task<IReadOnlyList<InvertedIndexDataModel>> FindByWordsAsync(IReadOnlyCollection<string> words, CancellationToken ct = default)
    {
        if (words.Count == 0) return Array.Empty<InvertedIndexDataModel>();
        var filter = Builders<InvertedIndexDataModel>.Filter.In(d => d.Word, words);
        return await _collection.Find(filter).ToListAsync(ct);
    }

    public async IAsyncEnumerable<InvertedIndexDataModel> StreamAllAsync([EnumeratorCancellation] CancellationToken ct = default)
    {
        using var cursor = await _collection
            .Find(FilterDefinition<InvertedIndexDataModel>.Empty)
            .ToCursorAsync(ct);

        while (await cursor.MoveNextAsync(ct))
            foreach (var doc in cursor.Current)
                yield return doc;
    }
}
