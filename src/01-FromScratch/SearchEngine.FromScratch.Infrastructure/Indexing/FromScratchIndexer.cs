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
    private readonly IPagesRepository _pages;
    private readonly IIndexStatsDao _stats;

    public FromScratchIndexer(IInvertedIndexDao index, IPagesRepository pages, IIndexStatsDao stats)
    {
        _index = index;
        _pages = pages;
        _stats = stats;
    }

    public async Task IndexAsync(WebPage page, CancellationToken ct = default)
    {
        if (!ObjectId.TryParse(page.Id, out var pageOid)) return;

        var byTerm = new Dictionary<string, PostingDataModel>(StringComparer.Ordinal);
        var lengthTitle = 0;
        var lengthContent = 0;

        foreach (var token in Tokenizer.Tokenize(page.Title))
        {
            var posting = GetOrCreate(byTerm, token.Value, pageOid);
            posting.TfTitle++;
            posting.PositionsTitle.Add(new PositionDataModel { Start = token.Start, End = token.End });
            lengthTitle++;
        }

        foreach (var token in Tokenizer.Tokenize(page.Content))
        {
            var posting = GetOrCreate(byTerm, token.Value, pageOid);
            posting.TfContent++;
            posting.PositionsContent.Add(new PositionDataModel { Start = token.Start, End = token.End });
            lengthContent++;
        }

        if (byTerm.Count == 0) return;

        var ops = byTerm.Select(kv => (kv.Key, kv.Value)).ToArray();
        await _index.UpsertPostingsAsync(ops, ct);
        await _pages.UpdateLengthsAsync(page.Id, lengthTitle, lengthContent, ct);
        await _stats.RecomputeAsync(ct);
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
