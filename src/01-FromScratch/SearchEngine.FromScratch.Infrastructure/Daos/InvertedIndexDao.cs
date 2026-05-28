using System.Runtime.CompilerServices;
using Microsoft.Extensions.Logging;
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

    public async Task UpsertPostingsAsync(IReadOnlyCollection<(string Term, PostingDataModel Posting)> postings, CancellationToken ct = default)
    {
        if (postings.Count == 0) return;

        var ops = new List<WriteModel<InvertedIndexDataModel>>(postings.Count * 2);
        foreach (var (term, posting) in postings)
        {
            var termFilter = Builders<InvertedIndexDataModel>.Filter.Eq(d => d.Term, term);

            var pull = Builders<InvertedIndexDataModel>.Update
                .PullFilter(d => d.Postings, p => p.DocId == posting.DocId);
            ops.Add(new UpdateOneModel<InvertedIndexDataModel>(termFilter, pull));

            var push = Builders<InvertedIndexDataModel>.Update
                .SetOnInsert(d => d.Term, term)
                .Push(d => d.Postings, posting);
            ops.Add(new UpdateOneModel<InvertedIndexDataModel>(termFilter, push) { IsUpsert = true });
        }

        try
        {
            await _collection.BulkWriteAsync(ops, new BulkWriteOptions { IsOrdered = true }, ct);
        }
        catch (MongoBulkWriteException<InvertedIndexDataModel> ex)
        {
            _logger.LogWarning(ex, "Partial bulk write: {Errors} errors", ex.WriteErrors.Count);
        }
    }

    public async Task<IReadOnlyList<InvertedIndexDataModel>> FindByTermsAsync(IReadOnlyCollection<string> terms, CancellationToken ct = default)
    {
        if (terms.Count == 0) return Array.Empty<InvertedIndexDataModel>();
        var filter = Builders<InvertedIndexDataModel>.Filter.In(d => d.Term, terms);
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
