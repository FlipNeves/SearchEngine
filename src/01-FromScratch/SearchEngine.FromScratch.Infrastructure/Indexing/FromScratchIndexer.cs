using MongoDB.Bson;
using SearchEngine.FromScratch.Core.Text;
using SearchEngine.FromScratch.Infrastructure.Daos;
using SearchEngine.FromScratch.Infrastructure.DataModels;
using SearchEngine.Shared.Domain.Entities;
using SearchEngine.Shared.Domain.Interfaces;

namespace SearchEngine.FromScratch.Infrastructure.Indexing;

public sealed class FromScratchIndexer : IPageIndexer
{
    private readonly IInvertedIndexDao _index;

    public FromScratchIndexer(IInvertedIndexDao index) => _index = index;

    public Task IndexAsync(WebPage page, CancellationToken ct = default)
    {
        if (!ObjectId.TryParse(page.Id, out var pageOid)) return Task.CompletedTask;

        var byTerm = new Dictionary<string, PostingDataModel>(StringComparer.Ordinal);

        foreach (var token in Tokenizer.Tokenize(page.Title))
        {
            var posting = GetOrCreate(byTerm, token.Value, pageOid);
            posting.TfTitle++;
            posting.PositionsTitle.Add(new PositionDataModel { Start = token.Start, End = token.End });
        }

        foreach (var token in Tokenizer.Tokenize(page.Content))
        {
            var posting = GetOrCreate(byTerm, token.Value, pageOid);
            posting.TfContent++;
            posting.PositionsContent.Add(new PositionDataModel { Start = token.Start, End = token.End });
        }

        if (byTerm.Count == 0) return Task.CompletedTask;

        var ops = byTerm.Select(kv => (kv.Key, kv.Value)).ToArray();
        return _index.UpsertPostingsAsync(ops, ct);
    }

    private static PostingDataModel GetOrCreate(Dictionary<string, PostingDataModel> byTerm, string term, ObjectId docId)
    {
        if (!byTerm.TryGetValue(term, out var posting))
        {
            posting = new PostingDataModel { DocId = docId };
            byTerm[term] = posting;
        }
        return posting;
    }
}
