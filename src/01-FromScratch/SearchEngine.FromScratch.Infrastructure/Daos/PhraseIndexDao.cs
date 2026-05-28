using Microsoft.Extensions.Logging;
using MongoDB.Driver;
using SearchEngine.FromScratch.Infrastructure.DataModels;
using SearchEngine.Shared.Persistence.Internal;

namespace SearchEngine.FromScratch.Infrastructure.Daos;

public sealed class PhraseIndexDao : IPhraseIndexDao
{
    private readonly IMongoCollection<PhraseIndexDataModel> _collection;
    private readonly ILogger<PhraseIndexDao> _logger;

    public PhraseIndexDao(IMongoDatabase database, ILogger<PhraseIndexDao> logger)
    {
        _collection = CollectionResolver.Resolve<PhraseIndexDataModel>(database);
        _logger = logger;
    }

    public async Task UpsertPostingsAsync(
        IReadOnlyCollection<(string Phrase, int Size, PostingDataModel Posting)> postings,
        CancellationToken ct = default)
    {
        if (postings.Count == 0) return;

        var ops = new List<WriteModel<PhraseIndexDataModel>>(postings.Count * 2);
        foreach (var (phrase, size, posting) in postings)
        {
            var phraseFilter = Builders<PhraseIndexDataModel>.Filter.Eq(d => d.Phrase, phrase);

            var pull = Builders<PhraseIndexDataModel>.Update
                .PullFilter(d => d.Postings, p => p.DocId == posting.DocId);
            ops.Add(new UpdateOneModel<PhraseIndexDataModel>(phraseFilter, pull));

            var push = Builders<PhraseIndexDataModel>.Update
                .SetOnInsert(d => d.Phrase, phrase)
                .SetOnInsert(d => d.Size, size)
                .Push(d => d.Postings, posting);
            ops.Add(new UpdateOneModel<PhraseIndexDataModel>(phraseFilter, push) { IsUpsert = true });
        }

        try
        {
            await _collection.BulkWriteAsync(ops, new BulkWriteOptions { IsOrdered = true }, ct);
        }
        catch (MongoBulkWriteException<PhraseIndexDataModel> ex)
        {
            _logger.LogWarning(ex, "Partial bulk write: {Errors} errors", ex.WriteErrors.Count);
        }
    }

    public async Task<IReadOnlyList<PhraseIndexDataModel>> FindByPhrasesAsync(
        IReadOnlyCollection<string> phrases,
        CancellationToken ct = default)
    {
        if (phrases.Count == 0) return Array.Empty<PhraseIndexDataModel>();
        var filter = Builders<PhraseIndexDataModel>.Filter.In(d => d.Phrase, phrases);
        return await _collection.Find(filter).ToListAsync(ct);
    }
}
